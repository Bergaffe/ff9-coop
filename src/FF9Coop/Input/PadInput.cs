using System;
using System.Reflection;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Input
{
    /// <summary>
    /// Fragt alle 4 XInput-Slots roh ab. Die Abfrage läuft am eigenen Harmony-Patch auf
    /// GamePad.GetState vorbei (siehe <see cref="RawScope"/>).
    /// </summary>
    internal static class PadInput
    {
        public const Int32 PadCount = 4;

        private static readonly GamePadState[] s_current = new GamePadState[PadCount];
        private static readonly GamePadState[] s_previous = new GamePadState[PadCount];
        private static readonly Single[] s_nextProbe = new Single[PadCount];

        // XInputGetState auf leeren Slots ist teuer, deshalb wird ein getrennter Slot nur 1x pro Sekunde geprüft.
        private const Single DisconnectedProbeInterval = 1f;

        [ThreadStatic]
        private static Boolean t_raw;

        /// <summary>True, solange wir selbst roh abfragen. Dann muss der GetState-Patch das Original laufen lassen.</summary>
        public static Boolean IsRawQuery => t_raw;

        public static GamePadState Current(Int32 pad) => s_current[pad];
        public static GamePadState Previous(Int32 pad) => s_previous[pad];
        public static Boolean IsConnected(Int32 pad) => s_current[pad].IsConnected;

        public static void Poll()
        {
            Single now = Time.realtimeSinceStartup;
            for (Int32 i = 0; i < PadCount; i++)
            {
                s_previous[i] = s_current[i];
                if (!s_current[i].IsConnected && now < s_nextProbe[i])
                    continue;
                s_current[i] = QueryRaw((PlayerIndex)i);
                if (!s_current[i].IsConnected)
                    s_nextProbe[i] = now + DisconnectedProbeInterval;
            }
        }

        public static GamePadState QueryRaw(PlayerIndex index)
        {
            t_raw = true;
            // Voll qualifiziert: Assembly-CSharp hat eine globale Wrapper-Klasse "GamePad", die den Namen überdeckt.
            try { return XInputDotNetPure.GamePad.GetState(index, GamePadDeadZone.Circular); }
            finally { t_raw = false; }
        }

        /// <summary>Irgendeine Taste, das Steuerkreuz oder ein Trigger gedrückt (Sticks zählen nicht).</summary>
        public static Boolean AnyHeld(GamePadState s)
        {
            if (!s.IsConnected)
                return false;
            GamePadButtons b = s.Buttons;
            GamePadDPad d = s.DPad;
            return b.A == ButtonState.Pressed || b.B == ButtonState.Pressed || b.X == ButtonState.Pressed || b.Y == ButtonState.Pressed
                || b.Start == ButtonState.Pressed || b.Back == ButtonState.Pressed || b.Guide == ButtonState.Pressed
                || b.LeftShoulder == ButtonState.Pressed || b.RightShoulder == ButtonState.Pressed
                || b.LeftStick == ButtonState.Pressed || b.RightStick == ButtonState.Pressed
                || d.Up == ButtonState.Pressed || d.Down == ButtonState.Pressed || d.Left == ButtonState.Pressed || d.Right == ButtonState.Pressed
                || s.Triggers.Left > 0.3f || s.Triggers.Right > 0.3f;
        }

        /// <summary>Aktivität für die Pad-Übernahme im geteilten Modus: Taste oder deutlich ausgelenkter Stick.</summary>
        public static Boolean IsActive(GamePadState s)
        {
            if (AnyHeld(s))
                return true;
            GamePadThumbSticks.StickValue l = s.ThumbSticks.Left;
            return Math.Abs(l.X) > 0.5f || Math.Abs(l.Y) > 0.5f;
        }

        public static Boolean PressedY(Int32 pad) =>
            s_current[pad].Buttons.Y == ButtonState.Pressed && s_previous[pad].Buttons.Y != ButtonState.Pressed;

        // ---- Neutraler, aber "verbundener" Zustand ----
        // Gibt man dem Spiel einen getrennten Zustand, pausiert XInputManager.Refresh das Spiel
        // ("controller disconnected"). Deshalb wird über den internen Konstruktor ein verbundener
        // Zustand ohne Eingaben gebaut.

        private static GamePadState? s_neutral;

        public static Boolean TryGetNeutral(out GamePadState neutral)
        {
            if (s_neutral == null)
            {
                try
                {
                    Type rawType = typeof(GamePadState).GetNestedType("RawState", BindingFlags.NonPublic);
                    ConstructorInfo ctor = typeof(GamePadState).GetConstructor(
                        BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new[] { typeof(Boolean), rawType, typeof(GamePadDeadZone) }, null);
                    System.Object state = ctor.Invoke(new[] { (System.Object)true, Activator.CreateInstance(rawType), GamePadDeadZone.Circular });
                    s_neutral = (GamePadState)state;
                }
                catch (Exception ex)
                {
                    CoopLog.Error(ex, "Could not create a neutral pad state");
                    s_neutral = default(GamePadState); // nicht verbunden → Aufrufer nutzt Fallback
                }
            }
            neutral = s_neutral.Value;
            return neutral.IsConnected;
        }
    }
}
