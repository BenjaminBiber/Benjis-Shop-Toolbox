using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Toolbox.Data.Models;

namespace Toolbox.Services;

public sealed record TfsRepoInfo(string Name, string RemoteUrl, string Project, string SourceUrl)
{
    public string Id { get; init; } = string.Empty;
    public string DefaultBranch { get; init; } = string.Empty;
}

public sealed record TfsRepoItemInfo(string Path, bool IsFolder);
public sealed record TfsProjectInfo(string Name, string Url);

public class TfsRepoService
{
    private readonly SettingsService _settings;
    private HttpClient? _client;
    private string? _clientPat;

    public TfsRepoService(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<TfsRepoInfo>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var urls = _settings.Settings.GetTfsProjectUrls()
            .Select(NormalizeProjectUrl)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();
        if (urls.Count == 0)
        {
            throw new InvalidOperationException("Keine TFS/Azure-DevOps Projekt-URLs konfiguriert.");
        }

        return await GetRepositoriesForUrlsAsync(urls, cancellationToken);
    }

    public async Task<IReadOnlyList<TfsProjectInfo>> GetProjectsAsync(
        string? collectionUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCollectionUrl(collectionUrl ?? _settings.Settings.TfsCollectionUrl);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Keine TFS/Azure-DevOps Collection-URL konfiguriert.");
        }

