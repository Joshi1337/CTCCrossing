# CTCCrossing – Deutsche Anleitung

[Aktuelles Release herunterladen](https://github.com/Joshi1337/CTCCrossing/releases/latest)

Die Mod ergänzt das originale 3D-CTC-Pult in Bryson um Crossing-Anzeigen und AUTO/CLOSE-Schalter für Bryson, Governors Island, Sylva und Andrews.

## Installation

1. Railroader vollständig beenden.
2. **CTCCrossing-<Version>.zip** aus dem Release herunterladen, nicht das automatische Quellcode-Archiv.
3. Den enthaltenen Ordner nach `%USERPROFILE%\Documents\Railroader\Mods` entpacken.
4. Im Spiel **CTCCrossing** unter Manage Mods aktivieren und Apply drücken.

Bei einem Update der bisherigen Prototypen die Dateien im bestehenden Ordner `BrysonCrossingLamp` ersetzen und die eigene settings.json behalten. Keine zweite Kopie daneben installieren. Die interne Kennung und der DLL-Dateiname bleiben aus Kompatibilitätsgründen gleich.

Voraussetzung ist Railroader Experimental mit erlaubten DLL-Mods. Gebaut gegen 0.6.2; andere Versionen sind nicht garantiert. Harmony ist enthalten.

## Bedienung

- **AUTO:** normale Zugerkennung.
- **CLOSE:** per Linksklick Schranken schließen und geschlossen halten.
- Zurück auf AUTO öffnet nicht zwangsweise: Bei einem Zug im Erkennungsbereich bleiben die Schranken geschlossen.
- **Rot blinkend:** Schranken schließen.
- **Dauerrot:** Endstellung beobachtet oder bei entfernten Übergängen aus der Laufzeit berechnet.
- **Aus:** offen/öffnend oder keine gültige Rückmeldung.
- **F8:** Diagnose für jeden Übergang.

Die Fernstellung ist eine Berechnung aus Spielbefehl, Cliplänge und Animationsgeschwindigkeit, kein unabhängiger Endlagensensor. F8 kennzeichnet diese Rückmeldung. Eine dunkle Lampe garantiert nicht, dass der Übergang offen ist.

Ab Version 0.4.0 können auch Mitspieler (ab Passenger-Berechtigung) AUTO/CLOSE bedienen. **Host und alle Mitspieler, die das Pult nutzen, müssen die neue Mod installiert und aktiviert haben**, möglichst dieselbe Version. Die Übergangszuordnung in settings.json muss übereinstimmen.

Die Schalterstellungen werden über die normale Spiel-Synchronisierung übertragen. Später beitretende Spieler übernehmen die aktuelle Stellung; beim erneuten Laden eines Spielstands durch den Host starten alle Schalter auf AUTO. Netzwerklatenz kann Änderungen verzögern. Spieler ohne Mod erhalten keine zusätzlichen Pultschalter. Die Mod während einer Multiplayer-Sitzung nicht einzeln entladen.

Die Zuordnung gilt für die vier Übergänge der Basiskarte. Automatisierte Tests prüfen die Steuerung mit einer simulierten Transportschnittstelle. Ein echter Steam-Test mit zwei Spielern steht noch aus: CLOSE/AUTO vom Mitspieler bedienen, Stellung beim Host prüfen und einen weiteren Spieler bei aktivem CLOSE beitreten lassen.

## Updates veröffentlichen

Im lokalen Clone auf `main` Änderungen bearbeiten und anschließend ausführen:

```powershell
pwsh ./scripts/Publish-Update.ps1 -Message "Beschreibung des Updates"
```

Das Skript erhöht die Patch-Version, testet, baut mit den lokal installierten Spielbibliotheken, erstellt den passenden Build-Nachweis, committet und pusht. Danach erstellt GitHub automatisch ein Release mit ZIP und Prüfsumme.

Für eine gezielte Version kann `-Version 0.4.0` ergänzt werden. Bei anderem Installationspfad `-GameManaged "D:\SteamLibrary\steamapps\common\Railroader\Railroader_Data\Managed"` angeben.

Ohne Commit/Push vorbereiten: `pwsh ./scripts/Prepare-Release.ps1 -Version 0.3.2`, danach wie gewohnt committen und pushen. Ohne neu gebaute passende DLL wird ein Code-Update nicht veröffentlicht. Bereits veröffentlichte Versionen bleiben unverändert. Die Spielbibliotheken werden niemals ins öffentliche Repository geladen.

Details zu Entwicklung, Konfiguration und Einschränkungen stehen im [englischen README](../README.md).

