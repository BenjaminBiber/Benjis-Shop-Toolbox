using System.Diagnostics;
using System.IO;
using System.Linq;
using Benjis_Shop_Toolbox.Models;

namespace Benjis_Shop_Toolbox.Services
{

public class ExtensionService
{
    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;

    public ExtensionService(SettingsService settings, NotificationService notifications)
    {
        _settings = settings;
        _notifications = notifications;
    }

    public IEnumerable<ExtensionInfo> GetExtensions(string shopExtensionsPath)
    {
        try
        {
            var repo = _settings.Settings.ExtensionRepoPath;
            var extensions = new List<ExtensionInfo>();
            if (Directory.Exists(repo))
            {
                foreach (var dir in Directory.EnumerateDirectories(repo, "Extensions", SearchOption.AllDirectories))
                {
                    foreach (var extDir in Directory.EnumerateDirectories(dir))
                    {
                        var name = Path.GetFileName(extDir);
                        var relative = Path.GetRelativePath(repo, extDir);
                        var repoName = relative.Split(Path.DirectorySeparatorChar)[0];
                        var linkPath = Path.Combine(shopExtensionsPath, name);
                        bool exists = File.Exists(linkPath) || Directory.Exists(linkPath);
                        extensions.Add(new ExtensionInfo(name, extDir, exists, repoName, Path.Combine(repo, repoName)));
                    }
                }
            }
            return extensions;
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Laden der Extensions: {ex.Message}");
            return new List<ExtensionInfo>();
        }
    }

    public ExtensionInfo GetExtensionByName(string repoName, string shopExtensionsPath)
    {
        var extensions = GetExtensions(shopExtensionsPath);
        return extensions.FirstOrDefault(x => x.Name.ToLower() == repoName.ToLower()) ?? new ExtensionInfo();
    }

    public bool CreateLink(string shopExtensionsPath, ExtensionInfo extension)
    {
        try
        {
            var linkPath = Path.Combine(shopExtensionsPath, extension.Name);
            if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
            {
                Directory.CreateSymbolicLink(linkPath, extension.Path);
            }
            _notifications.Success($"Link für {extension.Name} erstellt.");
            return true;
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Erstellen des Links: {ex.Message}");
            return false;
        }
    }

    public bool RemoveLink(string shopExtensionsPath, ExtensionInfo extension)
    {
        try
        {
            var linkPath = Path.Combine(shopExtensionsPath, extension.Name);
            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }
            else if (Directory.Exists(linkPath))
            {
                Directory.Delete(linkPath, true);
            }

            _notifications.Success($"Link für {extension.Name} entfernt.");
            return true;
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Entfernen des Links: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CloneRepositoryAsync(string gitUrl)
    {
        if (string.IsNullOrWhiteSpace(gitUrl))
        {
            _notifications.Error("Git URL ist leer.");
            return false;
        }

        try
        {
            var repoFolder = _settings.Settings.ExtensionRepoPath;
            Directory.CreateDirectory(repoFolder);

            var namePart = Path.GetFileNameWithoutExtension(gitUrl.TrimEnd('/')
                .Split('/').Last());
            var targetDir = Path.Combine(repoFolder, namePart);
            if (Directory.Exists(targetDir))
            {
                _notifications.Warning($"Repository {namePart} existiert bereits.");
                return false;
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
                return false;
            }

            await proc.WaitForExitAsync();
            if (proc.ExitCode == 0)
            {
                _notifications.Success($"Repository {namePart} geklont.");
                return true;
            }
            var error = await proc.StandardError.ReadToEndAsync();
            _notifications.Error(string.IsNullOrWhiteSpace(error)
                ? "Fehler beim Klonen." : $"Fehler beim Klonen: {error}");
            return false;
        }
        catch (Exception ex)
        {
            _notifications.Error($"Fehler beim Klonen: {ex.Message}");
            return false;
        }
    }
}
}
