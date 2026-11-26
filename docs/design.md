# FF9 Coop – Concept & Architecture

## Core idea
- **Only the host runs the game.** Other players sit at the same PC or connect via Steam Remote Play Together or Parsec, where they show up on the host as additional controllers.
- The mod routes input: depending on the situation, the game only accepts input from the player whose turn it is. No network synchronisation of two game instances is needed.
- **Shared camera**, no split screen.
- Designed for 2 players, extendable to 4.

## Battle
- Every character has an owner (configurable, otherwise assigned alternately).
- The command menu of a ready character only reacts to its owner's controller.
- A waiting player can "request" their ready character with a button.
- Optional: ATB pause while choosing commands, ready signal, P1/P2 markers next to the character names.

## Field and world map – leader + companion
The game's event engine knows exactly one controlled character. Therefore:
- **Leader:** as in the original. Triggers exits, battles, cutscenes and map changes.
- **Companion:** the second player with their own visible character from the party.
  - Walks freely on the walkmesh or the world map and collides with NPCs.
  - Can talk to NPCs, open chests, shop and play Tetra Master.
  - Is on a "leash": if too far away or after a map change, it is brought back to the leader.
  - Hidden during cutscenes, dialogs and menus.
- **Leader swap** by holding a button, outside of events.
- **Solo sections** (the story splits the party): choose who plays, or a fixed rule.
- Vehicles on the world map are controlled by the leader.
- Coloured "I'm here" hand with the player number above each character.

## Menus and minigames
- Main menu, shops, synthesis: everyone or leader only (configurable).
- Whoever starts a conversation also controls its choices.
- All assignments can be changed in an FF9-style co-op menu; the game pauses meanwhile.
- English and German, following the game language or set manually.

## Mods in multiplayer
- The game only runs on the host. All of the host's mods and settings apply automatically. Guests need no mods.
- A sync is only needed when switching hosts, since save games can depend on gameplay mods.
- The mod is a separate Memoria mod folder and replaces no game files. Its patches are additive (prefix/postfix), so it works with Moguri Mod and gameplay mods like Alternate Fantasy.

## Possible extensions
- Save assignments per save game
- Co-op profile to share mod list and settings when switching hosts
- Special rules for minigames
- Assign PlayStation/Switch controllers individually
