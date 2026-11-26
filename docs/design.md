# FF9 Coop – Design-Entscheidungen

Stand: 2026-11-26 · Grundlage: [phase0-spike.md](phase0-spike.md)

## Festgelegt
| Thema | Entscheidung |
|---|---|
| Spielweise | Lokal (mehrere Controller) **und** online (Remote Play Together / Parsec → Remote-Spieler = zusätzliche Pads am Host) |
| Spieleranzahl | 2 als Ziel, Architektur offen für bis zu 4 |
| Kamera | Gemeinsame Kamera, **kein Splitscreen** |
| Feld | Leader + Leine: Spieler 1 = Leader, Spieler 2 steuert sichtbare Begleiter-Figur |
| Weltkarte | Vorerst nur Leader steuert (Übergabe per Taste); Begleiter auf der Weltkarte als spätere Phase |

## Kampf
- **Besitz:** Jeder Party-Slot (0–3) hat einen Besitzer (Spieler-ID). Standard: abwechselnd verteilt (Slot 0/2 → P1, Slot 1/3 → P2).
- **Menü-Routing:** Wird ein Charakter aus der `ReadyQueue` aktiv (`BattleHUD.SwitchPlayer`), nimmt das Kommandomenü nur Eingaben vom Pad seines Besitzers an.
- **Übernahme-Regel (optional, Config):** Ist ein Charakter bereit und sein Besitzer hängt gerade in einem anderen Menü, darf der andere Spieler ihn übernehmen.
- **Ziel-Auswahl:** Gehört zum Besitzer des Menüs.
- **Solo-/Gast-Kämpfe:** Zuweisung nach Regel. Ist nur ein Charakter spielbar, erscheint kurz ein Auswahl-Popup „Wer spielt?“ (Default: letzter Besitzer).
- **ATB:** Bei Online-Spiel wird empfohlen, „Wait“ zu erzwingen (Config-Option).

## Feld – Leader + Leine
**Rahmenbedingung:** Die Event-Engine kennt genau eine gesteuerte Figur (`EventEngine._context.controlUID`). Alle Auslöser hängen daran.

- **Leader (P1):** wie Vanilla. Er löst Ausgänge, Kämpfe, Cutscenes und Kartenwechsel aus.
- **Begleiter (P2):**
  - Zusätzliches Actor-Modell eines Party-Mitglieds (wählbar, Standard: nächster Party-Slot nach dem Leader).
  - Läuft auf dem Walkmesh der aktuellen Karte, hat keine eigenen Story-Trigger.
  - **Leine:** Ist der Begleiter über die maximale Distanz hinaus entfernt, nicht mehr im Bild oder wechselt die Karte, wird er zum Leader teleportiert bzw. neu gespawnt.
  - **Interaktion:** P2 drückt „Bestätigen“ an NPC/Truhe → `controlUID` geht für diese eine Interaktion auf den Begleiter (bzw. Leader wird kurz stellvertretend positioniert) und danach zurück. Die Original-Skripte bleiben unverändert.
- **Sichtbarkeit:**
  - Der Begleiter ist nur sichtbar, solange `EventEngine.GetUserControl()` true ist.
  - In Cutscenes, Dialogen und Menüs wird er ausgeblendet.
  - Er wird auch ausgeblendet, wenn derselbe Charakter in der Szene bereits als NPC/Event-Objekt existiert.
- **Solo-Szenen** (Party-Wechsel per Skript, z. B. Vivi allein in Alexandria): Popup „Wer spielt?“. Der Gewählte wird Leader, der andere hat keinen Begleiter (oder Zuschauer-Modus).
- **Leader-Wechsel:** P1 kann die Leader-Rolle per Tastenkombination übergeben (nur außerhalb von Events).

## Weltkarte (vorerst)
- Ein Spieler steuert (Figur, Chocobo, Luftschiff). Übergabe per Tastenkombination.
- Später (eigene Phase): Begleiter mit Leine. Vorher zu prüfen: Laden Feldmodelle in `WMWorld`? Wie funktioniert das Block-Streaming um den Mittelpunkt? Wie verhält sich ein dynamischer Kamera-Zoom?

## Menüs, Dialoge, Minispiele
- **Hauptmenü, Shops, Synthese:** Standard „jeder darf“. Option „nur Leader“.
- **Dialogauswahl:** Leader. Option „jeder darf“.
- **Minispiele** (Seilspringen, Chocobo-Hot&Cold, Tetra Master, Racing Hippaul …): Leader bzw. per Popup. Tetra Master PvP als spätere Idee.

## Konfiguration & Persistenz
- `FF9Coop.ini` im Mod-Ordner: Spielerzahl, Pad-Zuordnung, Übernahme-Regel, Leinen-Distanz, Wait erzwingen, Menü-Rechte.
- **Besitz-Zuordnung je Spielstand** in einer Nebendatei (`FF9Coop_SlotX.json`). Das Original-Savegame bleibt unverändert.

## Phasen (überarbeitet)
| Phase | Inhalt |
|---|---|
| 1 | Mod-Gerüst (Memoria-Mod-DLL + Harmony), Multi-Pad-Input-Layer, Kampf-Besitz & Menü-Routing |
| 2 | Feld: Leader-Wechsel, Solo-Szenen-Popup, Menü-/Dialog-Rechte |
| 3 | Feld: Begleiter-Figur (Spawn, Bewegung, Leine, Ausblenden, Interaktion) |
| 4 | Online-Tests via Remote Play Together/Parsec, Latenz, Wait-Option |
| 5 | Weltkarte: Steuerungsübergabe → später Begleiter mit Leine |
| 6 | Polish: Rollen-Overlay, Persistenz, Installer, Minispiel-Regeln, Coop-Profil (Export/Import von Mod-Liste + Settings für Host-Wechsel, mit Abgleich/Warnung) |

## Mods im Multiplayer
- Das Spiel läuft nur beim Host. Alle Mods und Einstellungen des Hosts (`Memoria.ini`) gelten automatisch. Gäste brauchen keine Mods.
- Ein Abgleich ist nur beim **Host-Wechsel** nötig, weil Spielstände von Gameplay-Mods abhängen können. Dafür gibt es das Coop-Profil aus Phase 6.
- Unsere DLL heißt `Memoria.Scripts.FF9Coop.dll` und wird von Memoria pro Mod-Ordner geladen (wie `Memoria.Scripts.AlternateFantasy.dll`). Sie ist deshalb von `MergeScripts = 0` nicht betroffen.

## Hauptrisiken
- **Begleiter vs. Event-Skripte:** Doppelte Figuren, Kollisionen mit Skript-Bewegungen. Gegenmaßnahme: aggressives Ausblenden und eine Pflege-Liste pro Feld-ID.
- **AlternateFantasy:** Patches nur additiv (Prefix/Postfix).
