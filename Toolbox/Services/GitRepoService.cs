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
    private readonly CacheService _cache;

    public GitRepoService(INotificationService notifications, CacheService cache)
    {
        _notifications = notifications;
        _cache = cache;
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
        return await CloneRepositoryAsync(gitUrl, destinationRoot, null, null);
    }

    public async Task<(bool ok, string? targetDir)> CloneRepositoryAsync(
        string gitUrl,
        string destinationRoot,
        IProgress<string>? outputProgress,
        IProgress<string>? errorProgress)
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
                var msgExists = $"Repository {namePart} existiert bereits.";
                _notifications.Warning(msgExists);
                outputProgress?.Report(msgExists + Environment.NewLine);
                return (false, targetDir);
            }

            var psi = new ProcessStartInfo("git", $"clone {gitUrl} \"{targetDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            if (!proc.Start())
            {
                const string msg = "Klonvorgang konnte nicht gestartet werden.";
                _notifications.Error(msg);
                errorProgress?.Report(msg + Environment.NewLine);
                return (false, null);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnOutputDataReceived(object? _, DataReceivedEventArgs e)
            {
                if (e.Data is null) return;
                outputProgress?.Report(e.Data + Environment.NewLine);
            }

            void OnErrorDataReceived(object? _, DataReceivedEventArgs e)
            {
                if (e.Data is null) return;
                errorProgress?.Report(e.Data + Environment.NewLine);
            }

            void OnExited(object? _1, EventArgs _)
            {
                tcs.TrySetResult(true);
            }

            proc.OutputDataReceived += OnOutputDataReceived;
            proc.ErrorDataReceived += OnErrorDataReceived;
            proc.Exited += OnExited;

            try
            {
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                proc.OutputDataReceived -= OnOutputDataReceived;
                proc.ErrorDataReceived -= OnErrorDataReceived;
                proc.Exited -= OnExited;
            }

            if (proc.ExitCode == 0)
            {
                var msg = $"Repository {namePart} geklont.";
                _notifications.Success(msg);
                outputProgress?.Report(msg + Environment.NewLine);
                _cache.InvalidateExtensions();
                _cache.InvalidateThemes();
                return (true, targetDir);
            }

            const string errorMsg = "Fehler beim Klonen.";
            _notifications.Error(errorMsg);
            errorProgress?.Report(errorMsg + Environment.NewLine);
            return (false, null);
        }
        catch (Exception ex)
        {
            var msg = $"Fehler beim Klonen: {ex.Message}";
            _notifications.Error(msg);
            errorProgress?.Report(msg + Environment.NewLine);
            return (false, null);
        }
    }
}
