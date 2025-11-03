using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Web.Administration;
using Toolbox.Data.Models;
using Toolbox.Data.Models.Extensions;
using Toolbox.Data.Models.Interfaces;
using Toolbox.DataContexts;
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
        foreach (var site in siteCollection)
        {
            if (site == null)
            {
                continue;  
            }
            
            string physicalPath = site.GetSitePath();

            if (site.IsShop())
            {
                if (!Settings.ShopSettingsList.Any(x => x.SiteId == site.Id))
                {
                    var shopSetting = new ShopSetting()
                    {
                        SiteId = site.Id,
                        ShopYamlPath = physicalPath + @"\Shop.yaml",
                        ThemeFolderPath = physicalPath + @"\Themes"
                    };
                    Settings.ShopSettingsList.Add(shopSetting);
                }
            }
        }
        SaveSettings();
    }
    
    public bool AreThereAllShopPathSettings(SiteCollection siteCollection)
    {
        Load();
        return Settings.ShopSettingsList.Count == siteCollection.Count;
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
