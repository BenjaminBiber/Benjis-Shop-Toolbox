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
    
    public async Task<bool> BuildAsync(string directory, IProgress<string>? outputProgress = null, IProgress<string>? errorProgress = null)
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
                if (!await RunDotnetBuildAsync(sln, outputProgress, errorProgress)) return false;
            }
            foreach (var proj in projs)
            {
                if (!await RunDotnetBuildAsync(proj, outputProgress, errorProgress)) return false;
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

    private static async Task<bool> RunDotnetBuildAsync(string path, IProgress<string>? outputProgress, IProgress<string>? errorProgress)
    {
        var workDir = System.IO.Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        var psi = new ProcessStartInfo("cmd.exe", $"/c dotnet build \"{path}\" -c Release")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        try
        {
            if (!proc.Start())
            {
                errorProgress?.Report("dotnet build konnte nicht gestartet werden." + Environment.NewLine);
                return false;
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

            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            errorProgress?.Report($"Fehler beim Starten von dotnet build: {ex.Message}{Environment.NewLine}");
            return false;
        }
    }
}