        var client = GetClient();
        var apiUrl = BuildProjectsUrl(normalized);
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            return Array.Empty<TfsProjectInfo>();
        }

        using var response = await client.GetAsync(apiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("TFS-Projekte konnten nicht geladen werden.");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<TfsProjectsResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data?.Value == null || data.Value.Count == 0)
        {
            return Array.Empty<TfsProjectInfo>();
        }

        return data.Value
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new TfsProjectInfo(
                p.Name!,
                BuildProjectUrl(normalized, p.Name!)))
            .GroupBy(p => p.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<TfsRepoInfo>> GetRepositoriesForProjectAsync(
        string projectUrl,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProjectUrl(projectUrl);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("TFS-Projekt-URL ist ungueltig.");
        }

        return await GetRepositoriesForUrlsAsync(new List<string> { normalized }, cancellationToken);
    }

    public async Task<IReadOnlyList<TfsRepoItemInfo>> GetRepositoryItemsAsync(
        TfsRepoInfo repo,
        string? scopePath = "/",
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.SourceUrl) || string.IsNullOrWhiteSpace(repo.Id))
        {
            return Array.Empty<TfsRepoItemInfo>();
        }

        var client = GetClient();
        var branch = NormalizeBranchName(version ?? repo.DefaultBranch);
        var scope = string.IsNullOrWhiteSpace(scopePath) ? "/" : scopePath;
        var url = BuildRepositoryItemsUrl(repo.SourceUrl, repo.Id, scope, branch);
        if (string.IsNullOrWhiteSpace(url))
        {
            return Array.Empty<TfsRepoItemInfo>();
        }

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<TfsRepoItemInfo>();
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<TfsItemsResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data?.Value == null || data.Value.Count == 0)
        {
            return Array.Empty<TfsRepoItemInfo>();
        }

        return data.Value
            .Where(i => !string.IsNullOrWhiteSpace(i.Path))
            .Select(i => new TfsRepoItemInfo(i.Path!, i.IsFolder ?? false))
            .ToList();
    }

    public async Task<string?> GetFileContentAsync(
        TfsRepoInfo repo,
        string path,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.SourceUrl) || string.IsNullOrWhiteSpace(repo.Id) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var client = GetClient();
        var branch = NormalizeBranchName(version ?? repo.DefaultBranch);
        var url = BuildRepositoryItemContentUrl(repo.SourceUrl, repo.Id, path, branch);
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) || LooksLikeJson(payload))
        {
            if (TryExtractContentFromJson(payload, out var content))
            {
                return content;
            }

            if (TryExtractBlobUrlFromJson(payload, out var blobUrl))
            {
                var blobContent = await TryGetBlobContentAsync(client, blobUrl, cancellationToken);
                if (!string.IsNullOrWhiteSpace(blobContent))
                {
                    return blobContent;
                }
            }
        }

        return payload;
    }

    public static string NormalizeProjectUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim().TrimEnd('/');
        var idx = trimmed.IndexOf("/_apis", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            trimmed = trimmed.Substring(0, idx);
        }

        return trimmed.TrimEnd('/');
    }

    public static string NormalizeCollectionUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim().TrimEnd('/');
        var idx = trimmed.IndexOf("/_apis", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            trimmed = trimmed.Substring(0, idx);
        }

        return trimmed.TrimEnd('/');
    }

    private HttpClient GetClient()
    {
        var pat = _settings.Settings.TfsApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException("Kein TFS/Azure-DevOps API Key (PAT) konfiguriert.");
        }

        if (_client != null && string.Equals(_clientPat, pat, StringComparison.Ordinal))
        {
            return _client;
        }

        _client?.Dispose();
        _clientPat = pat;

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client = client;
        return _client;
    }

    private async Task<IReadOnlyList<TfsRepoInfo>> GetRepositoriesForUrlsAsync(
        IReadOnlyList<string> urls,
        CancellationToken cancellationToken)
    {
        var client = GetClient();
        var repos = new List<TfsRepoInfo>();

        foreach (var projectUrl in urls)
        {
            var apiUrl = BuildRepositoriesUrl(projectUrl);
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                continue;
            }

            try
            {
                using var response = await client.GetAsync(apiUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<TfsRepoResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (data?.Value == null)
                {
                    continue;
                }

                foreach (var repo in data.Value)
                {
                    var remoteUrl = repo.RemoteUrl ?? repo.WebUrl ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(remoteUrl))
                    {
                        continue;
                    }

                    repos.Add(new TfsRepoInfo(
                        repo.Name ?? remoteUrl,
                        remoteUrl,
                        repo.Project?.Name ?? ExtractProjectName(projectUrl),
                        projectUrl)
                    {
                        Id = repo.Id ?? string.Empty,
                        DefaultBranch = repo.DefaultBranch ?? string.Empty
                    });
                }
            }
            catch
            {
                // ignore per project failures
            }
        }

        return repos
            .GroupBy(r => r.RemoteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildRepositoriesUrl(string projectUrl)
    {
        if (string.IsNullOrWhiteSpace(projectUrl))
        {
            return string.Empty;
        }

        var trimmed = NormalizeProjectUrl(projectUrl).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return $"{trimmed}/_apis/git/repositories?api-version=6.0";
    }

    private static string BuildProjectsUrl(string collectionUrl)
    {
        if (string.IsNullOrWhiteSpace(collectionUrl))
        {
            return string.Empty;
        }

        var trimmed = NormalizeCollectionUrl(collectionUrl);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return $"{trimmed}/_apis/projects?api-version=6.0";
    }

    private static string BuildProjectUrl(string collectionUrl, string projectName)
    {
        if (string.IsNullOrWhiteSpace(collectionUrl) || string.IsNullOrWhiteSpace(projectName))
        {
            return string.Empty;
        }

        var trimmed = NormalizeCollectionUrl(collectionUrl);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var encoded = Uri.EscapeDataString(projectName);
        return $"{trimmed}/{encoded}";
    }

    private static string BuildRepositoryItemsUrl(string projectUrl, string repoId, string scopePath, string branch)
    {
        if (string.IsNullOrWhiteSpace(projectUrl) || string.IsNullOrWhiteSpace(repoId))
        {
            return string.Empty;
        }

        var baseUrl = NormalizeProjectUrl(projectUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        var scope = string.IsNullOrWhiteSpace(scopePath) ? "/" : scopePath;
        var scopeEncoded = Uri.EscapeDataString(scope);
        var branchEncoded = Uri.EscapeDataString(branch);

        return $"{baseUrl}/_apis/git/repositories/{repoId}/items" +
               $"?scopePath={scopeEncoded}&recursionLevel=Full&includeContentMetadata=false" +
               $"&versionDescriptor.version={branchEncoded}&versionDescriptor.versionType=branch&api-version=6.0";
    }

    private static string BuildRepositoryItemContentUrl(string projectUrl, string repoId, string path, string branch)
    {
        if (string.IsNullOrWhiteSpace(projectUrl) || string.IsNullOrWhiteSpace(repoId))
        {
            return string.Empty;
        }

        var baseUrl = NormalizeProjectUrl(projectUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        var pathEncoded = Uri.EscapeDataString(path);
        var branchEncoded = Uri.EscapeDataString(branch);

        return $"{baseUrl}/_apis/git/repositories/{repoId}/items" +
               $"?path={pathEncoded}&versionDescriptor.version={branchEncoded}&versionDescriptor.versionType=branch" +
               $"&includeContent=true&resolveLfs=true&api-version=6.0";
    }

    private static string ExtractProjectName(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.LastOrDefault() ?? string.Empty;
        }
        catch
        {
            return url;
        }
    }

    private static string NormalizeBranchName(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            return "master";
        }

        var trimmed = branch.Trim();
        const string prefix = "refs/heads/";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(prefix.Length);
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "master" : trimmed;
    }

    private static bool LooksLikeJson(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var trimmed = payload.TrimStart();
        return trimmed.StartsWith("{") || trimmed.StartsWith("[");
    }

    private static bool TryExtractContentFromJson(string payload, out string content)
    {
        content = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (doc.RootElement.TryGetProperty("content", out var contentProp) &&
                contentProp.ValueKind == JsonValueKind.String)
            {
                content = contentProp.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(content);
            }
        }
        catch
        {
            // ignore JSON parse errors
        }

        return false;
    }

    private static bool TryExtractBlobUrlFromJson(string payload, out string url)
    {
        url = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (doc.RootElement.TryGetProperty("_links", out var links) &&
                links.ValueKind == JsonValueKind.Object &&
                links.TryGetProperty("blob", out var blob) &&
                blob.ValueKind == JsonValueKind.Object &&
                blob.TryGetProperty("href", out var href) &&
                href.ValueKind == JsonValueKind.String)
            {
                url = href.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(url);
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static async Task<string?> TryGetBlobContentAsync(HttpClient client, string blobUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(blobUrl))
        {
            return null;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, blobUrl);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        using var response = await client.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(ct);
    }

    private sealed class TfsRepoResponse
    {
        public List<TfsRepoItem>? Value { get; set; }
    }

    private sealed class TfsRepoItem
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? RemoteUrl { get; set; }
        public string? WebUrl { get; set; }
        public string? DefaultBranch { get; set; }
        public TfsProject? Project { get; set; }
    }

    private sealed class TfsProject
    {
        public string? Name { get; set; }
    }

    private sealed class TfsItemsResponse
    {
        public List<TfsItem>? Value { get; set; }
    }

    private sealed class TfsProjectsResponse
    {
        public List<TfsProjectItem>? Value { get; set; }
    }

    private sealed class TfsProjectItem
    {
        public string? Name { get; set; }
    }

    private sealed class TfsItem
    {
        public string? Path { get; set; }
        public bool? IsFolder { get; set; }
    }
}
