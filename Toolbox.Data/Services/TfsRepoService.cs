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
public sealed record TfsProjectInfo(string Name, string Url)
{
    public Guid Id { get; init; }
    public string? DefaultTeamImageUrl { get; init; }
}

public sealed record TfsCommitInfo(string CommitId, string AuthorName, string AuthorEmail, DateTime Date, string Comment);

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
        var projectRefs = await projectClient.GetProjects();
        var validRefs = projectRefs.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();

        var fullProjects = await Task.WhenAll(validRefs.Select(async p =>
        {
            try
            {
                return await projectClient.GetProject(p.Name!);
            }
            catch
            {
                return null;
            }
        }));

        return validRefs
            .Select((p, i) => new TfsProjectInfo(p.Name!, BuildProjectUrl(normalized, p.Name!))
            {
                Id = p.Id,
                DefaultTeamImageUrl = fullProjects[i]?.DefaultTeamImageUrl
            })
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

    /// <summary>Returns commits (with author info) that touched the given file, newest first.</summary>
    public async Task<IReadOnlyList<TfsCommitInfo>> GetFileCommitsWithDetailsAsync(
        TfsRepoInfo repo,
        string filePath,
        int maxCount = 500,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.Id)) return Array.Empty<TfsCommitInfo>();

        var git = await GetGitClientAsync();

        try
        {
            var criteria = new GitQueryCommitsCriteria
            {
                ItemPath = filePath,
                Top = maxCount
            };

            var commits = await git.GetCommitsAsync(repo.Id, criteria, cancellationToken: cancellationToken);
            return commits
                .Where(c => !string.IsNullOrWhiteSpace(c.CommitId))
                .Select(c => new TfsCommitInfo(
                    c.CommitId!,
                    c.Author?.Name ?? "",
                    c.Author?.Email ?? "",
                    c.Author?.Date ?? DateTime.MinValue,
                    c.Comment ?? ""))
                .ToList();
        }
        catch
        {
            return Array.Empty<TfsCommitInfo>();
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

    /// <summary>
    /// Updates a file in the repo by pushing a single commit to the default branch.
    /// Returns the new commit ID.
    /// </summary>
    public async Task<string> PushFileUpdateAsync(
        TfsRepoInfo repo,
        string filePath,
        string newContent,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repo.Id))
            throw new InvalidOperationException("Repo-ID fehlt.");

        var git    = await GetGitClientAsync();
        var branch = NormalizeBranchName(repo.DefaultBranch);

        // Get current branch HEAD to provide oldObjectId
        var refs   = await git.GetRefsAsync(repo.Id, filter: $"heads/{branch}",
                                            cancellationToken: cancellationToken);
        var headRef = refs.FirstOrDefault()
            ?? throw new InvalidOperationException($"Branch '{branch}' nicht gefunden.");

        var push = new GitPush
        {
            RefUpdates = new List<GitRefUpdate>
            {
                new() { Name = $"refs/heads/{branch}", OldObjectId = headRef.ObjectId }
            },
            Commits = new List<GitCommitRef>
            {
                new()
                {
                    Comment = commitMessage,
                    Changes = new List<GitChange>
                    {
                        new()
                        {
                            ChangeType = VersionControlChangeType.Edit,
                            Item       = new GitItem { Path = filePath },
                            NewContent = new ItemContent
                            {
                                Content     = newContent,
                                ContentType = ItemContentType.RawText
                            }
                        }
                    }
                }
            }
        };

        var result = await git.CreatePushAsync(push, repo.Id, cancellationToken: cancellationToken);
        return result.Commits.FirstOrDefault()?.CommitId ?? "";
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

    /// <summary>
    /// Tries to load a project avatar via the REST avatar endpoint.
    /// Returns a base64 data URL on success, or null if not available.
    /// </summary>
    public async Task<string?> GetProjectAvatarAsDataUrlAsync(
        TfsProjectInfo project,
        string? collectionUrl = null,
        CancellationToken cancellationToken = default)
    {
        var pat = _settings.Settings.TfsApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pat)) return null;

        var normalized = NormalizeCollectionUrl(collectionUrl ?? _settings.Settings.TfsCollectionUrl);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        // Try REST avatar endpoint: GET {collection}/_apis/projects/{id}/avatar?api-version=5.0
        var avatarUrl = $"{normalized}/_apis/projects/{project.Id}/avatar?api-version=5.0";

        try
        {
            using var http = new System.Net.Http.HttpClient();
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var response = await http.GetAsync(avatarUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Generates a deterministic SVG avatar (colored circle with initial) from the project name.
    /// </summary>
    public static string GenerateSvgAvatar(string projectName)
    {
        var initial = string.IsNullOrWhiteSpace(projectName) ? "?" : projectName.Trim()[0].ToString().ToUpper();

        // Deterministic hue from project name
        var hash = 0;
        foreach (var c in projectName)
        {
            hash = c + ((hash << 5) - hash);
        }
        var hue = Math.Abs(hash) % 360;

        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 48 48">
              <circle cx="24" cy="24" r="24" fill="hsl({hue},55%,45%)"/>
              <text x="24" y="24" text-anchor="middle" dominant-baseline="central"
                    font-family="Segoe UI,Arial,sans-serif" font-size="22" font-weight="600" fill="white">{initial}</text>
            </svg>
            """;

        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{base64}";
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
