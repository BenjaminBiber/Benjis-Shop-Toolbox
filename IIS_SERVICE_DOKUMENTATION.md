# IIS-Service – Technisches Manuskript

> **Projekt:** TeamShop.ShopToolbox
> **Erstellt:** 2026-03-31
> **Autor:** Benjamin Biber

---

## Inhaltsverzeichnis

1. [Übersicht & Architektur](#1-übersicht--architektur)
2. [Klasse & Abhängigkeiten](#2-klasse--abhängigkeiten)
3. [Alle Methoden im Detail](#3-alle-methoden-im-detail)
4. [Extension Methods für Site](#4-extension-methods-für-site)
5. [Extension Methods für Binding](#5-extension-methods-für-binding)
6. [Einstellungen & Konfiguration](#6-einstellungen--konfiguration)
7. [AppInfo – Neustart-Zeitverfolgung](#7-appinfo--neustart-zeitverfolgung)
8. [Home.razor – UI-Integration](#8-homerazor--ui-integration)
9. [Sites.razor – UI-Integration](#9-sitesrazor--ui-integration)
10. [Shop.razor – UI-Integration](#10-shoprazor--ui-integration)
11. [IisSettings.razor – Konfigurationsseite](#11-iissettingsrazor--konfigurationsseite)
12. [Fehlerbehandlung & Benachrichtigungen](#12-fehlerbehandlung--benachrichtigungen)
13. [Dependency Injection](#13-dependency-injection)
14. [Vollständiges Ablaufdiagramm: Neustart](#14-vollständiges-ablaufdiagramm-neustart)
15. [Referenz: Alle Konfigurationswerte](#15-referenz-alle-konfigurationswerte)

---

## 1. Übersicht & Architektur

Der `IisService` ist der zentrale Dienst für die Verwaltung von **IIS-Websites (Sites)** und **Application Pools** in der ShopToolbox. Er abstrahiert die `Microsoft.Web.Administration`-API hinter einer einfachen Schnittstelle und ist in mehrere Razor-Seiten integriert.

### Was der Service leistet

| Aufgabe | Beschreibung |
|---|---|
| Sites steuern | Start, Stop, Restart von IIS-Websites |
| App Pools verwalten | Recycle oder Start des Application Pools |
| Shop-spezifische Aktionen | Bundler/Assets löschen vor Neustart |
| Neustart-Zeit speichern | Zeitstempel in Datenbank nach Restart |
| Tray-Icon-Steuerung | IIS-Kontrolle über Windows-Taskleiste |
| Benachrichtigungen | Erfolgs- und Fehlermeldungen an den Benutzer |

### Abhängige Komponenten

```
IisService
  │
  ├── Microsoft.Web.Administration.ServerManager  ← IIS-API
  ├── INotificationService                        ← Toast-Benachrichtigungen
  ├── ISettingsService                            ← Benutzereinstellungen
  └── IAppInfoService                             ← Neustart-Zeit in DB speichern
```

---

## 2. Klasse & Abhängigkeiten

**Datei:** `Toolbox.Data/Services/IisService.cs`
**DI-Registrierung:** `Program.cs`, Zeile 86 → `builder.Services.AddScoped<IisService>()`

### Klassenfelder

```csharp
public class IisService
{
    private INotificationService _notificationService;
    private ISettingsService _settingsService;
    private IAppInfoService _appinfoService;
    private ServerManager _serverManager;

    // Fester Pfad zum Shop-Bundler-Verzeichnis
    private readonly string _bundlerPath = "C:\\Windows\\Temp\\shopsystem\\bundler";
}
```

### Konstruktor

```csharp
public IisService(
    INotificationService notificationService,
    ISettingsService settingsService,
    IAppInfoService appinfo)
{
    _notificationService = notificationService;
    _serverManager = new ServerManager();   // ← Neue IIS-Verbindung bei jeder Instanz
    _settingsService = settingsService;
    _appinfoService = appinfo;
}
```

> **Hinweis:** Da `IisService` als `Scoped` registriert ist, wird bei jeder Blazor-Komponente eine neue Instanz erstellt – und damit auch ein neuer `ServerManager`.

---

## 3. Alle Methoden im Detail

### 3.1 `GetSites()`

```csharp
public SiteCollection GetSites()
```

**Was es tut:** Gibt alle konfigurierten IIS-Websites zurück.

**Ablauf:**
1. Ruft `_serverManager.Sites` ab
2. Gibt die komplette `SiteCollection` direkt zurück

**Rückgabe:** `SiteCollection` (alle IIS-Sites des Systems)

**Verwendet von:** `Home.razor` – füllt das Site-Auswahl-Dropdown

---

### 3.2 `GetSiteNameById()`

```csharp
public string? GetSiteNameById(long id)
```

**Was es tut:** Sucht eine IIS-Site anhand ihrer numerischen ID und gibt den Namen zurück.

**Ablauf:**
```csharp
return _serverManager.Sites.FirstOrDefault(x => x.Id == id)?.Name;
```

**Rückgabe:** Site-Name als `string` oder `null` wenn nicht gefunden

**Verwendet von:** `IisSettings.razor` – um Shop-Namen im Tray-Icon-Dropdown anzuzeigen

---

### 3.3 `StartIisApp()` *(ohne Parameter)*

```csharp
public void StartIisApp()
```

**Was es tut:** Startet die aktuell in den Einstellungen ausgewählte IIS-Site.

**Ablauf:**
1. Sucht Site anhand `_settingsService.Settings.IisAppName`
2. Wenn Site **nicht gefunden**: Stille Rückkehr (keine Benachrichtigung)
3. Wenn Site **gefunden**: Delegiert an `StartIisApp(site)` → zeigt Benachrichtigung

**Verwendet von:** `Home.razor` Start-Button, `Shop.razor` Start-Button

---

### 3.4 `StartIisApp(Site site, bool showNotification = true)` *(Kernmethode)*

```csharp
public void StartIisApp(Site site, bool showNotification = true)
```

**Was es tut:** Startet eine konkrete IIS-Site.

**Ablauf:**
1. `site.Start()` aufrufen *(Microsoft.Web.Administration)*
2. Bei Erfolg und `showNotification = true`:
   - Grüne Toast-Meldung: `"{site.Name} erfolgreich gestartet"`
3. Bei Exception:
   - Rote Toast-Meldung: `"Fehler beim Starten von {site.Name}: {ex.Message}"`

**Parameter:**

| Parameter | Beschreibung |
|---|---|
| `site` | Die zu startende IIS-Site |
| `showNotification` | `true` = Toast anzeigen; `false` = stille Operation (z.B. während Restart) |

**Verwendet von:**
- `Home.razor` (Start-Button, direkte Methodenreferenz)
- `Sites.razor` (Start-Button pro Site: `IisService.StartIisApp(context)`)
- `IisService.RestartIisApp()` intern (mit `showNotification = false`)
- `IisService.StartTrayIconSite()` intern

---

### 3.5 `StopCurrentIisApp()`

```csharp
public void StopCurrentIisApp()
```

**Was es tut:** Stoppt die aktuell in den Einstellungen ausgewählte IIS-Site.

**Ablauf:**
1. Sucht Site anhand `_settingsService.Settings.IisAppName`
2. Wenn Site **nicht gefunden**: Stille Rückkehr
3. Wenn Site **gefunden**: Delegiert an `StopIisApp(site)`

**Verwendet von:** `Home.razor` Stop-Button, `Shop.razor` Stop-Button

---

### 3.6 `StopIisApp(Site site, bool showNotification = true)` *(Kernmethode)*

```csharp
public void StopIisApp(Site site, bool showNotification = true)
```

**Was es tut:** Stoppt eine konkrete IIS-Site.

**Ablauf:**
1. `site.Stop()` aufrufen *(Microsoft.Web.Administration)*
2. Bei Erfolg und `showNotification = true`:
   - Grüne Toast-Meldung: `"{site.Name} erfolgreich gestoppt"`
3. Bei Exception:
   - Rote Toast-Meldung: `"Fehler beim Stoppen von {site.Name}: {ex.Message}"`

**Verwendet von:**
- `Home.razor` Stop-Button
- `Sites.razor` (Stop-Button pro Site)
- `IisService.RestartIisApp()` intern (mit `showNotification = false`)
- `IisService.StopTrayIconSite()` intern

---

### 3.7 `RestartIisApp(Site? site = null)` *(komplexeste Methode)*

```csharp
public async Task RestartIisApp(Site? site = null)
```

**Was es tut:** Führt einen vollständigen Neustart einer IIS-Site durch – inkl. optionaler Shop-spezifischer Bereinigungen, App-Pool-Recycle und Zeitstempel-Speicherung.

**Vollständiger Ablauf:**

**Schritt 1 – Site auflösen**
```csharp
var selectedSite = site ?? _serverManager.Sites.FirstOrDefault(
    x => x.Name == _settingsService.Settings.IisAppName);

if (selectedSite == null)
{
    _notificationService.ShowError("Seite konnte nicht gefunden werden");
    return;
}
```

**Schritt 2 – Site stoppen**
```csharp
StopIisApp(selectedSite, showNotification: false);
// Kein Toast – stille Operation im Restart-Ablauf
```

**Schritt 3 – Shop-Bereinigung (optional)**
```csharp
// Bundler löschen?
if (_settingsService.Settings.DeleteBundlerOnShopRestart && selectedSite.IsShop())
    DeleteShopBundler();

// Assets löschen?
if (_settingsService.Settings.DeleteAssetsOnShopRestart && selectedSite.IsShop())
    selectedSite.DeleteAssetFolder();
```

**Schritt 4 – App Pool recyceln**
```csharp
RecycleAppPool(selectedSite);
```

**Schritt 5 – Verzögerung**
```csharp
await Task.Delay(_settingsService.Settings.RestartDelaySeconds * 1000);
// Standard: 3 Sekunden (konfigurierbar)
```

**Schritt 6 – Site starten**
```csharp
StartIisApp(selectedSite, showNotification: false);
// Kein Toast – stille Operation
```

**Schritt 7 – Neustart-Zeit speichern**
```csharp
await _appinfoService.SetLastRestartTimeAsync(DateTime.Now);
// Schreibt IisRestartTime in SQLite-Datenbank
```

**Rückgabe:** `Task` (async wegen `Task.Delay` und `SetLastRestartTimeAsync`)

**Mindestdauer:** ~3 Sekunden + IIS-Overhead (Stop + Start)

**Verwendet von:**
- `Home.razor` → `RestartIisAppAndRefresh()` (ohne Parameter)
- `Sites.razor` → direkt mit Site-Objekt
- `Shop.razor` → ohne Parameter
- `IisService.RestartTrayIconSite()` → mit Tray-Icon-Site

---

### 3.8 `RecycleAppPool(Site site, bool showNotification = true)`

```csharp
public void RecycleAppPool(Site site, bool showNotification = true)
```

**Was es tut:** Recycelt (oder startet) den Application Pool der angegebenen Site.

**Ablauf:**
1. Neuen `ServerManager` erstellen (separate Instanz)
2. Root-Application ermitteln: `site.Applications["/"]`
3. App-Pool-Name auslesen: `rootApp?.ApplicationPoolName`
4. **Validierung:**
   - App-Pool-Name leer → Fehler: `"Kein Application Pool für Site '{site.Name}' gefunden."`
   - Pool nicht gefunden → Fehler: `"AppPool '{appPoolName}' nicht gefunden."`
5. **Recycle-Logik:**
   - Pool-State == `ObjectState.Stopped` → `pool.Start()`
   - Andernfalls → `pool.Recycle()`
6. Bei Erfolg und `showNotification = true`:
   - Toast: `"AppPool '{appPoolName}' recycelt."`

> **Warum ein neuer `ServerManager`?** Der App-Pool muss über eine frische Verbindung gesteuert werden, um sicherzustellen, dass der aktuelle Zustand korrekt gelesen wird.

**Aufgerufen von:** `RestartIisApp()` (immer, als Teil des Restart-Ablaufs)

---

### 3.9 `DeleteShopBundler()`

```csharp
public void DeleteShopBundler()
```

**Was es tut:** Löscht das temporäre Bundler-Verzeichnis des Shopsystems.

**Pfad:** `C:\Windows\Temp\shopsystem\bundler`

**Ablauf:**
1. Prüfen ob `_bundlerPath` existiert
2. **Existiert:** `Directory.Delete(_bundlerPath, true)` (rekursiv)
   → Toast: `"Shopsystem-Bundler wurde gelöscht"`
3. **Existiert nicht:** Toast: `"Bundler konnte nicht gefunden werden"`

**Aufgerufen von:** `RestartIisApp()` wenn `DeleteBundlerOnShopRestart = true` und Site ist ein Shop

---

### 3.10 `StartTrayIconSite()`

```csharp
public void StartTrayIconSite()
```

**Was es tut:** Startet die im Tray-Icon konfigurierte Site.

**Ablauf:**
1. `_settingsService.Settings.GetTrayIconSite()` aufrufen
2. Site `null` → Fehler: `"Keine Seite gefunden"`
3. Site gefunden → `StartIisApp(site, showNotification: false)`

**Verwendet von:** `Toolbox.TrayIcon`-Projekt (Taskleiste)

---

### 3.11 `StopTrayIconSite()`

```csharp
public void StopTrayIconSite()
```

Wie `StartTrayIconSite()`, ruft aber `StopIisApp(site, showNotification: false)` auf.

---

### 3.12 `RestartTrayIconSite()`

```csharp
public async Task RestartTrayIconSite()
```

Wie `StartTrayIconSite()`, ruft aber `await RestartIisApp(site)` auf.

---

## 4. Extension Methods für Site

**Datei:** `Toolbox.Data/Models/Extensions/SiteExtensions.cs`

Ergänzende Methoden für `Microsoft.Web.Administration.Site`-Objekte.

---

### 4.1 `IsShop(this Site site)`

```csharp
public static bool IsShop(this Site site)
```

**Was es tut:** Prüft, ob eine IIS-Site ein Shopsystem-Verzeichnis enthält.

**Logik:**
```csharp
var path = site.GetSitePath();

return path.EndsWith(@"\Shop")          // Pfad endet auf "\Shop"
    && File.Exists(path + @"\shop.yaml") // Datei shop.yaml vorhanden
    && Directory.Exists(path + @"\Themes"); // Verzeichnis Themes vorhanden
```

**Verwendet von:** `RestartIisApp()` – entscheidet, ob Bundler/Assets gelöscht werden sollen.

---

### 4.2 `GetSitePath(this Site site)`

```csharp
public static string GetSitePath(this Site site)
```

**Was es tut:** Gibt den physischen Dateisystempfad der Site zurück.

**Ablauf:**
```csharp
var rootApp = site.Applications["/"];
var vdir = rootApp.VirtualDirectories["/"];
return vdir.PhysicalPath;
```

**Beispielrückgabe:** `C:\inetpub\wwwroot\MyShop\Shop`

---

### 4.3 `DeleteAssetFolder(this Site site)`

```csharp
public static void DeleteAssetFolder(this Site site)
```

**Was es tut:** Löscht das `wwwroot/assets`-Verzeichnis der Site.

**Ablauf:**
```csharp
var shopPath = site.GetSitePath();
var assetsPath = Path.Combine(shopPath, "wwwroot", "assets");

if (Directory.Exists(assetsPath))
    Directory.Delete(assetsPath, true);
```

**Verwendet von:** `RestartIisApp()` wenn `DeleteAssetsOnShopRestart = true` und `site.IsShop()`

---

## 5. Extension Methods für Binding

**Datei:** `Toolbox.Data/Models/Extensions/BindingExtensions.cs`

Ergänzende Methoden für `Microsoft.Web.Administration.Binding`-Objekte.

---

### 5.1 `GetSiteUrl(this Binding binding)`

```csharp
public static string? GetSiteUrl(this Binding binding)
```

**Was es tut:** Wandelt eine IIS-Binding-Information in eine klickbare URL um.

**Eingabe:** IIS `BindingInformation` im Format `"IP:Port:Hostname"`, z.B. `"127.0.0.1:80:example.com"`

**Logik:**
- Extrahiert Protokoll, Port und Hostname
- Baut URL: `"http://example.com"` oder `"https://example.com:8443"`
- Standardports (80 für http, 443 für https) werden nicht angehängt

**Verwendet von:** `Home.razor` – "Im Browser öffnen"-Menü

---

### 5.2 `OpenInBrowser(this Binding binding)`

```csharp
public static void OpenInBrowser(this Binding binding)
```

**Was es tut:** Öffnet die URL des Bindings im Standard-Browser.

**Ablauf:**
```csharp
var url = binding.GetSiteUrl();
Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
```

**Verwendet von:** `Home.razor` – Menüeinträge im "Im Browser öffnen"-Dropdown

---

## 6. Einstellungen & Konfiguration

**Datei:** `Toolbox.Data/Models/ToolboxSettings.cs`

### IIS-relevante Einstellungsfelder

| Feld | Typ | Standard | Beschreibung |
|---|---|---|---|
| `IisAppName` | `string?` | `null` | Name der aktuell ausgewählten IIS-Site |
| `RestartDelaySeconds` | `int` | `3` | Wartezeit zwischen Stop und Start (Sekunden) |
| `DeleteBundlerOnShopRestart` | `bool` | `false` | Bundler-Verzeichnis beim Neustart löschen |
| `DeleteAssetsOnShopRestart` | `bool` | `false` | `wwwroot/assets` beim Neustart löschen |
| `TrayIconIisSite` | `long` | `long.MinValue` | Site-ID für die Tray-Icon-Steuerung |

### `GetTrayIconSite()` *(Hilfsmethode in ToolboxSettings)*

```csharp
public Site? GetTrayIconSite()
{
    var manager = new ServerManager();

    // Zuerst nach gespeicherter ID suchen
    var byId = manager.Sites.FirstOrDefault(x => x.Id == TrayIconIisSite);

    // Fallback: Suche nach IisAppName
    return byId ?? manager.Sites.FirstOrDefault(x => x.Name == IisAppName);
}
```

Stellt sicher, dass immer eine sinnvolle Site für die Tray-Icon-Steuerung gefunden wird – auch wenn keine explizite ID konfiguriert ist.

---

## 7. AppInfo – Neustart-Zeitverfolgung

**Datei:** `Toolbox.Data/Common/AppInfo.cs`

```csharp
public class AppInfo
{
    public const int SingletonId = 1;
    public int Id { get; set; }
    public DateTime StartTime { get; set; }       // App-Startzeit
    public DateTime IisRestartTime { get; set; }  // ← Letzter IIS-Neustart
    // ... weitere Felder
}
```

### `AppInfoService.SetLastRestartTimeAsync()`

```csharp
public async Task SetLastRestartTimeAsync(
    DateTime restartTime,
    CancellationToken cancellationToken = default)
```

**Ablauf:**
1. Aktuellen `AppInfo`-Datensatz aus SQLite laden
2. `appInfo.IisRestartTime = restartTime` setzen
3. `appInfo.UpdatedAt = DateTime.UtcNow` setzen
4. Speichern

**Aufgerufen von:** `RestartIisApp()` – immer am Ende eines erfolgreichen Neustarts

---

### `AppInfoService.GetAsync()`

```csharp
public async Task<AppInfo> GetAsync(CancellationToken cancellationToken = default)
```

**Verwendet von:** `Home.razor` – liest `IisRestartTime` für die Log-Zeitgrenze beim Start der Anwendung

---

## 8. Home.razor – UI-Integration

**Datei:** `Toolbox/Components/Pages/Home.razor`

### 8.1 IIS-Steuerungsbuttons (Zeilen ~57–61)

```html
<!-- Start -->
<MudButton Color="Color.Success"
           StartIcon="@Icons.Material.Filled.Start"
           Variant="Variant.Filled"
           OnClick="@IisService.StartIisApp">
    Start
</MudButton>

<!-- Neustart -->
<MudButton Color="Color.Warning"
           StartIcon="@Icons.Material.Filled.RestartAlt"
           Variant="Variant.Filled"
           OnClick="RestartIisAppAndRefresh">
    Neustart
</MudButton>

<!-- Stop -->
<MudButton Color="Color.Error"
           StartIcon="@Icons.Material.Filled.Stop"
           Variant="Variant.Filled"
           OnClick="@IisService.StopCurrentIisApp">
    Stop
</MudButton>
```

> **Hinweis:** Start und Stop verwenden direkte Methodenreferenzen. Neustart ruft eine lokale Wrapper-Methode auf.

---

### 8.2 `RestartIisAppAndRefresh()` *(Zeilen ~507–512)*

```csharp
private async Task RestartIisAppAndRefresh()
{
    await IisService.RestartIisApp();            // 1. IIS neu starten
    await RefreshRestartTimeIfChangedAsync(      // 2. Neue Restart-Zeit aus DB laden
        forceReload: true);
    await LoadLogs();                            // 3. Logs neu laden
}
```

Diese Methode verbindet den IIS-Neustart mit dem Log-System: Nach dem Neustart werden die Logs automatisch neu geladen und die Zeitgrenze aktualisiert.

---

### 8.3 Site-Auswahlmenü (Zeilen ~27–41)

```html
<MudSelect Value="Settings.IisAppName"
           ValueChanged="@((string? s) => SettingsService.ChangeSelectedIisApp(s))"
           Label="IIS App"
           Variant="Variant.Outlined">
    @foreach (var site in manager.Sites)
    {
        <MudSelectItem Value="@site.Name">@site.Name</MudSelectItem>
    }
</MudSelect>
```

- Farbindikator: Grün = gestartet, Rot = gestoppt, Gelb = wird gestartet/gestoppt
- Wechsel der Auswahl → `SettingsService.ChangeSelectedIisApp()` speichert die Wahl

---

### 8.4 "Im Browser öffnen"-Menü (Zeilen ~44–55)

```html
<MudMenu Icon="@Icons.Material.Filled.OpenInNew">
    @foreach (var binding in manager.Sites[Settings.IisAppName]
              ?.Bindings ?? Enumerable.Empty<Binding>())
    {
        @if (binding.GetSiteUrl() != null)
        {
            <MudMenuItem OnClick="@(() => binding.OpenInBrowser())"
                         Label="@binding.GetSiteUrl()"/>
        }
    }
</MudMenu>
```

Zeigt alle konfigurierten Bindings der ausgewählten Site als klickbare URLs an.

---

### 8.5 Initialisierung (Zeilen ~393–411)

```csharp
protected override async Task OnInitializedAsync()
{
    var appInfo = await AppInfoService.GetAsync();
    _appStartTime = appInfo.StartTime;
    _iisRestartTime = appInfo.IisRestartTime;

    // Wenn letzter Neustart vor mehr als 1 Stunde: alle Logs laden
    if ((DateTime.Now - appInfo.IisRestartTime).Hours >= 1)
    {
        var tmp = reload;
        reload = ReloadOption.AlleLogs;
        await LoadLogs();
        reload = tmp;
    }
}
```

---

## 9. Sites.razor – UI-Integration

**Datei:** `Toolbox/Components/Pages/Sites.razor`

Zeigt alle IIS-Sites in einer Tabelle – jede Zeile hat eigene Steuerungsbuttons.

```html
<!-- Start-Button pro Site -->
<MudButton Size="Size.Small"
           OnClick="@(() => IisService.StartIisApp(context))"
           Color="Color.Success" Variant="Variant.Filled">
    Start
</MudButton>

<!-- Stop-Button pro Site -->
<MudButton Size="Size.Small"
           OnClick="@(() => IisService.StopIisApp(context))"
           Color="Color.Error" Variant="Variant.Filled" Class="ms-1">
    Stop
</MudButton>

<!-- Neustart-Button pro Site -->
<MudButton Size="Size.Small"
           OnClick="@(() => IisService.RestartIisApp(context))"
           Color="Color.Warning" Variant="Variant.Filled" Class="ms-1">
    Neustarten
</MudButton>
```

Das `context`-Objekt ist das konkrete `Site`-Objekt aus der MudBlazor-Tabellenzeile – es wird direkt an den Service übergeben.

---

## 10. Shop.razor – UI-Integration

**Datei:** `Toolbox/Components/Pages/Shop.razor`

Steuert die aktuell ausgewählte Shop-Site über Icon-Buttons.

```html
<!-- Start -->
<MudIconButton Color="Color.Success"
               Icon="@Icons.Material.Filled.Start"
               OnClick="IisService.StartIisApp"/>

<!-- Neustart -->
<MudIconButton Color="Color.Warning"
               Icon="@Icons.Material.Filled.RestartAlt"
               OnClick="@(() => IisService.RestartIisApp())"/>

<!-- Stop -->
<MudIconButton Color="Color.Error"
               Icon="@Icons.Material.Filled.Stop"
               OnClick="IisService.StopCurrentIisApp"/>
```

Verwendet die parameterlosen Varianten (greifen intern auf `IisAppName` aus den Einstellungen zurück).

---

## 11. IisSettings.razor – Konfigurationsseite

**Datei:** `Toolbox/Components/Settings/IisSettings.razor`

### Neustart-Verzögerung (Zeilen ~17–20)

```html
<MudNumericField T="int"
    ValueChanged="(int i) => SettingsService.SaveSettingChanges(
        x => x.RestartDelaySeconds = i, NotificationService)"
    Value="Setting.RestartDelaySeconds"
    Label="Zeit zwischen Start und Stop von IIS-Seiten (Sekunden)"
    Min="1"/>
```

### Bundler-Option (Zeilen ~22–27)

```html
<MudCheckBox ValueChanged="(bool i) => SettingsService.SaveSettingChanges(
                 x => x.DeleteBundlerOnShopRestart = i, NotificationService)"
             Value="Setting.DeleteBundlerOnShopRestart"
             T="bool" Color="Color.Primary">
    Shop-Bundler beim Neustart löschen
</MudCheckBox>
```

Löscht `C:\Windows\Temp\shopsystem\bundler` beim Neustart, wenn die Site als Shop erkannt wird.

### Assets-Option (Zeilen ~28–33)

```html
<MudCheckBox ValueChanged="(bool i) => SettingsService.SaveSettingChanges(
                 x => x.DeleteAssetsOnShopRestart = i, NotificationService)"
             Value="Setting.DeleteAssetsOnShopRestart"
             T="bool" Color="Color.Primary">
    Shop-Assets(wwwroot) beim Neustart löschen
</MudCheckBox>
```

Löscht `{shopPath}/wwwroot/assets` beim Neustart.

### Tray-Icon-Site (Zeilen ~35–44)

```html
<MudSelect Placeholder="Shop zum Starten/Stoppen/Neustarten über die Taskbar"
           Label="Shop zum Starten/Stoppen/Neustarten über die Taskbar"
           Value="Setting.GetTrayIconSite().Id"
           ValueChanged="(long i) => SettingsService.SaveSettingChanges(
               x => x.TrayIconIisSite = i, NotificationService)"
           T="long">
    @foreach (var shopPaths in Setting.ShopSettingsList)
    {
        <MudSelectItem Value="shopPaths.SiteId">
            @GetShopDisplayName(shopPaths)
        </MudSelectItem>
    }
</MudSelect>
```

---

## 12. Fehlerbehandlung & Benachrichtigungen

Alle Benachrichtigungen laufen über `INotificationService` als Toast-Meldungen.

| Situation | Meldung | Typ |
|---|---|---|
| Site erfolgreich gestartet | `"{site.Name} erfolgreich gestartet"` | Erfolg (grün) |
| Site erfolgreich gestoppt | `"{site.Name} erfolgreich gestoppt"` | Erfolg (grün) |
| Fehler beim Starten | `"Fehler beim Starten von {name}: {ex.Message}"` | Fehler (rot) |
| Fehler beim Stoppen | `"Fehler beim Stoppen von {name}: {ex.Message}"` | Fehler (rot) |
| Restart – Site nicht gefunden | `"Seite konnte nicht gefunden werden"` | Fehler (rot) |
| App Pool recycelt | `"AppPool '{name}' recycelt."` | Erfolg (grün) |
| App Pool nicht gefunden | `"AppPool '{name}' nicht gefunden."` | Fehler (rot) |
| Kein App Pool für Site | `"Kein Application Pool für Site '{name}' gefunden."` | Fehler (rot) |
| Bundler gelöscht | `"Shopsystem-Bundler wurde gelöscht"` | Erfolg (grün) |
| Bundler nicht gefunden | `"Bundler konnte nicht gefunden werden"` | Fehler (rot) |
| Tray – keine Site | `"Keine Seite gefunden"` | Fehler (rot) |

> **Designentscheidung:** Während eines Restart-Vorgangs (`RestartIisApp`) werden Stop und Start **ohne Benachrichtigung** ausgeführt (`showNotification: false`), damit der Benutzer nicht mit zwei separaten Meldungen konfrontiert wird.

---

## 13. Dependency Injection

**Datei:** `Toolbox/Program.cs`

```csharp
// Zeile 86
builder.Services.AddScoped<IisService>();

// Abhängigkeiten von IisService
builder.Services.AddScoped<AppInfoService>();
builder.Services.AddScoped<IAppInfoService, AppInfoService>();  // Line 84-85
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>(); // Line 70-71
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>(); // Line 43-44
```

**Scope-Implikation:** Alle Dienste sind `Scoped` → eine Instanz pro Blazor-Komponente. Der `ServerManager` in `IisService` wird bei jeder Komponenteninstanz neu erstellt.

---

## 14. Vollständiges Ablaufdiagramm: Neustart

### Klick auf "Neustart" in Home.razor

```
Benutzer klickt "Neustart"-Button
        │
        ▼
RestartIisAppAndRefresh()   [Home.razor]
        │
        ▼
IisService.RestartIisApp()  [kein Parameter → nutzt IisAppName]
        │
        ├─ Site suchen: Settings.IisAppName
        │        │
        │    Nicht gefunden?
        │        │
        │        ▼
        │   Toast: "Seite konnte nicht gefunden werden"
        │   return
        │
        │    Gefunden? → weiter ↓
        │
        ▼
StopIisApp(site, showNotification: false)
  └─> site.Stop()  [Microsoft.Web.Administration]
        │
        ▼
site.IsShop()?
  ├─ Ja + DeleteBundlerOnShopRestart = true?
  │       │
  │       ▼
  │  DeleteShopBundler()
  │  └─> Directory.Delete("C:\Windows\Temp\shopsystem\bundler", true)
  │
  ├─ Ja + DeleteAssetsOnShopRestart = true?
  │       │
  │       ▼
  │  site.DeleteAssetFolder()
  │  └─> Directory.Delete("{sitePath}/wwwroot/assets", true)
  │
  └─ Nein → überspringen
        │
        ▼
RecycleAppPool(site)
  └─> Neuer ServerManager
  └─> Pool.State == Stopped? → pool.Start()
                               sonst → pool.Recycle()
  └─> Toast: "AppPool '{name}' recycelt."
        │
        ▼
Task.Delay(RestartDelaySeconds * 1000)
[Standard: 3 Sekunden]
        │
        ▼
StartIisApp(site, showNotification: false)
  └─> site.Start()  [Microsoft.Web.Administration]
        │
        ▼
AppInfoService.SetLastRestartTimeAsync(DateTime.Now)
  └─> AppInfo.IisRestartTime = DateTime.Now → SQLite speichern
        │
        ▼  [zurück in RestartIisAppAndRefresh]
        │
        ▼
RefreshRestartTimeIfChangedAsync(forceReload: true)
  └─> Neue IisRestartTime aus DB lesen
  └─> _logSinceTime aktualisieren
  └─> Log-Cursor zurücksetzen
        │
        ▼
LoadLogs()
  └─> Neue Logs aus Windows Event Log laden
  └─> UI aktualisieren (Tabelle, Erweiterungen)
```

---

### Tray-Icon-Neustart (aus Taskleiste)

```
Benutzer klickt "Neustart" im Tray-Icon-Kontextmenü
        │
        ▼
IisService.RestartTrayIconSite()
        │
        ▼
Settings.GetTrayIconSite()
  ├─ Nach ID suchen: Settings.TrayIconIisSite
  └─ Fallback: Nach Name suchen: Settings.IisAppName
        │
    Nicht gefunden?
        │
        ▼
   Toast: "Keine Seite gefunden"
   return
        │
    Gefunden?
        │
        ▼
IisService.RestartIisApp(site)
  └─ (gleicher Ablauf wie oben, mit konkretem Site-Objekt)
```

---

### Stop und Start (einfache Operationen)

```
Klick auf "Start"                   Klick auf "Stop"
      │                                   │
      ▼                                   ▼
IisService.StartIisApp()          IisService.StopCurrentIisApp()
      │                                   │
Settings.IisAppName               Settings.IisAppName
  → Site suchen                     → Site suchen
      │                                   │
  Nicht gefunden → return           Nicht gefunden → return
      │                                   │
      ▼                                   ▼
StartIisApp(site, true)           StopIisApp(site, true)
  └─> site.Start()                  └─> site.Stop()
  └─> Toast: "...gestartet"         └─> Toast: "...gestoppt"
```

---

## 15. Referenz: Alle Konfigurationswerte

| Einstellung | Feld in ToolboxSettings | Standard | Beschreibung |
|---|---|---|---|
| Ausgewählte Site | `IisAppName` | `null` | Name der aktiven IIS-Site für Hauptfenster |
| Neustart-Verzögerung | `RestartDelaySeconds` | `3` | Sekunden zwischen Stop und Start |
| Bundler löschen | `DeleteBundlerOnShopRestart` | `false` | `C:\Windows\Temp\shopsystem\bundler` löschen |
| Assets löschen | `DeleteAssetsOnShopRestart` | `false` | `wwwroot/assets` löschen |
| Tray-Icon-Site | `TrayIconIisSite` | `long.MinValue` | Site-ID für Taskleiste (Fallback auf IisAppName) |

---

*Ende des Manuskripts*
