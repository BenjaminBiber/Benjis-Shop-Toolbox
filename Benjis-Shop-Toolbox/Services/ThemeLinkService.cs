using System.IO;
using System.Diagnostics;
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
                        var relative = Path.GetRelativePath(repo, themeDir);
                        var repoName = relative.Split(Path.DirectorySeparatorChar)[0];
                        var linkPath = Path.Combine(shop, name);
                        bool exists = File.Exists(linkPath) || Directory.Exists(linkPath);
                        themes.Add(new ThemeInfo(name, themeDir, exists, repoName));
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

        public async Task<bool> CloneRepositoryAsync(string gitUrl)
        {
            if (string.IsNullOrWhiteSpace(gitUrl))
                return false;

            var repoFolder = _settings.Settings.RepoPath;
            Directory.CreateDirectory(repoFolder);

            var namePart = Path.GetFileNameWithoutExtension(gitUrl.TrimEnd('/')
                .Split('/').Last());
            var targetDir = Path.Combine(repoFolder, namePart);
            if (Directory.Exists(targetDir))
                return false;

            var psi = new ProcessStartInfo("git", $"clone {gitUrl} \"{targetDir}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            if (proc == null)
                return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
    }
}
