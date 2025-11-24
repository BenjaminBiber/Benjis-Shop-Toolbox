using System.IO;
using Microsoft.Web.Administration;
using Toolbox.Data.Models.Extensions;
using Toolbox.Data.Models.Interfaces;
using Binding = Microsoft.Web.Administration.Binding;

namespace Toolbox.Services;

public class IisService
{

    private INotificationService _notificationService;
    private ISettingsService _settingsService;
    private IAppInfoService _appinfoService;
    private ServerManager _serverManager;
    
    private readonly string _bundlerPath = "C:\\Windows\\Temp\\shopsystem\\bundler";  
    
    public IisService(INotificationService notificationService,  ISettingsService settingsService, IAppInfoService appinfo)
    {
        _notificationService = notificationService;
        _serverManager = new ServerManager();
        _settingsService = settingsService;
        _appinfoService = appinfo;
    }
  
    public SiteCollection GetSites()
    {
        return _serverManager.Sites;
    }

    public void StartTrayIconSite()
    {
        var site = _settingsService.Settings.GetTrayIconSite();
        if (site == null)
        {
            _notificationService.Error("Keine Seite gefunden");
            return;
        }
        StartIisApp(site);
    }
    
    public void StopTrayIconSite()
    {
        var site = _settingsService.Settings.GetTrayIconSite();
        if (site == null)
        {
            _notificationService.Error("Keine Seite gefunden");
            return;
        }
        StopIisApp(site);
    }
    
    public async Task RestartTrayIconSite()
    {
        var site = _settingsService.Settings.GetTrayIconSite();
        if (site == null)
        {
            _notificationService.Error("Keine Seite gefunden");
            return;
        }
        await RestartIisApp(site);
    }
    
    public void StartIisApp()
    {
        var site = _serverManager.Sites.FirstOrDefault(x => x.Name == _settingsService.Settings.IisAppName);
        if (site == null)
        {
            return;
        }
        StartIisApp(site);
    }

    public string? GetSiteNameById(long id)
    {
        return _serverManager.Sites.FirstOrDefault(x => x.Id == id)?.Name;
    }
    
     public void StartIisApp(Site site, bool showNotification = true)
    {
        try
        {
            site.Start();
            if (showNotification)
            {
                _notificationService.Success($"{site.Name} erfolgreich gestartet");
            }
        }
        catch (Exception ex)
        {
            _notificationService.Error($"Fehler beim Starten von {site.Name}:  {ex.Message}");
        }
    }

    public void StopCurrentIisApp()
    {
        var site = _serverManager.Sites.FirstOrDefault(x => x.Name == _settingsService.Settings.IisAppName);
        if (site == null)
        {
            return;
        }
        StopIisApp(site);
    }
     
    public void StopIisApp(Site site, bool showNotification = true)
    {
        try
        {
            site.Stop();
            if (showNotification)
            {
                _notificationService.Success($"{site.Name} erfolgreich gestoppt");
            }
        }
        catch (Exception ex)
        {
                _notificationService.Error($"Fehler beim Stoppen von {site.Name}:  {ex.Message}");
        }
    }

    public async Task RestartIisApp(Site? site = null)
    {
        var selectedSite = site ?? _serverManager.Sites.FirstOrDefault(x => x.Name == _settingsService.Settings.IisAppName);
        if (selectedSite == null)
        {
            _notificationService.Error("Seite konnte nicht gefunden werden");
            return;
        }
        StopIisApp(selectedSite, false);

        if (_settingsService.Settings.DeleteBundlerOnShopRestart && selectedSite.IsShop())
        {
            DeleteShopBundler();    
        }
        
        if (_settingsService.Settings.DeleteAssetsOnShopRestart && selectedSite.IsShop())
        {
            selectedSite.DeleteAssetFolder();    
        }
        
        RecycleAppPool(selectedSite);

        await Task.Delay(_settingsService.Settings.RestartDelaySeconds * 1000);
        StartIisApp(selectedSite, false);

        await _appinfoService.SetLastRestartTimeAsync(DateTime.Now);
    }

    public void RecycleAppPool(Site site, bool showNotification = true)
    {
        try
        {
            using var manager = new ServerManager();

            // Standardmäßig die Root-Application als Referenz nehmen
            var rootApp = site.Applications["/"];
            var appPoolName = rootApp?.ApplicationPoolName;

            if (string.IsNullOrWhiteSpace(appPoolName))
            {
                _notificationService.Error($"Kein Application Pool für Site '{site.Name}' gefunden.");
                return;
            }

            var pool = manager.ApplicationPools[appPoolName];
            if (pool == null)
            {
                _notificationService.Error($"AppPool '{appPoolName}' nicht gefunden.");
                return;
            }

            if (pool.State == ObjectState.Stopped)
            {
                pool.Start();
            }
            else
            {
                pool.Recycle();
            }

            if (showNotification)
            {
                _notificationService.Success($"AppPool '{appPoolName}' recycelt.");
            }
        }
        catch (Exception ex)
        {
            _notificationService.Error($"Fehler beim AppPool-Recycling: {ex.Message}");
        }
    }

    public void DeleteShopBundler()
    {
        if (Directory.Exists(_bundlerPath))
        {
            Directory.Delete(_bundlerPath, true);
            _notificationService.Success($"Shopsystem-Bundler wurde gel�scht");
        }
        else
        {
            _notificationService.Error("Bundler konnte nicht gefunden werden");
        }
    }
}

