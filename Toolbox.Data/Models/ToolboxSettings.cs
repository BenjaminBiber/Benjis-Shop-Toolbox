using Microsoft.Web.Administration;
using System.Collections.Generic;
using System.Linq;

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
    public string? PinnedExtensionGroups { get; set; }
    public string? PinnedThemeGroups { get; set; }
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
        PinnedExtensionGroups = null;
        PinnedThemeGroups = null;
    }

    public IEnumerable<string> GetExtensionRoots() => SplitPaths(ExtensionsRepositoryPath);
    public IEnumerable<string> GetThemeRoots() => SplitPaths(ThemeRepositoryPath);

    public string? GetPrimaryExtensionRoot() => GetExtensionRoots().FirstOrDefault();
    public string? GetPrimaryThemeRoot() => GetThemeRoots().FirstOrDefault();

    public void SetExtensionRoots(IEnumerable<string> paths) => ExtensionsRepositoryPath = JoinPaths(paths);
    public void SetThemeRoots(IEnumerable<string> paths) => ThemeRepositoryPath = JoinPaths(paths);

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

    private static IEnumerable<string> SplitPaths(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Enumerable.Empty<string>();

        return raw
            .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static string JoinPaths(IEnumerable<string> paths)
    {
        if (paths == null)
            return string.Empty;

        var normalized = paths
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join(";", normalized);
    }
}
