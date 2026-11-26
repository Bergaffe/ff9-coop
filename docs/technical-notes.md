# FF9 Coop – Technical Notes

## Loading through Memoria
- Memoria (`ScriptsLoader`) loads `StreamingAssets/Scripts/Memoria.Scripts.<ModFolder>.dll` per mod folder. The mod therefore needs neither a Memoria fork nor modified game files.
- The Steam version runs on Unity 5.2 with an old Mono (.NET 3.5). The mod is built against the DLLs from `x64/FF9_Data/Managed`.
- Runtime patches use **Harmony 1.2.0.1**. Harmony 2.x crashes under this Mono while patching (access violation in `mono.dll`).
- Crash guard: `FF9Coop.patching` is created before patching and deleted afterwards. If the file still exists at startup, nothing is patched and the game stays playable.
- After Memoria updates (the launcher updates itself) the mod should be rebuilt and tested.

## Input
- The port itself only reads controller 1, but all controller queries go through `XInputDotNetPure.GamePad.GetState`.
- The mod polls all four XInput slots separately and only passes the state of the players currently in control to the game. The keyboard is allowed or blocked the same way.
- When the active player changes, all buttons must be released first so no input "slips through".

## Battle
- `BattleHUD`: `ReadyQueue` (characters with full ATB), `CurrentPlayerIndex` and `SwitchPlayer` determine whose menu is open; the responsible player follows from that.
- ATB pause via a postfix on `IsNativeEnableAtb`.

## Field
- The controlled character is the `FieldMapActorController` with `isPlayer`. The player has control while `EventEngine.GetUserControl()` holds.
- The companion is not an event object but its own model from `ModelFactory` with field animations. Its height comes from the walkmesh triangles.
- Companion interactions: the leader is placed at the companion's position for one frame, the original collision and talk checks run, then everything is restored. The game scripts stay unchanged.

## World map
- The companion model lives in `WMWorld.TranslatingObjectsGroup`.
- Ground height and walkability as for the player character. Sinking into forests and water via `ff9.w_movementGetSliceHeight`.

## Localisation
- `Lang.T(english, german)`; "Auto" uses German when `Localization.CurrentSymbol` is `GR`, English otherwise.

## Compatibility
- Gameplay mods like Alternate Fantasy bring their own `Memoria.Scripts` DLLs. That's why the mod never replaces whole methods, it only hooks in before or after them.
- Memoria merges PlayStation and Switch controllers (JoyShockLibrary) into every XInput slot, so they can't be assigned individually yet.
