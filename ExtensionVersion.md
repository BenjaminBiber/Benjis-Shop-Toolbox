# ExtensionVersion Service

Diese Datei erklaert den Service `IExtensionVersionService` in der Toolbox und wie er funktioniert.

## Zweck
Der Service uebernimmt das Versionieren einer Shop-Extension in einem Extension-Root-Ordner. Dabei werden:
- `InstallExtension.sql` aktualisiert (Version im UPDATE-Statement),
- alle `.csproj` bzw. `AssemblyInfo.cs` Versionen gesetzt,
- `version.json` geschrieben,
- optional `changelog.json` erweitert.

## Registrierung (DI)
Der Service ist bereits registriert:
`Toolbox\Program.cs`
- `builder.Services.AddScoped<IExtensionVersionService, ExtensionVersionService>();`

## API
Interface:
- `SetVersion(string extensionRoot, Version version, string? changelogMessage = null)`
- `UpgradeVersion(string extensionRoot, ExtensionSemanticVersion semanticVersion, string? changelogMessage = null)`
- `ReadVersion(string extensionRoot)`

Rueckgabe (bei Set/Upgrade):
- `ExtensionVersionResult` mit
  - `Success` (bool)
  - `PreviousVersion` / `NewVersion`
  - `Messages`, `Warnings`, `Errors`

## Semantische Versionen
Mapping der `ExtensionSemanticVersion`:
- `Major` / `Shop` => `MAJOR+1.0.0`
- `Minor` / `Feature` => `MAJOR.(MINOR+1).0`
- `Build` / `Bug` => `MAJOR.MINOR.(BUILD+1)`

## Verhalten im Detail
1) Root validieren
   - Pfad wird normalisiert, muss existieren.
2) Aktuelle Version lesen
   - Aus `version.json`. Falls fehlt/ungueltig: Warnung bzw. Fehler.
3) Dateien aktualisieren
   - `InstallExtension.sql` (falls vorhanden im `*.Install`-Projekt).
   - Alle `.csproj`:
     - Neue .NET Projekte: `AssemblyVersion`, `FileVersion`, `PackageVersion`
     - Alte .NET Projekte: `AssemblyInfo.cs` wird angepasst
   - `version.json` wird geschrieben/ueberschrieben.
4) `changelog.json`
   - Nur wenn Datei existiert und eine `changelogMessage` uebergeben wurde.
   - Wenn Version bereits existiert, wird der Eintrag ueberschrieben (mit Warnung).

## Beispiel (Code)
```csharp
@inject IExtensionVersionService ExtensionVersionService

var result = ExtensionVersionService.UpgradeVersion(
    extensionRoot: @"C:\Repos\MyCustomer.MyExtension",
    semanticVersion: ExtensionSemanticVersion.Minor,
    changelogMessage: "Neue Feature-X-Integration"
);

if (!result.Success)
{
    // result.Errors / result.Warnings ausgeben
}
```

## Typische Stolperfallen
- `version.json` fehlt: Upgrade ist nicht moeglich (SetVersion funktioniert).
- Kein `*.Install` Projekt oder keine `InstallExtension.sql`: dann wird nur gewarnt.
- `changelog.json` ohne `changelogMessage`: wird uebersprungen.

