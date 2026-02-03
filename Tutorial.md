# Agents.md – Interaktives Tutorial als modernes Overlay

## Ziel
Das bereits bestehende Tutorial soll zu einem **modernen, interaktiven Overlay** umgebaut werden.  
Dieses Overlay dient als **geführte Einführung für neue Nutzer** und erklärt alle relevanten Funktionen der Anwendung Schritt für Schritt.

Zusätzlich soll das Tutorial durch **kleine Aufgaben / Interaktionen** sicherstellen, dass der Nutzer die Inhalte verstanden hat.

---

## Anforderungen

### 1. Tutorial als Overlay
- Das Tutorial wird als **Overlay über der bestehenden UI** angezeigt
- Der Nutzer kann die Anwendung weiterhin sehen, relevante UI-Elemente werden hervorgehoben
- Fokus liegt immer auf dem aktuell erklärten Bereich (Spotlight / Highlight)

---

### 2. Schritt-für-Schritt-Erklärung
- Das Tutorial ist in **klar definierte Schritte (Steps)** unterteilt
- Jeder Schritt erklärt:
  - Was der Bereich / die Funktion ist
  - Wofür sie verwendet wird
  - Was der Nutzer dort typischerweise macht
- Kurze, verständliche Texte (kein Fließtext-Wall)

---

### 3. Interaktive Aufgaben
- Bestimmte Schritte enthalten **kleine Aufgaben**, z. B.:
  - Klicke auf einen bestimmten Button
  - Öffne eine Seite oder einen Dialog
  - Wähle eine Option aus
- Der nächste Schritt wird **erst freigeschaltet**, wenn die Aufgabe korrekt ausgeführt wurde
- Ziel: Wissen aktiv abfragen, nicht nur passiv erklären

---

### 4. Fortschritt & Status
- Der Fortschritt des Tutorials wird gespeichert
- Beim erneuten Start der Anwendung:
  - Wird das Tutorial **nicht erneut automatisch gestartet**, wenn es bereits abgeschlossen wurde
- Optional:
  - Möglichkeit, das Tutorial später manuell erneut zu starten (z. B. über Einstellungen / Hilfe)

---

### 5. Tutorial überspringen
- Es gibt von Beginn an eine **Option zum Überspringen**
- Beim Überspringen:
  - Wird das Tutorial als „abgeschlossen“ markiert
  - Der Nutzer wird nicht erneut automatisch damit konfrontiert
- Optional:
  - Kurzer Hinweis, dass das Tutorial jederzeit nachholbar ist

---

### 6. Technische Leitlinien
- Tutorial-Logik ist **vom Fachcode getrennt**
- Schritte sind **konfigurierbar / erweiterbar**
- Inhalte (Texte, Aufgaben, Reihenfolge) sollen leicht anpassbar sein
- Keine harte Kopplung an konkrete UI-Implementierungen

---

## Nicht-Ziele
- Kein statisches PDF oder externer Guide
- Kein reines Tooltip-System ohne Nutzerinteraktion
- Keine Zwangsnutzung ohne Skip-Möglichkeit

---

## Ergebnis
Ein modernes, benutzerfreundliches Onboarding, das:
- Neue Nutzer sicher durch die Anwendung führt
- Verständnis aktiv überprüft
- Nicht aufdringlich ist
- Und jederzeit übersprungen oder erneut gestartet werden kann
