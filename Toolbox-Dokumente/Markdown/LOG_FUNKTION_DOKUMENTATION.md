# Log-Funktion auf der Startseite – Technisches Manuskript

> **Projekt:** TeamShop.ShopToolbox
> **Erstellt:** 2026-03-31
> **Autor:** Benjamin Biber

---

## Inhaltsverzeichnis

1. [Übersicht & Architektur](#1-übersicht--architektur)
2. [Datenmodelle](#2-datenmodelle)
3. [Services](#3-services)
4. [Home.razor – Die Startseite](#4-homerazor--die-startseite)
5. [Vollständiger Workflow: Log laden](#5-vollständiger-workflow-log-laden)
6. [Such- und Filter-Logik](#6-such--und-filter-logik)
7. [Auto-Refresh-Timer](#7-auto-refresh-timer)
8. [Erweiterungs-Extraktion aus Logs](#8-erweiterungs-extraktion-aus-logs)
9. [UI-Komponenten & Dialoge](#9-ui-komponenten--dialoge)
10. [Einstellungen & Konfiguration](#10-einstellungen--konfiguration)
11. [Windows Event Log Integration](#11-windows-event-log-integration)
12. [Dependency Injection](#12-dependency-injection)
13. [Datenbankschema](#13-datenbankschema)
14. [Konstanten & Konfigurationswerte](#14-konstanten--konfigurationswerte)
15. [Vollständiges Ablaufdiagramm](#15-vollständiges-ablaufdiagramm)

---

## 1. Übersicht & Architektur

Die ShopToolbox ist eine **Blazor Desktop-Anwendung** (WPF-basiert), die auf **.NET 9.0** läuft. Die Log-Funktion auf der Startseite liest Einträge direkt aus dem **Windows-Ereignisprotokoll (Windows Event Log)** – es gibt keine eigene Datenbanktabelle für Logs.

### Projektstruktur (relevante Projekte)

| Projekt | Rolle |
|---|---|
| `Toolbox` | Haupt-UI (Blazor-Komponenten, Pages) |
| `Toolbox.Data` | Datenmodelle, Services, EF Core |

### Kernkomponenten der Log-Funktion

```
Home.razor                  ← Startseite (UI + State-Management)
  │
  ├── LogService             ← Liest Windows Event Log
  │     ├── TryReadWithEventLogReader()   [bevorzugt]
  │     └── TryReadWithEventLog()         [Fallback]
  │
  ├── EventLogService        ← Liefert Liste verfügbarer Log-Namen
  ├── AppInfoService         ← Liefert App-Startzeit & IIS-Neustart-Zeit
  └── SettingsService        ← Lädt Benutzereinstellungen
```

---

## 2. Datenmodelle

### 2.1 `LogEntry`
**Datei:** `Toolbox.Data/Models/Logs/LogEntry.cs`

Repräsentiert einen einzelnen Log-Eintrag, wie er in der Tabelle angezeigt wird.

```csharp
public class LogEntry
{
    public DateTime Time { get; set; }          // Zeitstempel des Ereignisses
    public LogLevel Level { get; set; }         // Schweregrad
    public string Message { get; set; }         // Rohtext des Ereignisses
    public LogMessage ParsedMessage { get; set; }  // Geparste Struktur
    public string FullMessage { get; set; }     // Vollständiger Nachrichtentext
    public string SearchText { get; set; }      // Gecachter Text für Suche
    public int Count { get; set; } = 1         // Anzahl gebündelter Einträge
}
```

**Wichtig:** `Count > 1` bedeutet, dass mehrere identische Einträge zu einem zusammengefasst wurden (Bundling-Feature).

---

### 2.2 `LogMessage`
**Datei:** `Toolbox.Data/Models/Logs/LogMessage.cs`

Repräsentiert die **geparste Struktur** eines Log-Eintrags (nach Regex-Zerlegung).

```csharp
public class LogMessage
{
    public string Origin { get; set; }    // ServiceId oder ShopId
    public string Metadata { get; set; } // Metadaten-Zeile
    public string Message { get; set; }  // Eigentliche Meldung

    // Gibt true zurück, wenn Message und Metadata befüllt sind
    public bool IsValid { get; }

    // Formatiert die Nachricht nach gewünschten Typen
    public string GetFormattedMessage(HashSet<MessageType> messageTypes)
}
```

Das erwartete **Eintragsformat im Windows Event Log** ist:

```
[ServiceId|ShopId]: <Herkunft>
Metadata: <Metadaten>
Message: <Meldung>
```

---

### 2.3 `LogLevel`
**Datei:** `Toolbox.Data/Models/Logs/LogLevel.cs`

```csharp
public enum LogLevel
{
    All,          // "Alle"
    Information,  // "Information"
    Warning,      // "Warnung"
    Error,        // "Fehler"
    Critical      // "Kritisch"
}
```

`All` ist nur intern für Abfragen relevant – in der UI werden die einzelnen Level als Multi-Select-Filter angeboten.

---

### 2.4 `MessageType`
**Datei:** `Toolbox.Data/Models/Logs/MessageType.cs`

Steuert, welche Teile einer `LogMessage` bei der Formatierung berücksichtigt werden.

```csharp
public enum MessageType
{
    Metadata,  // "Meta Daten"
    Message,   // "Meldung"
    Origin     // "Herkunft"
}
```

---

### 2.5 `LogBatch`
**Datei:** `Toolbox.Data/Services/LogService.cs` (innere Klasse)

Ergebnisobjekt von `GetLogsBatch()`. Enthält die geladenen Einträge **und** die aktuellen Record-IDs für inkrementelles Nachladen.

```csharp
public sealed class LogBatch
{
    public IReadOnlyList<LogEntry> Entries { get; }
    public IReadOnlyDictionary<string, long> LastRecordIds { get; }
}
```

---

### 2.6 `ReloadOption`
**Datei:** `Toolbox.Data/Common/ReloadTime.cs`

Bestimmt, ab welchem Zeitpunkt Logs geladen werden.

```csharp
public enum ReloadOption
{
    AlleLogs,               // DateTime.MinValue → alle verfügbaren Logs
    SeitStartDerAnwendung,  // seit App-Start
    SeitLetztemNeuladen     // seit letztem IIS-Neustart
}
```

---

## 3. Services

### 3.1 `LogService`
**Datei:** `Toolbox.Data/Services/LogService.cs`

Der Kern-Service für das Lesen und Parsen von Windows-Ereignisprotokoll-Einträgen.

#### Konstruktoren

```csharp
public LogService(string logName)
public LogService(IEnumerable<string> logNames)
```

Nimmt einen oder mehrere Windows-Event-Log-Namen (z.B. `"4SELLERS"`). Kann mit Semikolon getrennte Namen aus den Einstellungen verarbeiten.

---

#### Methode: `GetLogs()`

```csharp
public IEnumerable<LogEntry> GetLogs(DateTime since, LogLevel level = LogLevel.All)
```

Einfacher Wrapper um `GetLogsBatch()`. Gibt nur die Einträge zurück, ohne die Record-IDs.

---

#### Methode: `GetLogsBatch()` *(Hauptmethode)*

```csharp
public LogBatch GetLogsBatch(
    DateTime since,
    LogLevel level = LogLevel.All,
    IReadOnlyDictionary<string, long>? sinceRecordIds = null)
```

**Ablauf:**
1. Iteriert über alle konfigurierten Log-Namen (`_logNames`)
2. Versucht zuerst `TryReadWithEventLogReader()` (neue WEL-API)
3. Falls das fehlschlägt: Fallback auf `TryReadWithEventLog()` (Legacy-API)
4. Sammelt alle gelesenen Einträge
5. Sortiert absteigend nach `Time`
6. Gibt `LogBatch` mit Einträgen und max. Record-IDs zurück

**Inkrementelles Laden:** Wenn `sinceRecordIds` übergeben wird, liest der Service nur Einträge mit höherer Record-ID – verhindert doppeltes Verarbeiten bereits bekannter Einträge.

---

#### Methode: `ParseLog()` *(statisch)*

```csharp
public static LogMessage ParseLog(string logText)
```

Zerlegt den rohen Log-Text per Regex in strukturierte Felder.

**Regex-Pattern:**
```regex
^(?:(?<Origin>(ServiceId|ShopId):\s+.+?)\r?\n)?\s*Metadata:\s*(?<Metadata>.+?)\r?\n\s*Message:\s*(?<Message>.+)$
```

| Gruppe | Bedeutung | Beispiel |
|---|---|---|
| `Origin` | Herkunft (optional) | `ServiceId: CheckoutService` |
| `Metadata` | Metadaten-Zeile | `OrderId=12345, UserId=42` |
| `Message` | Eigentliche Meldung | `Order successfully placed` |

Regex-Optionen: `Singleline` + `Multiline` → erlaubt mehrzeilige Nachrichten.

---

#### Methode: `ExtractTimestamp()`

```csharp
private static DateTime? ExtractTimestamp(string logText)
```

**Pattern:**
```regex
Timestamp:\s*(\d{4}\.\d{2}\.\d{2} \d{2}:\d{2}:\d{2}\.\d{3})
```

Liest einen explizit eingebetteten Zeitstempel aus dem Log-Text (Format: `yyyy.MM.dd HH:mm:ss.fff`). Wenn kein Timestamp gefunden wird, verwendet der Service die `TimeCreated`-Zeit des Windows-Ereignisses.

---

#### Methode: `TryReadWithEventLogReader()` *(privat)*

```csharp
private bool TryReadWithEventLogReader(
    string logName, DateTime since, LogLevel level,
    long? sinceRecordId, List<LogEntry> entries,
    out long maxRecordId)
```

Verwendet die moderne **`System.Diagnostics.Eventing.Reader.EventLogReader`-API**. Baut eine XPath-Abfrage über `BuildQuery()` und iteriert durch die Ergebnisse. Bevorzugte Methode.

---

#### Methode: `TryReadWithEventLog()` *(privat)*

```csharp
private bool TryReadWithEventLog(
    string logName, DateTime since, LogLevel level,
    long? sinceRecordId, List<LogEntry> entries,
    out long maxRecordId)
```

Fallback auf die ältere **`System.Diagnostics.EventLog`-API**. Iteriert rückwärts durch alle Einträge und filtert manuell nach Zeitstempel und Log-Level.

---

#### Methode: `BuildQuery()` *(privat)*

```csharp
private static string BuildQuery(DateTime since, LogLevel level, long? sinceRecordId)
```

Erzeugt eine **XPath-Abfrage** für den EventLogReader, die nach Zeitraum, Log-Level und Record-ID filtert.

---

#### Hilfsmethoden (Mapping)

| Methode | Eingang | Ausgang |
|---|---|---|
| `MapEntryType(EventLogEntryType)` | Windows-Ereignistyp | `LogLevel` |
| `MapLevel(byte?)` | Numerischer Level | `LogLevel` |
| `MapLevelValue(LogLevel)` | `LogLevel` | `int` (für XPath) |
| `CreateEntry(...)` | Rohdaten | `LogEntry` |

---

### 3.2 `EventLogService`
**Datei:** `Toolbox.Data/Services/EventLogService.cs`

```csharp
public IReadOnlyList<string> GetLogNames(): List<string>
```

Ruft alle auf dem System verfügbaren Windows-Event-Log-Namen ab. Gibt eine sortierte, deduplizierte Liste zurück – wird für das Log-Name-Auswahlmenü in den Filtern verwendet.

---

### 3.3 `AppInfoService`
**Datei:** `Toolbox.Data/Services/AppInfoService.cs`

Verwaltet `AppInfo` in der SQLite-Datenbank.

**Für die Log-Funktion relevant:**
- `StartTime` → Untergrenze für `ReloadOption.SeitStartDerAnwendung`
- `IisRestartTime` → Untergrenze für `ReloadOption.SeitLetztemNeuladen`

---

### 3.4 `SettingsService`
**Datei:** `Toolbox.Data/Services/SettingsService.cs`

Lädt und speichert `ToolboxSettings` aus der SQLite-Datenbank. Stellt alle Log-relevanten Einstellungen bereit (Log-Namen, Auto-Refresh, Bundling etc.).

---

## 4. Home.razor – Die Startseite

**Datei:** `Toolbox/Components/Pages/Home.razor`
**Route:** `@page "/"`

Die Startseite ist das zentrale UI-Element. Sie verwaltet den kompletten Log-State und die Darstellung.

### Wichtige private Felder

```csharp
// Log-Daten
private List<LogEntry> _logs = new();              // Alle geladenen Logs
private IEnumerable<LogEntry> _displayLogs = [];   // Gefilterte/gebündelte Anzeige
private LogEntry? _selectedLog;                     // Aktuell ausgewählter Eintrag
private LogService? _logService;                    // Aktueller Service

// State
private bool _isLoading;                            // Ladezustand
private Dictionary<string, long> _lastRecordIds = new(); // Für inkrementelles Laden

// Zeit
private DateTime _logSinceTime;                     // Untere Zeitgrenze
private DateTime _appStartTime;                     // App-Startzeit
private DateTime _iisRestartTime;                   // IIS-Neustart-Zeit

// Filter
private HashSet<LogLevel> _selectedLevels = new(); // Aktive Level-Filter
private ReloadOption _reloadOption;                 // Lade-Modus
private string _searchText = "";                    // Suchtext (roh)
private string _searchTextNormalized = "";          // Suchtext (normalisiert)

// Auto-Refresh
private bool _autoRefresh;
private int _refreshSeconds;
private System.Timers.Timer? _refreshTimer;

// Erweiterungen (aus Logs extrahiert)
private List<string> _loadedExtensions = [];
private List<string> _excludedExtensions = [];
```

---

### Initialisierung: `OnInitializedAsync()`

**Zeilen ~393–411**

```
OnInitializedAsync()
  ├─> AppInfoService.GetAsync()
  │     → Speichert _appStartTime und _iisRestartTime
  ├─> SettingsService.IsConfigured?
  │     Nein → Zeige Konfigurations-Hinweis
  └─> Ja → ApplySettings()
              └─> LoadLogs()
```

---

### `ApplySettings()`

**Zeilen ~413–427**

Reagiert auf Einstellungsänderungen (z.B. nach Speichern in den Einstellungen).

```csharp
private void ApplySettings()
{
    // Neuen LogService mit konfigurierten Log-Namen erstellen
    UpdateLogService();     // Settings.GetLogNames() → new LogService(names)

    // Cursor zurücksetzen → nächstes Laden liest alle Einträge neu
    ResetLogCursor();       // _lastRecordIds.Clear()

    // Einstellungen übernehmen
    _autoRefresh = Settings.AutoRefreshEnabled;
    _refreshSeconds = Settings.AutoRefreshSeconds;

    // Zeitgrenze setzen
    _logSinceTime = Settings.OnlySinceRestart
        ? _iisRestartTime
        : _appStartTime;

    LoadLogs();
}
```

---

## 5. Vollständiger Workflow: Log laden

### `LoadLogs()` – Die Kernmethode

**Zeilen ~443–474**

```csharp
private async Task LoadLogs(ReloadOption option = default)
```

#### Schritt-für-Schritt-Ablauf

**Schritt 1: Ladeindikator setzen**
```csharp
_isLoading = true;
StateHasChanged();
```

**Schritt 2: IIS-Neustart-Zeit prüfen**
```csharp
await RefreshRestartTimeIfChangedAsync();
```
Prüft, ob IIS seit dem letzten Laden neu gestartet wurde. Falls ja, wird `_logSinceTime` und `_lastRecordIds` aktualisiert, um nur neue Logs zu zeigen.

**Schritt 3: Zeitgrenze bestimmen**

| `ReloadOption` | `since`-Wert |
|---|---|
| `AlleLogs` | `DateTime.MinValue` |
| `SeitLetztemNeuladen` | `_logSinceTime` (IIS-Neustart oder App-Start) |
| `SeitStartDerAnwendung` | `_appStartTime` |

**Schritt 4: Logs vom Service holen**
```csharp
var batch = _logService.GetLogsBatch(since, LogLevel.All, _lastRecordIds);
```

**Schritt 5: Record-IDs aktualisieren**
```csharp
UpdateLogCursor(batch.LastRecordIds);
// Speichert die höchste Record-ID je Log-Quelle für nächstes inkrementelles Laden
```

**Schritt 6: Logs zusammenführen**
```csharp
// Inkrementell: Neue Einträge vorne einfügen
_logs.InsertRange(0, batch.Entries);

// Vollständig (z.B. Filter-Änderung): Liste ersetzen
_logs = batch.Entries.ToList();
```

**Schritt 7: Cache begrenzen**
```csharp
TrimLogCache();
// Kürzt _logs auf maximal MaxLogEntries = 5.000 Einträge
```

**Schritt 8: Erweiterungen extrahieren**
```csharp
UpdateLoadedExtensions();
// Sucht in Log-Messages nach Extension-Informationen (siehe Kapitel 8)
```

**Schritt 9: Filtern & Bündeln**
```csharp
_displayLogs = FilterAndBundle(_logs.Where(e => _selectedLevels.Contains(e.Level)));
```

**Schritt 10: UI aktualisieren**
```csharp
_isLoading = false;
StateHasChanged();
```

---

### `RefreshRestartTimeIfChangedAsync()`

Vergleicht die gespeicherte `_iisRestartTime` mit dem aktuellen Wert aus `AppInfoService`. Hat sich die Zeit geändert (= IIS wurde neu gestartet), werden der Log-Cursor und die Zeitgrenze zurückgesetzt.

---

### `ResetLogCursor()`

```csharp
private void ResetLogCursor()
{
    _lastRecordIds.Clear();
}
```

Leert das Dictionary der zuletzt bekannten Record-IDs. Das nächste `LoadLogs()` liest dann alle Einträge ab der konfigurierten `since`-Zeit neu – kein inkrementelles Laden.

---

### `UpdateLogCursor()`

```csharp
private void UpdateLogCursor(IReadOnlyDictionary<string, long> lastRecordIds)
{
    foreach (var (logName, recordId) in lastRecordIds)
        _lastRecordIds[logName] = recordId;
}
```

Speichert für jede Log-Quelle die höchste gelesene Record-ID. Beim nächsten Laden werden nur Einträge mit **höherer** Record-ID abgerufen.

---

## 6. Such- und Filter-Logik

### 6.1 Suchtext-Eingabe

**`OnSearchTextChanged()`** (Zeilen ~934–956)

Der Benutzer tippt in das Suchfeld → Event wird ausgelöst → **Debounce-Timer** (250 ms) startet. Nach Ablauf wird `ApplySearchText()` aufgerufen. Verhindert unnötige Neuberechnungen bei schneller Eingabe.

```csharp
private const int SearchDebounceMs = 250;
```

---

**`ApplySearchText()`** (Zeilen ~958–962)

```csharp
private void ApplySearchText()
{
    _searchTextNormalized = _searchText.Trim().ToLowerInvariant();
    // Erneut filtern und rendern
}
```

Normalisiert den Suchtext (Leerzeichen trimmen, Kleinschreibung) für case-insensitive Suche.

---

### 6.2 `FilterAndBundle()`

**Zeilen ~312–376**

Hauptmethode für Filterung und optionales Bündeln identischer Einträge.

```csharp
private IEnumerable<LogEntry> FilterAndBundle(IEnumerable<LogEntry> source)
```

#### Easter Egg
```csharp
if (_searchTextNormalized == "andy ist der beste")
    // Besondere Anzeige... ;)
```

#### Suchfilter
```csharp
if (!string.IsNullOrEmpty(_searchTextNormalized))
    source = source.Where(e =>
        GetSearchText(e).Contains(_searchTextNormalized));
```

#### Bundling (`Settings.BundleLogs`)

Wenn aktiviert, werden aufeinanderfolgende identische Log-Einträge zusammengefasst:
- Gleiche Nachricht → `Count` wird erhöht
- In der Tabelle erscheint dann z.B. `(×5)` neben dem Eintrag

#### Sortierung
```csharp
.OrderByDescending(e => e.Time)
// Neueste Einträge oben
```

---

### 6.3 `GetSearchText()`

**Zeilen ~964–973**

```csharp
private string GetSearchText(LogEntry entry)
{
    // Gecachten Wert zurückgeben oder neu berechnen
    return entry.SearchText ??= entry.FullMessage.ToLowerInvariant();
}
```

Cacht den normalisierten Suchtext direkt im `LogEntry` – vermeidet wiederholte `ToLowerInvariant()`-Aufrufe beim Filtern.

---

### 6.4 `Highlight()`

**Zeilen ~378–391**

```csharp
private MarkupString Highlight(string text)
{
    var encoded = HttpUtility.HtmlEncode(text);
    if (string.IsNullOrEmpty(_searchTextNormalized))
        return new MarkupString(encoded);

    return new MarkupString(
        Regex.Replace(encoded, Regex.Escape(_searchTextNormalized),
            m => $"<mark>{m.Value}</mark>",
            RegexOptions.IgnoreCase));
}
```

Hebt Suchtreffer in der Tabellen- und Detailansicht mit `<mark>`-Tags hervor. Ergebnis ist ein `MarkupString` (Blazor-Raw-HTML).

---

## 7. Auto-Refresh-Timer

**`StartTimer()`** (Zeilen ~688–714)

```csharp
private void StartTimer()
{
    _refreshTimer?.Dispose();

    if (_autoRefresh && _refreshSeconds > 0)
    {
        _refreshTimer = new System.Timers.Timer(_refreshSeconds * 1000);
        _refreshTimer.Elapsed += async (_, _) => await InvokeAsync(LoadLogs);
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }
}
```

Erstellt einen `System.Timers.Timer`, der alle `_refreshSeconds` Sekunden `LoadLogs()` aufruft. `InvokeAsync()` stellt sicher, dass der UI-Thread korrekt benachrichtigt wird (Blazor-Threading).

### Verfügbare Intervalle

| Anzeigename | Intervall |
|---|---|
| Nicht Neuladen | 0 (kein Timer) |
| 3 sek | 3 Sekunden |
| 5 sek | 5 Sekunden |
| 10 sek | 10 Sekunden |
| 15 sek | 15 Sekunden |
| 30 sek | 30 Sekunden |
| 1 min | 1 Minute |
| 2 min | 2 Minuten |
| 5 min | 5 Minuten |
| 10 min | 10 Minuten |
| 15 min | 15 Minuten |
| 30 min | 30 Minuten |

---

## 8. Erweiterungs-Extraktion aus Logs

**`UpdateLoadedExtensions()`** (Zeilen ~536–686)

Die Startseite analysiert automatisch Log-Nachrichten und extrahiert daraus geladene und ausgeschlossene **Shop-Erweiterungen (Extensions)**.

### Geladene Erweiterungen

**Regex:**
```regex
Completely loaded and initialized extensions:\s*(?<exts>.+)
```

### Ausgeschlossene Erweiterungen

**Regex:**
```regex
Excluded extensions:\s*(?<exts>.+)
```

### Verarbeitung

1. Treffer im Komma-separierten Format parsen
2. In `_loadedExtensions` / `_excludedExtensions` speichern
3. Zugehörigen `LogEntry` in `_loadedExtensionLogs` / `_excludedExtensionLogs` speichern
4. Zeitstempel für "seit Neustart" vs. "alle Zeiten" unterscheiden

### UI-Darstellung

Die Erweiterungen erscheinen als klickbare **Chips** auf der Startseite (Zeilen ~64–128). Ein Klick auf einen Chip öffnet den zugehörigen Log-Eintrag in einem Dialog.

---

## 9. UI-Komponenten & Dialoge

### 9.1 `Home.razor` – UI-Aufbau

#### Suchleiste und Filter (Zeilen ~141–200)

| Element | Beschreibung |
|---|---|
| Texteingabe `_searchTextInput` | Volltextsuche mit 250ms Debounce |
| Level-Multi-Select | Information, Warning, Error, Critical |
| Log-Name-Multi-Select | Aus `EventLogService.GetLogNames()` |
| Reload-Option-Dropdown | `AlleLogs`, `SeitStartDerAnwendung`, `SeitLetztemNeuladen` |
| Auto-Refresh-Dropdown | Intervall-Auswahl aus `ReloadTime` |
| Button „Zeit auf jetzt setzen" | Setzt `_logSinceTime = DateTime.Now` |
| Button „Filter zurücksetzen" | Setzt alle Filter auf Standard |
| Refresh-Icon | Manuelles Neuladen |

#### Log-Tabelle (Zeilen ~135–228)

MudBlazor `MudTable` mit:

| Spalte | Details |
|---|---|
| Zeit | Sortierbar, Format `dd.MM.yyyy HH:mm:ss` |
| Level | Farbiges Icon (rot = Error, orange = Warning, etc.) |
| Nachricht | Gekürzter Text mit Highlight-Markierung |

- Paginierung: 50 / 100 / alle Einträge pro Seite
- Klick auf Zeile → `_selectedLog` setzen → Detailansicht öffnet sich

#### Log-Detailpanel (Zeilen ~230–253)

Zwei Spalten:
- **Metadaten:** `Origin` und `Metadata` aus `LogMessage`
- **Nachricht:** `Message` mit Scrollbereich und Highlight

---

### 9.2 `LogDialog.razor`
**Datei:** `Toolbox/Components/Dialogs/LogDialog.razor`

Generischer Log-Anzeige-Dialog für Operationslogs (z.B. IIS-Start/Stop). Akzeptiert:
- Einfachen `string text`
- Oder `LogBuffer` (Live-Updates mit `Changed`-Event)

Scrollt automatisch zum Ende, wenn sich der `LogBuffer`-Inhalt ändert.

---

### 9.3 `LogEntryDialog.razor`
**Datei:** `Toolbox/Components/Dialogs/LogEntryDialog.razor`

Zeigt einen einzelnen `LogEntry` im Detail:
- Titel: Level + Zeitstempel
- Geparste Metadaten (einklappbar)
- Vollständige Nachricht mit Zeilenumbrüchen

---

### 9.4 `LogBuffer`
**Datei:** `Toolbox/Components/Dialogs/LogBuffer.cs`

Thread-sicherer In-Memory-Puffer für Echtzeit-Operationslogs.

```csharp
public class LogBuffer
{
    private readonly StringBuilder _builder = new();
    private readonly object _syncRoot = new();

    public event Action? Changed;       // Feuert bei Änderung
    public string Text { get; }         // Thread-sicherer Getter

    public void Append(string? text)
    public void AppendLine(string? text)
}
```

Wird verwendet, um IIS-Operationslogs (Start/Stop/Restart) in Echtzeit im `LogDialog` anzuzeigen.

---

## 10. Einstellungen & Konfiguration

**Datei:** `Toolbox.Data/Models/ToolboxSettings.cs`

### Log-relevante Einstellungen

| Eigenschaft | Typ | Standard | Beschreibung |
|---|---|---|---|
| `LogName` | `string?` | `"4SELLERS"` | Semikolon-getrennte Windows Event Log-Namen |
| `AutoRefreshEnabled` | `bool` | `false` | Auto-Refresh aktivieren |
| `AutoRefreshSeconds` | `int` | `60` | Intervall in Sekunden (0 = deaktiviert) |
| `OnlySinceRestart` | `bool` | `true` | Nur Logs seit letztem IIS-Neustart anzeigen |
| `BundleLogs` | `bool` | `false` | Identische Einträge zusammenfassen |

### Hilfsmethoden in `ToolboxSettings`

```csharp
// Gibt Log-Namen als IEnumerable zurück (splittet by Semikolon/Newline)
public IEnumerable<string> GetLogNames()

// Speichert Log-Namen als Semikolon-getrennte Zeichenkette
public void SetLogNames(IEnumerable<string> names)
```

---

## 11. Windows Event Log Integration

### Zwei Lesemethoden

#### Methode 1: `EventLogReader` (bevorzugt)

Verwendet `System.Diagnostics.Eventing.Reader` – die moderne Windows-API für das Ereignisprotokoll.

**Vorteile:**
- XPath-basiertes Filtern (effizient, serverseitig)
- Record-ID-Unterstützung für inkrementelles Laden
- Bessere Performance bei großen Log-Mengen

**XPath-Abfrage-Beispiel:**
```xpath
*[System[TimeCreated[@SystemTime>='2024-01-01T00:00:00Z'] and Level<=2 and EventRecordID>12345]]
```

---

#### Methode 2: `EventLog` (Fallback)

Verwendet `System.Diagnostics.EventLog` – die ältere .NET-API.

**Ablauf:**
1. Rückwärts durch alle Einträge iterieren
2. Manuell nach Zeitstempel filtern (`entry.TimeGenerated >= since`)
3. Manuell nach Log-Level filtern
4. Bei Erreichen der `sinceRecordId` abbrechen

---

### Log-Format im Windows Event Viewer

Das System erwartet folgende Struktur im Ereignis-Text:

```
[Optional] ServiceId: CheckoutService
           oder
           ShopId: shop-de
Metadata: OrderId=12345 UserId=42 SessionId=abc
Message: Bestellung erfolgreich aufgegeben
```

Eingebetteter Zeitstempel (optional, ersetzt `TimeCreated`):
```
Timestamp: 2024.01.15 14:30:00.000
```

---

### Level-Mapping

| Windows EventLogEntryType | LogLevel (intern) |
|---|---|
| `Information` | `LogLevel.Information` |
| `Warning` | `LogLevel.Warning` |
| `Error` | `LogLevel.Error` |
| `FailureAudit` | `LogLevel.Critical` |

| Numerischer Level (ETW) | LogLevel (intern) |
|---|---|
| 1 | `LogLevel.Critical` |
| 2 | `LogLevel.Error` |
| 3 | `LogLevel.Warning` |
| 4 | `LogLevel.Information` |

---

## 12. Dependency Injection

**Datei:** `Toolbox/Program.cs`

```csharp
// Services
builder.Services.AddScoped<EventLogService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AppInfoService>();
builder.Services.AddScoped<IAppInfoService, AppInfoService>();

// Datenbank (SQLite)
builder.Services.AddDbContext<InternalAppDbContext>(
    options => options.UseSqlite(connStr));
```

> **Hinweis:** `LogService` wird **nicht** im DI-Container registriert. Er wird in `Home.razor` direkt instanziiert (`new LogService(logNames)`), weil er mit dynamischen Log-Namen konfiguriert wird, die sich zur Laufzeit ändern können.

---

## 13. Datenbankschema

**Datei:** `Toolbox.Data/DataContexts/InternalAppDbContext.cs`
**Speicherort:** `%AppData%\BenjisToolbox\toolbox.db`

### Relevante Tabellen für die Log-Funktion

| Tabelle | Modell | Beschreibung |
|---|---|---|
| `Settings` | `ToolboxSettings` | Singleton (Id = 1), enthält alle Log-Einstellungen |
| `AppInfos` | `AppInfo` | Singleton, speichert App-Startzeit und IIS-Neustart-Zeit |

> **Wichtig:** Log-Einträge selbst werden **nicht** in der Datenbank gespeichert. Sie werden bei jedem Ladevorgang frisch aus dem Windows Event Log gelesen.

### `AppInfo`-Felder (relevant)

```csharp
public DateTime StartTime { get; set; }       // Zeitpunkt des App-Starts
public DateTime IisRestartTime { get; set; }  // Letzter IIS-Neustart
```

---

## 14. Konstanten & Konfigurationswerte

| Konstante / Wert | Datei | Wert | Bedeutung |
|---|---|---|---|
| `MaxLogEntries` | `Home.razor` | `5000` | Max. Anzahl gecachter Log-Einträge |
| `SearchDebounceMs` | `Home.razor` | `250` | Debounce-Verzögerung Suche (ms) |
| `SingletonId` | `ToolboxSettings` | `1` | Primärschlüssel der Einstellungen |
| Default `LogName` | `ToolboxSettings` | `"4SELLERS"` | Standard-Event-Log-Name |
| Default `AutoRefreshSeconds` | `ToolboxSettings` | `60` | Standard-Refresh-Intervall |
| Timestamp-Format | `LogService` | `yyyy.MM.dd HH:mm:ss.fff` | Format im Log-Text |

---

## 15. Vollständiges Ablaufdiagramm

### Haupt-Log-Ladevorgang

```
Auslöser: Benutzer klickt Refresh / Timer feuert / Einstellungen ändern sich
                                    │
                                    ▼
                          LoadLogs(ReloadOption)
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
     RefreshRestartTimeIfChangedAsync()    'since' DateTime bestimmen
     (IIS neu gestartet? → Cursor reset)  nach ReloadOption
                    │                               │
                    └───────────────┬───────────────┘
                                    ▼
                   LogService.GetLogsBatch(since, All, lastRecordIds)
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
       TryReadWithEventLogReader()        TryReadWithEventLog()
       (EventLogReader API / XPath)       (Legacy EventLog API)
       [bevorzugt]                        [Fallback]
                    │                               │
                    └───────────────┬───────────────┘
                                    ▼
                         Für jeden Log-Eintrag:
                         ┌─────────────────────┐
                         │ ExtractTimestamp()   │  ← aus Log-Text
                         │ ParseLog()           │  ← Origin/Metadata/Message
                         │ MapLevel()           │  ← Windows-Type → LogLevel
                         │ CreateEntry()        │  ← LogEntry erstellen
                         └─────────────────────┘
                                    │
                                    ▼
                   LogBatch zurückgeben (sortiert, absteigend)
                                    │
                                    ▼
                         UpdateLogCursor()
                         (Record-IDs speichern)
                                    │
                                    ▼
                    Logs in _logs einfügen / ersetzen
                                    │
                                    ▼
                           TrimLogCache()
                           (max. 5.000 Einträge)
                                    │
                                    ▼
                       UpdateLoadedExtensions()
                       (Regex: geladene/ausgeschl. Extensions)
                                    │
                                    ▼
                    Level-Filter anwenden (_selectedLevels)
                                    │
                                    ▼
                         FilterAndBundle()
                         ┌────────────────────────────┐
                         │ 1. Easter Egg Check        │
                         │ 2. Suchtext filtern        │
                         │ 3. Bundling (optional)     │
                         │ 4. Absteigend sortieren    │
                         └────────────────────────────┘
                                    │
                                    ▼
                    _displayLogs = gefilterte Einträge
                                    │
                                    ▼
                         StateHasChanged()
                         MudTable neu rendern
```

---

### Such-Workflow

```
Benutzer tippt in Suchfeld
        │
        ▼
OnSearchTextChanged()
        │
   250ms warten (Debounce)
        │
        ▼
ApplySearchText()
  → _searchTextNormalized = input.Trim().ToLowerInvariant()
        │
        ▼
FilterAndBundle() wird neu aufgerufen
        │
        ▼
GetSearchText(entry)
  → entry.SearchText (gecacht) oder FullMessage.ToLowerInvariant()
        │
        ▼
.Contains(_searchTextNormalized)
        │
        ▼
Matching-Einträge → _displayLogs
        │
        ▼
Highlight() – <mark>-Tags in Anzeige
```

---

### Auto-Refresh-Workflow

```
ApplySettings() oder Benutzer ändert Intervall
        │
        ▼
StartTimer()
        │
   _autoRefresh && _refreshSeconds > 0?
        │                   │
       Ja                  Nein
        │                   │
        ▼                   ▼
  Timer erstellen     Timer nicht starten
  (Interval = Sek.)
        │
   Timer.Elapsed feuert
        │
        ▼
InvokeAsync(LoadLogs)
(inkrementell: nutzt _lastRecordIds)
```

---

*Ende des Manuskripts*
