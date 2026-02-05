using System.Text.RegularExpressions;

namespace Toolbox.Data.Services;

public sealed record ExtensionReportResult(
    IReadOnlyDictionary<string, List<string>> MatchingExtensions,
    IReadOnlyDictionary<string, List<string>> InstalledExtensions,
    int TotalRepositories,
    int ProcessedRepositories,
    int SqlFileCount);

public sealed class TfsExtensionReportService
{
    private static readonly Regex ExtensionRegex = new(
        @"@extensionTypeId\s*int\s*=\s*2[\s\S]*?@extensionName\s*sysname\s*=\s*(?<q>''?)(?<name>[^']+)\k<q>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly TfsRepoService _tfsRepoService;

    public TfsExtensionReportService(TfsRepoService tfsRepoService)
    {
        _tfsRepoService = tfsRepoService;
    }

    public async Task<ExtensionReportResult> BuildReportAsync(
        IReadOnlyList<TfsRepoInfo> repositories,
        IReadOnlyCollection<string> extensionsToCheck,
        string? repoNameRegex = null,
        IProgress<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        if (repositories == null || repositories.Count == 0)
        {
            return new ExtensionReportResult(
                new Dictionary<string, List<string>>(),
                new Dictionary<string, List<string>>(),
                0,
                0,
                0);
        }

        if (extensionsToCheck == null || extensionsToCheck.Count == 0)
        {
            return new ExtensionReportResult(
                new Dictionary<string, List<string>>(),
                new Dictionary<string, List<string>>(),
                repositories.Count,
                0,
                0);
        }

        Regex? repoFilter = null;
        if (!string.IsNullOrWhiteSpace(repoNameRegex))
        {
            repoFilter = new Regex(repoNameRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        var extensionSet = new HashSet<string>(extensionsToCheck, StringComparer.OrdinalIgnoreCase);
        var installedExtensions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var totalSqlFiles = 0;

        log?.Report($"Repos gesamt: {repositories.Count}{Environment.NewLine}");

        var processedRepos = 0;
        foreach (var repo in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (repoFilter != null && !repoFilter.IsMatch(repo.Name))
            {
                continue;
            }

            log?.Report($"Repo: {repo.Name}{Environment.NewLine}");

            try
            {
                var items = await _tfsRepoService.GetRepositoryItemsAsync(repo, "/", null, cancellationToken);
                if (items.Count == 0)
                {
                    log?.Report("  Keine Dateien gefunden." + Environment.NewLine);
                    continue;
                }

                var installFolder = items.FirstOrDefault(i =>
                    i.IsFolder && i.Path.Contains(".Install", StringComparison.OrdinalIgnoreCase));
                if (installFolder == null)
                {
                    log?.Report("  Kein .Install-Ordner gefunden." + Environment.NewLine);
                    continue;
                }

                var installPath = NormalizeRepoPath(installFolder.Path);
                var databasePath = NormalizeRepoPath($"{installPath}/Database");

                var dbFolder = items.FirstOrDefault(i =>
                    i.IsFolder && string.Equals(NormalizeRepoPath(i.Path), databasePath, StringComparison.OrdinalIgnoreCase));
                if (dbFolder == null)
                {
                    log?.Report("  Kein Database-Ordner gefunden." + Environment.NewLine);
                    continue;
                }

                var sqlFiles = items
                    .Where(i => !i.IsFolder
                                && NormalizeRepoPath(i.Path).StartsWith(databasePath + "/", StringComparison.OrdinalIgnoreCase)
                                && i.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (sqlFiles.Count == 0)
                {
                    log?.Report("  Keine SQL-Dateien gefunden." + Environment.NewLine);
                    continue;
                }

                totalSqlFiles += sqlFiles.Count;
                processedRepos++;

                foreach (var sqlFile in sqlFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var content = await _tfsRepoService.GetFileContentAsync(repo, sqlFile.Path, null, cancellationToken);
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            continue;
                        }

                        var found = ExtractExtensions(content);
                        if (found.Count == 0)
                        {
                            continue;
                        }

                        if (installedExtensions.TryGetValue(repo.Name, out var list))
                        {
                            list.AddRange(found);
                        }
                        else
                        {
                            installedExtensions[repo.Name] = new List<string>(found);
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Report($"  Fehler beim Lesen {sqlFile.Path}: {ex.Message}{Environment.NewLine}");
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Report($"  Fehler: {ex.Message}{Environment.NewLine}");
            }
        }

        var matching = GetMatchingExtensions(extensionSet, installedExtensions);
        return new ExtensionReportResult(matching, installedExtensions, repositories.Count, processedRepos, totalSqlFiles);
    }

    private static List<string> ExtractExtensions(string sqlFileContent)
    {
        var matches = ExtensionRegex.Matches(sqlFileContent);
        if (matches.Count == 0)
        {
            return new List<string>();
        }

        var result = new List<string>();
        foreach (Match match in matches)
        {
            var name = match.Groups["name"]?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name.Trim());
            }
        }

        return result;
    }

    private static Dictionary<string, List<string>> GetMatchingExtensions(
        HashSet<string> extensionSet,
        IDictionary<string, List<string>> installedValues)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in installedValues)
        {
            var matches = kvp.Value
                .Where(val => extensionSet.Contains(val))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count > 0)
            {
                result[kvp.Key] = matches;
            }
        }

        return result;
    }

    private static string NormalizeRepoPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').TrimEnd('/');
    }
}

