# IHK-Projektdokumentation – Kontextdatei für NotebookLM
## Zentrales Steuerungs- und Loganalysemodul für Shopsysteme

> **Hinweis:** Diese Datei dient als Wissensbasis für NotebookLM zur Erstellung der IHK-Projektdokumentation.
> Platzhalter sind mit `[TODO]` markiert und müssen manuell ergänzt werden.

---

## 1. Deckblatt / Projektübersicht

| Feld | Wert |
|---|---|
| Projekttitel | Zentrales Steuerungs- und Loganalysemodul für Shopsysteme |
| Auftraggeber | 4SELLERS GmbH, Nelkenweg 6a, 86641 Rain am Lech |
| Projektverantwortlicher | Benjamin Biber |
| Ausbildungsberuf | Fachinformatiker Anwendungsentwicklung |
| Projektbeginn | 02.03.2026 |
| Projektabschluss | 20.03.2026 |
| Geplanter Gesamtaufwand | 80 Stunden (ca. 26 Stunden pro Woche) |
| Entwicklungsumgebung | JetBrains Rider |
| Versionsverwaltung | Git |

---

## 2. Projektbeschreibung & Ist-Analyse

### Unternehmenskontext

Die **4SELLERS GmbH** bietet integrierte Komplettlösungen für Onlinehändler an und entwickelt Software für den Omnichannel-Vertrieb. Das Unternehmen entwickelt und betreibt ein eigenes Shopsystem, das über den **Internet Information Services (IIS)** von Microsoft als Webanwendung auf Windows-Systemen gehostet wird.

### Bestehende Shop-Toolbox

Die Shop-Toolbox ist ein internes Werkzeug, das wesentliche Funktionen zur Unterstützung der Entwicklung in einer zentralen Anwendung bündelt. Sie wird vom Entwicklungsteam der 4SELLERS GmbH täglich genutzt.

### Ausgangssituation / Problem

Im Rahmen der Weiterentwicklung des Shopsystems fallen regelmäßig wiederkehrende administrative Tätigkeiten an, die zum Zeitpunkt des Projektstarts manuell durchgeführt werden müssen:

1. **Analyse von Fehlermeldungen:** Entwickler müssen die Windows-Ereignisanzeige manuell öffnen, navigieren und Einträge durchsuchen, um relevante Fehler zu finden. Dies ist zeitaufwändig und erfordert das Wechseln zwischen Anwendungen.

2. **Neustart von Shopsystemen:** Das Starten, Stoppen und Neustarten von IIS-gehosteten Anwendungen erfolgt manuell über den IIS-Manager oder PowerShell. Dabei müssen zusätzlich statische Dateien (z. B. CSS-Stylesheets) manuell bereinigt werden, um einen sauberen Neustart zu gewährleisten.

Diese manuellen Prozesse führen zu einem erhöhten Zeitaufwand und beeinträchtigen die Effizienz des Entwicklungsteams.

---

## 3. Soll-Konzept / Zielsetzung

Ziel des Projekts ist es, die oben beschriebenen manuellen Abläufe durch zwei neue Funktionen innerhalb der bestehenden Shop-Toolbox zu automatisieren und zu optimieren.

### Funktion 1: Windows-Ereignisanzeige / Log-Analyse

- Auslesen von Logeinträgen aus der **Windows-Ereignisanzeige** (Windows Event Viewer)
- Darstellung der Logs in einer **dynamischen Tabelle** innerhalb der Shop-Toolbox
- Erweiterung der Ansicht um **Suchleiste**, **Filterung** (nach Log-Level) und **Sortierfunktion**
- **Detail-Ansicht** für einzelne Log-Einträge (Metadaten, Nachricht, Zeitstempel)
- **Auto-Refresh** in konfigurierbarem Intervall (0–300 Sekunden)
- **Log-Bündelung:** Identische Einträge werden zusammengefasst (konfigurierbar)
- Konfigurierbare Auswahl der Ereignisprotokoll-Quellen (Log-Namen)

### Funktion 2: IIS-Steuerungsmodul

- **Starten, Stoppen und Neustarten** von im IIS gehosteten Shopsystemen direkt aus der Shop-Toolbox
- **Statusanzeige** des aktuellen IIS-Site-Status in der Benutzeroberfläche
- **App-Pool-Recycling** als alternative Neustart-Methode
- Beim Neustart: **automatisierte Bereinigung statischer Dateien** (CSS-Bundler-Cache unter `C:\Windows\Temp\shopsystem\bundler` und optionale `wwwroot`-Bereinigung), um die erneute Generierung zu erzwingen
- Konfigurierbare **Verzögerung** zwischen Stopp und Start (in Sekunden)

