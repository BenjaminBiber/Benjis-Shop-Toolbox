using System.Diagnostics;
using System.IO;
using System.Linq;
using MudBlazor;
using Toolbox.Components.Dialogs;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Services;

public class GitRepoService
{
    private readonly INotificationService _notifications;

    public GitRepoService(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public string GetRepoName(string gitUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gitUrl)) return string.Empty;

            if (Uri.TryCreate(gitUrl, UriKind.Absolute, out var uri))
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var gitIdx = Array.FindIndex(segments, s => s.Equals("_git", StringComparison.OrdinalIgnoreCase));
                var candidate = (gitIdx >= 0 && gitIdx < segments.Length - 1)
                    ? segments[gitIdx + 1]
                    : segments.Length > 0 ? segments[^1] : string.Empty;
                if (candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    candidate = candidate[..^4];
                return candidate;
            }
            
            var trimmed = gitUrl.TrimEnd('/', '\\');
            var last = trimmed.Split('/', '\\').LastOrDefault() ?? string.Empty;
            return Path.GetFileNameWithoutExtension(last);
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<(bool ok, string? targetDir)> CloneRepositoryAsync(string gitUrl, string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(gitUrl))
        {
            _notifications.Error("Git URL ist leer.");
            return (false, null);
        }

        try
        {
            Directory.CreateDirectory(destinationRoot);
            var namePart = GetRepoName(gitUrl);
            if (string.IsNullOrWhiteSpace(namePart))
            {
                _notifications.Error("Repositoryname aus URL konnte nicht ermittelt werden.");
                return (false, null);
            }
            var targetDir = Path.Combine(destinationRoot, namePart);
            if (Directory.Exists(targetDir))
            {
                _notifications.Warning($"Repository {namePart} existiert bereits.");
                return (false, targetDir);
            }

            var psi = new ProcessStartInfo("git", $"clone {gitUrl} \"{targetDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                _notifications.Error("Klonvorgang konnte nicht gestartet werden.");
                return (false, null);
            }

            await proc.WaitForExitAsync();
            if (proc.ExitCode == 0)
            {
                _notifications.Success($"Repository {namePart} geklont.");
                return (true, targetDir);
            }
            var error = await proc.StandardError.ReadToEndAsync();
            _notifications.Error(string.IsNullOrWhiteSpace(error) ?
                "Fehler beim Klonen." : $"Fehler beim Klonen: {error}");
            return (false, null);
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Klonen: {ex.Message}");
            return (false, null);
        }
    }
}
