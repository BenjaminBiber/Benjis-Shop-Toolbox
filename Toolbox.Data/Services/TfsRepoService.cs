using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Toolbox.Data.Models;

namespace Toolbox.Data.Services;

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

    private VssConnection? _connection;
    private string? _connectionKey;

    public TfsRepoService(SettingsService settings)
    {
        _settings = settings;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns repos for the project URLs configured in settings (used by Extensions).
    /// </summary>
    public async Task<IReadOnlyList<TfsRepoInfo>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var urls = _settings.Settings.GetTfsProjectUrls()
            .Select(NormalizeProjectUrl)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .ToList();

        if (urls.Count == 0)
            throw new InvalidOperationException("Keine TFS/Azure-DevOps Projekt-URLs konfiguriert.");

        var git = await GetGitClientAsync();
        var repos = new List<TfsRepoInfo>();

        foreach (var projectUrl in urls)
        {
            var projectName = ExtractProjectName(projectUrl);
            if (string.IsNullOrWhiteSpace(projectName)) continue;

            try
            {
                var projectRepos = await git.GetRepositoriesAsync(projectName, cancellationToken: cancellationToken);
                repos.AddRange(projectRepos.Select(r => MapRepo(r, projectName, projectUrl)));
            }
            catch { /* skip inaccessible projects */ }
        }

        return repos
            .GroupBy(r => r.RemoteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns ALL Git repositories across all projects in the TFS collection.
    /// </summary>
    public async Task<IReadOnlyList<TfsRepoInfo>> GetAllRepositoriesInCollectionAsync(
        string? collectionUrl = null,
        CancellationToken cancellationToken = default)
    {
        var git = await GetGitClientAsync(collectionUrl);
        var repos = await git.GetRepositoriesAsync(cancellationToken: cancellationToken);

        return repos
            .Select(r => MapRepo(r, r.ProjectReference?.Name ?? string.Empty,
                NormalizeProjectUrl(r.RemoteUrl ?? r.WebUrl?.ToString() ?? string.Empty)))
            .OrderBy(r => r.Project, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<TfsProjectInfo>> GetProjectsAsync(
        string? collectionUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCollectionUrl(collectionUrl ?? _settings.Settings.TfsCollectionUrl);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Keine TFS/Azure-DevOps Collection-URL konfiguriert.");

        var connection = GetConnection(normalized);
        var projectClient = await connection.GetClientAsync<Microsoft.TeamFoundation.Core.WebApi.ProjectHttpClient>(cancellationToken);
        var projects = await projectClient.GetProjects();

        return projects
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new TfsProjectInfo(p.Name!, BuildProjectUrl(normalized, p.Name!)))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<TfsRepoInfo>> GetRepositoriesForProjectAsync(
        string projectUrl,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProjectUrl(projectUrl);
        var projectName = ExtractProjectName(normalized);
        if (string.IsNullOrWhiteSpace(projectName))
            throw new InvalidOperationException("TFS-Projekt-URL ist ungueltig.");

        var git = await GetGitClientAsync();
        var repos = await git.GetRepositoriesAsync(projectName, cancellationToken: cancellationToken);
        return repos.Select(r => MapRepo(r, projectName, normalized)).ToList();
    }

    public async Task<IReadOnlyList<TfsRepoItemInfo>> GetRepositoryItemsAsync(
        TfsRepoInfo repo,
        string? scopePath = "/",
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.Id)) return Array.Empty<TfsRepoItemInfo>();

        var git = await GetGitClientAsync();
        var branch = NormalizeBranchName(version ?? repo.DefaultBranch);
        var scope = string.IsNullOrWhiteSpace(scopePath) ? "/" : scopePath;

        try
        {
            var items = await git.GetItemsAsync(
                repo.Id,
                scopePath: scope,
                recursionLevel: VersionControlRecursionType.Full,
                versionDescriptor: new GitVersionDescriptor { Version = branch, VersionType = GitVersionType.Branch },
                cancellationToken: cancellationToken);

            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.Path))
                .Select(i => new TfsRepoItemInfo(i.Path!, i.IsFolder))
                .ToList();
        }
        catch
        {
            return Array.Empty<TfsRepoItemInfo>();
        }
    }

    public async Task<string?> GetFileContentAsync(
        TfsRepoInfo repo,
        string path,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.Id) || string.IsNullOrWhiteSpace(path)) return null;

        var git = await GetGitClientAsync();
        var branch = NormalizeBranchName(version ?? repo.DefaultBranch);

        try
        {
            using var stream = await git.GetItemContentAsync(
                repo.Id,
                path,
                versionDescriptor: new GitVersionDescriptor { Version = branch, VersionType = GitVersionType.Branch },
                cancellationToken: cancellationToken);

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns commit IDs that touched the given file, newest first.</summary>
    public async Task<IReadOnlyList<string>> GetFileCommitIdsAsync(
        TfsRepoInfo repo,
        string filePath,
        int maxCount = 200,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.Id)) return Array.Empty<string>();

        var git = await GetGitClientAsync();

        try
        {
            var criteria = new GitQueryCommitsCriteria
            {
                ItemPath = filePath,
                Top = maxCount
            };

            var commits = await git.GetCommitsAsync(repo.Id, criteria, cancellationToken: cancellationToken);
            return commits.Select(c => c.CommitId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList()!;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Returns the raw content of a file at a specific commit.</summary>
    public async Task<string?> GetFileContentAtCommitAsync(
        TfsRepoInfo repo,
        string filePath,
        string commitId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.Id)) return null;

        var git = await GetGitClientAsync();

        try
        {
            using var stream = await git.GetItemContentAsync(
                repo.Id,
                filePath,
                versionDescriptor: new GitVersionDescriptor { Version = commitId, VersionType = GitVersionType.Commit },
                cancellationToken: cancellationToken);

            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // URL helpers (kept public for use in Blazor components)
    // -------------------------------------------------------------------------

    public static string NormalizeProjectUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim().TrimEnd('/');
        var idx = trimmed.IndexOf("/_apis", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) trimmed = trimmed[..idx];
        return trimmed.TrimEnd('/');
    }

    public static string NormalizeCollectionUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var trimmed = url.Trim().TrimEnd('/');
        var idx = trimmed.IndexOf("/_apis", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) trimmed = trimmed[..idx];
        return trimmed.TrimEnd('/');
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<GitHttpClient> GetGitClientAsync(string? collectionUrl = null)
    {
        var connection = GetConnection(collectionUrl);
        return await connection.GetClientAsync<GitHttpClient>();
    }

    private VssConnection GetConnection(string? collectionUrl = null)
    {
        var pat = _settings.Settings.TfsApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pat))
            throw new InvalidOperationException("Kein TFS/Azure-DevOps API Key (PAT) konfiguriert.");

        var url = NormalizeCollectionUrl(collectionUrl ?? _settings.Settings.TfsCollectionUrl);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Keine TFS/Azure-DevOps Collection-URL konfiguriert.");

        var key = $"{url}|{pat}";
        if (_connection != null && _connectionKey == key)
            return _connection;

        _connection?.Dispose();
        _connectionKey = key;
        _connection = new VssConnection(new Uri(url), new VssBasicCredential(string.Empty, pat));
        return _connection;
    }

    private static TfsRepoInfo MapRepo(GitRepository r, string projectName, string sourceUrl) =>
        new TfsRepoInfo(
            r.Name ?? string.Empty,
            r.RemoteUrl ?? r.WebUrl?.ToString() ?? string.Empty,
            projectName,
            sourceUrl)
        {
            Id = r.Id.ToString(),
            DefaultBranch = r.DefaultBranch ?? string.Empty
        };

    private static string BuildProjectUrl(string collectionUrl, string projectName)
    {
        var trimmed = NormalizeCollectionUrl(collectionUrl);
        if (string.IsNullOrWhiteSpace(trimmed) || string.IsNullOrWhiteSpace(projectName))
            return string.Empty;
        return $"{trimmed}/{Uri.EscapeDataString(projectName)}";
    }

    private static string ExtractProjectName(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        try
        {
            var segments = new Uri(url).AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.LastOrDefault() ?? string.Empty;
        }
        catch { return url; }
    }

    private static string NormalizeBranchName(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch)) return "master";
        var trimmed = branch.Trim();
        const string prefix = "refs/heads/";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[prefix.Length..];
        return string.IsNullOrWhiteSpace(trimmed) ? "master" : trimmed;
    }
}