---

## 4. Technologiestack

### Frameworks & Laufzeitumgebung

| Technologie | Version | Verwendungszweck |
|---|---|---|
| .NET | 9.0 | Zielframework der gesamten Anwendung |
| Blazor Desktop (BlazorDesktop) | 9.0.2 | UI-Framework – Blazor-Komponenten in WPF-Host |
| WPF (Windows Presentation Foundation) | .NET 9 | Nativer Windows-App-Host (`UseWPF=true`) |
| WebView2 | integriert in BlazorDesktop | Rendering-Engine für Blazor-UI |
| Windows Forms | .NET 9 | Ergänzende native UI-Komponenten (`UseWindowsForms=true`) |
| MudBlazor | 8.13.0 | Material-Design-Komponentenbibliothek für Blazor |
| C# | 13 | Implementierungssprache |

### Relevante NuGet-Pakete

| Paket | Version | Zweck |
|---|---|---|
| `Microsoft.Web.Administration` | 11.1.0 | IIS-Verwaltung (Sites starten/stoppen/neustarten) |
| `System.Diagnostics.EventLog` | 9.0.0 | Zugriff auf Windows-Ereignisanzeige |
| `Microsoft.EntityFrameworkCore` | 9.0.10 | ORM für Datenbankzugriff |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.10 | SQLite-Datenbankprovider |
| `DartSassBuilder` | 1.1.0 | SCSS-Kompilierung für Stylesheets |
| `YamlDotNet` | 16.3.0 | Parsing von Shop-YAML-Konfigurationsdateien |
| `BenjaminBiber.ApplicationState` | 1.0.6 | Anwendungs-State-Management |
| `BenjaminBiber.DynamicInput` | 0.1.1 | Dynamische Eingabekomponenten |
| `Markdig` | 0.38.0 | Markdown-Rendering |
| `Blazor.AceEditorJs` | 1.2.1 | Code-Editor-Komponente |
| `Microsoft.TeamFoundationServer.Client` | 20.256.2 | Azure DevOps / TFS-Integration |

### Entwicklungsumgebung

- **IDE:** JetBrains Rider
- **Betriebssystem:** Windows 11 Enterprise
- **Ziel-OS:** Windows 10 (10.0.22000.0) und neuer

---

## 5. Projektphasen & Zeitplanung

### Übersicht

| Phase | Aufgaben | Stunden |
|---|---|---|
| Analyse und Konzeptionierung | Überblick über bestehende Komponenten, Konzepterstellung | 12 h |
| Implementierung | Entwicklung beider Funktionen (IIS + Logging) | 40 h |
| Testen | Smoke-Tests, Integrationstests, User-Acceptance-Tests | 18 h |
| Dokumentation | Verfassen der Projektdokumentation | 10 h |
| **Gesamt** | | **80 h** |

### Implementierungsphase – Detailaufgaben (40 Stunden)

- Entwicklung der Steuerungslogik für den IIS
- Integration von Aktionsschaltflächen (Start, Stopp, Neustart) einschließlich Statusanzeige in die Benutzeroberfläche
- Implementierung des Dienstes zum Auslesen der Windows-Ereignisanzeige
- Entwicklung einer performanten Such- und Filterfunktion für Logdaten
- Implementierung einer dynamischen Logging-Tabelle mit Suchleiste, Filter- und Sortierfunktion

### Testphase – Detailaufgaben (18 Stunden)

- **Smoke-Tests:** Grundlegende Funktionstests aller implementierten Features
- **Integrationstests:** Test des Zusammenspiels von IIS-Service, EventLog-Service und UI-Komponenten
- **User-Acceptance-Tests (UAT):** Abnahme durch das Entwicklungsteam der 4SELLERS GmbH

---

## 6. Architektur

### Lösungsstruktur

Die Anwendung ist als **.NET Multi-Projekt-Lösung** aufgebaut und besteht aus zwei für das Abschlussprojekt relevanten Teilprojekten:

| Projekt | Typ | Aufgabe |
|---|---|---|
| `Toolbox` | Blazor Desktop App (WPF-Host) | Präsentationsschicht: UI-Komponenten, Razor Pages, Dialogs, Services |
| `Toolbox.Data` | .NET Class Library | Datenschicht: Business-Logic-Services, Datenmodelle, EF Core DbContext |

