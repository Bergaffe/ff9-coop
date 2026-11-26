using System;
using FF9Coop.Input;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Game
{
    /// <summary>
    /// Leader-Wechsel: Ein Spieler, der gerade NICHT Leader ist, hält Back/Select (Pad) bzw. die
    /// LeaderKey (Tastatur) für LeaderHoldSeconds und übernimmt damit Feld und Weltkarte.
    ///
    /// Nur der Nicht-Leader kann übernehmen. Seine Eingaben sieht das Spiel in diesem Moment nicht,
    /// das Halten löst also nichts im Spiel aus (Select = ATE). Nach dem Wechsel wartet der
    /// Input-Router, bis die Taste losgelassen ist.
    /// </summary>
    internal static class LeaderControl
    {
        private static readonly Single[] s_holdStart = new Single[CoopConfig.MaxPlayers + 1];
        private static readonly Boolean[] s_needsRelease = new Boolean[CoopConfig.MaxPlayers + 1];

        public static void Update()
        {
            CoopConfig cfg = CoopMod.Config;
            Boolean allowed = CanSwitchNow();
            Single now = Time.realtimeSinceStartup;

            for (Int32 player = 1; player <= cfg.PlayerCount; player++)
            {
                Boolean held = IsHoldingLeaderInput(player);
                if (!held)
                {
                    s_holdStart[player] = 0f;
                    s_needsRelease[player] = false;
                    continue;
                }
                if (s_needsRelease[player] || player == cfg.LeaderPlayer || !allowed)
                    continue;

                if (s_holdStart[player] <= 0f)
                {
                    s_holdStart[player] = now;
                    continue;
                }
                if (now - s_holdStart[player] >= cfg.LeaderHoldSeconds)
                {
                    Int32 old = cfg.LeaderPlayer;
                    cfg.LeaderPlayer = player;
                    s_needsRelease[player] = true;
                    CoopLog.Message($"Leader: P{old} -> P{player}");
                    CoopBehaviour.Notify(Lang.T($"P{player} is now the leader", $"P{player} ist jetzt Leader"));
                }
            }
        }

        /// <summary>Fortschritt 0..1 des Haltens für das HUD (höchster Wert aller Nicht-Leader).</summary>
        public static Single HoldProgress
        {
            get
            {
                CoopConfig cfg = CoopMod.Config;
                if (cfg.LeaderHoldSeconds <= 0f)
                    return 0f;
                Single best = 0f;
                for (Int32 p = 1; p <= cfg.PlayerCount; p++)
                    if (s_holdStart[p] > 0f && !s_needsRelease[p] && p != cfg.LeaderPlayer)
                        best = Math.Max(best, (Time.realtimeSinceStartup - s_holdStart[p]) / cfg.LeaderHoldSeconds);
                return Math.Min(1f, best);
            }
        }

        /// <summary>Feld mit Spielerkontrolle (keine Cutscene) oder Weltkarte.</summary>
        public static Boolean CanSwitchNow()
        {
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            if (ui == null)
                return false;
            if (ui.State == UIManager.UIState.WorldHUD)
                return true;
            if (ui.State != UIManager.UIState.FieldHUD)
                return false;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            return events != null && events.GetUserControl();
        }

        private static Boolean IsHoldingLeaderInput(Int32 player)
        {
            CoopConfig cfg = CoopMod.Config;
            if (cfg.KeyboardPlayer == player && UnityEngine.Input.GetKey(cfg.LeaderKey))
                return true;
            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (cfg.PadPlayer[pad] != player || !PadInput.IsConnected(pad))
                    continue;
                GamePadState s = PadInput.Current(pad);
                // Back/Select, or L2 + R2 together (DualShock/DualSense: Share/Create is often not mapped to Back)
                if (s.Buttons.Back == ButtonState.Pressed || (s.Triggers.Left > 0.5f && s.Triggers.Right > 0.5f))
                    return true;
            }
            return false;
        }
    }
}
