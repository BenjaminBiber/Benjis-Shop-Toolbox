using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Data.Services;

public class VmwareVm
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PowerState { get; set; } = "";
    public int MemoryMib { get; set; }
    public int CpuCount { get; set; }
    public int CoresPerSocket { get; set; }
    public string GuestOs { get; set; } = "";
    public string? IpAddress { get; set; }
    public string? HostName { get; set; }
    public bool DetailsLoaded { get; set; }
}

public class VmwareSnapshot
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class VmwareService
{
    private readonly ISettingsService _settingsService;
    private HttpClient? _client;
    private string? _sessionToken;

    public bool IsConnected => _sessionToken != null && _client != null;
    public string? LastError { get; private set; }

    public VmwareService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private HttpClient CreateClient()
    {
        var handler = new HttpClientHandler();
        if (_settingsService.Settings?.VCenterIgnoreSslErrors == true)
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private string BaseUrl => (_settingsService.Settings?.VCenterUrl ?? "").TrimEnd('/');

    /// <summary>
    /// Creates a vSphere session via POST /rest/com/vmware/cis/session (Basic Auth).
    /// Uses vmw-cis-session-id for all subsequent requests.
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        LastError = null;
        _client?.Dispose();
        _client = null;
        _sessionToken = null;

        try
        {
            var settings = _settingsService.Settings;
            if (string.IsNullOrWhiteSpace(settings?.VCenterUrl))
            {
                LastError = "Keine vCenter URL konfiguriert.";
                return false;
            }

            _client = CreateClient();

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{settings.VCenterUsername}:{settings.VCenterPassword}"));

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{BaseUrl}/rest/com/vmware/cis/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Anmeldung fehlgeschlagen: HTTP {(int)response.StatusCode}";
                _client.Dispose();
                _client = null;
                return false;
            }

            var body = await response.Content.ReadAsStringAsync();
            // Response format: {"value": "SESSION_TOKEN"}
            var parsed = JsonSerializer.Deserialize<ValueWrapper<string>>(body, JsonOpts);
            _sessionToken = parsed?.Value ?? body.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(_sessionToken))
            {
                LastError = "Kein Session-Token erhalten.";
                _client.Dispose();
                _client = null;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _sessionToken = null;
            _client?.Dispose();
            _client = null;
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected || _client == null) return;
        try
        {
            await _client.SendAsync(CreateRequest(HttpMethod.Delete,
                "/rest/com/vmware/cis/session"));
        }
        catch { }
        finally
        {
            _sessionToken = null;
            _client?.Dispose();
            _client = null;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}{path}");
        if (_sessionToken != null)
            request.Headers.Add("vmw-cis-session-id", _sessionToken);
        return request;
    }

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<List<VmwareVm>> GetVmsAsync()
    {
        if (!IsConnected || _client == null)
            throw new InvalidOperationException("Nicht verbunden");

        var response = await _client.SendAsync(CreateRequest(HttpMethod.Get, "/rest/vcenter/vm"));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ValueWrapper<List<VmListItem>>>(json, JsonOpts);
        var items = result?.Value ?? new();

        return items.Select(v => new VmwareVm
        {
            Id = v.Vm,
            Name = v.Name,
            PowerState = v.PowerState,
            MemoryMib = v.MemorySizeMiB,
            CpuCount = v.CpuCount
        }).OrderBy(v => v.Name).ToList();
    }

    public async Task LoadVmDetailsAsync(VmwareVm vm)
    {
        if (!IsConnected || _client == null) return;

        // Guest identity (IP, Hostname, OS name) – only available when VMware Tools runs
        try
        {
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Get, $"/rest/vcenter/vm/{vm.Id}/guest/identity"));
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ValueWrapper<GuestIdentity>>(json, JsonOpts);
                var identity = result?.Value;
                if (identity != null)
                {
                    vm.IpAddress = identity.IpAddress;
                    vm.HostName = identity.HostName;
                    if (!string.IsNullOrEmpty(identity.FullName?.DefaultMessage))
                        vm.GuestOs = identity.FullName.DefaultMessage;
                }
            }
        }
        catch { }

        // VM hardware details
        try
        {
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Get, $"/rest/vcenter/vm/{vm.Id}"));
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ValueWrapper<VmDetailResponse>>(json, JsonOpts);
                var details = result?.Value;
                if (details != null)
                {
                    if (string.IsNullOrEmpty(vm.GuestOs) && !string.IsNullOrEmpty(details.GuestOs))
                        vm.GuestOs = FormatGuestOs(details.GuestOs);
                    if (details.Memory != null)
                        vm.MemoryMib = details.Memory.SizeMib;
                    if (details.Cpu != null)
                    {
                        vm.CpuCount = details.Cpu.Count;
                        vm.CoresPerSocket = details.Cpu.CoresPerSocket;
                    }
                }
            }
        }
        catch { }

        vm.DetailsLoaded = true;
    }

    private static string FormatGuestOs(string raw)
    {
        var lower = raw.Replace('_', ' ').ToLowerInvariant();
        return lower.Length > 0 ? char.ToUpper(lower[0]) + lower[1..] : raw;
    }

    public async Task<bool> PowerActionAsync(string vmId, string action)
    {
        if (!IsConnected || _client == null) return false;
        try
        {
            LastError = null;
            // /rest/ API uses sub-path: /rest/vcenter/vm/{vm}/power/start|stop|reset|suspend
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Post, $"/rest/vcenter/vm/{vmId}/power/{action}"));
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Fehler {(int)response.StatusCode}: {response.ReasonPhrase}";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public Task<bool> PowerOnAsync(string vmId) => PowerActionAsync(vmId, "start");
    public Task<bool> PowerOffAsync(string vmId) => PowerActionAsync(vmId, "stop");
    public Task<bool> ResetAsync(string vmId) => PowerActionAsync(vmId, "reset");
    public Task<bool> SuspendAsync(string vmId) => PowerActionAsync(vmId, "suspend");

    public async Task<bool> GuestPowerActionAsync(string vmId, string action)
    {
        if (!IsConnected || _client == null) return false;
        try
        {
            LastError = null;
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Post,
                    $"/rest/vcenter/vm/{vmId}/guest/power?action={action}"));
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Fehler {(int)response.StatusCode} (VMware Tools aktiv?)";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public Task<bool> ShutdownGuestAsync(string vmId) => GuestPowerActionAsync(vmId, "shutdown");
    public Task<bool> RebootGuestAsync(string vmId) => GuestPowerActionAsync(vmId, "reboot");

    public async Task<List<VmwareSnapshot>> GetSnapshotsAsync(string vmId)
    {
        if (!IsConnected || _client == null) return new();
        try
        {
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Get, $"/rest/vcenter/vm/{vmId}/snapshot"));
            if (!response.IsSuccessStatusCode) return new();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ValueWrapper<SnapshotListResponse>>(json, JsonOpts);
            return (result?.Value?.Snapshots ?? new())
                .Select(s => new VmwareSnapshot
                {
                    Id = s.Snapshot,
                    Name = s.Name,
                    Description = s.Description ?? "",
                    CreatedAt = s.CreationTime
                })
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }
        catch { return new(); }
    }

    public async Task<bool> CreateSnapshotAsync(string vmId, string name, string description,
        bool memory, bool quiesce)
    {
        if (!IsConnected || _client == null) return false;
        try
        {
            LastError = null;
            var request = CreateRequest(HttpMethod.Post, $"/rest/vcenter/vm/{vmId}/snapshot");
            var body = JsonSerializer.Serialize(new
            {
                spec = new { name, description, memory, quiesce }
            });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Fehler beim Erstellen: HTTP {(int)response.StatusCode}";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async Task<bool> RevertSnapshotAsync(string vmId, string snapshotId)
    {
        if (!IsConnected || _client == null) return false;
        try
        {
            LastError = null;
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Post,
                    $"/rest/vcenter/vm/{vmId}/snapshot/{snapshotId}?action=revert"));
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Fehler beim Wiederherstellen: HTTP {(int)response.StatusCode}";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public async Task<bool> DeleteSnapshotAsync(string vmId, string snapshotId)
    {
        if (!IsConnected || _client == null) return false;
        try
        {
            LastError = null;
            var response = await _client.SendAsync(
                CreateRequest(HttpMethod.Delete,
                    $"/rest/vcenter/vm/{vmId}/snapshot/{snapshotId}"));
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Fehler beim Löschen: HTTP {(int)response.StatusCode}";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    // ── Internal DTOs ────────────────────────────────────────────────────────

    private sealed class ValueWrapper<T>
    {
        [JsonPropertyName("value")] public T? Value { get; init; }
    }

    private sealed class VmListItem
    {
        [JsonPropertyName("vm")] public string Vm { get; init; } = "";
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("power_state")] public string PowerState { get; init; } = "";
        [JsonPropertyName("memory_size_MiB")] public int MemorySizeMiB { get; init; }
        [JsonPropertyName("cpu_count")] public int CpuCount { get; init; }
    }

    private sealed class GuestIdentity
    {
        [JsonPropertyName("ip_address")] public string? IpAddress { get; init; }
        [JsonPropertyName("host_name")] public string? HostName { get; init; }
        [JsonPropertyName("full_name")] public FullNameObj? FullName { get; init; }
    }

    private sealed class FullNameObj
    {
        [JsonPropertyName("default_message")] public string? DefaultMessage { get; init; }
    }

    private sealed class VmDetailResponse
    {
        [JsonPropertyName("guest_OS")] public string? GuestOs { get; init; }
        [JsonPropertyName("memory")] public MemoryInfo? Memory { get; init; }
        [JsonPropertyName("cpu")] public CpuInfo? Cpu { get; init; }
    }

    private sealed class MemoryInfo
    {
        [JsonPropertyName("size_MiB")] public int SizeMib { get; init; }
    }

    private sealed class CpuInfo
    {
        [JsonPropertyName("count")] public int Count { get; init; }
        [JsonPropertyName("cores_per_socket")] public int CoresPerSocket { get; init; }
    }

    private sealed class SnapshotListResponse
    {
        [JsonPropertyName("snapshots")] public List<SnapshotItem>? Snapshots { get; init; }
    }

    private sealed class SnapshotItem
    {
        [JsonPropertyName("snapshot")] public string Snapshot { get; init; } = "";
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("creation_time")] public DateTime CreationTime { get; init; }
    }
}
