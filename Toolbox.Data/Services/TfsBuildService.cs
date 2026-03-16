using System.Text.Json;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Toolbox.Data.Services;

public sealed record PipelineDefinitionInfo(int Id, string Name, string Folder);

public sealed class PipelineBuildStatus
{
    public int    Id         { get; init; }
    public string Status     { get; init; } = "";  // NotStarted, InProgress, Completed
    public string Result     { get; init; } = "";  // None, Succeeded, Failed, Canceled, PartiallySucceeded
    public string WebUrl     { get; init; } = "";
    public DateTime? Started  { get; init; }
    public DateTime? Finished { get; init; }

    public bool IsRunning   => Status is "InProgress" or "NotStarted";
    public bool IsCompleted => Status == "Completed";
    public bool Succeeded   => IsCompleted && Result == "Succeeded";
    public bool Failed      => IsCompleted && Result is "Failed" or "PartiallySucceeded";
    public bool Canceled    => IsCompleted && Result == "Canceled";
}

public class TfsBuildService
{
    private readonly SettingsService _settings;
    private VssConnection? _connection;
    private string? _connectionKey;

    public TfsBuildService(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<PipelineDefinitionInfo>> GetPipelineDefinitionsAsync(
        string projectUrl,
        string? nameFilter = null,
        CancellationToken ct = default)
    {
        var (connection, project) = GetConnectionAndProject(projectUrl);
        var client = await connection.GetClientAsync<BuildHttpClient>(ct);
        var defs = await client.GetDefinitionsAsync(project, name: nameFilter, cancellationToken: ct);

        return defs
            .OrderBy(d => d.Name)
            .Select(d => new PipelineDefinitionInfo(d.Id, d.Name, d.Path ?? "\\"))
            .ToList();
    }

    public async Task<PipelineBuildStatus> QueueBuildAsync(
        string projectUrl,
        int definitionId,
        Dictionary<string, string> variables,
        CancellationToken ct = default)
    {
        var (connection, project) = GetConnectionAndProject(projectUrl);
        var client = await connection.GetClientAsync<BuildHttpClient>(ct);

        var build = new Build
        {
            Definition = new DefinitionReference { Id = definitionId },
            Parameters = JsonSerializer.Serialize(variables)
        };

        var queued = await client.QueueBuildAsync(build, project: project, cancellationToken: ct);
        return MapStatus(queued, projectUrl, project);
    }

    public async Task<PipelineBuildStatus> GetBuildStatusAsync(
        string projectUrl,
        int buildId,
        CancellationToken ct = default)
    {
        var (connection, project) = GetConnectionAndProject(projectUrl);
        var client = await connection.GetClientAsync<BuildHttpClient>(ct);
        var build  = await client.GetBuildAsync(project, buildId, cancellationToken: ct);
        return MapStatus(build, projectUrl, project);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private PipelineBuildStatus MapStatus(Build b, string projectUrl, string project)
    {
        var collectionUrl = ExtractCollectionUrl(projectUrl);
        var webUrl        = $"{collectionUrl}/{Uri.EscapeDataString(project)}/_build/results?buildId={b.Id}";

        return new PipelineBuildStatus
        {
            Id       = b.Id,
            Status   = b.Status?.ToString()  ?? "None",
            Result   = b.Result?.ToString()  ?? "None",
            WebUrl   = webUrl,
            Started  = b.StartTime,
            Finished = b.FinishTime
        };
    }

    private (VssConnection connection, string project) GetConnectionAndProject(string projectUrl)
    {
        var pat = _settings.Settings.TfsApiKey ?? "";
        if (string.IsNullOrWhiteSpace(pat))
            throw new InvalidOperationException("Kein TFS PAT konfiguriert.");

        var normalized    = projectUrl.TrimEnd('/');
        var lastSlash     = normalized.LastIndexOf('/');
        var projectName   = Uri.UnescapeDataString(normalized[(lastSlash + 1)..]);
        var collectionUrl = normalized[..lastSlash];

        var key = $"{collectionUrl}|{pat}";
        if (_connection == null || _connectionKey != key)
        {
            _connection?.Dispose();
            _connectionKey = key;
            _connection    = new VssConnection(new Uri(collectionUrl),
                                               new VssBasicCredential(string.Empty, pat));
        }

        return (_connection, projectName);
    }

    private static string ExtractCollectionUrl(string projectUrl)
    {
        var normalized = projectUrl.TrimEnd('/');
        var lastSlash  = normalized.LastIndexOf('/');
        return lastSlash > 0 ? normalized[..lastSlash] : normalized;
    }
}
