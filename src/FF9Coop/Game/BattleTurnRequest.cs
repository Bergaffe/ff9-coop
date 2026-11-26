using System;
using System.Collections.Generic;
using System.Reflection;
using FF9Coop.Input;
using Harmony;

namespace FF9Coop.Game
{
    /// <summary>
    /// "Zug anfordern": Das Kommandomenü gehört gerade P1, aber ein Charakter von P2 ist ebenfalls bereit.
    /// Drückt P2 dann Y/Dreieck, wird auf seinen Charakter gewechselt.
    ///
    /// Bildet die Vanilla-Logik nach (BattleHUD.Scene.cs, Dreieck = "nächster bereiter Charakter"):
    /// Nur wenn das oberste Kommandomenü aktiv ist; der bisherige Charakter wandert ans Ende der ReadyQueue.
    /// </summary>
    internal static class BattleTurnRequest
    {
        private static MethodInfo s_switchPlayer;
        private static FieldInfo s_unconscious;
        private static Boolean s_initialized;
        private static Int32 s_lastPollId = -1;

        private static void Init()
        {
            s_initialized = true;
            s_switchPlayer = AccessTools.Method(typeof(BattleHUD), "SwitchPlayer", new[] { typeof(Int32) });
            s_unconscious = AccessTools.Field(typeof(BattleHUD), "_unconsciousStateList");
            if (s_switchPlayer == null)
                CoopLog.Warning("BattleHUD.SwitchPlayer nicht gefunden – Zug-Anfordern deaktiviert");
        }

        /// <summary>Aus CoopBehaviour.Update; verarbeitet jede Pad-Abfrage genau einmal.</summary>
        public static void Update()
        {
            if (!CoopMod.Config.BattleTurnRequest || InputRouter.PollId == s_lastPollId)
                return;
            s_lastPollId = InputRouter.PollId;

            if (!s_initialized)
                Init();
            if (s_switchPlayer == null)
                return;

            UIManager ui = PersistenSingleton<UIManager>.Instance;
            if (ui == null || ui.State != UIManager.UIState.BattleHUD)
                return;
            BattleHUD hud = UIManager.Battle;
            if (hud == null)
                return;

            Int32 current = hud.CurrentPlayerIndex;
            if (current < 0 || ButtonGroupState.ActiveGroup != BattleHUD.CommandGroupButton)
                return;

            Int32 currentOwner = BattleOwnership.GetOwner(current);
            CoopConfig cfg = CoopMod.Config;

            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                Int32 player = cfg.PadPlayer[pad];
                if (player == currentOwner || !PadInput.IsConnected(pad) || !PadInput.PressedY(pad))
                    continue;
                if (TrySwitchTo(hud, player, current))
                    return;
            }

            // Tastatur: direkt über UnityEngine.Input lesen, denn unser Patch sperrt UnityXInput.Input für die Tastatur,
            // solange ihr Spieler nicht dran ist. Genau dann soll die Taste aber wirken.
            if (cfg.KeyboardPlayer != currentOwner && UnityEngine.Input.GetKeyDown(cfg.TurnRequestKey))
                TrySwitchTo(hud, cfg.KeyboardPlayer, current);
        }

        private static Boolean TrySwitchTo(BattleHUD hud, Int32 player, Int32 current)
        {
            List<Int32> queue = hud.ReadyQueue;
            List<Int32> unconscious = s_unconscious?.GetValue(hud) as List<Int32>;

            for (Int32 i = 0; i < queue.Count; i++)
            {
                Int32 candidate = queue[i];
                if (candidate == current || hud.InputFinishList.Contains(candidate) || (unconscious != null && unconscious.Contains(candidate)))
                    continue;
                if (BattleOwnership.GetOwner(candidate) != player)
                    continue;

                // Wie Vanilla: aktuellen Charakter ans Ende, gewünschten nach vorne.
                if (queue.Remove(current))
                    queue.Add(current);
                queue.Remove(candidate);
                queue.Insert(0, candidate);

                s_switchPlayer.Invoke(hud, new Object[] { candidate });
                CoopLog.Message($"P{player} übernimmt Zug (Slot {candidate})");
                CoopBehaviour.Notify($"P{player} übernimmt den Zug");
                return true;
            }
            return false;
        }
    }
}
