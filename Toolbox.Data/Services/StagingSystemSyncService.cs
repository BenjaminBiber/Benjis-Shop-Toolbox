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

            // Get commit history for StagingSystem.yml
            var commitIds = await _tfs.GetFileCommitIdsAsync(stagingRepo, YamlPath, 500, ct);
            if (commitIds.Count == 0) continue;

            progress?.Report($"[{stagingRepo.Project}] {commitIds.Count} Commits gefunden, lese Versionen...");

            var seenVmNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var commitId in commitIds)
            {
                ct.ThrowIfCancellationRequested();

                var content = await _tfs.GetFileContentAtCommitAsync(stagingRepo, YamlPath, commitId, ct);
                if (string.IsNullOrWhiteSpace(content)) continue;

                var vmName = ExtractYamlValue(content, "ESXNewVmName");
                if (string.IsNullOrWhiteSpace(vmName) || seenVmNames.Contains(vmName)) continue;
                seenVmNames.Add(vmName);

                var customerName = ExtractYamlValue(content, "TFSProject") ?? stagingRepo.Project;
                var customerId = ExtractYamlValue(content, "CustomerId");

                var existing = await _db.VmCustomerMappings
                    .FirstOrDefaultAsync(m => m.VmName == vmName, ct);

                if (existing == null)
                {
                    _db.VmCustomerMappings.Add(new VmCustomerMapping
                    {
                        VmName = vmName,
                        CustomerName = customerName,
                        TfsProjectName = stagingRepo.Project,
                        CustomerId = customerId,
                        LastSynced = now
                    });
                    result.Added++;
                }
                else
                {
                    existing.CustomerName = customerName;
                    existing.TfsProjectName = stagingRepo.Project;
                    existing.CustomerId = customerId;
                    existing.LastSynced = now;
                    result.Updated++;
                }
            }

            progress?.Report($"[{stagingRepo.Project}] {seenVmNames.Count} VM(s) gefunden.");
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
