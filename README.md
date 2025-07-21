### Todos: 
- auslesen aller Themes im Repo ordner
- Abändern des Themes im shop.yaml
- ändern der db connection in der shop.yaml
- klonen eines Themes in den repo ordner
- erstellen eines symlinks für das Theme
- löschen von symlinks von Themes
- Log filtering
- Favicon ändern
- Responsive

## ThemeLinker CLI

Das Kommandozeilentool `ThemeLinker` durchsucht einen Repo-Ordner rekursiv nach Themes und kann im Shop-Themes-Ordner passende Symlinks anlegen oder entfernen.

**Aufruf:**
```bash
# mit Standardpfaden
dotnet run --project ThemeLinker/ThemeLinker.csproj

# mit eigenen Pfaden und automatischem Anlegen fehlender Links
dotnet run --project ThemeLinker/ThemeLinker.csproj -- --repo /Pfad/zum/repo-ordner --shop /Pfad/zum/shop-themes-ordner --auto
```

Ohne `--auto` fragt das Tool für jedes Theme, ob ein Link angelegt oder entfernt werden soll.
