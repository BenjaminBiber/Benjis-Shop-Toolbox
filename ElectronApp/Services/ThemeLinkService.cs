using System.IO;
using System.Diagnostics;
using Benjis_Shop_Toolbox.Models;
using System.Linq;

namespace Benjis_Shop_Toolbox.Services
{
    public class ThemeLinkService
    {
        private readonly SettingsService _settings;
        private readonly NotificationService _notifications;

        public ThemeLinkService(SettingsService settings, NotificationService notifications)
        {
            _settings = settings;
            _notifications = notifications;
        }

        public IEnumerable<ThemeInfo> GetThemes(string shopThemesPath, string shopYamlPath)
        {
            try
            {
                if (string.IsNullOrEmpty(shopYamlPath))
                {
                    return new List<ThemeInfo>();
                }

                var config = ShopYamlLoader.LoadConfiguration(shopYamlPath);
                var repo = _settings.Settings.RepoPath;
                var shop = shopThemesPath;
                var themes = new List<ThemeInfo>();
                if (Directory.Exists(repo))
                {
                    foreach (var dir in Directory.EnumerateDirectories(repo, "Themes", SearchOption.AllDirectories))
                    {
                        foreach (var themeDir in Directory.EnumerateDirectories(dir))
                        {
                            var name = Path.GetFileName(themeDir);
                            var relative = Path.GetRelativePath(repo, themeDir);
                            var repoName = relative.Split(Path.DirectorySeparatorChar)[0];
                            var linkPath = Path.Combine(shop, name);
                            bool exists = File.Exists(linkPath) || Directory.Exists(linkPath);
                            themes.Add(new ThemeInfo(name, themeDir, exists, repoName,Path.Combine(repo, repoName), ShopYamlLoader.IsThemeOverride(config, name)));
                        }
                    }
                }
                return themes;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Laden der Themes: {ex.Message}");
                return new List<ThemeInfo>();
            }
        }

        public ThemeInfo GetThemeByName(string repoName, string shopThemesPath, string shopYamlPath)
        {
            var themes = GetThemes(shopThemesPath, shopYamlPath);
            return themes.FirstOrDefault(x => x.Name.ToLower() == repoName.ToLower()) ?? new ThemeInfo();
        }
        
        public bool SetThemeOverwrite(string shopYamlPath, ThemeInfo theme)
        {
            if (string.IsNullOrEmpty(shopYamlPath))
            {
                _notifications.Error("Kein Pfad zur shop.yaml konfiguriert.");
                return false;
            }

            try
            {
                var config = ShopYamlLoader.LoadConfiguration(shopYamlPath);
                config.ThemeOverwrite = theme.Name;
                ShopYamlLoader.UpdateConfig(shopYamlPath, theme.Name);
                _notifications.Success($"Theme wurde auf {theme.Name} gesetzt.");
                return true;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Setzen des Themes: {ex.Message}");
                return false;
            }
        }
        
        public bool CreateLink(string shopThemesPath, ThemeInfo theme)
        {
            try
            {
                var linkPath = Path.Combine(shopThemesPath, theme.Name);
                if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
                {
                    Directory.CreateSymbolicLink(linkPath, theme.Path);
                }
                _notifications.Success($"Link für {theme.Name} erstellt.");
                return true;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Erstellen des Links: {ex.Message}");
                return false;
            }
        }

        public bool RemoveLink(string shopThemesPath, ThemeInfo theme)
        {
            try
            {
                var linkPath = Path.Combine(shopThemesPath, theme.Name);
                if (File.Exists(linkPath))
                {
                    File.Delete(linkPath);
                }
                else if (Directory.Exists(linkPath))
                {
                    Directory.Delete(linkPath, true);
                }

                _notifications.Success($"Link für {theme.Name} entfernt.");
                return true;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Entfernen des Links: {ex.Message}");
                return false;
            }
        }

        public bool LinkAndOverwrite(string repoName, string shopThemesPath, string shopYamlPath)
        {
            try
            {
                var themes = GetThemes(shopThemesPath, shopYamlPath).Where(t => t.Repo == repoName).ToList();
                foreach (var theme in themes)
                {
                    if (!theme.LinkExists)
                    {
                        CreateLink(shopThemesPath, theme);
                    }
                }
                if (themes.Count > 0)
                {
                    SetThemeOverwrite(shopYamlPath, themes[0]);
                }
                return true;
            }
            catch (Exception ex)
            {
                _notifications.Error($"Fehler beim Verlinken: {ex.Message}");
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
                var repoFolder = _settings.Settings.RepoPath;
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
                _notifications.Error(string.IsNullOrWhiteSpace(error) ?
                    "Fehler beim Klonen." : $"Fehler beim Klonen: {error}");
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
