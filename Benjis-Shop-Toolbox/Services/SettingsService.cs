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

        public void Save()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            IsConfigured = !string.IsNullOrWhiteSpace(Settings.IisAppName) && !string.IsNullOrWhiteSpace(Settings.LogName);
        }
    }
}
