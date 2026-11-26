using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FF9Coop.Input;
using FF9Coop.Ui;
using Harmony;
using UnityEngine;
using XInputDotNetPure;
using Object = System.Object;

namespace FF9Coop.Game
{
    /// <summary>
    /// Koop-Komfort im Kampf:
    /// - ATB anhalten, solange ein Spieler im Kommandomenü wählt (fair bei Absprachen/Online-Verzögerung,
    ///   besonders mit schweren Mods). Patch auf BattleHUD.IsNativeEnableAtb, das auch FF9s "Warten" steuert.
    /// - Bereit-Signal: kurzer Vibrationsimpuls am Controller des Besitzers, sobald sein Charakter bereit ist.
    /// - P1/P2-Markierung neben den Namen in der Gruppenanzeige.
    /// </summary>
    internal static class BattleCoop
    {
        private const Single PulseSeconds = 0.18f;
        private const Single PulseStrength = 0.55f;

        private static readonly HashSet<Int32> s_knownReady = new HashSet<Int32>();
        private static readonly Single[] s_pulseUntil = new Single[PadInput.PadCount];

        private static FieldInfo s_partyDetail;
        private static Boolean s_reflectionInit, s_reflectionFailed;

        // ---------------- Patch: ATB anhalten ----------------

        public static void Apply(HarmonyInstance harmony)
        {
            MethodInfo original = AccessTools.Method(typeof(BattleHUD), "IsNativeEnableAtb");
            if (original == null)
            {
                CoopLog.Warning("BattleHUD.IsNativeEnableAtb nicht gefunden – ATB-Pause inaktiv");
                return;
            }
            harmony.Patch(original, null, new HarmonyMethod(typeof(BattleCoop).GetMethod(nameof(IsNativeEnableAtb_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
            CoopLog.Message("Patch OK: BattleHUD.IsNativeEnableAtb");
        }

        private static void IsNativeEnableAtb_Postfix(BattleHUD __instance, ref Boolean __result)
        {
            if (__result && CoopMod.Config.Enabled && CoopMod.Config.BattlePauseWhileChoosing && __instance.CurrentPlayerIndex >= 0)
                __result = false;
        }

        // ---------------- Bereit-Signal ----------------

        public static void Update()
        {
            UpdatePulses();

            UIManager ui = PersistenSingleton<UIManager>.Instance;
            BattleHUD hud = UIManager.Battle;
            if (ui == null || ui.State != UIManager.UIState.BattleHUD || hud == null)
            {
                s_knownReady.Clear();
                return;
            }

            List<Int32> ready = hud.ReadyQueue;
            // Nicht mehr bereite vergessen, damit der nächste Zug wieder signalisiert wird
            s_knownReady.RemoveWhere(i => !ready.Contains(i));
            foreach (Int32 index in ready)
            {
                if (!s_knownReady.Add(index) || hud.InputFinishList.Contains(index))
                    continue;
                if (CoopMod.Config.ReadySignal)
                    Pulse(BattleOwnership.GetOwner(index));
            }
        }

        private static void Pulse(Int32 player)
        {
            CoopConfig cfg = CoopMod.Config;
            Single until = Time.realtimeSinceStartup + PulseSeconds;
            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (cfg.PadPlayer[pad] != player || !PadInput.IsConnected(pad))
                    continue;
                XInputDotNetPure.GamePad.SetVibration((PlayerIndex)pad, PulseStrength, PulseStrength);
                s_pulseUntil[pad] = until;
            }
        }

        private static void UpdatePulses()
        {
            Single now = Time.realtimeSinceStartup;
            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (s_pulseUntil[pad] <= 0f || now < s_pulseUntil[pad])
                    continue;
                s_pulseUntil[pad] = 0f;
                XInputDotNetPure.GamePad.SetVibration((PlayerIndex)pad, 0f, 0f);
            }
        }

        // ---------------- P1/P2-Markierung ----------------

        public static readonly Color[] PlayerColors =
        {
            new Color(0.40f, 0.65f, 1.00f, 1f), // P1 blau
            new Color(1.00f, 0.45f, 0.40f, 1f), // P2 rot
            new Color(0.45f, 0.90f, 0.45f, 1f), // P3 grün
            new Color(1.00f, 0.85f, 0.35f, 1f), // P4 gelb
        };

        public static void DrawMarkers()
        {
            CoopConfig cfg = CoopMod.Config;
            if (!cfg.BattleMarkers || cfg.PlayerCount < 2 || s_reflectionFailed)
                return;
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            BattleHUD hud = UIManager.Battle;
            Camera uiCam = UICamera.mainCamera;
            if (ui == null || ui.State != UIManager.UIState.BattleHUD || hud == null || uiCam == null)
                return;

            if (!s_reflectionInit)
            {
                s_reflectionInit = true;
                s_partyDetail = AccessTools.Field(typeof(BattleHUD), "_partyDetail");
                if (s_partyDetail == null)
                {
                    s_reflectionFailed = true;
                    CoopLog.Warning("BattleHUD._partyDetail nicht gefunden – P1/P2-Markierung inaktiv");
                    return;
                }
            }

            Object panel = s_partyDetail.GetValue(hud);
            Object table = Member(panel, "Characters");
            if (!(Member(table, "Entries") is IEnumerable entries))
                return;

            Ff9Skin.EnsureInitialized();
            Single s = Ff9Skin.Scale;
            foreach (Object entry in entries)
            {
                if (entry == null || !(Member(entry, "PlayerId") is Int32 playerId) || playerId < 0)
                    continue;
                if (!(Member(Member(entry, "Name"), "Label") is UILabel label) || !label.gameObject.activeInHierarchy)
                    continue;

                // Linke Kante und Mitte des Namensfelds in Bildschirm-Koordinaten (GUI: y von oben)
                Vector3[] corners = label.worldCorners;
                Vector3 bl = uiCam.WorldToScreenPoint(corners[0]);
                Vector3 tl = uiCam.WorldToScreenPoint(corners[1]);
                Single centerY = Screen.height - (bl.y + tl.y) / 2f;
                Single height = Math.Abs(tl.y - bl.y);

                Int32 owner = BattleOwnership.GetOwner(playerId);
                Color c = PlayerColors[Mathf.Clamp(owner - 1, 0, PlayerColors.Length - 1)];
                Boolean active = hud.CurrentPlayerIndex == playerId;

                Single w = 52f * s, h = Math.Max(26f * s, height * 0.8f);
                Rect tag = new Rect(bl.x - w - 10f * s, centerY - h / 2f, w, h);
                Ff9Skin.Fill(tag, new Color(0f, 0f, 0f, 0.55f));
                Ff9Skin.Fill(new Rect(tag.x, tag.y, 4f * s, tag.height), c);
                Ff9Skin.Label(new Rect(tag.x + 4f * s, tag.y, tag.width - 4f * s, tag.height), "P" + owner,
                    active ? Ff9Skin.AccentColor : c, 0.62f, TextAnchor.MiddleCenter);
            }
        }

        private static Object Member(Object target, String name)
        {
            if (target == null)
                return null;
            Type t = target.GetType();
            FieldInfo f = AccessTools.Field(t, name);
            if (f != null)
                return f.GetValue(target);
            PropertyInfo p = AccessTools.Property(t, name);
            return p?.GetValue(target, null);
        }
    }
}
