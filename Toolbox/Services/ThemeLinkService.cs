using System.IO;
using System.Linq;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Interfaces;
using Toolbox.Data.Services;

namespace Toolbox.Services;

public class ThemeLinkService
{
    private readonly INotificationService _notifications;
    private readonly SettingsService _settings;
    private readonly GitRepoService _git;
    private readonly CacheService _cache;

    public ThemeLinkService(INotificationService notifications, SettingsService settings, GitRepoService git, CacheService cache)
    {
        _notifications = notifications;
        _settings = settings;
        _git = git;
        _cache = cache;
    }

    public IEnumerable<ThemeInfo> GetThemes(string shopThemesPath, string shopYamlPath)
    {
        return _cache.GetThemes(shopThemesPath, shopYamlPath, () =>
        {
            try
            {
                if (string.IsNullOrEmpty(shopYamlPath))
                    return new List<ThemeInfo>();

                var config = ShopYamlService.LoadConfiguration(shopYamlPath);
                var repos = _settings.Settings.GetThemeRoots().ToList();
                var shop = shopThemesPath;
                var themes = new List<ThemeInfo>();

                foreach (var repo in repos)
                {
                    if (!Directory.Exists(repo))
                        continue;

                    foreach (var dir in Directory.EnumerateDirectories(repo, "Themes", SearchOption.AllDirectories))
                    {
                        foreach (var themeDir in Directory.EnumerateDirectories(dir))
                        {
                            var name = Path.GetFileName(themeDir);
                            var relative = Path.GetRelativePath(repo, themeDir);
                            var repoName = relative.Split(Path.DirectorySeparatorChar)[0];
                            var linkPath = Path.Combine(shop, name);
                            bool exists = File.Exists(linkPath) || Directory.Exists(linkPath);
                            var isOverwrite = string.Equals(config.ZionConfiguration.ThemeOverwrite, name, StringComparison.OrdinalIgnoreCase);
                            themes.Add(new ThemeInfo(name, themeDir, exists, repoName, Path.Combine(repo, repoName), isOverwrite));
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
        });
    }

    public ThemeInfo GetThemeByName(string repoName, string shopThemesPath, string shopYamlPath)
    {
        var themes = GetThemes(shopThemesPath, shopYamlPath);
        return themes.FirstOrDefault(x => x.ThemeFolder.Contains(repoName, StringComparison.OrdinalIgnoreCase)) ?? new ThemeInfo();
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
            ShopYamlService.SetNewThemeOverwrite(shopYamlPath, theme.Name);
            _notifications.Success($"Theme wurde auf {theme.Name} gesetzt.");
            _cache.InvalidateThemes(shopYamlPath: shopYamlPath);
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

            _notifications.Success($"Link fuer {theme.Name} erstellt.");
            _cache.InvalidateThemes(shopThemesPath);
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

            _notifications.Success($"Link fuer {theme.Name} entfernt.");
            _cache.InvalidateThemes(shopThemesPath);
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
            _cache.InvalidateThemes(shopThemesPath, shopYamlPath);
            var themes = GetThemes(shopThemesPath, shopYamlPath).Where(t => t.Repo == repoName).ToList();
            foreach (var theme in themes)
            {
                if (!theme.LinkExists)
                    CreateLink(shopThemesPath, theme);
            }

            if (themes.Count > 0)
                SetThemeOverwrite(shopYamlPath, themes[0]);

            _cache.InvalidateThemes(shopThemesPath, shopYamlPath);
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
        var repoFolder = _settings.Settings.GetThemeRoots().FirstOrDefault(Directory.Exists)
                         ?? _settings.Settings.ThemeRepositoryPath;
        if (string.IsNullOrWhiteSpace(repoFolder))
        {
            _notifications.Error("Kein Theme-Repo Pfad konfiguriert.");
            return false;
        }
        var (ok, _) = await _git.CloneRepositoryAsync(gitUrl, repoFolder);
        if (ok)
        {
            _cache.InvalidateThemes();
        }
        return ok;
    }
}
