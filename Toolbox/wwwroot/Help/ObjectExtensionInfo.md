## Grundkonzept

Falls man prüfen will, welche Object-Extensions auf einem Kundensystem durch Extensions installiert wurden, z.B. für Updates / Migrationen kann man durch diesen Dialog die Manuelle Arbeit automatisieren.
## So funktioniert der Check

1. #### **Quelle wählen:**
    Da die Toolbox keinen Zugriff auf das Kundensystem hat, muss die Object-Extension Tabelle als CSV exportiert und in die Toolbox kopiert werden.


2. #### **TFS-Projekt angeben:**
   Dannach muss das Kundenprojekt angegeben werden, damit auf alle Kundenextensions zugegriffen werden kann.

3. #### **Abgleich:**
   Die Toolbox liest die Install-SQLs der Extensions aus dem TFS und gleicht sie mit den Object Extensions der Quelle ab.

4. #### **Ergebnis:**
   Du erhältst eine Tabelle: **Extension-Repository** und **Object Extension** (nur die, die durch eine Extension installiert wurden).
