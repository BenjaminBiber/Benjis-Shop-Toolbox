using Microsoft.Web.Administration;

namespace Toolbox.Data.Models;

public class ToolboxSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; }
    public string? IisAppName { get; set; }
    public string? LogName { get; set; }
    public string ThemeRepositoryPath { get; set; }
    public string ExtensionsRepositoryPath { get; set; }
    public string GeneralFolderPath { get; set; }
    public int AutoRefreshSeconds { get; set; } 
    public bool AutoRefreshEnabled { get; set; }
    public bool OnlySinceRestart { get; set; } 
    public bool RestartShopOnThemeChange { get; set; } 

    public int RestartDelaySeconds { get; set; } 

    public bool BundleLogs { get; set; } = false;
    public bool DeleteBundlerOnShopRestart { get; set; }
    public bool DeleteAssetsOnShopRestart { get; set; }
    public long TrayIconIisSite { get; set; }
    public List<ShopSetting> ShopSettingsList { get; set; }


    public ToolboxSettings()
    {
        Id = SingletonId;
        IisAppName = null;
        LogName = "4SELLERS";
        AutoRefreshSeconds = 60;
        ThemeRepositoryPath = "C:\\Dev_Git\\KundenThemes";
        ExtensionsRepositoryPath = "C:\\Dev_Git\\Extensions";
        GeneralFolderPath = "C:\\Dev_Git";
        AutoRefreshEnabled = false;
        OnlySinceRestart = true;
        RestartShopOnThemeChange = true;
        RestartDelaySeconds = 3;
        BundleLogs = false;
        ShopSettingsList = new List<ShopSetting>();
        DeleteBundlerOnShopRestart = false;
        TrayIconIisSite = long.MinValue;
    }

    public ShopSetting? GetShopSettingForCurrentSite()
    {
        if (ShopSettingsList == null || !ShopSettingsList.Any() || string.IsNullOrEmpty(IisAppName))
        {
            return null;
        }

        var manager = new ServerManager();
        var site = manager.Sites.FirstOrDefault(x => x.Name == IisAppName);
        if (site == null)
        {
            return null;
        }
        return ShopSettingsList.FirstOrDefault(x => x.SiteId == site.Id);
    }

    public Site? GetTrayIconSite()
    {
        var manager = new ServerManager();
        return (manager.Sites.FirstOrDefault(x => x.Id == TrayIconIisSite)) ?? manager.Sites.FirstOrDefault(x => x.Name == IisAppName);
    }
}
