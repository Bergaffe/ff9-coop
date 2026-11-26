# Phase 0 – Technischer Spike (FF9 Koop-Mod)

Stand: 2026-11-26

## Umgebung
| Komponente | Befund |
|---|---|
| Spiel | `C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY IX` |
| Memoria | installiert, Version **2025-07-13** (passender Quell-Commit: `78f8d849`) |
| Aktive Mods (Log) | AlternateFantasy, 4K Video Pack – Ultimate Remaster, MoguriVideo, MoguriMain |
| Runtime | Unity-Mono, **.NET 3.5** (Assembly-CSharp & Memoria.Scripts: `v3.5`) |
| Tools | git, dotnet SDK, Visual Studio 2022 Community (`E:\Microsoft Visual Studio\2022\Community`) |
| Quellcode | `external/Memoria` (aktueller master `c716e1fa`, nur als Referenz) |

## Wichtigste Erkenntnisse

### 1. Eigene Mod-DLL ohne Memoria-Fork möglich ✅
`Assembly-CSharp/Memoria/Scripts/ScriptsLoader.cs` (`Initialize`) lädt **pro Mod-Ordner**
`StreamingAssets/Scripts/Memoria.Scripts.<ModOrdner>.dll`.
→ Unsere Mod wird ein normaler Memoria-Mod-Ordner (z. B. `FF9Coop/`) neben MoguriMain.
→ Moguri-Kompatibilität ist strukturell gegeben (keine gemeinsamen Dateien).
→ Von dort aus kann **HarmonyX/Lib.Harmony (net35-Build)** geladen werden, um Methoden zur Laufzeit zu patchen.

### 2. Steam-Port liest nur Controller 1 ❌ → selbst bauen
- `Global/Hono/HonoInputManager.cs:558` und `:586` → `GamePad.GetState(PlayerIndex.One)`
- `UnityXInput/XInputManager.cs` erkennt aber alle 4 XInput-Slots (`GetState(candidate)`).
→ Eigener Input-Layer: XInput-Pads 1–4 getrennt pollen, je nach „Besitzer“ des aktuellen Kontexts in den bestehenden Input einspeisen (Patch auf `HonoInputManager`).

### 3. PS1-Zwei-Pad-Reste nur rudimentär ⚠️
- `Global/Event/EventInput.cs:376-391` – `PSXCntlPadMask[0..1]`, Event-Opcode in `EventEngine.DoEventCode.cs:2424` („pad number 0 or 1“).
- Das sind nur Tastenmasken für Feldskripte; die eigentliche Multi-Controller-Kampflogik des PS1 ist im Port **nicht** vorhanden.

### 4. Hook-Punkte Kampf ✅
`Global/Battle/BattleHUD/`:
- `BattleHUD.Public.cs:27` – `ReadyQueue` (Charaktere mit voller ATB)
- `BattleHUD.cs:1282` – `SwitchPlayer(playerId)` setzt `CurrentPlayerIndex`, öffnet Menü
- `BattleHUD.cs:464` – `DisplayCommand()`, `:1838` – `SetCommandVisibility()`
→ Plan: Patch auf `SwitchPlayer`/Input-Abfrage im BattleHUD – Eingaben nur vom Pad des Besitzers von `CurrentPlayerIndex` akzeptieren.

### 5. Hook-Punkte Feld ✅
`Global/Field/Map/Actor/FieldMapActorController.cs`:
- `isPlayer`-Flag (Zeilen 28, 164–210) markiert die gesteuerte Figur
- Bewegung über `HonoInputManager.GetAxis()` (:601), Laufen über `UIManager.Input.GetKey(Control.Cancel)` (:198)
- `EventEngine.GetUserControl()` (:210) = Spieler hat Kontrolle → Trigger für „aktiver Fahrer“
→ Solo-Szenen: Wechsel der `isPlayer`-Figur / Party-Änderung abfangen → Auswahl-Overlay.

### 6. Bestehende Memoria-Erweiterungspunkte
`ScriptsLoader.GetOverloadedMethod(...)`-Interfaces, z. B. `IOverloadOnBattleInitScript` (`Global/battle/battle.cs:546`), `IOverloadOnCommandRunScript` (`Global/btl_cmd.cs:521`) – für Kampfstart-Logging/-Setup ohne Harmony nutzbar.

## Erkenntnisse aus dem ersten Spieltest (2026-11-26)
- **Harmony 2.x (2.3.3) bringt das Spiel zum Absturz:** Zugriffsverletzung in `mono.dll+0xe1f95` beim Patchen. Unity 5.2.3 hat ein zu altes Mono für MonoMod.Core.
- **Harmony 1.2.0.1 funktioniert:** Alle Patches gehen durch, das Spiel läuft stabil, das HUD ist sichtbar.
- Der Memoria-Launcher hat Memoria selbst auf **2026-11-09** aktualisiert, und Assembly-CSharp wurde dabei ersetzt. Nach Memoria-Updates muss die Mod deshalb neu gebaut und getestet werden.
- **Windows Smart App Control** blockiert den nicht signierten `FF9_Launcher.exe`, und auch `FF9.exe` und die Memoria-Plugins sind nicht signiert. Unter Smart App Control lässt sich die Mod deshalb nicht nutzen. Das gehört in die Installationshinweise.
- Absturzschutz: `FF9Coop/FF9Coop.patching` wird vor dem Patchen angelegt und danach gelöscht. Existiert die Datei beim Start noch, wird nicht gepatcht.

## Kompatibilitäts-Hinweis
**AlternateFantasy** ist ein Gameplay-Mod (ändert Kampf/Balancing). Er bringt eventuell eigene `Memoria.Scripts`-DLLs mit. Darum müssen unsere Harmony-Patches „additiv“ sein (Prefix/Postfix, kein Ersetzen ganzer Methoden).

## Nächste Schritte (Phase 1)
1. Projekt `src/FF9Coop` (net35, Referenzen auf `Assembly-CSharp.dll`, `Memoria.Prime.dll`, `UnityEngine.dll` aus `x64/FF9_Data/Managed`) + Lib.Harmony net35.
2. Mod-Ordner `FF9Coop/StreamingAssets/Scripts/Memoria.Scripts.FF9Coop.dll`, in `Memoria.ini` unter `[Mod] FolderNames` eintragen.
3. Smoke-Test: Log-Eintrag „[FF9Coop] loaded“ + Harmony-Postfix auf `BattleHUD.SwitchPlayer` loggt Charakter-ID.
4. Danach Multi-Pad-Polling und Owner-Check im Kampf.