Daneben existieren die Hilfsprojekte `Toolbox.Updater` (automatische Aktualisierung) und `Toolbox.Versioning` (Versionsverwaltung), die nicht Gegenstand des Abschlussprojekts sind.

### Schichtenarchitektur

```
┌─────────────────────────────────────────────────┐
│              Toolbox (UI-Schicht)                │
│  Components/Pages/   Components/Dialogs/         │
│  Components/Settings/   Services/                │
│  (Razor-Komponenten, MudBlazor, Blazor Desktop)  │
├─────────────────────────────────────────────────┤
│           Toolbox.Data (Datenschicht)            │
│  Services/   Models/   DataContexts/   Common/   │
│  (Business Logic, EF Core, IIS, EventLog)        │
├─────────────────────────────────────────────────┤
│              Windows / IIS (Systemebene)         │
│  Windows Event Log   IIS (Internet Info Service) │
└─────────────────────────────────────────────────┘
```

### Dependency Injection

Die Anwendung verwendet das .NET Standard-DI-System (`Microsoft.Extensions.DependencyInjection`). Alle Services werden in `Program.cs` registriert und per Constructor Injection in Komponenten und Services injiziert.

---

## 7. Implementierung: IIS-Steuerungsmodul

### Zuständige Klasse: `IisService` (`Toolbox.Data/Services/IisService.cs`)

Die gesamte IIS-Steuerungslogik ist in der Klasse `IisService` gekapselt. Sie wird als Singleton-Service registriert und nutzt das NuGet-Paket `Microsoft.Web.Administration` für den Zugriff auf den IIS.

#### Abhängigkeiten (Constructor Injection)

| Interface | Beschreibung |
|---|---|
| `INotificationService` | Anzeige von Erfolgs-/Fehlermeldungen in der UI |
| `ISettingsService` | Zugriff auf Anwendungseinstellungen (z. B. Restart-Delay, Lösch-Optionen) |
| `IAppInfoService` | Lesen und Setzen von App-Metadaten (z. B. letzter Restart-Zeitpunkt) |

#### Kernmethoden

| Methode | Beschreibung |
|---|---|
| `GetSites()` | Gibt alle im IIS konfigurierten Sites zurück (`IEnumerable<Site>`) |
| `StartIisApp(Site site, bool showNotification)` | Startet eine IIS-Site |
| `StopIisApp(Site site, bool showNotification)` | Stoppt eine IIS-Site |
| `RestartIisApp(Site? site)` | Stoppt, wartet (konfigurierbarer Delay), bereinigt optionale Dateien, startet neu |
| `RecycleAppPool(Site site, bool showNotification)` | Recycelt den Application Pool der Site |
| `DeleteShopBundler()` | Löscht den Bundler-Cache unter `C:\Windows\Temp\shopsystem\bundler` |
| `GetSiteNameById(long id)` | Gibt den Site-Namen anhand der IIS-Site-ID zurück |

#### Neustart-Ablauf (`RestartIisApp`)

1. IIS-Site stoppen (`StopIisApp`)
2. Warten (`RestartDelaySeconds` aus den Settings)
3. Optional: Bundler-Cache löschen (`DeleteBundlerOnShopRestart`)
4. Optional: `wwwroot`-Assets löschen (`DeleteAssetsOnShopRestart`)
5. IIS-Site starten (`StartIisApp`)
6. `AppInfoService.SetLastRestartTimeAsync()` aufrufen

#### Verwendete Technologie

```csharp
using Microsoft.Web.Administration;

// Beispiel: Site stoppen
using var serverManager = new ServerManager();
var site = serverManager.Sites[siteName];
site.Stop();
serverManager.CommitChanges();
```

### UI-Integration

| Datei | Beschreibung |
|---|---|
| `Toolbox/Components/Pages/Home.razor` | Hauptseite mit Start-/Stopp-/Neustart-Schaltflächen und Statusanzeige |
| `Toolbox/Components/Pages/Sites.razor` | Tabellenübersicht aller IIS-Sites mit Massenaktionen |
| `Toolbox/Components/Settings/IisSettings.razor` | Einstellungsseite: Restart-Delay, Bundler-Option, Assets-Option |

---

## 8. Implementierung: Windows-Ereignisanzeige / Log-Analyse

### Zuständige Klassen

