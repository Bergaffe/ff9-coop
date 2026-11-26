# FF9 Coop – Konzept & Architektur

## Grundprinzip
- **Nur der Host führt das Spiel aus.** Mitspieler sitzen lokal am selben PC oder sind über Steam Remote Play Together bzw. Parsec verbunden. Dann erscheinen sie am Host als zusätzliche Controller.
- Die Mod verteilt die Eingaben: Je nach Situation nimmt das Spiel nur die Eingaben des Spielers an, der gerade dran ist. Eine Netzwerk-Synchronisation zweier Spielinstanzen ist dafür nicht nötig.
- **Gemeinsame Kamera**, kein Splitscreen.
- Ausgelegt für 2 Spieler, erweiterbar auf bis zu 4.

## Kampf
- Jeder Charakter hat einen Besitzer (einstellbar, sonst abwechselnd verteilt).
- Das Kommandomenü eines bereiten Charakters reagiert nur auf den Controller seines Besitzers.
- Ein wartender Spieler kann seinen bereiten Charakter per Tastendruck „anfordern“.
- Optional: ATB-Pause während der Befehlswahl, Bereit-Signal, P1/P2-Markierung an den Charakternamen.

## Feld und Weltkarte – Leader + Begleiter
Die Event-Engine des Spiels kennt genau eine gesteuerte Figur. Darum gilt:
- **Leader:** wie im Original. Er löst Ausgänge, Kämpfe, Cutscenes und Kartenwechsel aus.
- **Begleiter:** zweiter Spieler mit eigener, sichtbarer Figur eines Party-Mitglieds.
  - Läuft frei auf dem Walkmesh bzw. auf der Weltkarte und kollidiert mit NPCs.
  - Kann NPCs ansprechen, Truhen öffnen, handeln und Tetra Master spielen.
  - Hängt an einer „Leine“: Ist er zu weit weg oder wechselt die Karte, wird er zum Leader zurückgesetzt.
  - In Cutscenes, Dialogen und Menüs ist er ausgeblendet.
- **Leader-Wechsel** per gehaltener Taste, außerhalb von Events.
- **Solo-Abschnitte** (die Story trennt die Gruppe): Auswahl, wer spielt, oder eine feste Regel.
- Fahrzeuge auf der Weltkarte steuert der Leader.
- Farbige „Ich bin hier“-Hand mit Spielernummer über jeder Figur.

## Menüs und Minispiele
- Hauptmenü, Shops, Synthese: jeder oder nur der Leader (einstellbar).
- Wer ein Gespräch beginnt, bedient auch dessen Auswahl.
- Alle Zuweisungen lassen sich im Koop-Menü im FF9-Stil ändern, das Spiel pausiert solange.

## Mods im Multiplayer
- Das Spiel läuft nur beim Host. Alle Mods und Einstellungen des Hosts gelten automatisch. Gäste brauchen keine Mods.
- Abgleich ist nur beim Wechsel des Hosts nötig, weil Spielstände von Gameplay-Mods abhängen können.
- Die Mod ist ein eigener Memoria-Mod-Ordner und ersetzt keine Spieldateien. Ihre Patches sind additiv (Prefix/Postfix). Dadurch verträgt sie sich mit Moguri Mod und Gameplay-Mods wie AlternateFantasy.

## Mögliche Erweiterungen
- Zuweisungen pro Spielstand speichern
- Koop-Profil zum Teilen von Mod-Liste und Einstellungen beim Host-Wechsel
- Sonderregeln für Minispiele
- PlayStation-/Switch-Controller einzeln zuordnen
- Mehrsprachigkeit (Englisch)
