using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Toolbox.Data.Models;

namespace Toolbox.Services;

public sealed record TfsRepoInfo(string Name, string RemoteUrl, string Project, string SourceUrl);

public class TfsRepoService
{
    private readonly SettingsService _settings;

    public TfsRepoService(SettingsService settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<TfsRepoInfo>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        var urls = _settings.Settings.GetTfsProjectUrls().ToList();
        if (urls.Count == 0)
        {
            throw new InvalidOperationException("Keine TFS/Azure-DevOps Projekt-URLs konfiguriert.");
        }

        var pat = _settings.Settings.TfsApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException("Kein TFS/Azure-DevOps API Key (PAT) konfiguriert.");
        }

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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
                        projectUrl));
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

        var trimmed = projectUrl.Trim().TrimEnd('/');
        if (trimmed.Contains("_apis", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"{trimmed}/_apis/git/repositories?api-version=6.0";
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

    private sealed class TfsRepoResponse
    {
        public List<TfsRepoItem>? Value { get; set; }
    }

    private sealed class TfsRepoItem
    {
        public string? Name { get; set; }
        public string? RemoteUrl { get; set; }
        public string? WebUrl { get; set; }
        public TfsProject? Project { get; set; }
    }

    private sealed class TfsProject
    {
        public string? Name { get; set; }
    }
}
