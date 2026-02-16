# agents.md — Slotmaschine (3x3) „Sizzling Hot“-Lookalike UI (Nachbau)

## Ziel
Baue eine **Slotmaschine im Stil** des Screenshots, aber mit **3 Walzen × 3 Reihen (3x3)**. Fokus: **UI/Look**, einfache Animationen, saubere Struktur (Assets austauschbar).

## Rahmenbedingungen
- Grid: **3 Spalten × 3 Reihen**
- Symbole als **Sprites/PNGs** (Platzhalter erlaubt)
- Gewinnlinien: mind. **horizontale 3 Reihen** + optional Diagonalen
- Effekte: **Glow**, **Neon-Gewinnrahmen**, **Coin-Particles** (optional)

---

## UI-Aufbau (Layout-Spezifikation)

### 1) Hintergrund (Canvas/Root)
- Vollflächiger Hintergrund: **dunkelrot → orange** Verlauf mit radialem Glow
- Vordergrund: **Goldmünzen** am unteren Rand (dekorativ)
- Leichte Lens-Flare/Light-rays oben (optional)

### 2) Slot-Gehäuse (Main Frame)
- Form: abgerundetes Rechteck
- Farbe: kräftiges Rot, innen dunkler, außen leichte goldene Kante
- Drop-shadow nach unten (3D-Anmutung)

### 3) Header (oben im Frame)
- Zentrierter Titel: **„Sizzling Hot deluxe“**
  - Gold/Orange Verlauf + Glow
- Links: Home-Icon
- Rechts: Hamburger-Menü Icon

### 4) Reel Area (3x3)
- Hintergrund: sehr dunkles **Violett/Schwarz**
- Trennlinien zwischen Spalten
- Zellen: 3x3 Slots, jeweils zentriert
- **Gewinn-Overlay**: Neon-grüner Rahmen um gewinnende Zellen (pulsierend)

### 5) Rechte Seitenleiste (Controls)
- Großer runder **START** Button (grün, circular arrow)
- Darunter: **TOTAL BET** Kreis mit Zahl (goldene Schrift)

### 6) Footer (unten im Frame)
- Zentrale Statusanzeige: „PLEASE PLACE YOUR BET“
- Buttons:
  - MENU, INFO links
  - MAX BET, AUTO rechts
- Guthabenanzeige unten mittig (z.B. 1.300.000)

---

## Symbol-Set (Assets)
Benötigt (als PNG/SVG, cartoonig, glossy, dicke Kontur):
- 7 (flammend)
- Stern
- Kirschen
- Zitrone
- Orange
- Trauben
- Wassermelone
- Aubergine

**Asset-Regeln**
- Einheitliche Größe (z.B. 256x256 oder 512x512)
- Glanzpunkt oben links
- Transparenter Hintergrund

---

## Game-Logik (Minimal, aber sauber)

### Datenmodelle
- `SymbolType` enum (Seven, Star, Cherry, Lemon, Orange, Grape, Melon, Eggplant)
- `Grid[3][3]` aktuelles Ergebnis
- `Paytable`: Multiplikator pro Linie je Symbol (Konfig-Datei möglich)

### Gewinnlinien (3x3)
Mindestens:
- Row0: (0,0)(1,0)(2,0)
- Row1: (0,1)(1,1)(2,1)
- Row2: (0,2)(1,2)(2,2)

Optional:
- Diagonal1: (0,0)(1,1)(2,2)
- Diagonal2: (0,2)(1,1)(2,0)

### Gewinn-Erkennung
- Linie gewinnt, wenn **alle 3 Symbole gleich**
- Ergebnis:
  - markiere Zellen der Gewinnlinie (neon-grün)
  - spiele Win-Animation (Pulse)
  - optional: Coin-Particles

---

## Animationen & Effekte
### Reel Spin (Fake Spin reicht)
- Beim Start:
  - spiele kurze Spin-Animation (z.B. 800–1200ms)
  - Symbole „scrollen“/blur (oder einfach schnell wechseln)
  - am Ende „snap“ auf Endergebnis

### Win Highlight
- Neon-grüner Rahmen um Gewinnzellen
- Pulsieren (Scale/Opacity)
- optional: Glow verstärken

### Coins (optional)
- Beim Gewinn: 10–30 Münz-Sprites von oben nach unten mit leichter Rotation

---

## Implementierungsaufgaben (Agent-Plan)

### Aufgabe 1 — Projektstruktur
- Lege Ordner an:
  - `/assets/symbols`
  - `/assets/ui`
  - `/src/components`
  - `/src/game`
- Definiere zentrale Konstanten:
  - Farben (Rot, Gold, Violett, Neon-Grün)
  - Grid-Größe (3x3)

### Aufgabe 2 — UI Grundgerüst
- Render Hintergrund + Frame + Header + Footer + rechte Controls
- Platzhalter für Reel Area (3x3 Grid)

### Aufgabe 3 — Reel Grid (3x3)
- Implementiere Zellenlayout (gleichmäßige Kacheln)
- Jede Zelle rendert ein Symbol-Sprite
- Spalten-Trennlinien und dunkles Reel-Panel

### Aufgabe 4 — Spin-Flow
- Start-Button triggert:
  - `isSpinning = true`
  - schnelle Symbolwechsel/Scroll
  - final `Grid` setzen
- Nach Spin:
  - `isSpinning = false`
  - Gewinnlinien prüfen

### Aufgabe 5 — Win Detection & Overlay
- Prüfe definierte Linien
- Wenn Gewinn:
  - setze `winningCells` (Koordinaten)
  - aktiviere pulsing Neon-Frame pro Zelle
  - update Statusanzeige („YOU WIN …“)

### Aufgabe 6 — Polish
- Glow/Shadow konsistent
- Hover-States für Buttons
- Optional: Coin-Particles bei Gewinn

---

## Akzeptanzkriterien
- UI sieht **klar nach Casino-Slot** aus (rot/gold/violett, Glow)
- Grid ist **3x3**, sauber skaliert
- Start-Button löst Spin aus, Ergebnis erscheint
- Gewinnlinie wird erkannt und **neon-grün** markiert
- Footer zeigt Status + Bet/Guthaben

---

## Konfiguration (Empfohlen)
- `config.json` (oder vergleichbar) für:
  - Paytable
  - Bet-Stufen
  - Aktivierte Linien (Rows only vs Rows+Diagonals)

---

## Hinweise für Design-Treue
- Sättigung hoch, Kontraste stark
- Titel + Symbole haben sichtbaren Glow
- Reel-Hintergrund sehr dunkel, damit Symbole „poppen“
- Neon-Grün nur für Gewinne, sonst sparsam
