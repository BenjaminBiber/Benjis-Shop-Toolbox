# Settings-Service – Technisches Manuskript

> **Projekt:** TeamShop.ShopToolbox
> **Erstellt:** 2026-03-31
> **Autor:** Benjamin Biber

---

## Inhaltsverzeichnis

1. [Übersicht & Architektur](#1-übersicht--architektur)
2. [Interface & Klasse](#2-interface--klasse)
3. [Alle Methoden im Detail](#3-alle-methoden-im-detail)
4. [ToolboxSettings – Das Settings-Modell](#4-toolboxsettings--das-settings-modell)
5. [ShopSetting – Shop-Konfiguration](#5-shopsetting--shop-konfiguration)
6. [DatabaseConnection – Datenbankverbindung](#6-databaseconnection--datenbankverbindung)
7. [Datenbankschema & EF Core](#7-datenbankschema--ef-core)
8. [Initialisierung beim App-Start](#8-initialisierung-beim-app-start)
9. [UI-Komponenten (Settings-Seiten)](#9-ui-komponenten-settings-seiten)
10. [NotificationService](#10-notificationservice)
11. [Dependency Injection](#11-dependency-injection)
12. [Verwendungsmuster im gesamten Code](#12-verwendungsmuster-im-gesamten-code)
13. [Vollständiges Ablaufdiagramm](#13-vollständiges-ablaufdiagramm)

---

## 1. Übersicht & Architektur

Der `SettingsService` ist der zentrale Dienst für das Laden, Speichern und Verwalten aller Anwendungseinstellungen in der ShopToolbox. Er kapselt den Zugriff auf die SQLite-Datenbank und stellt allen Komponenten ein einziges gecachtes `ToolboxSettings`-Objekt zur Verfügung.

### Kernaufgaben

| Aufgabe | Beschreibung |
|---|---|
| Einstellungen laden | Beim Start aus SQLite lesen und cachen |
| Einstellungen speichern | Änderungen persistieren (EF Core) |
| Shop-Pfade erkennen | Filesystem + IIS scannen für Shop-Konfigurationen |
| IIS-App wechseln | Aktuell ausgewählte Site speichern |
| Import / Export | Einstellungen als JSON-Datei sichern und wiederherstellen |
| Konfigurationsstatus | `IsConfigured`-Flag für das Haupt-UI |

### Abhängigkeiten

```
SettingsService
  │
  ├── InternalAppDbContext     ← EF Core, SQLite
  └── (INotificationService)  ← wird von Callers übergeben, nicht injiziert
```

---

## 2. Interface & Klasse

### `ISettingsService`
**Datei:** `Toolbox.Data/Models/Interfaces/ISettingsService.cs`

```csharp
public interface ISettingsService
{
    ToolboxSettings Settings { get; }
}
```

Das Interface ist bewusst minimal gehalten – es stellt nur den lesenden Zugriff auf `Settings` bereit. Alle schreibenden Methoden (wie `SaveSettingChanges`, `ChangeSelectedIisApp`) sind direkt auf der Klasse vorhanden und werden über die konkrete Instanz aufgerufen.

---

### `SettingsService`
**Datei:** `Toolbox.Data/Services/SettingsService.cs`
**Namespace:** `Toolbox.Data.Services`
**Implementiert:** `ISettingsService`

#### Felder & Properties

```csharp
private readonly InternalAppDbContext _db;

public ToolboxSettings Settings { get; private set; }
// Das gecachte Settings-Objekt – einmalig geladen, dann im RAM gehalten

public bool IsConfigured { get; private set; }
// true = Einstellungen in DB vorhanden UND IisAppName gesetzt
// false = Keine DB-Einträge oder Fehler beim Laden
```

#### Konstruktor

```csharp
public SettingsService(InternalAppDbContext db)
{
    _db = db;
    Load();   // ← Beim ersten Erstellen sofort aus DB laden
}
```

---

## 3. Alle Methoden im Detail

### 3.1 `Load()` *(privat)*

**Zeilen ~25–53**

```csharp
private void Load()
```

**Was es tut:** Initialisiert `Settings` und `IsConfigured` aus der SQLite-Datenbank.

**Ablauf:**

1. **Datenbank sicherstellen:** `_db.Database.EnsureCreated()`
2. **Settings-Datensatz lesen:**
   ```csharp
   var settings = _db.Settings
       .Include(x => x.ShopSettingsList)
       .FirstOrDefault();
   ```
3. **Settings gefunden:**
   - Wenn `settings.IisAppName == null`:
     - Versuche, ersten laufenden IIS-Site-Namen zu ermitteln
     - Schreibe ihn als Standardwert
   - `Settings = settings`
   - `IsConfigured = true`
4. **Keine Settings / Exception:**
   - `IsConfigured = false`

> **Wichtig:** `Load()` wird nur im Konstruktor aufgerufen. Einstellungsänderungen zur Laufzeit werden direkt am gecachten `Settings`-Objekt vorgenommen und dann per EF Core gespeichert – kein erneutes Laden nötig, da EF Core das Objekt trackt.

---

### 3.2 `SaveSettings()`

**Zeilen ~68–86**

```csharp
public bool SaveSettings()
```

**Was es tut:** Persistiert das aktuelle `Settings`-Objekt in SQLite.

**Ablauf:**
1. Prüfen ob `_db != null`
2. `_db.Database.EnsureCreated()`
3. `_db.Settings.Update(Settings)` – EF Core markiert das Objekt als geändert
4. `_db.SaveChanges()` – Schreibt in SQLite
5. Gibt `true` bei Erfolg zurück

**Fehlerbehandlung:** Alle Exceptions werden still gefangen → gibt `false` zurück

**Aufgerufen von:** Fast allen anderen Methoden als finaler Schritt.

---

### 3.3 `SaveSettingChanges()` *(Hauptmethode für UI)*

**Zeilen ~88–99**

```csharp
public void SaveSettingChanges(
    Action<ToolboxSettings> change,
    INotificationService notificationService)
```

**Was es tut:** Führt eine Änderungs-Aktion auf dem Settings-Objekt aus und speichert danach.

**Ablauf:**
1. `change == null`? → Fehler-Toast: `"Keine Änderungen zum Speichern"` → return
2. `change(Settings)` ausführen *(die Lambda-Action modifiziert das Settings-Objekt)*
3. Erfolgs-Toast: `"Einstellungen erfolgreich gespeichert"`
4. `SaveSettings()` aufrufen

**Verwendungsmuster in der UI:**
```csharp
// Beispiel aus IisSettings.razor
SettingsService.SaveSettingChanges(
    x => x.RestartDelaySeconds = newValue,
    NotificationService
);
```

Der erste Parameter ist immer ein Lambda der Form `x => x.Property = value`. Das erlaubt eine kompakte, einzeilige Speicherung beliebiger Einstellungen ohne Code-Duplikation.

---

### 3.4 `ChangeSelectedIisApp()`

**Zeilen ~312–330**

```csharp
public bool ChangeSelectedIisApp(string iis)
```

**Was es tut:** Wechselt die aktuell ausgewählte IIS-Site und speichert sofort.

**Ablauf:**
1. `_db == null`? → return `false`
2. `_db.Database.EnsureCreated()`
3. `Settings.IisAppName = iis`
4. `_db.SaveChanges()` *(direkt, nicht über `SaveSettings()`)*
5. Gibt `true` bei Erfolg zurück

> **Warum direktes `SaveChanges()`?** Diese Methode ist auf schnelle, häufige Aufrufe ausgelegt (z.B. Dropdown-Wechsel im UI). Sie vermeidet den Overhead von `SaveSettings()` und zeigt auch keinen Erfolgs-Toast.

**Aufgerufen von:** `Home.razor` – ValueChanged-Handler des Site-Auswahlmenüs

---

### 3.5 `FillShopPathSettings()` *(komplexeste Methode)*

**Zeilen ~101–236**

```csharp
public void FillShopPathSettings(SiteCollection siteCollection)
```

**Was es tut:** Scannt Filesystem und IIS nach Shop-Verzeichnissen und füllt `ShopSettingsList`.

**Ablauf in drei Phasen:**

**Phase 1 – IIS-Site-Map aufbauen (Zeilen ~103–121)**
```
Für jede IIS-Site in siteCollection:
  ├─ Site-Pfad ermitteln: site.Applications["/"].VirtualDirectories["/"].PhysicalPath
  ├─ Suche nach Shop.yaml oder shop.yaml (case-insensitive)
  └─ Falls gefunden: NormalizePath(yamlPath) → SiteId in Dictionary speichern
```
Ergebnis: `Dictionary<string (YAML-Pfad), long (Site-ID)>`

**Phase 2 – Filesystem-Scan (Zeilen ~123–194)**
```
Root-Pfad: Settings.GeneralFolderPath
  │
  ▼
EnumerateShopFolders(root)
  │  Breadth-First-Traversal, ignoriert: .git, node_modules, bin, obj, etc.
  │  Ein Ordner gilt als Shop, wenn:
  │    → Shop.yaml / shop.yaml vorhanden
  │    → Themes/ Unterordner vorhanden
  │
  ▼ Für jeden gefundenen Shop-Ordner (parallel, ConcurrentDictionary):
  ├─ ShopCandidate erstellen (YAML-Pfad, Theme-Pfad, SiteId)
  ├─ Bereits in ShopSettingsList vorhanden (selber YAML-Pfad)?
  │    Ja  → Vorhandenen ShopSetting aktualisieren
  │    Nein → Neuen ShopSetting hinzufügen
  └─ SiteId aus Phase-1-Dictionary übernehmen (falls vorhanden)
```

**Phase 3 – Fehlende IIS-Sites ergänzen (Zeilen ~196–234)**
```
Für jede IIS-Site in siteCollection:
  ├─ site.IsShop() prüfen (Shop.yaml + Themes/ vorhanden?)
  ├─ Shop.yaml-Pfad ermitteln
  ├─ Bereits in ShopSettingsList?
  │    Ja  → SiteId aktualisieren
  │    Nein → Neuen ShopSetting erstellen
  └─ Besonderheit: Auch IIS-Sites ohne Match im Filesystem werden aufgenommen
```

**Abschluss:** `SaveSettings()` – persistiert alle Änderungen

**Aufgerufen von:** `Program.cs` beim App-Start (einmalig)

---

### 3.6 `EnumerateShopFolders()` *(privat, statisch)*

**Zeilen ~238–276**

```csharp
private static IEnumerable<string> EnumerateShopFolders(string root)
```

**Was es tut:** Durchsucht ein Verzeichnis rekursiv nach Shop-Ordnern.

**Algorithmus:** Breadth-First mit Queue

```csharp
var queue = new Queue<string>();
queue.Enqueue(root);

while (queue.Count > 0)
{
    var dir = queue.Dequeue();

    // Shop-Erkennung
    if (HasShopYaml(dir) && HasThemesFolder(dir))
        yield return dir;

    // Unterordner einreihen (außer ignorierten)
    foreach (var sub in Directory.EnumerateDirectories(dir))
        if (!IsIgnored(sub))
            queue.Enqueue(sub);
}
```

**Ignorierte Ordner:**
`.git`, `.svn`, `.hg`, `.vs`, `.idea`, `.vscode`, `bin`, `obj`, `packages`, `node_modules`

**Shop-Erkennung:** Ein Ordner ist ein Shop, wenn er enthält:
1. `Shop.yaml` **oder** `shop.yaml` (case-insensitive)
2. `Themes/`-Unterverzeichnis

---

### 3.7 `NormalizePath()` *(privat, statisch)*

**Zeilen ~278–294**

```csharp
private static string NormalizePath(string? path)
```

**Was es tut:** Normalisiert Dateipfade für konsistente Vergleiche.

**Ablauf:**
1. Leer/null → leerer String
2. `Path.GetFullPath(path)` → absolute Pfade, relative auflösen
3. Trailing-Separatoren trimmen
4. Bei Exception: einfaches `Trim()` als Fallback

**Zweck:** Stellt sicher, dass `C:\Dev\Shop\` und `C:\Dev\Shop` als gleich erkannt werden, auch bei gemischten Slash-Varianten.

---

### 3.8 `AreThereAllShopPathSettings()`

**Zeilen ~304–310**

```csharp
public bool AreThereAllShopPathSettings(SiteCollection siteCollection)
```

**Was es tut:** Prüft, ob mindestens eine Shop-Konfiguration vorhanden ist.

**Ablauf:**
1. `Load()` aufrufen (frischer DB-Stand)
2. Gibt `Settings.ShopSettingsList?.Any()` zurück

**Verwendet von:** Startup-Prüfung, ob Initialkonfiguration abgeschlossen ist.

---

### 3.9 `Export()`

**Zeilen ~357–373**

```csharp
public bool Export()
```

**Was es tut:** Exportiert die aktuellen Einstellungen als JSON-Datei in den Downloads-Ordner.

**Ablauf:**
1. Downloads-Pfad ermitteln: `Environment.SpecialFolder.UserProfile + "\Downloads"`
2. Dateiname: `Toolbox-Settings-{DateTime.Now:yyyy-MM-dd}.json`
3. Einstellungen mit `System.Text.Json` serialisieren (indentiert)
4. In Datei schreiben

**Fehlerbehandlung:** Exception → gibt `false` zurück

---

### 3.10 `Import()`

**Zeilen ~332–355**

```csharp
public bool Import(string path)
```

**Was es tut:** Lädt Einstellungen aus einer JSON-Exportdatei.

**Ablauf:**
1. Datei existiert? Nein → `false`
2. JSON lesen und zu `ToolboxSettings` deserialisieren
3. `Settings = deserializedSettings`
4. `SaveSettings()` – in DB persistieren

**Fehlerbehandlung:** Exception → gibt `false` zurück

---

### 3.11 `LoadSettingsFromExport()`

**Zeilen ~55–66**

```csharp
public bool LoadSettingsFromExport()
```

**Was es tut:** Lädt automatisch die neueste Export-Datei aus dem Downloads-Ordner.

**Ablauf:**
1. Downloads-Ordner nach `Toolbox-Settings-*.json` durchsuchen
2. Neueste Datei nach Dateinamen-Sortierung auswählen
3. `Import(path)` aufrufen

**Fehlerbehandlung:** Keine passende Datei → gibt `false` zurück

---

### 3.12 `ShopCandidate` *(private innere Klasse)*

**Zeilen ~296–302**

```csharp
private sealed class ShopCandidate
{
    public string ShopYamlPath { get; init; } = string.Empty;
    public string ThemeFolderPath { get; init; } = string.Empty;
    public bool HasSiteId { get; init; }
    public long SiteId { get; init; }
}
```

Temporäres Datenhalter-Objekt, das während `FillShopPathSettings()` für die parallele Verarbeitung verwendet wird, bevor die Daten in `ShopSetting`-Objekte überführt werden.

---

## 4. ToolboxSettings – Das Settings-Modell

**Datei:** `Toolbox.Data/Models/ToolboxSettings.cs`

**Singleton-Pattern:** Immer `Id = 1`. Eine DB-Constraint (`CK_AppSettings_Singleton`) verhindert mehrere Datensätze. Beim ersten Start wird ein Standard-Datensatz eingeseeded.

### Alle Properties

#### Allgemein

| Property | Typ | Standard | Beschreibung |
|---|---|---|---|
| `Id` | `int` | `1` | Primärschlüssel (immer 1) |
| `GeneralFolderPath` | `string` | `"C:\\Dev_Git"` | Basis-Ordner für Shop-Scan |
| `AllowBetaUpdates` | `bool` | `false` | Beta-Versionen beim Update zulassen |
| `PinnedExtensionGroups` | `string?` | `null` | Angepinnte Extension-Gruppen (serialisiert) |
| `PinnedThemeGroups` | `string?` | `null` | Angepinnte Theme-Gruppen (serialisiert) |

#### IIS

| Property | Typ | Standard | Beschreibung |
|---|---|---|---|
| `IisAppName` | `string?` | `null` | Aktuell ausgewählte IIS-Site |
| `RestartDelaySeconds` | `int` | `3` | Wartezeit zwischen Stop und Start (Sekunden) |
| `DeleteBundlerOnShopRestart` | `bool` | `false` | Bundler-Verzeichnis beim Neustart löschen |
| `DeleteAssetsOnShopRestart` | `bool` | `false` | `wwwroot/assets` beim Neustart löschen |
| `TrayIconIisSite` | `long` | `long.MinValue` | Site-ID für Tray-Icon-Steuerung |

#### Logs

| Property | Typ | Standard | Beschreibung |
|---|---|---|---|
| `LogName` | `string?` | `"4SELLERS"` | Semikolon-getrennte Windows Event Log-Namen |
| `AutoRefreshSeconds` | `int` | `60` | Automatisches Neuladen der Logs (0 = deaktiviert) |
| `AutoRefreshEnabled` | `bool` | `false` | Auto-Refresh aktivieren |
| `OnlySinceRestart` | `bool` | `true` | Nur Logs seit letztem IIS-Neustart anzeigen |
| `BundleLogs` | `bool` | `false` | Identische Log-Einträge bündeln |

#### Repositories / Dateipfade

| Property | Typ | Standard | Beschreibung |
|---|---|---|---|
| `ThemeRepositoryPath` | `string` | `"C:\\Dev_Git\\KundenThemes"` | Theme-Repository-Pfad(e) |
| `ExtensionsRepositoryPath` | `string` | `"C:\\Dev_Git\\Extensions"` | Extension-Repository-Pfad(e) |
| `RestartShopOnThemeChange` | `bool` | `true` | Shop nach Theme-Wechsel automatisch neu starten |

#### Azure DevOps / TFS

| Property | Typ | Standard | Beschreibung |
|---|---|---|---|
| `TfsProjectUrls` | `string?` | `""` | Semikolon-getrennte Projekt-URLs |
| `TfsCollectionUrl` | `string?` | `"https://tfs.4sellers.de/tfs/ERP-Kunden/"` | Collection-Basis-URL |
| `TfsApiKey` | `string?` | `""` | Personal Access Token |

#### VMware vCenter

| Property | Typ | Standard | Beschreibung |
|---|---|---|---|
| `VCenterUrl` | `string?` | `"https://staging-vc-1.logic-base.local"` | vCenter-Verbindungs-URL |
| `VCenterUsername` | `string?` | `null` | Benutzername |
| `VCenterPassword` | `string?` | `null` | Passwort |
| `VCenterIgnoreSslErrors` | `bool` | `false` | Selbst-signierte Zertifikate akzeptieren |

#### Navigation

| Property | Typ | Beschreibung |
|---|---|---|
| `ShopSettingsList` | `List<ShopSetting>` | Alle erkannten Shop-Konfigurationen |

---

### Hilfsmethoden in ToolboxSettings

#### Pfad-Listen (Semikolon-getrennt)

```csharp
// Getter – splittet bei ';', '\n', '\r'
public IEnumerable<string> GetExtensionRoots()
public IEnumerable<string> GetThemeRoots()
public IEnumerable<string> GetLogNames()
public IEnumerable<string> GetTfsProjectUrls()

// Shortcut für ersten Eintrag
public string? GetPrimaryExtensionRoot()
public string? GetPrimaryThemeRoot()

// Setter – verbindet mit ';'
public void SetExtensionRoots(IEnumerable<string> paths)
public void SetThemeRoots(IEnumerable<string> paths)
public void SetLogNames(IEnumerable<string> names)
public void SetTfsProjectUrls(IEnumerable<string> urls)

// Interner Helfer
private static IEnumerable<string> SplitPaths(string? raw)
public static string JoinPaths(IEnumerable<string> paths)
```

**Beispiel:**
```
LogName = "4SELLERS;Application"
GetLogNames() → ["4SELLERS", "Application"]
```

---

#### Shop-/Site-Lookups

```csharp
// Gibt ShopSetting für aktuell ausgewählte IisAppName zurück
public ShopSetting? GetShopSettingForCurrentSite()

// Gibt Site-Objekt für TrayIconIisSite zurück (Fallback auf IisAppName)
public Site? GetTrayIconSite()
```

---

## 5. ShopSetting – Shop-Konfiguration

**Datei:** `Toolbox.Data/Models/ShopSetting.cs`

Repräsentiert eine einzelne Shop-Installation und verknüpft Dateisystem-Pfade mit einer IIS-Site.

```csharp
public class ShopSetting
{
    public int Id { get; set; }                                        // Primärschlüssel
    public long SiteId { get; set; }                                   // IIS-Site-ID (long.MinValue = keine)
    public string ThemeFolderPath { get; set; } = string.Empty;       // Pfad zum Themes/-Ordner
    public string ShopYamlPath { get; set; } = string.Empty;          // Pfad zur Shop.yaml
    public ToolboxSettings ToolboxSettings { get; set; } = default!;  // Navigationsproperty
}
```

### Methoden

| Methode | Rückgabe | Beschreibung |
|---|---|---|
| `GetShopYamlContent()` | `ShopsystemConfig` | Liest und deserialisiert `Shop.yaml` von Disk |
| `GetConnectionString()` | `string?` | Extrahiert DB-Verbindungsstring aus YAML |
| `GetConnection()` | `DatabaseConnection` | Gibt ersten DB-Connection-Eintrag aus YAML zurück |
| `OpenInVsc()` | `void` | Öffnet YAML-Datei in Visual Studio Code |
| `OpenInExplorer()` | `void` | Öffnet Shop-Ordner im Windows Explorer |

---

## 6. DatabaseConnection – Datenbankverbindung

**Datei:** `Toolbox.Data/Models/ShopYaml/DatabaseConnection.cs`

Repräsentiert eine SQL-Server-Verbindungskonfiguration aus der `Shop.yaml`.

```csharp
public class DatabaseConnection
{
    public string Server { get; set; }               // SQL-Server-Name
    public string Database { get; set; }             // Datenbankname
    public string User { get; set; }                 // Benutzername
    public string Password { get; set; }             // Passwort
    public int MaxPoolSize { get; set; }             // Connection-Pool-Größe
    public bool Encrypt { get; set; }                // SSL-Verschlüsselung
    public bool TrustServerCertificate { get; set; } // Selbst-signierte Zertifikate
}
```

### Standardwerte

| Feld | Standard |
|---|---|
| `Server` | `"(localdb)\\MSSQLLocalDB"` |
| `Database` | `"Shopsystem"` |
| `User` | `"shopsystem"` |
| `Password` | `"Test123!"` |
| `MaxPoolSize` | `1000` |
| `Encrypt` | `true` |
| `TrustServerCertificate` | `true` |

### Methoden

| Methode | Beschreibung |
|---|---|
| `GetConnectionString()` | Formatierter ADO.NET-Verbindungsstring |
| `DeepClone()` | Unabhängige Kopie |
| `Equals()` / `GetHashCode()` | Wertgleichheit |
| `ToString()` | Lesbare Darstellung |

---

## 7. Datenbankschema & EF Core

**Datei:** `Toolbox.Data/DataContexts/InternalAppDbContext.cs`
**Datenbank:** SQLite, Pfad `%AppData%\BenjisToolbox\toolbox.db`

### DbSets

```csharp
public DbSet<ToolboxSettings> Settings => Set<ToolboxSettings>();
public DbSet<AppInfo> AppInfos => Set<AppInfo>();
public DbSet<ShopSetting> ShopSettings => Set<ShopSetting>();
public DbSet<DatabaseConnection> ShopDatabaseConnections => Set<DatabaseConnection>();
public DbSet<VmCustomerMapping> VmCustomerMappings => Set<VmCustomerMapping>();
```

### Settings-Tabelle (Konfiguration)

```csharp
// Tabellenname
modelBuilder.Entity<ToolboxSettings>().ToTable("Settings");

// ID wird nie automatisch generiert (manuell = 1)
modelBuilder.Entity<ToolboxSettings>()
    .Property(x => x.Id)
    .ValueGeneratedNever();

// Singleton-Constraint: nur Id = 1 erlaubt
modelBuilder.Entity<ToolboxSettings>()
    .HasCheckConstraint("CK_AppSettings_Singleton", "Id = 1");

// 1:n Beziehung zu ShopSettingsList
modelBuilder.Entity<ToolboxSettings>()
    .HasMany(x => x.ShopSettingsList)
    .WithOne(x => x.ToolboxSettings)
    .IsRequired();

// Standard-Seed
modelBuilder.Entity<ToolboxSettings>().HasData(new ToolboxSettings());
```

### Datenbank-Pragmas (gesetzt beim App-Start)

```sql
PRAGMA journal_mode=WAL;      -- Write-Ahead Logging (bessere Performance)
PRAGMA foreign_keys=ON;       -- Fremdschlüssel-Constraints aktivieren
```

---

## 8. Initialisierung beim App-Start

**Datei:** `Toolbox/Program.cs`, Zeilen ~100–125

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InternalAppDbContext>();

    // SQLite-Optimierungen
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

    // Datenbank-Migrationen anwenden
    await db.Database.MigrateAsync();

    // Shop-Pfade scannen und befüllen
    var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
    var manager = new ServerManager();
    settingsService.FillShopPathSettings(manager.Sites);
}
```

**Reihenfolge:**
1. DB-Verbindung herstellen
2. Performance-Pragmas setzen
3. Pending-Migrationen anwenden
4. `FillShopPathSettings()` – erkennt Shops im Filesystem und in IIS
5. Ab jetzt ist `SettingsService` für alle Komponenten einsatzbereit

---

## 9. UI-Komponenten (Settings-Seiten)

### 9.1 `GeneralSettings.razor`
**Datei:** `Toolbox/Components/Settings/GeneralSettings.razor`

Verwaltet allgemeine Repository-Pfade und Azure-DevOps-Einstellungen.

| Einstellung | Methode / Aktion |
|---|---|
| Theme-Repository-Pfade | `Settings.SetThemeRoots(...)` + `SaveSettingChanges()` |
| Extension-Repository-Pfade | `Settings.SetExtensionRoots(...)` + `SaveSettingChanges()` |
| Azure DevOps Collection-URL | `SaveSettingChanges(x => x.TfsCollectionUrl = ...)` |
| Azure DevOps Projekt-URLs | `Settings.SetTfsProjectUrls(...)` + `SaveSettingChanges()` |
| Azure DevOps API-Key | `SaveSettingChanges(x => x.TfsApiKey = ...)` |
| Basis-Ordner | `SaveSettingChanges(x => x.GeneralFolderPath = ...)` mit Debounce |
| Beta-Updates | `SaveSettingChanges(x => x.AllowBetaUpdates = ...)` |

---

### 9.2 `IisSettings.razor`
**Datei:** `Toolbox/Components/Settings/IisSettings.razor`

Verwaltet IIS-Neustart-Verhalten und Tray-Icon-Konfiguration.

```html
<!-- Neustart-Verzögerung -->
<MudNumericField T="int"
    ValueChanged="(int i) => SettingsService.SaveSettingChanges(
        x => x.RestartDelaySeconds = i, NotificationService)"
    Value="Setting.RestartDelaySeconds"
    Label="Zeit zwischen Start und Stop von IIS-Seiten (Sekunden)"
    Min="1"/>

<!-- Bundler löschen -->
<MudCheckBox ValueChanged="(bool i) => SettingsService.SaveSettingChanges(
                 x => x.DeleteBundlerOnShopRestart = i, NotificationService)"
             Value="Setting.DeleteBundlerOnShopRestart">
    Shop-Bundler beim Neustart löschen
</MudCheckBox>

<!-- Assets löschen -->
<MudCheckBox ValueChanged="(bool i) => SettingsService.SaveSettingChanges(
                 x => x.DeleteAssetsOnShopRestart = i, NotificationService)"
             Value="Setting.DeleteAssetsOnShopRestart">
    Shop-Assets(wwwroot) beim Neustart löschen
</MudCheckBox>

<!-- Tray-Icon-Site -->
<MudSelect Value="Setting.GetTrayIconSite().Id"
           ValueChanged="(long i) => SettingsService.SaveSettingChanges(
               x => x.TrayIconIisSite = i, NotificationService)"
           T="long">
    @foreach (var shop in Setting.ShopSettingsList)
    {
        <MudSelectItem Value="shop.SiteId">@GetShopDisplayName(shop)</MudSelectItem>
    }
</MudSelect>
```

---

### 9.3 `LogSettings.razor`
**Datei:** `Toolbox/Components/Settings/LogSettings.razor`

Verwaltet Einstellungen für die Log-Ansicht.

| Einstellung | Aktion |
|---|---|
| Windows Event Logs (Multi-Select) | `Settings.SetLogNames(...)` + `SaveSettingChanges()` |
| Auto-Refresh-Intervall | `SaveSettingChanges(x => x.AutoRefreshSeconds = ...)` |
| Auto-Refresh aktivieren | `SaveSettingChanges(x => x.AutoRefreshEnabled = ...)` |
| Logs bündeln | `SaveSettingChanges(x => x.BundleLogs = ...)` |
| Nur seit Neustart | `SaveSettingChanges(x => x.OnlySinceRestart = ...)` |

---

### 9.4 `ShopSettings.razor`
**Datei:** `Toolbox/Components/Settings/ShopSettings.razor`

Verwaltet die `ShopSettingsList` manuell.

- Tabelle aller erkannten Shop-Konfigurationen
- Bearbeiten: `SiteId`, `ShopYamlPath`, `ThemeFolderPath`
- Einträge hinzufügen (Dialog mit Validierung)
- Einträge löschen

Alle Änderungen über `SaveSettingChanges()` oder direkt via `SaveSettings()`.

---

### 9.5 `VmwareSettings.razor`
**Datei:** `Toolbox/Components/Settings/VmwareSettings.razor`

Verwaltet VMware vCenter-Verbindungsdaten.

| Einstellung | Aktion |
|---|---|
| vCenter URL | `SaveSettingChanges(x => x.VCenterUrl = ...)` |
| Benutzername | `SaveSettingChanges(x => x.VCenterUsername = ...)` |
| Passwort (mit Anzeigen/Verbergen) | `SaveSettingChanges(x => x.VCenterPassword = ...)` |
| SSL-Fehler ignorieren | `SaveSettingChanges(x => x.VCenterIgnoreSslErrors = ...)` |
| Verbindung testen | Prüft die eingegebenen Zugangsdaten |

---

### 9.6 `SettingsHelp.razor`
**Datei:** `Toolbox/Components/Settings/SettingsHelp.razor`

Zeigt Hilfe-Texte zu Einstellungen an.

- Liest `[Description]`-Attribute der Properties via **Reflection**
- Kann Markdown-Dateien für ausführlichere Beschreibungen laden
- Wird als Side-Panel oder Dialog eingebunden

---

## 10. NotificationService

**Datei:** `Toolbox/Services/NotificationService.cs`
**Interface:** `Toolbox.Data/Models/Interfaces/INotificationService.cs`

```csharp
public interface INotificationService
{
    void Success(string message);
    void Error(string message);
    void Info(string message);
    void Warning(string message);
}
```

**Implementierung** mit MudBlazor Snackbar:

```csharp
public class NotificationService : INotificationService
{
    private readonly ISnackbar _snackbar;

    public void Success(string message) => _snackbar.Add(message, Severity.Success);
    public void Error(string message)   => _snackbar.Add(message, Severity.Error);
    public void Info(string message)    => _snackbar.Add(message, Severity.Info);
    public void Warning(string message) => _snackbar.Add(message, Severity.Warning);
}
```

`SaveSettingChanges()` gibt immer folgenden Toast aus:
- Erfolg: `"Einstellungen erfolgreich gespeichert"` (grün)
- Kein Change-Objekt: `"Keine Änderungen zum Speichern"` (rot)

---

## 11. Dependency Injection

**Datei:** `Toolbox/Program.cs`

```csharp
// Zeilen 70–71
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Datenbankkontext (für SettingsService notwendig)
builder.Services.AddDbContext<InternalAppDbContext>(
    options => options.UseSqlite(connStr)); // Line 64

// NotificationService
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
```

**Scope:** `Scoped` – eine Instanz pro Blazor-Komponenten-Baum. Da `Load()` im Konstruktor aufgerufen wird, sind Einstellungen sofort verfügbar.

**Zugriffsmuster:**
- Komponenten, die nur lesen: Injizieren `ISettingsService`
- Komponenten, die auch schreiben: Injizieren `SettingsService` direkt

---

## 12. Verwendungsmuster im gesamten Code

### Lesezugriff

```csharp
// In Home.razor
@inject ISettingsService SettingsService

// Verwendung
var logNames = SettingsService.Settings.GetLogNames();
var iisApp   = SettingsService.Settings.IisAppName;
```

### Einstellung ändern und speichern

```csharp
// Standard-Muster in allen Settings-Razor-Komponenten
SettingsService.SaveSettingChanges(
    x => x.PropertyName = newValue,
    NotificationService
);
```

### IIS-App wechseln

```csharp
// In Home.razor – Site-Auswahlmenü
<MudSelect Value="Settings.IisAppName"
           ValueChanged="@((string? s) => SettingsService.ChangeSelectedIisApp(s))">
```

### Konfigurationscheck

```csharp
// In Home.razor – OnInitializedAsync
if (SettingsService.IsConfigured)
    await ApplySettings();
else
    // Konfigurationshinweis anzeigen
```

### In IisService

```csharp
// IisService nutzt Settings über ISettingsService
var site = _serverManager.Sites
    .FirstOrDefault(x => x.Name == _settingsService.Settings.IisAppName);
```

---

## 13. Vollständiges Ablaufdiagramm

### App-Start

```
Program.cs startet
        │
        ▼
AddDbContext<InternalAppDbContext>()
AddScoped<SettingsService>()
        │
        ▼
app.Services.CreateScope()
        │
        ├─> PRAGMA journal_mode=WAL
        ├─> PRAGMA foreign_keys=ON
        ├─> db.Database.MigrateAsync()
        │
        ▼
SettingsService wird instanziiert
  └─> Konstruktor → Load()
        ├─> EnsureCreated()
        ├─> _db.Settings.Include(ShopSettingsList).FirstOrDefault()
        │      │
        │   Gefunden?
        │      ├─ Ja:
        │      │   ├─ IisAppName null? → ersten IIS-Site-Namen setzen
        │      │   └─ IsConfigured = true
        │      └─ Nein / Exception:
        │          └─ IsConfigured = false
        │
        ▼
FillShopPathSettings(manager.Sites)
  │
  ├─ Phase 1: IIS → YAML-Map aufbauen
  │     Für jede Site: GetSitePath() → Shop.yaml suchen → SiteId merken
  │
  ├─ Phase 2: Filesystem-Scan
  │     EnumerateShopFolders(GeneralFolderPath)
  │       └─ BFS-Traversal (ignoriert: .git, node_modules, bin, ...)
  │       └─ Shop = hat Shop.yaml + Themes/
  │     Parallel: ShopCandidates erstellen
  │     ShopSettingsList aktualisieren / ergänzen
  │
  ├─ Phase 3: Fehlende IIS-Sites ergänzen
  │     Für jede IIS-Site: IsShop()? → ShopSetting erstellen/aktualisieren
  │
  └─ SaveSettings()
```

---

### Einstellung speichern (UI)

```
Benutzer ändert Wert im UI (z.B. Checkbox)
        │
        ▼
ValueChanged-Handler feuert
        │
        ▼
SettingsService.SaveSettingChanges(
    x => x.Property = newValue,
    NotificationService
)
        │
    change == null?
        ├─ Ja → Toast: "Keine Änderungen zum Speichern" → return
        └─ Nein:
              │
              ▼
        change(Settings)     // Lambda modifiziert gecachtes Settings-Objekt
              │
              ▼
        Toast: "Einstellungen erfolgreich gespeichert"
              │
              ▼
        SaveSettings()
          ├─> EnsureCreated()
          ├─> _db.Settings.Update(Settings)
          └─> _db.SaveChanges()  // Schreibt in SQLite
```

---

### IIS-App wechseln

```
Benutzer wählt andere Site im Dropdown
        │
        ▼
MudSelect.ValueChanged → ChangeSelectedIisApp(siteName)
        │
        ▼
Settings.IisAppName = siteName
        │
        ▼
_db.SaveChanges()    // Direkt – kein SaveSettings(), kein Toast
```

---

### Import / Export

```
Export:
  SettingsService.Export()
    ├─> Downloads-Pfad: %USERPROFILE%\Downloads
    ├─> Dateiname: Toolbox-Settings-{yyyy-MM-dd}.json
    ├─> JSON serialisieren (indentiert)
    └─> Datei schreiben

Import (manuell):
  SettingsService.Import(path)
    ├─> Datei lesen
    ├─> JSON → ToolboxSettings deserialisieren
    ├─> Settings = deserializedSettings
    └─> SaveSettings()

Import (automatisch):
  SettingsService.LoadSettingsFromExport()
    ├─> Downloads-Ordner nach Toolbox-Settings-*.json durchsuchen
    ├─> Neueste Datei auswählen
    └─> Import(path) aufrufen
```

---

*Ende des Manuskripts*
