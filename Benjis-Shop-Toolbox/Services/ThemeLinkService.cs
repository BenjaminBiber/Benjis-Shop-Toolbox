using System.IO;
using Benjis_Shop_Toolbox.Models;

namespace Benjis_Shop_Toolbox.Services
{
    public class ThemeLinkService
    {
        private readonly SettingsService _settings;
        public ThemeLinkService(SettingsService settings)
        {
            _settings = settings;
        }

        public IEnumerable<ThemeInfo> GetThemes()
        {
            var repo = _settings.Settings.RepoPath;
            var shop = _settings.Settings.ShopThemesPath;
            var themes = new List<ThemeInfo>();
            if (Directory.Exists(repo))
            {
                foreach (var dir in Directory.EnumerateDirectories(repo, "Themes", SearchOption.AllDirectories))
                {
                    foreach (var themeDir in Directory.EnumerateDirectories(dir))
                    {
                        var name = Path.GetFileName(themeDir);
                        var linkPath = Path.Combine(shop, name);
                        bool exists = File.Exists(linkPath) || Directory.Exists(linkPath);
                        themes.Add(new ThemeInfo(name, themeDir, exists));
                    }
                }
            }
            return themes;
        }

        public void CreateLink(ThemeInfo theme)
        {
            var linkPath = Path.Combine(_settings.Settings.ShopThemesPath, theme.Name);
            if (!Directory.Exists(linkPath) && !File.Exists(linkPath))
            {
                Directory.CreateSymbolicLink(linkPath, theme.Path);
            }
        }

        public void RemoveLink(ThemeInfo theme)
        {
            var linkPath = Path.Combine(_settings.Settings.ShopThemesPath, theme.Name);
            if (File.Exists(linkPath) || Directory.Exists(linkPath))
            {
                File.Delete(linkPath);
            }
        }
    }
}
