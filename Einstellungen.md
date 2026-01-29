# Einstellungen – Settings-Übersicht

Diese Tabelle listet alle Settings, die in der Seite **Einstellungen** konfigurierbar sind.

| Bereich | UI-Label | Setting (Code) | Beschreibung |
| --- | --- | --- | --- |
| Allgemein · Themes | Repo Pfade | `ToolboxSettings.ThemeRepositoryPath` (via `GetThemeRoots/SetThemeRoots`) | Liste der Theme-Repository-Wurzelordner (mehrere Pfade sind möglich). Wird zum Finden/Verwalten von Themes genutzt. |
| Allgemein · Themes | Shop bei Theme-Wechsel neustarten | `ToolboxSettings.RestartShopOnThemeChange` | Startet den Shop automatisch neu, wenn ein Theme-Wechsel erfolgt. |
| Allgemein · Extensions | Repo Pfade | `ToolboxSettings.ExtensionsRepositoryPath` (via `GetExtensionRoots/SetExtensionRoots`) | Liste der Extension-Repository-Wurzelordner (mehrere Pfade sind möglich). Wird zum Finden/Verwalten von Extensions genutzt. |
| Allgemein · Pfade | Shopsystem Ordnerpfad | `ToolboxSettings.GeneralFolderPath` | Basis-Ordner, der nach Shops (Shop.yaml + Themes) gescannt wird. Dient u. a. zum Befüllen der Shop-Listen. |
| Allgemein · Updater | Beta-Versionen installieren | `ToolboxSettings.AllowBetaUpdates` | Erlaubt dem Updater, Beta-Versionen zu berücksichtigen/zu installieren. |
| Logs · Ereignisanzeige | Log-Name | `ToolboxSettings.LogName` | Name des Windows-Ereignislogs, aus dem die Logs gelesen werden. |
| Logs · Ereignisanzeige | Zeitlicher Abstand zwischen automatischem Neuladen der Logs | `ToolboxSettings.AutoRefreshSeconds` (setzt zusätzlich `AutoRefreshEnabled`) | Intervall in Sekunden für das automatische Neuladen der Logs. Ein Wert von 0 deaktiviert Auto-Refresh. |
| Logs · Ereignisanzeige | Gleiche Logs bündeln | `ToolboxSettings.BundleLogs` | Fasst identische/ähnliche Logeinträge zusammen, um die Anzeige zu reduzieren. |
| IIS · Neustart | Zeit zwischen Start und Stop von IIS-Seiten (Sekunden) | `ToolboxSettings.RestartDelaySeconds` | Wartezeit zwischen Stop und Start beim IIS-Neustart. |
| IIS · Neustart | Shop-Bundler beim Neustart löschen | `ToolboxSettings.DeleteBundlerOnShopRestart` | Löscht beim Neustart den Shop-Bundler, bevor der Shop wieder startet. |
| IIS · Neustart | Shop-Assets (wwwroot) beim Neustart löschen | `ToolboxSettings.DeleteAssetsOnShopRestart` | Löscht beim Neustart das `wwwroot`-Assets-Verzeichnis des Shops. |
| IIS · Taskbar | Shop zum Starten/Stoppen/Neustarten über die Taskbar | `ToolboxSettings.TrayIconIisSite` | Legt fest, welche IIS-Site über die Taskbar-Aktionen (Tray-Icon) gesteuert wird. |
