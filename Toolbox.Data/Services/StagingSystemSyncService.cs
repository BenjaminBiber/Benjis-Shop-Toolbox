using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models;

namespace Toolbox.Data.Services;

public sealed class SyncResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int ProjectsScanned { get; set; }
    public int ReposFound { get; set; }
    public List<string> Errors { get; } = new();
}

public class StagingSystemSyncService
{
    private const string RepoName = "Shopsystem.StagingSystem";
    private const string YamlPath = "/StagingSystem.yml";

    private readonly TfsRepoService _tfs;
    private readonly InternalAppDbContext _db;

    public StagingSystemSyncService(TfsRepoService tfs, InternalAppDbContext db)
    {
        _tfs = tfs;
        _db = db;
    }

    public async Task<List<VmCustomerMapping>> GetMappingsAsync()
        => await _db.VmCustomerMappings.OrderBy(m => m.CustomerName).ToListAsync();

    public async Task<SyncResult> SyncAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new SyncResult();

        // Load ALL repos across the entire TFS collection (no project list needed)
        progress?.Report("Lade alle TFS-Repos aus der Collection...");
        IReadOnlyList<TfsRepoInfo> allRepos;
        try
        {
            allRepos = await _tfs.GetAllRepositoriesInCollectionAsync(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Repos konnten nicht geladen werden: {ex.Message}");
            return result;
        }

        var stagingRepos = allRepos
            .Where(r => string.Equals(r.Name, RepoName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        result.ProjectsScanned = allRepos.Select(r => r.Project).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        result.ReposFound = stagingRepos.Count;
        progress?.Report($"{stagingRepos.Count} '{RepoName}' Repos in {result.ProjectsScanned} Projekten gefunden.");

        var now = DateTime.Now;

        foreach (var stagingRepo in stagingRepos)
        {
            ct.ThrowIfCancellationRequested();

            progress?.Report($"[{stagingRepo.Project}] Lade Git-History von {YamlPath}...");

            // Get full commit details (includes author name/date and commit message)
            var commits = await _tfs.GetFileCommitsWithDetailsAsync(stagingRepo, YamlPath, 500, ct);
            if (commits.Count == 0) continue;

            progress?.Report($"[{stagingRepo.Project}] {commits.Count} Commits gefunden, lese Versionen...");

            // Read all YAML content once (commits are newest-first)
            var commitContents = new List<(TfsCommitInfo Commit, string? VmName, string? Content)>(commits.Count);
            foreach (var commit in commits)
            {
                ct.ThrowIfCancellationRequested();
                var content = await _tfs.GetFileContentAtCommitAsync(stagingRepo, YamlPath, commit.CommitId, ct);
                var vmName  = string.IsNullOrWhiteSpace(content) ? null : ExtractYamlValue(content, "ESXNewVmName");
                commitContents.Add((commit, vmName, content));
            }

            // Config map: most recent values per VM (newest-first → first occurrence wins)
            var configMap = new Dictionary<string, (string CustomerName, string? CustomerId, string? RdpPassword, string RdpUsername)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, vmName, content) in commitContents)
            {
                if (vmName == null || configMap.ContainsKey(vmName)) continue;
                configMap[vmName] = (
                    ExtractYamlValue(content!, "TFSProject") ?? stagingRepo.Project,
                    ExtractYamlValue(content!, "CustomerId"),
                    ExtractYamlValue(content!, "GuestPassword"),
                    ExtractYamlValue(content!, "GuestUser") ?? "Administrator"
                );
            }

            // Creator map: oldest commit where VM name first appeared (oldest-first → first occurrence wins)
            // This works regardless of commit message format — any author who introduced the VM name is the creator.
            var creatorMap = new Dictionary<string, (string Name, string Email, DateTime Date)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (commit, vmName, _) in Enumerable.Reverse(commitContents))
            {
                if (vmName == null || creatorMap.ContainsKey(vmName) || string.IsNullOrWhiteSpace(commit.AuthorName)) continue;
                creatorMap[vmName] = (commit.AuthorName, commit.AuthorEmail, commit.Date);
            }

            // Apply to database
            foreach (var (vmName, config) in configMap)
            {
                ct.ThrowIfCancellationRequested();
                creatorMap.TryGetValue(vmName, out var creator);

                var existing = await _db.VmCustomerMappings
                    .FirstOrDefaultAsync(m => m.VmName == vmName, ct);

                if (existing == null)
                {
                    _db.VmCustomerMappings.Add(new VmCustomerMapping
                    {
                        VmName         = vmName,
                        CustomerName   = config.CustomerName,
                        TfsProjectName = stagingRepo.Project,
                        CustomerId     = config.CustomerId,
                        RdpUsername    = config.RdpUsername,
                        RdpPassword    = config.RdpPassword,
                        LastSynced     = now,
                        CreatedBy      = creator.Name,
                        CreatedByEmail = creator.Name != null ? creator.Email : null,
                        CreatedAt      = creator.Name != null ? creator.Date : null
                    });
                    result.Added++;
                }
                else
                {
                    existing.CustomerName   = config.CustomerName;
                    existing.TfsProjectName = stagingRepo.Project;
                    existing.CustomerId     = config.CustomerId;
                    existing.RdpUsername    = config.RdpUsername;
                    existing.RdpPassword    = config.RdpPassword;
                    existing.LastSynced     = now;
                    // Always update creator — re-sync finds the true oldest author from full history
                    if (creator.Name != null)
                    {
                        existing.CreatedBy      = creator.Name;
                        existing.CreatedByEmail = creator.Email;
                        existing.CreatedAt      = creator.Date;
                    }
                    result.Updated++;
                }
            }

            progress?.Report($"[{stagingRepo.Project}] {configMap.Count} VM(s) gefunden.");
        }

        await _db.SaveChangesAsync(ct);
        progress?.Report($"Fertig. {result.Added} neu, {result.Updated} aktualisiert.");
        return result;
    }

    private static string? ExtractYamlValue(string content, string key)
    {
        var match = Regex.Match(content,
            @"^\s+" + Regex.Escape(key) + @"\s*:\s*['""]?(.*?)['""]?\s*$",
            RegexOptions.Multiline);
        if (!match.Success) return null;
        return match.Groups[1].Value.Trim('\'', '"', ' ');
    }
}
