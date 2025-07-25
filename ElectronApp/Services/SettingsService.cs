using System.IO;
using System.Text.Json;
using Benjis_Shop_Toolbox.Models;

namespace Benjis_Shop_Toolbox.Services
{
    public class SettingsService
    {
        private readonly string _filePath;
        public ToolboxSettings Settings { get; private set; }
        public bool IsConfigured { get; private set; }

        public SettingsService()
        {
            _filePath = Path.Combine(AppContext.BaseDirectory, "settings.json");
            Settings = Load();
        }

        private ToolboxSettings Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var settings = JsonSerializer.Deserialize<ToolboxSettings>(json);
                    if (settings != null)
                    {
                        IsConfigured = !string.IsNullOrWhiteSpace(settings.IisAppName)
                            && !string.IsNullOrWhiteSpace(settings.LogName);
                        return settings;
                    }
                }
            }
            catch
            {
                // ignore errors and fall back to defaults
            }

            IsConfigured = false;
            return new ToolboxSettings();
        }

        public bool Save(string? iis = null, string? repoPath = null, string? shopPath = null)
        {
            if (!string.IsNullOrEmpty(iis))
            {
                Settings.IisAppName = iis;
            }
            if (!string.IsNullOrEmpty(repoPath))
            {
                Settings.RepoPath = repoPath;
            }
            if (!string.IsNullOrEmpty(shopPath))
            {
                Settings.ShopThemesPath = shopPath;
            }

            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
                IsConfigured = !string.IsNullOrWhiteSpace(Settings.IisAppName) &&
                    !string.IsNullOrWhiteSpace(Settings.LogName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Import(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<ToolboxSettings>(json);
                if (settings == null)
                {
                    return false;
                }

                Settings = settings;
                return Save();
            }
            catch
            {
                return false;
            }
        }

        public bool Export()
        {
            try
            {
                var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloads);
                var fileName = $"settings-{DateTime.Now:yyyy-MM-dd}.json";
                var destPath = Path.Combine(downloads, fileName);
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(destPath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
