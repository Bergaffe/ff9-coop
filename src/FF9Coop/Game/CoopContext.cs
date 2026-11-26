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
                    // Hat der Begleiter-Spieler ein Gespräch begonnen, bedient er Dialog und Auswahl
                    if (InteractionOwner.Player != 0)
                    {
                        Description = $"Gespräch · P{InteractionOwner.Player}";
                        return Mask(InteractionOwner.Player);
                    }
                    Description = $"Feld · Leader P{cfg.LeaderPlayer}"
                                  + (Companion.IsVisible ? $" · Begleiter {Companion.DisplayName(Companion.CharacterCode)} P{Companion.CompanionPlayer(cfg)}" : "");
                    return Mask(cfg.LeaderPlayer);
                case UIManager.UIState.WorldHUD:
                    Description = $"Weltkarte · Leader P{cfg.LeaderPlayer}"
                                  + (WorldCompanion.IsVisible ? $" · Begleiter {Companion.DisplayName(WorldCompanion.CharacterCode)} P{Companion.CompanionPlayer(cfg)}" : "");
                    return Mask(cfg.LeaderPlayer);
                case UIManager.UIState.QuadMist:
                case UIManager.UIState.QuadMistBattle:
                    Description = "Tetra Master";
                    return Mask(InteractionOwner.Player != 0 ? InteractionOwner.Player : cfg.LeaderPlayer);

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
                    Description = InteractionOwner.Player != 0 ? $"Menü · P{InteractionOwner.Player}" : "Menü";
                    // Shop o. Ä. vom Begleiter-Spieler geöffnet → er darf auch bei MenuControl = Leader
                    if (cfg.MenuControl == MenuControlMode.Leader)
                        return Mask(cfg.LeaderPlayer) | (InteractionOwner.Player != 0 ? Mask(InteractionOwner.Player) : 0);
                    return AllPlayers;

                default:
                    // Titel, Laden/Speichern, Pause, Kampfergebnis, Game Over, …: jeder darf.
                    Description = "";
                    return AllPlayers;
            }
        }
    }
}
