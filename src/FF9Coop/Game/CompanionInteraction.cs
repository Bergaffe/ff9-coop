using System;
using System.Reflection;
using FF9Coop.Input;
using Harmony;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Game
{
    /// <summary>
    /// Spieler 2 spricht mit NPCs, öffnet Truhen/Auslöser, handelt in Shops und fordert zu Tetra Master heraus.
    ///
    /// Trick: Die Spiel-Prüfung (EventCollision.CheckNPCInput / CheckQuadInput) arbeitet mit der Leader-Figur.
    /// Für genau einen Aufruf setzen wir deren Position und Blickrichtung auf die des Begleiters, führen die
    /// Original-Prüfung aus (Reichweite, Blickwinkel, "ansprechbar") und stellen sofort zurück. Das Skript
    /// des NPCs läuft danach ganz normal; nur die Eingaben (Dialog, Auswahl, Shop) gehören dem Begleiter-Spieler.
    /// </summary>
    internal static class CompanionInteraction
    {
        private const Int32 TagTalk = 3;   // Funktion "Ansprechen" eines Event-Objekts
        private const Int32 TagCards = 8;  // Funktion "Tetra-Master-Herausforderung"

        private static Boolean s_init;
        private static MethodInfo s_isNpcTalkable, s_isQuadTalkable, s_clearPath;

        public static void Update()
        {
            if (!Companion.IsVisible)
                return;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            FieldMap map = events?.fieldmap;
            if (map?.playerController?.originalActor == null || !events.GetUserControl())
                return;
            if (InteractionOwner.Player != 0)
                return;

            CoopConfig cfg = CoopMod.Config;
            Int32 player = Companion.CompanionPlayer(cfg);
            if (player == 0)
                return;

            ReadButtons(cfg, player, out Boolean talk, out Boolean cards);
            if (!talk && !cards)
                return;

            if (!s_init)
                Init();

            if (TryInteract(events, map, cards ? TagCards : TagTalk))
            {
                InteractionOwner.Begin(player);
                CoopLog.Message($"P{player} interacts as companion ({(cards ? "Tetra Master" : "talk")})");
            }
        }

        private static void Init()
        {
            s_init = true;
            s_isNpcTalkable = AccessTools.Method(typeof(EventCollision), "IsNPCTalkable");
            s_isQuadTalkable = AccessTools.Method(typeof(EventCollision), "IsQuadTalkable");
            s_clearPath = AccessTools.Method(typeof(EventCollision), "ClearPathFinding");
            if (s_isNpcTalkable == null || s_isQuadTalkable == null)
                CoopLog.Warning("EventCollision checks not found – companion interaction limited");
        }

        private static void ReadButtons(CoopConfig cfg, Int32 player, out Boolean talk, out Boolean cards)
        {
            talk = cards = false;
            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (cfg.PadPlayer[pad] != player || !PadInput.IsConnected(pad))
                    continue;
                GamePadButtons now = PadInput.Current(pad).Buttons, before = PadInput.Previous(pad).Buttons;
                talk |= now.A == ButtonState.Pressed && before.A != ButtonState.Pressed;
                cards |= now.X == ButtonState.Pressed && before.X != ButtonState.Pressed; // X = Quadrat
            }
            if (cfg.KeyboardPlayer == player)
            {
                KeyCode[] keys = PersistenSingleton<HonoInputManager>.Instance?.InputKeysPrimary;
                if (keys != null && keys.Length > (Int32)Control.Special)
                {
                    talk |= UnityEngine.Input.GetKeyDown(keys[(Int32)Control.Confirm]);
                    cards |= UnityEngine.Input.GetKeyDown(keys[(Int32)Control.Special]);
                }
            }
        }

        private static Boolean TryInteract(EventEngine events, FieldMap map, Int32 tag)
        {
            FieldMapActorController ctrl = map.playerController;
            Actor leader = ctrl.originalActor;
            if (leader.go == null || leader.pos == null)
                return false;

            // --- Leader kurz an die Stelle des Begleiters setzen ---
            Vector3 savedCurPos = ctrl.curPos;
            Single[] savedPos = (Single[])leader.pos.Clone();
            Vector3 savedRot = leader.rotAngle;
            Vector3 savedWorld = leader.go.transform.position;

            Vector3 cp = Companion.LocalPosition;
            try
            {
                ctrl.curPos = cp;
                leader.pos[0] = cp.x; leader.pos[1] = cp.y; leader.pos[2] = cp.z;
                Vector3 rot = leader.rotAngle;
                rot[1] = Companion.Yaw;
                leader.rotAngle = rot;
                leader.go.transform.position = Companion.WorldPosition;

                return TryNpc(events, leader, tag) || TryQuad(events, leader, tag);
            }
            finally
            {
                // --- sofort zurück; das Bild wird erst nach diesem Frame gezeichnet ---
                ctrl.curPos = savedCurPos;
                Array.Copy(savedPos, leader.pos, savedPos.Length);
                leader.rotAngle = savedRot;
                leader.go.transform.position = savedWorld;
            }
        }

        /// <summary>Nachbau von EventCollision.CheckNPCInput ohne Tastenprüfung (die Taste kam vom Begleiter-Spieler).</summary>
        private static Boolean TryNpc(EventEngine events, Actor leader, Int32 tag)
        {
            Single distance = events.nilFloat;
            Obj obj = EventCollision.Collision(events, leader, 4, ref distance);
            if (obj == null || !Invoke(s_isNpcTalkable, obj))
                return false;
            EventCollision.sSysAngle = EventCollision.CollisionAngle(leader, obj);
            if (EventCollision.sSysAngle <= -1024 || EventCollision.sSysAngle >= 1024)
                return false;
            leader.listener = obj.uid;
            if (!events.Request(obj, 1, tag, false))
                return false;
            if (tag == TagCards)
                EMinigame.SetQuadmistOpponentId(obj);
            s_clearPath?.Invoke(null, new System.Object[] { leader });
            return true;
        }

        /// <summary>Nachbau von EventCollision.CheckQuadInput (Truhen, Schilder, Bodenauslöser).</summary>
        private static Boolean TryQuad(EventEngine events, Actor leader, Int32 tag)
        {
            Obj quad = events.TreadQuad(leader, 4);
            if (quad == null || !Invoke(s_isQuadTalkable, leader, quad))
                return false;
            if (!events.Request(quad, 1, tag, false))
                return false;
            if (tag == TagCards)
                EMinigame.SetQuadmistOpponentId(quad);
            s_clearPath?.Invoke(null, new System.Object[] { leader });
            return true;
        }

        private static Boolean Invoke(MethodInfo check, params System.Object[] args)
        {
            if (check == null)
                return false;
            return (Boolean)check.Invoke(null, args);
        }
    }

    /// <summary>
    /// Wer gerade eine Interaktion (Dialog, Shop, Tetra Master …) bedient. 0 = niemand (normale Regeln).
    /// Beginnt mit dem Ansprechen und endet, wenn das Spiel die Kontrolle zurückgibt.
    /// </summary>
    internal static class InteractionOwner
    {
        private enum State { None, Pending, Active }

        private static State s_state;
        private static Int32 s_player;
        private static Single s_since;

        public static Int32 Player => s_state == State.None ? 0 : s_player;

        public static void Begin(Int32 player)
        {
            s_player = player;
            s_state = State.Pending;
            s_since = Time.realtimeSinceStartup;
            CoopBehaviour.Notify(Lang.T($"P{player} is talking", $"P{player} spricht"));
        }

        public static void Update()
        {
            if (s_state == State.None)
                return;
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            if (ui == null || events == null)
            {
                End();
                return;
            }
            // Kampf startet aus dem Gespräch heraus → normale Kampfregeln
            if (ui.State == UIManager.UIState.BattleHUD)
            {
                End();
                return;
            }
            Boolean inMenuOrGame = ui.State != UIManager.UIState.FieldHUD;
            Boolean control = events.GetUserControl() && !inMenuOrGame;

            if (s_state == State.Pending)
            {
                if (!control)
                    s_state = State.Active;
                else if (Time.realtimeSinceStartup - s_since > 1.5f)
                    End(); // Skript hat die Kontrolle nie entzogen (z. B. nur ein Geräusch)
            }
            else if (control)
            {
                End();
            }
        }

        private static void End()
        {
            s_state = State.None;
            s_player = 0;
        }
    }
}