#### `EventLogService` (`Toolbox.Data/Services/EventLogService.cs`)

Hilfsdienst zur Enumeration der verfügbaren Windows-Ereignisprotokolle.

| Methode | Beschreibung |
|---|---|
| `GetLogNames()` | Gibt alle verfügbaren Event-Log-Namen des Systems zurück |

#### `LogService` (`Toolbox.Data/Services/LogService.cs`)

Kernservice für das Lesen, Parsen und Filtern von Logeinträgen aus der Windows-Ereignisanzeige.

| Methode | Beschreibung |
|---|---|
| `GetLogs(DateTime since, LogLevel level)` | Gibt Logs ab einem Zeitpunkt und ab einem Level zurück |
| `GetLogsBatch(DateTime since, LogLevel level, IReadOnlyDictionary<string, long>? sinceRecordIds)` | Inkrementelles Laden via Record-ID-Cursor (vermeidet Duplikate) |
| `ParseLog(string logText)` | Parst den rohen Log-Text in ein `LogMessage`-Objekt (Regex: `Origin`, `Metadata`, `Message`) |
| `ExtractTimestamp(string logText)` | Extrahiert den Zeitstempel aus dem Log-Text |
| `TryReadWithEventLogReader()` | Primäre Lesestrategie via `EventLogReader` (WQL-Query) |
| `TryReadWithEventLog()` | Fallback-Strategie via klassischer `EventLog`-Klasse |
| `BuildQuery()` | Erstellt WQL-Abfrage mit Filterung nach Zeitstempel, Record-ID und Level |

#### Lesestrategie

Der `LogService` verwendet eine **primäre Strategie mit Fallback**:

1. **Primär:** `EventLogReader` mit WQL-Query (`System.Diagnostics.Eventing.Reader`) – performant, filterbar
2. **Fallback:** Klassische `EventLog`-Klasse – breite Kompatibilität

**WQL-Query-Beispiel:**
```xml
<QueryList>
  <Query Id="0" Path="Application">
    <Select Path="Application">
      *[System[TimeCreated[@SystemTime &gt;= '2026-03-02T00:00:00'] and EventRecordID &gt; 12345]]
    </Select>
  </Query>
</QueryList>
```

#### `LogBatch` (innere Klasse in `LogService`)

| Eigenschaft | Typ | Beschreibung |
|---|---|---|
| `Entries` | `IReadOnlyList<LogEntry>` | Gelesene Log-Einträge |
| `LastRecordIds` | `IReadOnlyDictionary<string, long>` | Cursor: letzte Record-ID pro Log-Quelle für inkrementelles Laden |

---

## 9. Benutzeroberfläche

### Hauptseite: `Home.razor`

Die Hauptseite bündelt beide Kernfunktionen:

**IIS-Bereich:**
- Dropdown zur Auswahl der aktiven IIS-Site
- Schaltflächen: **Start**, **Stopp**, **Neustart**
- Statusanzeige des aktuellen Site-Zustands
- Anzeige: Zeitpunkt des letzten IIS-Neustarts
- Anzeige: Anzahl der seit dem letzten Neustart geladenen Extensions

**Log-Bereich (MudTable):**
- Tabellarische Darstellung aller `LogEntry`-Objekte
- Spalten: Zeitstempel, Level, Meldung (geparst)
- **Suchleiste** (clientseitige Volltextsuche über `SearchText`-Property)
- **Level-Filter** (All, Information, Warning, Error, Critical)
- **Auto-Refresh** in konfigurierbarem Intervall
- Klick auf Zeile → öffnet `LogEntryDialog`

### Dialoge

| Datei | Beschreibung |
|---|---|
| `LogEntryDialog.razor` | Detailansicht eines einzelnen Log-Eintrags: Level, Zeitstempel, Metadaten, vollständige Nachricht (HTML-kodiert, mit Zeilenumbrüchen) |
| `LogDialog.razor` | Live-Log-Viewer mit `LogBuffer`-Integration und automatischem Scroll-to-Bottom (JS Interop) |
| `SelectShopDialog.razor` | Dialog zur Auswahl einer IIS-Site / eines Shops |

### Einstellungsseiten

| Datei | Konfigurierbare Optionen |
|---|---|
| `LogSettings.razor` | Auswahl der Event-Log-Quellen (Multi-Select), Auto-Refresh-Intervall (0–300 s), Log-Bündelung aktivieren/deaktivieren |
| `IisSettings.razor` | Standard-IIS-App-Name, Restart-Delay (Sekunden), Bundler-Cache löschen beim Neustart, Assets löschen beim Neustart |

