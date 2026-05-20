# Grundprinzipien der Toolbox-Seiten

## 1. Themes-Seite (`/themes`)

### Zweck
Verwaltung von Theme-Repositories und deren symbolischen Links in Shop-Verzeichnissen. Nutzer können verfügbare Themes durchsuchen, Symlinks erstellen/entfernen und das aktive Theme in der `shop.yaml` setzen.

### Datenfluss
1. `ThemeLinkService.GetThemes()` scannt konfigurierte Theme-Roots auf `Themes/`-Unterverzeichnisse
2. Liest `shop.yaml` aus, um das aktuell gesetzte `ThemeOverwrite` zu ermitteln
3. Ergebnisse werden 30 Sekunden im Cache gehalten
4. UI zeigt Themes **gruppiert nach Repository** in einer erweiterbaren Tabelle

### Kernaktionen
| Aktion | Was passiert |
|---|---|
| Symlink erstellen | `CreateLink()` legt Junction im Shop-Themes-Ordner an |
| Symlink entfernen | `RemoveLink()` löscht die Junction |
| "In Use" Checkbox | `SetThemeOverwrite()` schreibt `ThemeOverwrite` in `shop.yaml`, optional IIS-Neustart |
| Repository klonen | `CloneRepositoryAsync()` klont Git-Repo in das Theme-Root |

### Besonderheiten
- Gruppen können oben **angepinnt** werden (persistiert in Settings)
- Suche filtert nach Name oder Theme-Ordner, klappt Gruppen automatisch auf/zu
- Neustart-Verhalten über `RestartShopOnThemeChange`-Setting steuerbar

---

## 2. Extensions-Seite (`/extensions`)

### Zweck
Verwaltung von Extension-Repositories: Projektstruktur erkennen, Builds ausführen, Datenbank-Installation und Versions-Tracking.

### Datenfluss
1. `ExtensionsService.GetExtensions()` scannt konfigurierte Extension-Roots
2. Erkennt Projekttypen anhand von Verzeichnis-Namensmustern (`*.Shop`, `*.Install`, `*.Data`, etc.)
3. Liest `version.json` für Versionsinformation
4. Ergebnisse werden 30 Sekunden gecacht
5. UI zeigt Extensions **gruppiert nach Kundenname** (Präfix vor dem letzten Punkt im Namen)

### Kernaktionen
| Aktion | Was passiert |
|---|---|
| Build | `dotnet build -c Release` auf erstem `.sln`/`.csproj`, Ausgabe streamt live ins LogDialog |
| DB-Installation | `InstallExtension.sql` via SqlCmd gegen die Shop-Datenbank ausführen |
| Version aktualisieren | `version.json` nach SemVer-Schema anpassen (Major/Minor/Build oder Custom) |
| Repository klonen | Klont Extension-Repo in das Extension-Root |

### Besonderheiten
- Build-Ausgabe wird **event-basiert** und non-blocking in die UI gestreamt
- Datenbankoperationen nutzen den Datenbankstring aus `shop.yaml` des gewählten Shops
- Changelog-Anzeige über separaten Dialog
- Gruppen ebenfalls pinnbar

---

## 3. VirtualMachines-Seite (`/virtualmachines`)

### Zweck
vCenter-Integration zur VM-Verwaltung: Statusübersicht, Power-Aktionen, Snapshots, RDP-Verbindungen und Synchronisation von Kunden-Mappings aus TFS/Azure DevOps.

### Datenfluss
1. `VmwareService.ConnectAsync()` authentifiziert sich per Basic Auth gegen die vSphere REST API
2. `GetVmsAsync()` lädt alle VMs; Details (IP, OS, Hostname) werden **parallel nachgeladen** (max. 10 gleichzeitig via Semaphore)
3. VMs werden im Cache gehalten, Kunden-Mappings kommen aus der Datenbank
4. `StagingSystemSyncService.SyncAsync()` scannt TFS-Repos nach `StagingSystem.yml` und extrahiert VM-Metadaten aus der Git-Historie
5. `PipelineTrackingService` pollt alle 3 Sekunden laufende Pipelines und benachrichtigt die UI via Events
6. UI zeigt VMs als **Karten-Grid** mit Filterung und Sortierung (client-seitig)

### Kernaktionen
| Aktion | Was passiert |
|---|---|
| Power On/Off/Suspend/Reset | REST-Call an vSphere API, 2,5 s Verzögerung, dann Reload |
| Shutdown/Reboot Guest | Sanfte OS-Befehle via VMware Tools |
| RDP verbinden | `cmdkey` speichert Credentials, `mstsc` öffnet RDP-Session |
| Snapshot erstellen/zurücksetzen/löschen | vSphere Snapshot API, inkl. Memory/Quiesce-Optionen |
| Mappings synchronisieren | TFS-Repos scannen, VM-Namen aus YAML extrahieren, DB aktualisieren |
| Pipeline starten | Öffnet RunPipelineDialog und trackt laufende Builds |

### Besonderheiten
- **Creator-Ermittlung** via ältesten Git-Commit mit dem VM-Namen in `StagingSystem.yml`
- Kunden-Logo wird anhand der `CustomerId` aus dem Mapping geladen
- Status-Badge für aktive Pipelines in der Toolbar
- VMs können erst gelöscht werden, wenn sie ausgeschaltet sind

---

## Gemeinsame Muster aller drei Seiten

| Muster | Beschreibung |
|---|---|
| **Service-Trennung** | UI-Logik im Razor-Component, Filesystem-/API-Operationen im Service |
| **30s-Cache** | Teure Operationen (Scans, API-Calls) werden kurz gecacht |
| **Shop-Kontext** | Der ausgewählte Shop bestimmt Pfade, Datenbankverbindung und Konfigurationsziel |
| **Gruppierte Tabellen/Karten** | Inhalte sind nach Repo/Kunde/Status gegliedert, Gruppen sind pinnbar |
| **Async Loading** | Ladeindikator + `StateHasChanged()` für manuelle UI-Updates |
| **Dialoge** | Sekundäre Workflows (Klonen, Versionierung, Snapshots) laufen in Modal-Dialogen |
| **Snackbar-Feedback** | Erfolg/Fehler-Rückmeldungen über Toast-Notifications |
