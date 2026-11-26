using System;
using Memoria;
using Memoria.Data;

namespace FF9Coop.Game
{
    /// <summary>Welcher Spieler steuert welchen Kampf-Charakter.</summary>
    internal static class BattleOwnership
    {
        /// <summary>
        /// Besitzer eines Kampf-Slots (BattleHUD.CurrentPlayerIndex / ReadyQueue-Eintrag).
        /// Reihenfolge: [Owners] aus der INI, sonst Slot abwechselnd auf die Spieler verteilt.
        /// </summary>
        public static Int32 GetOwner(Int32 battleIndex, out CharacterId character)
        {
            character = CharacterId.NONE;
            CoopConfig cfg = CoopMod.Config;
            try
            {
                BattleUnit unit = FF9StateSystem.Battle.FF9Battle.GetUnit(battleIndex);
                if (unit != null)
                    character = unit.PlayerIndex;
            }
            catch (Exception)
            {
                // Kampf wird gerade auf-/abgebaut. Dann gilt die Slot-Regel.
            }

            if (character != CharacterId.NONE && cfg.Owners.TryGetValue(character, out Int32 owner))
                return owner;
            return (battleIndex % cfg.PlayerCount) + 1;
        }

        public static Int32 GetOwner(Int32 battleIndex) => GetOwner(battleIndex, out _);
    }
}
