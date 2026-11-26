# FF9 Coop

Koop-Multiplayer-Mod für **Final Fantasy IX (Steam)** auf Basis von [Memoria](https://github.com/Albeoris/Memoria). Kompatibel mit Moguri Mod.

- Planung: [docs/design.md](docs/design.md)
- Technische Analyse: [docs/phase0-spike.md](docs/phase0-spike.md)

## Steuerung
| Aktion | Controller | Tastatur |
|---|---|---|
| Zug anfordern (Kampf, eigener Charakter ist bereit) | **Y** / Dreieck | **T** |
| Leader übernehmen (Feld/Weltkarte ohne Cutscene, nur der Nicht-Leader) | **Back/Select 1 s halten** | **L 1 s halten** |
| Begleiter: NPC ansprechen, Truhe/Schild/Auslöser benutzen, Shop | **A** | Bestätigen-Taste des Spiels |
| Begleiter: Tetra Master herausfordern | **X** (Quadrat) | Spezial-Taste des Spiels |
| Koop-Menü öffnen/schließen (alle Zuweisungen; das Spiel pausiert) | **L3 + R3** (beide Sticks drücken) | **K** |

Im Koop-Menü stellst du ein:
- Spieleranzahl, Leader und wer Menüs bedienen darf
- welcher Spieler die Tastatur und welcher jeden Controller benutzt. Ein ● zeigt, welcher Controller gerade gedrückt wird.
- welcher Spieler im Kampf welchen Charakter steuert
- den Begleiter in Städten: an/aus und welcher Charakter

Änderungen wirken sofort. „Speichern und schließen“ schreibt sie in die `FF9Coop.ini`, „Abbrechen“ (B/Esc) verwirft sie.

Tasten und Haltedauer lassen sich in `FF9Coop.ini` ändern (`TurnRequestKey`, `LeaderKey`, `LeaderHoldSeconds`).

## Stand: v0.6.0
- **v0.6.0:**
  - Begleiter auf der Weltkarte (nur zu Fuß)
  - „Wer spielt …?“-Fenster in Solo-Abschnitten
  - Vibration geht an den Controller des aktiven Spielers
- **v0.5.0:** Der Begleiter spricht NPCs an, öffnet Truhen, handelt und spielt Tetra Master. Wer ein Gespräch beginnt, bedient es auch.
- **v0.4.0:** Koop-Menü im FF9-Stil (K / L3+R3)
- **v0.3.0:** Sichtbare Begleiter-Figur im Feld
- **v0.2.0:** Leader-Wechsel, „Zug anfordern“ per Tastatur, HUD-Einblendungen

### Phase 1 (v0.1.0)
- Mehrere XInput-Controller werden getrennt ausgewertet. Das Spiel sieht immer nur die Eingaben der Spieler, die gerade dran sind.
- **Kampf:** Das Kommandomenü eines Charakters reagiert nur auf den Controller seines Besitzers (`[Owners]` in `FF9Coop.ini`).
- **Zug anfordern:** Ist ein eigener Charakter bereit, holt der wartende Spieler ihn mit **Y/Dreieck**. Das geht nur, wenn das Hauptkommando-Menü offen ist.
- **Feld, Weltkarte, Tetra Master:** Es steuert der Leader.
- **Menüs:** Es darf jeder. Einstellbar mit `MenuControl = Leader`.
- Ein HUD oben links zeigt, wer gerade dran ist.

## Bauen & installieren
Voraussetzungen: .NET SDK (getestet mit 9.0), FF9 Steam mit Memoria (≥ 2025-07-13).

```powershell
dotnet build -c Release src\FF9Coop
powershell -ExecutionPolicy Bypass -File tools\install.ps1          # installieren / aktualisieren
powershell -ExecutionPolicy Bypass -File tools\install.ps1 -Uninstall
```

Liegt das Spiel woanders, gibst du beim Build `-p:GameDir="…"` und beim Install-Skript `-GameDir "…"` mit.

Das Install-Skript macht drei Dinge:
- Es kopiert `mod\FF9Coop` ins Spielverzeichnis.
- Es trägt `FF9Coop` in `Memoria.ini` unter `FolderNames`/`Priorities` ein.
- Beim ersten Lauf legt es ein Backup als `Memoria.ini.ff9coop.bak` an.

Eine vorhandene `FF9Coop.ini` im Spiel wird bei Updates nicht überschrieben.

## Testen (Phase 1)
1. Spiel starten und bis zum Titelbildschirm warten.
2. In `Memoria.log` im Spielordner müssen diese Zeilen stehen:
   ```
   [FF9Coop] Version 0.1.0 geladen aus …
   [FF9Coop] Patch: GamePad.GetState
   …
   [FF9Coop] Initialisierung abgeschlossen
   [FF9Coop] Laufzeit-Objekt erstellt
   ```
   Oben links erscheint `Koop · P1`.
3. Mit **2 Controllern** einen Kampf starten (Prima Vista, erster Kampf).
   - Die Startparty ist Zidane, Blank, Marcus und Cinna. Ohne Eintrag in `[Owners]` werden sie nach Slot verteilt: Slot 0/2 → P1, Slot 1/3 → P2.
   - Erwartung: Öffnet sich das Menü für einen P2-Charakter, reagiert nur Pad 2. Das HUD zeigt dann `P2 · Kampf: Blank`.
   - Ist ein P2-Charakter bereit, während P1 am Zug ist: Pad 2 drückt Y, und das Menü wechselt.
4. **Mit nur einem Controller** greift die Ausfallsicherung. Pad 1 darf dann auch die Charaktere von P2 steuern.

Fehler stehen ebenfalls mit `[FF9Coop]` in `Memoria.log`.

## Fehlerbehebung
- **Das Spiel startet nicht, Steam meldet „Anwendungssteuerungsrichtlinie hat diese Datei blockiert“:** Windows Smart App Control blockiert den nicht signierten Memoria-Launcher. Abhilfe schafft nur, Smart App Control abzuschalten (Windows-Sicherheit → App- & Browsersteuerung).
- **Die Mod ist inaktiv, und im Log steht „Letzter Start ist beim Patchen abgestürzt“:** Lösch `FF9Coop\FF9Coop.patching` im Spielordner und starte neu.
- **Nach einem Memoria-Update:** Neu bauen und installieren. Wir verwenden Harmony 1.2, weil Harmony 2.x unter Unity 5.2 abstürzt.

## Bekannte Einschränkungen
- PlayStation- und Switch-Controller (über JoyShockLibrary) mischt Memoria in jeden XInput-Slot. Sie lassen sich noch nicht einem einzelnen Spieler zuordnen. Xbox-Controller und die virtuellen Pads von Steam Remote Play Together funktionieren.
- Vibration geht immer an Pad 1.
- Ist die Tastatur gesperrt, gelten auch Memoria-Hotkeys darüber als gesperrt (`GateKeyboard = 0` schaltet die Sperre ab).
