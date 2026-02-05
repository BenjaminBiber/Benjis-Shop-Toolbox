using Microsoft.Web.Administration;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Toolbox.Data.Models;

public class ToolboxSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; }
    public string? IisAppName { get; set; }
    [Description("Liste der Windows-Ereignislog-Namen. Mehrere Logs sind möglich (Trennung per Semikolon). Hinweis: Je mehr Logs ausgewählt sind, desto länger kann das Laden dauern.")]
    public string? LogName { get; set; }
    [Description("Liste der TFS/Azure-DevOps Projekt-URLs (Trennung per Semikolon).")]
    public string? TfsProjectUrls { get; set; }
    [Description("TFS/Azure-DevOps Collection-URL (z.B. https://tfs.server/tfs/Collection).")]
    public string? TfsCollectionUrl { get; set; }
    [Description("TFS/Azure-DevOps API Key (PAT) für die Repository-Abfrage.")]
    public string? TfsApiKey { get; set; }
    [Description("Liste der Theme-Repository-Wurzelordner (mehrere Pfade sind möglich). Wird zum Finden/Verwalten von Themes genutzt.")]
    public string ThemeRepositoryPath { get; set; }
    [Description("Liste der Extension-Repository-Wurzelordner (mehrere Pfade sind möglich). Wird zum Finden/Verwalten von Extensions genutzt.")]
    public string ExtensionsRepositoryPath { get; set; }
    [Description("Basis-Ordner, der nach Shops (Shop.yaml + Themes) gescannt wird. Dient u. a. zum Befüllen der Shop-Listen.")]
    public string GeneralFolderPath { get; set; }
    [Description("Intervall in Sekunden für das automatische Neuladen der Logs. Ein Wert von 0 deaktiviert Auto-Refresh.")]
    public int AutoRefreshSeconds { get; set; } 
    public bool AutoRefreshEnabled { get; set; }
    public bool OnlySinceRestart { get; set; } 
    [Description("Startet den Shop automatisch neu, wenn ein Theme-Wechsel erfolgt.")]
    public bool RestartShopOnThemeChange { get; set; } 

    [Description("Wartezeit zwischen Stop und Start beim IIS-Neustart.")]
    public int RestartDelaySeconds { get; set; } 

    [Description("Fasst identische/ähnliche Logeinträge zusammen, um die Anzeige zu reduzieren.")]
    public bool BundleLogs { get; set; } = false;
    [Description("Erlaubt dem Updater, Beta-Versionen zu berücksichtigen/zu installieren.")]
    public bool AllowBetaUpdates { get; set; }
    [Description("Löscht beim Neustart den Shop-Bundler, bevor der Shop wieder startet.")]
    public bool DeleteBundlerOnShopRestart { get; set; }
    [Description("Löscht beim Neustart das wwwroot-Assets-Verzeichnis des Shops.")]
    public bool DeleteAssetsOnShopRestart { get; set; }
    [Description("Legt fest, welche IIS-Site über die Taskbar-Aktionen (Tray-Icon) gesteuert wird.")]
    public long TrayIconIisSite { get; set; }
    public string? PinnedExtensionGroups { get; set; }
    public string? PinnedThemeGroups { get; set; }
    public List<ShopSetting> ShopSettingsList { get; set; }


    public ToolboxSettings()
    {
        Id = SingletonId;
        IisAppName = null;
        LogName = "4SELLERS";
        TfsProjectUrls = string.Empty;
        TfsCollectionUrl = "https://tfs.4sellers.de/tfs/ERP-Kunden/";
        TfsApiKey = string.Empty;
        AutoRefreshSeconds = 60;
        ThemeRepositoryPath = "C:\\Dev_Git\\KundenThemes";
        ExtensionsRepositoryPath = "C:\\Dev_Git\\Extensions";
        GeneralFolderPath = "C:\\Dev_Git";
        AutoRefreshEnabled = false;
        OnlySinceRestart = true;
        RestartShopOnThemeChange = true;
        RestartDelaySeconds = 3;
        BundleLogs = false;
        AllowBetaUpdates = false;
        ShopSettingsList = new List<ShopSetting>();
        DeleteBundlerOnShopRestart = false;
        TrayIconIisSite = long.MinValue;
        PinnedExtensionGroups = null;
        PinnedThemeGroups = null;
    }

    public IEnumerable<string> GetExtensionRoots() => SplitPaths(ExtensionsRepositoryPath);
    public IEnumerable<string> GetThemeRoots() => SplitPaths(ThemeRepositoryPath);
    public IEnumerable<string> GetLogNames() => SplitPaths(LogName);
    public IEnumerable<string> GetTfsProjectUrls() => SplitPaths(TfsProjectUrls);

    public string? GetPrimaryExtensionRoot() => GetExtensionRoots().FirstOrDefault();
    public string? GetPrimaryThemeRoot() => GetThemeRoots().FirstOrDefault();

    public void SetExtensionRoots(IEnumerable<string> paths) => ExtensionsRepositoryPath = JoinPaths(paths);
    public void SetThemeRoots(IEnumerable<string> paths) => ThemeRepositoryPath = JoinPaths(paths);
    public void SetLogNames(IEnumerable<string> names) => LogName = JoinPaths(names);
    public void SetTfsProjectUrls(IEnumerable<string> urls) => TfsProjectUrls = JoinPaths(urls);

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

