## SQL-Abfrage ausführen

1. Öffne **SQL Server Management Studio (SSMS)**.
2. Verbinde dich mit der gewünschten **Shop-Datenbank**.
3. Führe das folgende SQL-Skript aus, um die installierten Extensions abzufragen:

```sql
SELECT ExtensionName FROM ObjectExtensions WHERE ExtensionTypeId = 2; 
```
## Ergebnisse als CSV exportieren
1. Markiere das Abfrageergebnis in SSMS.
2. Klicke mit der rechten Maustaste auf das Ergebnis.
3. Wähle „Ergebnisse speichern unter…“.
   ![SSMS Ergebnis speichern](images/ObjectExtensionCSV.png)
4. Speichere die Datei als CSV-Datei.
5. Kopiere die CSV-Datei anschließend auf den PC, auf dem das Tool heruntergeladen wurde.

