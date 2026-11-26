using System;
using System.Threading;
using FF9Coop.Game;
using XInputDotNetPure;

namespace FF9Coop.Input
{
    /// <summary>
    /// Entscheidet einmal pro Frame, welches Pad das Spiel "sieht".
    ///
    /// Das Spiel liest Controller ausschließlich über XInputDotNetPure.GamePad.GetState
    /// (XInputManager.Refresh, HonoInputManager.CheckPersistentInput/IsJoystickConnect).
    /// Unser Patch auf GetState liefert stattdessen <see cref="EffectiveState"/>: den Zustand
    /// des Pads eines berechtigten Spielers, oder einen neutralen Zustand.
    /// </summary>
    internal static class InputRouter
    {
        public static Int32 MainThreadId { get; private set; } = -1;
        public static Int32 PollId { get; private set; }

        /// <summary>False = keine Umleitung (z. B. gar kein Pad verbunden). GetState läuft dann normal.</summary>
        public static Boolean HasEffective { get; private set; }
        public static GamePadState EffectiveState { get; private set; }
        public static Boolean KeyboardAllowed { get; private set; } = true;

        public static Int32 AllowedMask { get; private set; }
        /// <summary>Spieler, dessen Eingaben gerade ankommen (für das HUD), 0 = unbekannt.</summary>
        public static Int32 ActivePlayer { get; private set; }
        public static Boolean WaitingForRelease => s_requireRelease;

        private static Int32 s_effectivePad = -1;
        private static Boolean s_requireRelease;

        /// <summary>Wird am Anfang von XInputManager.Update aufgerufen (Hauptthread, 1x pro Frame).</summary>
        public static void Tick()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            CoopMod.EnsureBehaviour();

            PadInput.Poll();
            PollId++;

            CoopConfig cfg = CoopMod.Config;

            // Koop-Menü offen: Das Spiel bekommt gar nichts (das Menü liest die Geräte selbst roh)
            if (Ui.CoopMenu.IsOpen || Ui.SoloScenePrompt.IsOpen)
            {
                KeyboardAllowed = false;
                s_effectivePad = -1;
                Boolean anyPad = false;
                for (Int32 i = 0; i < PadInput.PadCount; i++)
                    anyPad |= PadInput.IsConnected(i);
                if (anyPad)
                    SetNeutralOrPassThrough();
                else
                    HasEffective = false;
                return;
            }
            Int32 allowed = CoopContext.GetAllowedPlayers();
            AllowedMask = allowed;

            Boolean keyboardOwnerAllowed = (allowed & CoopContext.Mask(cfg.KeyboardPlayer)) != 0;

            Int32 candidates = 0;   // Bitmaske über Pads
            Int32 connected = 0;
            for (Int32 i = 0; i < PadInput.PadCount; i++)
            {
                if (!PadInput.IsConnected(i))
                    continue;
                connected |= 1 << i;
                if ((allowed & CoopContext.Mask(cfg.PadPlayer[i])) != 0)
                    candidates |= 1 << i;
            }

            // Ausfallsicherung: Hat kein berechtigter Spieler ein Gerät (Pad oder Tastatur),
            // dürfen alle. Sonst wäre das Spiel blockiert, z. B. wenn P2 sein Pad abzieht.
            Boolean failOpen = candidates == 0 && !keyboardOwnerAllowed;
            if (failOpen)
                candidates = connected;

            KeyboardAllowed = !cfg.GateKeyboard || failOpen || keyboardOwnerAllowed;

            if (connected == 0)
            {
                // Kein Pad: nichts umleiten, das Spiel verhält sich wie Vanilla.
                HasEffective = false;
                s_effectivePad = -1;
                ActivePlayer = cfg.KeyboardPlayer;
                return;
            }

            if (candidates == 0)
            {
                // Nur die Tastatur ist berechtigt. Alle Pads sind stumm, gelten aber als "verbunden", damit das Spiel nicht pausiert.
                s_effectivePad = -1;
                ActivePlayer = cfg.KeyboardPlayer;
                SetNeutralOrPassThrough();
                return;
            }

            SelectPad(candidates);

            ActivePlayer = cfg.PadPlayer[s_effectivePad];
            GamePadState current = PadInput.Current(s_effectivePad);
            if (s_requireRelease && !PadInput.AnyHeld(current))
                s_requireRelease = false;

            if (s_requireRelease)
                SetNeutralOrPassThrough();
            else
            {
                EffectiveState = current;
                HasEffective = true;
            }
        }

        /// <summary>XInput-Slot, dessen Eingaben das Spiel gerade sieht (-1 = keiner/Tastatur).</summary>
        public static Int32 EffectivePad => s_effectivePad;

        /// <summary>Beim nächsten Tick neu wählen und gehaltene Tasten erst nach dem Loslassen durchlassen.</summary>
        public static void ForceRelease()
        {
            s_effectivePad = -1;
        }

        private static void SelectPad(Int32 candidates)
        {
            Boolean currentValid = s_effectivePad >= 0 && (candidates & (1 << s_effectivePad)) != 0;

            if (!currentValid)
            {
                // Besitzerwechsel: bevorzugt das Pad, auf dem gerade etwas passiert.
                Int32 next = FirstPad(candidates, requireActivity: true);
                if (next < 0)
                    next = FirstPad(candidates, requireActivity: false);
                s_effectivePad = next;
                // Gehaltene Tasten des neuen Pads erst nach dem Loslassen durchlassen.
                s_requireRelease = PadInput.AnyHeld(PadInput.Current(next));
                return;
            }

            // Geteilter Modus (mehrere berechtigte Pads): Übernahme durch das Pad mit neuer Aktivität,
            // aber nur, wenn das bisherige Pad gerade nichts hält.
            if ((candidates & ~(1 << s_effectivePad)) != 0 && !PadInput.IsActive(PadInput.Current(s_effectivePad)))
            {
                for (Int32 i = 0; i < PadInput.PadCount; i++)
                {
                    if (i == s_effectivePad || (candidates & (1 << i)) == 0)
                        continue;
                    if (PadInput.IsActive(PadInput.Current(i)) && !PadInput.IsActive(PadInput.Previous(i)))
                    {
                        s_effectivePad = i;
                        s_requireRelease = false; // die neue Eingabe soll sofort zählen
                        return;
                    }
                }
            }
        }

        private static Int32 FirstPad(Int32 candidates, Boolean requireActivity)
        {
            for (Int32 i = 0; i < PadInput.PadCount; i++)
                if ((candidates & (1 << i)) != 0 && (!requireActivity || PadInput.IsActive(PadInput.Current(i))))
                    return i;
            return -1;
        }

        private static void SetNeutralOrPassThrough()
        {
            if (PadInput.TryGetNeutral(out GamePadState neutral))
            {
                EffectiveState = neutral;
                HasEffective = true;
            }
            else
            {
                HasEffective = false;
            }
        }
    }
}
