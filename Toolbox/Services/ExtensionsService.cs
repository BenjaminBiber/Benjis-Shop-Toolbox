using System.Diagnostics;
using System.IO;
using System.Linq;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Services;

public class ExtensionsService
{
    private readonly INotificationService _notifications;
    private readonly SettingsService _settings;

    public ExtensionsService(INotificationService notifications, SettingsService settings)
    {
        _notifications = notifications;
        _settings = settings;
    }

    public IEnumerable<ExtensionInfo> GetExtensions()
    {
        try
        {
            var root = _settings.Settings.ExtensionsRepositoryPath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return Enumerable.Empty<ExtensionInfo>();

            var list = new List<ExtensionInfo>();
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                var hasSln = Directory.GetFiles(dir, "*.sln", SearchOption.AllDirectories).Length > 0;
                var hasProj = Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories).Length > 0;
                list.Add(new ExtensionInfo
                {
                    Name = name,
                    Path = dir,
                    HasSolution = hasSln,
                    HasProjects = hasProj
                });
            }

            return list.OrderBy(x => x.Name).ToList();
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Laden der Extensions: {ex.Message}");
            return Enumerable.Empty<ExtensionInfo>();
        }
    }

    public async Task<(bool ok, string? targetDir)> CloneRepositoryAsync(string gitUrl)
    {
        if (string.IsNullOrWhiteSpace(gitUrl))
        {
            _notifications.Error("Git URL ist leer.");
            return (false, null);
        }

        try
        {
            var repoFolder = _settings.Settings.ExtensionsRepositoryPath;
            Directory.CreateDirectory(repoFolder);

            var namePart = GetRepoName(gitUrl);
            var targetDir = Path.Combine(repoFolder, namePart);
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

    public string GetRepoName(string gitUrl)
    {
        if (!Uri.TryCreate(gitUrl, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Bei Azure DevOps/TFS steht der Reponame nach "_git"
        var gitIdx = Array.FindIndex(segments, s => s.Equals("_git", StringComparison.OrdinalIgnoreCase));
        var candidate = (gitIdx >= 0 && gitIdx < segments.Length - 1)
            ? segments[gitIdx + 1]
            : segments[^1];

        // Falls mal ein .git angehängt ist, abknipsen – Punkte im Namen bleiben erhalten
        if (candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[..^4];

        return candidate;
    }
    
    public async Task<bool> BuildAsync(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                _notifications.Error($"Ordner existiert nicht: {directory}");
                return false;
            }

            var slns = Directory.GetFiles(directory, "*.sln", SearchOption.AllDirectories);
            var projs = slns.Length == 0 ? Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories) : Array.Empty<string>();

            if (slns.Length == 0 && projs.Length == 0)
            {
                _notifications.Warning("Keine Solution oder Projekte zum Bauen gefunden.");
                return false;
            }

            foreach (var sln in slns)
            {
                if (!await RunDotnetBuildAsync(sln)) return false;
            }
            foreach (var proj in projs)
            {
                if (!await RunDotnetBuildAsync(proj)) return false;
            }

            _notifications.Success("Build erfolgreich abgeschlossen.");
            return true;
        }
        catch (Exception ex)
        {
            _notifications.Error($"Build-Fehler: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> RunDotnetBuildAsync(string path)
    {
        var workDir = System.IO.Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        // Use cmd to show a visible window with live output
        var psi = new ProcessStartInfo("cmd.exe", $"/c dotnet build \"{path}\" -c Release")
        {
            UseShellExecute = true,
            CreateNoWindow = false,
            WorkingDirectory = workDir,
            WindowStyle = ProcessWindowStyle.Normal
        };

        using var proc = Process.Start(psi);
        if (proc == null) return false;
        await proc.WaitForExitAsync();
        return proc.ExitCode == 0;
    }
}
