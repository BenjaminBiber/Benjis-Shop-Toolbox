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
    private readonly GitRepoService _git;

    public ExtensionsService(INotificationService notifications, SettingsService settings, GitRepoService git)
    {
        _notifications = notifications;
        _settings = settings;
        _git = git;
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
                var hasShopProject = Directory.GetDirectories(dir, "*.Shop", SearchOption.AllDirectories).Length > 0;
                var hasDataProject = Directory.GetDirectories(dir, "*.Data", SearchOption.AllDirectories).Length > 0;
                var hasInstallProject = Directory.GetDirectories(dir, "*.Install", SearchOption.AllDirectories).Length > 0;
                var isThemeV4 = Directory.GetDirectories(dir, "4SELLERS_Responsive_4", SearchOption.AllDirectories).Length > 0;
                list.Add(new ExtensionInfo
                {
                    Name = name,
                    Path = dir,
                    HasSolution = hasSln,
                    HasProjects = hasProj,
                    HasShopProject = hasShopProject,
                    HasDataProject = hasDataProject,
                    HasInstallProject = hasInstallProject,
                    HasThemeV4 = isThemeV4
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
        var repoFolder = _settings.Settings.ExtensionsRepositoryPath;
        return await _git.CloneRepositoryAsync(gitUrl, repoFolder);
    }

    public string GetRepoName(string gitUrl) => _git.GetRepoName(gitUrl);
    
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

