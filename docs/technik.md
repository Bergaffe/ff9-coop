# FF9 Coop – Technische Notizen

## Laden über Memoria
- Memoria (`ScriptsLoader`) lädt pro Mod-Ordner `StreamingAssets/Scripts/Memoria.Scripts.<ModOrdner>.dll`. Die Mod braucht deshalb keinen Memoria-Fork und keine geänderten Spieldateien.
- Die Steam-Version läuft auf Unity 5.2 mit altem Mono (.NET 3.5). Die Mod wird gegen die DLLs aus `x64/FF9_Data/Managed` gebaut.
- Laufzeit-Patches mit **Harmony 1.2.0.1**. Harmony 2.x stürzt unter diesem Mono beim Patchen ab (Zugriffsverletzung in `mono.dll`).
- Absturzschutz: Vor dem Patchen wird `FF9Coop.patching` angelegt und danach gelöscht. Existiert die Datei beim Start noch, wird nicht gepatcht und das Spiel bleibt spielbar.
- Nach Memoria-Updates (der Launcher aktualisiert selbstständig) sollte die Mod neu gebaut und getestet werden.

## Eingabe
- Der Port liest von sich aus nur Controller 1. Alle Controller-Abfragen laufen aber über `XInputDotNetPure.GamePad.GetState`.
- Die Mod fragt alle vier XInput-Slots getrennt ab und gibt dem Spiel nur den Zustand der Spieler, die gerade dran sind. Die Tastatur wird auf dieselbe Weise freigegeben oder gesperrt.
- Beim Wechsel des aktiven Spielers müssen erst alle Tasten losgelassen werden, damit keine Eingabe „durchrutscht“.

## Kampf
- `BattleHUD`: `ReadyQueue` (Charaktere mit voller ATB), `CurrentPlayerIndex` und `SwitchPlayer` bestimmen, wessen Menü offen ist. Daraus ergibt sich der zuständige Spieler.
- ATB-Pause über einen Postfix auf `IsNativeEnableAtb`.

## Feld
- Die gesteuerte Figur ist `FieldMapActorController` mit `isPlayer`. Kontrolle hat der Spieler, solange `EventEngine.GetUserControl()` gilt.
- Der Begleiter ist kein Event-Objekt, sondern eine eigene Figur aus `ModelFactory` mit Feld-Animationen. Die Höhe kommt aus den Walkmesh-Dreiecken.
- Interaktionen des Begleiters: Der Leader wird für einen Frame an seine Position gesetzt, die originale Kollisions- und Gesprächsprüfung läuft, danach wird zurückgesetzt. Die Spielskripte bleiben unverändert.

## Weltkarte
- Die Begleiter-Figur hängt in `WMWorld.TranslatingObjectsGroup`.
- Bodenhöhe und Begehbarkeit wie bei der Spielfigur. Einsinken in Wald und Wasser über `ff9.w_movementGetSliceHeight`.

## Kompatibilität
- Gameplay-Mods wie AlternateFantasy bringen eigene `Memoria.Scripts`-DLLs mit. Darum ersetzt die Mod keine Methoden vollständig, sondern hängt sich nur davor oder danach ein.
- PlayStation- und Switch-Controller (JoyShockLibrary) mischt Memoria in jeden XInput-Slot. Sie lassen sich daher noch nicht einzeln zuordnen.
