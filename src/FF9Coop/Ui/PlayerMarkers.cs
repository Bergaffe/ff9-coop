using System;
using System.Reflection;
using Assets.Scripts.Common;
using FF9Coop.Game;
using Harmony;
using UnityEngine;

namespace FF9Coop.Ui
{
    /// <summary>
    /// "Ich bin hier"-Hand für alle Spieler: farbig je Spieler (P1 blau, P2 rot, …) und mit Spielernummer.
    ///
    /// Das Spiel zeigt beim Betreten eines Bildschirms kurz eine Hand über der Spielerfigur
    /// (EIcon.ShowHereIcon → BubbleUI, Typ CURSOR; abschaltbar über Memoria [Icons] HideCursor).
    /// Diese Hand wird ausgeblendet und stattdessen über Leader UND Begleiter dieselbe Grafik
    /// (CommonAsset/EventIcons/cursor_hand_here) eingefärbt gezeichnet. Kopfhöhe und Projektion
    /// wie im Spiel (BubbleMappingInfo.GetActorInfo, UIFieldMapFollowTarget: PSX-GTE der Feldkamera).
    /// </summary>
    internal static class PlayerMarkers
    {
        public const Int32 ModeOff = 0;       // Original-Hand des Spiels, unverändert
        public const Int32 ModeOnEnter = 1;   // wie im Spiel: kurz nach dem Betreten eines Bildschirms
        public const Int32 ModeAlways = 2;    // immer, solange gesteuert wird

        private static Texture2D s_hand;
        private static Boolean s_handLoaded;
        private static FieldInfo s_hereIconShow;
        private static Boolean s_reflectionDone;

        public static void Apply(HarmonyInstance harmony)
        {
            MethodInfo filter = AccessTools.Method(typeof(BubbleUI), "FilterFlags");
            if (filter == null)
            {
                CoopLog.Warning("BubbleUI.FilterFlags nicht gefunden – Original-Hand bleibt sichtbar");
                return;
            }
            harmony.Patch(filter, null, new HarmonyMethod(typeof(PlayerMarkers).GetMethod(nameof(FilterFlags_Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
            CoopLog.Message("Patch OK: BubbleUI.FilterFlags");
        }

        private static Boolean Active
        {
            get
            {
                CoopConfig cfg = CoopMod.Config;
                return cfg.Enabled && cfg.PlayerHands != ModeOff && cfg.PlayerCount >= 2;
            }
        }

        /// <summary>Original-Hand ausblenden, wir zeichnen die farbigen selbst.</summary>
        private static void FilterFlags_Postfix(ref BubbleUI.Flag[] __result)
        {
            if (!Active || __result == null || Array.IndexOf(__result, BubbleUI.Flag.CURSOR) < 0)
                return;
            __result = Array.FindAll(__result, f => f != BubbleUI.Flag.CURSOR);
        }

        // EIcon und PSX sind im Spiel "internal" → per Reflection; die Projektion als Delegate (läuft pro Frame)
        private delegate Vector3 GteRtpt(Vector3 vertex, Matrix4x4 localRTS, Matrix4x4 globalRT, Single viewDist, Vector2 offset);
        private static GteRtpt s_project;

        private static void InitReflection()
        {
            if (s_reflectionDone)
                return;
            s_reflectionDone = true;
            Assembly game = typeof(BubbleUI).Assembly;
            Type eicon = game.GetType("EIcon");
            if (eicon != null)
                s_hereIconShow = AccessTools.Field(eicon, "hereIconShow");
            Type psx = game.GetType("PSX");
            MethodInfo rtpt = psx?.GetMethod("CalculateGTE_RTPT", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Vector3), typeof(Matrix4x4), typeof(Matrix4x4), typeof(Single), typeof(Vector2) }, null);
            if (rtpt != null)
                s_project = (GteRtpt)Delegate.CreateDelegate(typeof(GteRtpt), rtpt);
            if (s_hereIconShow == null || s_project == null)
                CoopLog.Warning($"Spieler-Hand: EIcon={(s_hereIconShow != null)} PSX={(s_project != null)} – teilweise inaktiv");
        }

        private static Boolean GameShowsHereIcon()
        {
            InitReflection();
            return s_hereIconShow != null && (Boolean)s_hereIconShow.GetValue(null);
        }

        public static void Draw()
        {
            if (!Active)
                return;
            CoopConfig cfg = CoopMod.Config;
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            FieldMap map = events?.fieldmap;
            FieldMapActorController leader = map?.playerController;
            if (ui == null || ui.State != UIManager.UIState.FieldHUD || leader?.originalActor == null || !events.GetUserControl())
                return;
            if (PersistenSingleton<SceneDirector>.Instance != null && PersistenSingleton<SceneDirector>.Instance.IsFading)
                return;

            Boolean recentlyShown = Companion.IsVisible && Time.realtimeSinceStartup - Companion.ShownSince < 2.5f;
            if (cfg.PlayerHands == ModeOnEnter && !GameShowsHereIcon() && !recentlyShown)
                return;

            if (!s_handLoaded)
            {
                s_handLoaded = true;
                s_hand = AssetManager.Load<Texture2D>("CommonAsset/EventIcons/cursor_hand_here", false);
                if (s_hand == null)
                    CoopLog.Warning("Hand-Grafik cursor_hand_here nicht gefunden – nutze Ersatz");
            }

            Actor actor = leader.originalActor;
            // Kopfhöhe wie BubbleMappingInfo.GetActorInfo (negatives y = oben)
            Vector3 headOffset = new Vector3(0f, (-actor.eye * actor.scaley >> 6) + 50, 0f);

            DrawHand(map, leader.curPos + headOffset, cfg.LeaderPlayer);
            if (Companion.IsVisible)
                DrawHand(map, Companion.LocalPosition + headOffset, Companion.CompanionPlayer(cfg)); // Augenhöhe des Leaders als Näherung
        }

        private static void DrawHand(FieldMap map, Vector3 localPos, Int32 player)
        {
            if (player <= 0)
                return;
            InitReflection();
            Camera cam = map.GetMainCamera();
            BGCAM_DEF bgCam = map.GetCurrentBgCamera();
            if (cam == null || bgCam == null || s_project == null)
                return;

            Vector3 projected = s_project(localPos, Matrix4x4.identity, bgCam.GetMatrixRT(), bgCam.GetViewDistance(), map.GetProjectionOffset());
            Vector3 screen = cam.WorldToScreenPoint(projected);
            Single x = screen.x, y = Screen.height - screen.y;

            Single size = Screen.height * 0.075f;
            Single bob = Mathf.Sin(Time.realtimeSinceStartup * 5f) * size * 0.08f;
            Rect r = new Rect(x - size / 2f, y - size + bob, size, size);
            Color color = BattleCoop.PlayerColors[Mathf.Clamp(player - 1, 0, BattleCoop.PlayerColors.Length - 1)];

            Ff9Skin.EnsureInitialized();
            Color old = GUI.color;
            GUI.color = color;
            if (s_hand != null)
                GUI.DrawTexture(r, s_hand, ScaleMode.ScaleToFit);
            else
                Ff9Skin.Fill(new Rect(r.x + size * 0.25f, r.y + size * 0.15f, size * 0.5f, size * 0.5f), color);
            GUI.color = old;

            // Spielernummer auf dem Handrücken (oberer Teil der nach unten zeigenden Hand)
            Rect numberRect = new Rect(r.x, r.y + size * 0.05f, r.width, size * 0.5f);
            Ff9Skin.Label(numberRect, player.ToString(), Color.white, 0.95f, TextAnchor.MiddleCenter);
        }
    }
}