### LogBuffer (`Toolbox/Components/Dialogs/LogBuffer.cs`)

Thread-sicherer Puffer für Live-Log-Ausgaben:

```csharp
public class LogBuffer
{
    private readonly object _syncRoot = new();
    public event Action? Changed;

    public void Append(string? text)    // Fügt Text an
    public void AppendLine(string? text) // Fügt Text mit Zeilenumbruch an
}
```

---

## 10. Datenmodelle

### `LogEntry` (`Toolbox.Data/Models/Logs/LogEntry.cs`)

Repräsentiert einen einzelnen Eintrag aus der Windows-Ereignisanzeige.

| Property | Typ | Beschreibung |
|---|---|---|
| `Time` | `DateTime` | Zeitstempel des Eintrags |
| `Level` | `LogLevel` | Schweregrad (Information, Warning, Error, Critical) |
| `Message` | `string` | Rohtext der Nachricht |
| `ParsedMessage` | `LogMessage` | Geparste, strukturierte Darstellung |
| `FullMessage` | `string` | Formatierter Anzeigetext |
| `SearchText` | `string` | Kleingeschriebener Suchtext für clientseitige Filterung |
| `Count` | `int` | Anzahl gebündelter identischer Einträge |

### `LogMessage` (`Toolbox.Data/Models/Logs/LogMessage.cs`)

Strukturierte Darstellung des geparsten Log-Inhalts.

| Property | Typ | Beschreibung |
|---|---|---|
| `Origin` | `string` | Herkunft (ServiceId / ShopId) |
| `Metadata` | `string` | Kontextinformationen |
| `Message` | `string` | Eigentliche Nachricht |

```csharp
// Selektive Formatierung je nach gewünschten Komponenten
public string GetFormattedMessage(HashSet<MessageType> messageTypes)
```

### `LogLevel` (`Toolbox.Data/Models/Logs/LogLevel.cs`)

```csharp
public enum LogLevel
{
    All,
    Information,
    Warning,
    Error,
    Critical
}
```

### `MessageType` (`Toolbox.Data/Models/Logs/MessageType.cs`)

```csharp
public enum MessageType
{
    Metadata,
    Message,
    Origin
}
```

---

## 11. Datenpersistenz & Einstellungen

### Datenbank

- **Datenbanktyp:** SQLite
- **Datei:** `toolbox.db` im AppData-Verzeichnis des Benutzers
- **ORM:** Entity Framework Core 9.0
- **DbContext:** `InternalAppDbContext` (`Toolbox.Data/DataContexts/`)

### `ToolboxSettings` (`Toolbox.Data/Models/ToolboxSettings.cs`)

Alle Anwendungseinstellungen werden in einer **Single-Row-Tabelle** persistiert (Id = 1, per EF-Check-Constraint erzwungen).

Relevante Felder für das Abschlussprojekt:

| Property | Typ | Beschreibung |
|---|---|---|
| `LogName` | `string?` | Semikolon-getrennte Liste der Event-Log-Quellen |
| `AutoRefreshSeconds` | `int` | Auto-Refresh-Intervall für Logs (0 = deaktiviert) |
| `BundleLogs` | `bool` | Identische Logs zusammenfassen |
| `IisAppName` | `string?` | Name der Standard-IIS-Anwendung |
| `RestartDelaySeconds` | `int` | Wartezeit zwischen Stop und Start beim Neustart |
| `DeleteBundlerOnShopRestart` | `bool` | Bundler-Cache beim Neustart löschen |
| `DeleteAssetsOnShopRestart` | `bool` | wwwroot-Assets beim Neustart löschen |
| `TrayIconIisSite` | `long` | IIS-Site-ID für das Tray-Icon |
| `ShopSettingsList` | `List<ShopSetting>` | Shop-spezifische Konfigurationen |

### `AppInfo` (`Toolbox.Data/Common/AppInfo.cs`)

Laufzeit-Metadaten der Anwendung (nicht persistiert, nur im Arbeitsspeicher):

| Property | Typ | Beschreibung |
|---|---|---|
| `StartTime` | `DateTime` | Startzeitpunkt der Anwendung |
| `IisRestartTime` | `DateTime` | Zeitpunkt des letzten IIS-Neustarts |

### `SettingsService`

