## Begriffserklärung

- **Komponenten**  
  Blazor-Komponenten, die innerhalb von Blazor-Seiten oder anderen Komponenten verwendet werden.

- **Seiten**  
  Blazor-Seiten, die über das `@page`-Attribut verfügen und direkt routbar sind.

---

## Vorgehen

1. **Auslagerung der Styles**
   - Sämtliche Styles von Komponenten und Seiten werden in **SCSS-Dateien** ausgelagert.
   - **Komponenten-Styles** liegen als SCSS-Dateien im `wwwroot`-Ordner und werden zentral in eine `app.scss` importiert.
   - **Seiten-Styles** werden in **Scoped-SCSS-Dateien** ausgelagert (z. B. `Page.razor.scss`).

2. **Build-Verarbeitung**
   - Alle SCSS-Dateien werden beim Build in CSS-Dateien kompiliert.
   - Scoped-SCSS-Dateien werden in entsprechende **Scoped-CSS-Dateien** umgewandelt.
   - Die SCSS-Dateien der Komponenten werden zu einer gemeinsamen `app.css` im `wwwroot`-Ordner gebündelt.

3. **Zentrale Variablen**
   - Es wird eine `vars.scss` angelegt.
   - In dieser Datei werden alle wiederverwendeten Werte (z. B. Farben, Abstände, Margins, Paddings etc.) als Variablen definiert, sofern sie mehr als einmal verwendet werden.

4. **Automatisierung**
   - Die Umwandlung von SCSS zu CSS erfolgt **bei jedem Build automatisch**.
