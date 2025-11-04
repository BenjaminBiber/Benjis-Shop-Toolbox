using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.Administration;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Extensions;
using Toolbox.Data.Models.Interfaces;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Services;

public class SettingsService : ISettingsService
{
    private readonly InternalAppDbContext _db;
    public ToolboxSettings Settings { get; private set; }
    public bool IsConfigured { get; private set; }

    public SettingsService(InternalAppDbContext db)
    {
        _db = db;
        Load();
    }

    private void Load(string? _ = null)
    {
        try
        {
            _db.Database.EnsureCreated();
            Settings = _db.Settings.Include(x => x.ShopSettingsList).FirstOrDefault();
            if (Settings != null)
            {
                if (String.IsNullOrEmpty(Settings.IisAppName))
                {
                    var manager = new ServerManager();
                    Settings.IisAppName = manager.Sites.FirstOrDefault(x => x.State == ObjectState.Started)?.Name;
                    if (String.IsNullOrEmpty(Settings.IisAppName))
                    {
                        SaveSettings();
                    }
                }
                IsConfigured = true;
                return;
            }
        }
        catch
        {
            return ;
        }

        IsConfigured = false;
        return ;
    }

    public bool LoadSettingsFromExport()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var files = Directory.GetFiles(downloads);
        var candidate = files.Where(x => x.Contains("Toolbox-Settings-"))
                             .OrderByDescending(x => x)
                             .FirstOrDefault();
        if (candidate == null)
            return false;

        return Import(candidate);
    }

    public bool SaveSettings()
    {
        if(_db == null)
        {
            return false;
        }

        try
        {
            _db.Database.EnsureCreated();
            _db.Update(Settings);
            _db.SaveChanges();
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public void SaveSettingChanges(Action<ToolboxSettings> change, NotificationService notificationService)
    {
        if (change == null)
        {
            notificationService.Error("Keine Änderungen zum Speichern");
            return;
        }

        change(Settings);
        notificationService.Success("Einstellungen erfolgreich gespeichert");
        SaveSettings();
    }
    
    public void FillShopPathSettings(SiteCollection siteCollection)
    {
        // 1) Scan the entire GeneralFolderPath for shops first
        try
        {
            var root = Settings.GeneralFolderPath;
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                foreach (var shopFolder in EnumerateShopFolders(root))
                {
                    var yamlLower = Path.Combine(shopFolder, "shop.yaml");
                    var yamlUpper = Path.Combine(shopFolder, "Shop.yaml");
                    var yamlPath = File.Exists(yamlUpper) ? yamlUpper : yamlLower;
                    if (!File.Exists(yamlPath)) continue;

                    var themesPath = Path.Combine(shopFolder, "Themes");
                    if (!Directory.Exists(themesPath)) continue;

                    if (!Settings.ShopSettingsList.Any(x =>
                            string.Equals(x.ShopYamlPath, yamlPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        Settings.ShopSettingsList.Add(new ShopSetting
                        {
                            SiteId = long.MinValue, // not bound to an IIS site
                            ShopYamlPath = yamlPath,
                            ThemeFolderPath = themesPath
                        });
                    }
                }
            }
        }
        catch
        {
            // ignore scanning errors
        }

        // 2) Then augment with IIS sites (if any) for paths not already present
        foreach (var site in siteCollection)
        {
            if (site == null) continue;

            var physicalPath = site.GetSitePath();
            if (site.IsShop())
            {
                var yamlPath = Path.Combine(physicalPath, "Shop.yaml");
                var themesPath = Path.Combine(physicalPath, "Themes");
                if (!Settings.ShopSettingsList.Any(x =>
                        x.SiteId == site.Id ||
                        string.Equals(x.ShopYamlPath, yamlPath, StringComparison.OrdinalIgnoreCase)))
                {
                    Settings.ShopSettingsList.Add(new ShopSetting
                    {
                        SiteId = site.Id,
                        ShopYamlPath = yamlPath,
                        ThemeFolderPath = themesPath
                    });
                }
            }
        }
        SaveSettings();
    }

    private static IEnumerable<string> EnumerateShopFolders(string root)
    {
        var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", ".hg", ".vs", ".idea", ".vscode",
            "bin", "obj", "packages", "node_modules"
        };

        var q = new Queue<string>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            string current;
            try { current = q.Dequeue(); }
            catch { yield break; }

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly); }
            catch { subdirs = Array.Empty<string>(); }

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (ignore.Contains(name)) continue;

                // Heuristik: Shop-Ordner enthält Shop.yaml/shop.yaml und Themes
                var hasYaml = File.Exists(Path.Combine(sub, "Shop.yaml")) || File.Exists(Path.Combine(sub, "shop.yaml"));
                var hasThemes = Directory.Exists(Path.Combine(sub, "Themes"));
                if (hasYaml && hasThemes)
                {
                    yield return sub;
                }
                else
                {
                    q.Enqueue(sub);
                }
            }
        }
    }
    
    public bool AreThereAllShopPathSettings(SiteCollection siteCollection)
    {
        Load();
        // Als 'gefüllt' gilt bereits, wenn mindestens ein ShopSetting existiert.
        // Die eigentliche Befüllung scannt zuerst den GeneralFolderPath, danach IIS.
        return Settings.ShopSettingsList != null && Settings.ShopSettingsList.Any();
    }
    
    public bool ChangeSelectedIisApp(string iis)
    {
        if(_db == null)
        {
            return false;
        }

        try
        {
            _db.Database.EnsureCreated();
            Settings.IisAppName = iis;
            _db.SaveChanges();
            return true;
        }
        catch (Exception e)
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
            return SaveSettings();
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
            var fileName = $"Toolbox-Settings-{DateTime.Now:yyyy-MM-dd}.json";
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