Lädt Einstellungen beim Start aus der Datenbank und cached sie im Arbeitsspeicher. Änderungen werden via `SaveSettingChanges()` sofort in SQLite persistiert.

---

## 12. Testkonzept

### Testphasen (18 Stunden gesamt)

#### Smoke-Tests
- Grundlegender Funktionstest aller implementierten Features nach Abschluss der Implementierung
- Überprüfung: Startet die Anwendung fehlerfrei?
- Überprüfung: Werden IIS-Sites korrekt aufgelistet?
- Überprüfung: Werden Logs aus der Ereignisanzeige geladen?

#### Integrationstests
- Testen des Zusammenspiels von `IisService` und `Microsoft.Web.Administration`
- Testen des Zusammenspiels von `LogService` und Windows Event Log API
- Testen der Settings-Persistenz: Werden Änderungen korrekt gespeichert und geladen?
- Testen des Neustart-Ablaufs inkl. Dateilöschung

#### User-Acceptance-Tests (UAT)
- Abnahme durch das Entwicklungsteam der 4SELLERS GmbH
- Prüfung aller Funktionen gegen die Anforderungen aus dem Projektantrag
- [TODO: Testergebnisse ergänzen]

---

## 13. Soll-Ist-Vergleich

[TODO: Nach Projektabschluss ausfüllen]

| Anforderung | Soll | Ist | Status |
|---|---|---|---|
| Logs aus Windows-Ereignisanzeige auslesen | EventLogReader-Integration | [TODO] | [TODO] |
| Logs tabellarisch darstellen | MudTable mit allen Einträgen | [TODO] | [TODO] |
| Suchfunktion für Logs | Volltextsuche über SearchText | [TODO] | [TODO] |
| Filterfunktion für Logs | Filter nach LogLevel | [TODO] | [TODO] |
| Sortierfunktion für Logs | MudTable-Spalten sortierbar | [TODO] | [TODO] |
| IIS-Site starten | StartIisApp() via ServerManager | [TODO] | [TODO] |
| IIS-Site stoppen | StopIisApp() via ServerManager | [TODO] | [TODO] |
| IIS-Site neustarten | RestartIisApp() mit Delay | [TODO] | [TODO] |
| Bundler-Cache beim Neustart löschen | DeleteShopBundler() | [TODO] | [TODO] |
| Zeitaufwand eingehalten (80h) | 80 Stunden | [TODO] | [TODO] |

---

## 14. Fazit & Reflexion

[TODO: Nach Projektabschluss ausfüllen]

- Was wurde erreicht?
- Welche Herausforderungen gab es?
- Was würde ich beim nächsten Mal anders machen?
- Welchen Mehrwert bringt das Ergebnis dem Entwicklungsteam?

---

## Glossar

| Begriff | Erklärung |
|---|---|
| IIS | Internet Information Services – Webserver von Microsoft für Windows-Betriebssysteme |
| Windows-Ereignisanzeige | Windows Event Viewer – Systemwerkzeug zur Protokollierung von System-, Sicherheits- und Anwendungsereignissen |
| Blazor Desktop | .NET-Framework für Desktop-Anwendungen mit Blazor-UI, gehostet in einer WPF-Anwendung via WebView2 |
| WPF | Windows Presentation Foundation – UI-Framework für Windows-Desktop-Anwendungen |
| WebView2 | Microsoft-Komponente zum Einbetten von Webinhalten (Chromium-basiert) in native Apps |
| MudBlazor | Open-Source Material-Design-Komponentenbibliothek für Blazor |
| EventLogReader | .NET-Klasse (`System.Diagnostics.Eventing.Reader`) für performantes Lesen der Windows-Ereignisanzeige via WQL |
| WQL | Windows Query Language – SQL-ähnliche Abfragesprache für WMI/Event Log |
| App Pool | IIS Application Pool – isolierter Prozess für Webanwendungen im IIS |
| Bundler-Cache | Zwischengespeicherte generierte CSS-Dateien unter `C:\Windows\Temp\shopsystem\bundler` |
| EF Core | Entity Framework Core – ORM (Object-Relational Mapper) für .NET |
| SQLite | Dateibasiertes relationales Datenbanksystem |
| NuGet | Paketmanager für .NET |
| DI / IoC | Dependency Injection / Inversion of Control – Entwurfsmuster für lose Kopplung |
| UAT | User Acceptance Test – Abnahmetest durch den Auftraggeber |
