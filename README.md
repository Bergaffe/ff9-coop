# FF9 Coop

Koop-Multiplayer-Mod für **Final Fantasy IX (Steam)** auf Basis von [Memoria](https://github.com/Albeoris/Memoria). Kompatibel mit Moguri Mod.

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

## Installation (Release)
1. [Memoria](https://github.com/Albeoris/Memoria) bzw. Moguri Mod installieren.
2. `FF9Coop-x.y.z.zip` aus den [Releases](../../releases) in den FF9-Spielordner entpacken (es entsteht der Ordner `FF9Coop`).
3. Im Memoria-Launcher unter Mods „FF9 Coop“ aktivieren, oder in `Memoria.ini` unter `[Mod]` `FF9Coop` bei `FolderNames` vorne eintragen.

## Stand: v0.8.0 (Beta)
- **v0.8.0:** Farbige „Ich bin hier“-Hand mit Spielernummer über jeder Figur (Menü: Spieler-Hand)
- **v0.7.0:**
  - Begleiter kollidiert mit NPCs
  - Begleiter versinkt auf der Weltkarte in Wald und Wasser
  - Solo-Abschnitte als feste Menüoption
  - Kampf: ATB-Pause bei Befehlswahl, Bereit-Signal, P1/P2-Markierung
  - weitere Charaktere als Begleiter wählbar
- **v0.6.0:**
  - Begleiter auf der Weltkarte (nur zu Fuß)
  - „Wer spielt …?“-Fenster in Solo-Abschnitten
  - Vibration geht an den Controller des aktiven Spielers
- **v0.5.0:** Der Begleiter spricht NPCs an, öffnet Truhen, handelt und spielt Tetra Master. Wer ein Gespräch beginnt, bedient es auch.
- **v0.4.0:** Koop-Menü im FF9-Stil (K / L3+R3)
- **v0.3.0:** Sichtbare Begleiter-Figur im Feld
- **v0.2.0:** Leader-Wechsel, „Zug anfordern“ per Tastatur, HUD-Einblendungen
- **v0.1.0:** Getrennte Controller, Kampf-Koop (Kommandomenü nur für den Besitzer des Charakters), „Zug anfordern“, Status-HUD

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

## Läuft die Mod?
- Oben links erscheint `Koop · P1`.
- In `Memoria.log` im Spielordner stehen Zeilen mit `[FF9Coop]`, darunter `Initialisierung abgeschlossen`. Fehler stehen dort ebenfalls.
- Ist nur ein Controller angeschlossen, darf dieser auch die Charaktere der anderen Spieler steuern.

## Fehlerbehebung
- **Das Spiel startet nicht, Steam meldet „Anwendungssteuerungsrichtlinie hat diese Datei blockiert“:** Windows Smart App Control blockiert den nicht signierten Memoria-Launcher. Abhilfe schafft nur, Smart App Control abzuschalten (Windows-Sicherheit → App- & Browsersteuerung).
- **Die Mod ist inaktiv, und im Log steht „Letzter Start ist beim Patchen abgestürzt“:** Lösch `FF9Coop\FF9Coop.patching` im Spielordner und starte neu.
- **Nach einem Memoria-Update:** Neu bauen und installieren. Wir verwenden Harmony 1.2, weil Harmony 2.x unter Unity 5.2 abstürzt.

## Bekannte Einschränkungen
- PlayStation- und Switch-Controller (über JoyShockLibrary) mischt Memoria in jeden XInput-Slot. Sie lassen sich noch nicht einem einzelnen Spieler zuordnen. Xbox-Controller und die virtuellen Pads von Steam Remote Play Together funktionieren.
- Ist die Tastatur gesperrt, gelten auch Memoria-Hotkeys darüber als gesperrt (`GateKeyboard = 0` schaltet die Sperre ab).

## Für Entwickler
[Konzept](docs/design.md) · [Technische Notizen](docs/technik.md)
