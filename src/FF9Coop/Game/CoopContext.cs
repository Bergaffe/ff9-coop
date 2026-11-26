using System;
using Memoria;
using Memoria.Data;

namespace FF9Coop.Game
{
    /// <summary>
    /// Ermittelt aus dem Spielzustand, welche Spieler gerade Eingaben machen dürfen.
    /// Ergebnis ist eine Bitmaske: Bit 0 = P1, Bit 1 = P2, …
    /// </summary>
    internal static class CoopContext
    {
        public static Int32 AllPlayers => (1 << CoopMod.Config.PlayerCount) - 1;
        public static Int32 Mask(Int32 player) => 1 << (player - 1);

        /// <summary>Kurzbeschreibung für HUD/Log, z. B. "Kampf: Vivi".</summary>
        public static String Description { get; private set; } = "";

        public static Int32 GetAllowedPlayers()
        {
            CoopConfig cfg = CoopMod.Config;
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            if (ui == null)
            {
                Description = "";
                return AllPlayers;
            }

            switch (ui.State)
            {
                case UIManager.UIState.BattleHUD:
                {
                    BattleHUD hud = UIManager.Battle;
                    Int32 index = hud != null ? hud.CurrentPlayerIndex : -1;
                    if (index < 0)
                    {
                        Description = "Kampf";
                        return AllPlayers;
                    }
                    Int32 owner = BattleOwnership.GetOwner(index, out CharacterId character);
                    Description = $"Kampf: {character}";
                    return Mask(owner);
                }

                case UIManager.UIState.FieldHUD:
                    Description = $"Feld · Leader P{cfg.LeaderPlayer}"
                                  + (Companion.IsVisible ? $" · Begleiter {Companion.CharacterCode} P{Companion.CompanionPlayer(cfg)}" : "");
                    return Mask(cfg.LeaderPlayer);
                case UIManager.UIState.WorldHUD:
                    Description = $"Weltkarte · Leader P{cfg.LeaderPlayer}";
                    return Mask(cfg.LeaderPlayer);
                case UIManager.UIState.QuadMist:
                case UIManager.UIState.QuadMistBattle:
                    Description = "Tetra Master";
                    return Mask(cfg.LeaderPlayer);

                case UIManager.UIState.MainMenu:
                case UIManager.UIState.Item:
                case UIManager.UIState.Ability:
                case UIManager.UIState.Equip:
                case UIManager.UIState.Status:
                case UIManager.UIState.Card:
                case UIManager.UIState.Config:
                case UIManager.UIState.Shop:
                case UIManager.UIState.NameSetting:
                case UIManager.UIState.PartySetting:
                case UIManager.UIState.Chocograph:
                    Description = "Menü";
                    return cfg.MenuControl == MenuControlMode.Leader ? Mask(cfg.LeaderPlayer) : AllPlayers;

                default:
                    // Titel, Laden/Speichern, Pause, Kampfergebnis, Game Over, …: jeder darf.
                    Description = "";
                    return AllPlayers;
            }
        }
    }
}
