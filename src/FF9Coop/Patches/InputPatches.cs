using System;
using System.Reflection;
using System.Threading;
using FF9Coop.Input;
using Harmony;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Patches
{
    /// <summary>
    /// Patches für die Eingabe. Alle sind Prefixe, die nur eingreifen, wenn der Router aktiv ist.
    /// Sonst läuft das Original unverändert (additiv, damit andere Mods wie AlternateFantasy nicht stören).
    /// </summary>
    internal static class InputPatches
    {
        private static Func<String, Single> s_getXAxis;
        private static Action<String> s_step;

        /// <param name="step">Wird vor jedem Patch mit dessen Namen aufgerufen (für die Absturz-Marker-Datei).</param>
        public static void Apply(HarmonyInstance harmony, Action<String> step)
        {
            s_step = step;
            // 1) Frame-Tick: vor XInputManager.Refresh die Router-Entscheidung treffen
            Patch(harmony, AccessTools.Method(typeof(UnityXInput.XInputManager), "Update"), nameof(XInputManager_Update_Prefix));

            // 2) Alle Controller-Abfragen des Spiels umleiten
            //    (voll qualifiziert: die globale Klasse "GamePad" aus Assembly-CSharp ist nur ein Wrapper auf XInputManager)
            Patch(harmony, AccessTools.Method(typeof(XInputDotNetPure.GamePad), "GetState", new[] { typeof(PlayerIndex), typeof(GamePadDeadZone) }),
                nameof(GamePad_GetState_Prefix));

            // 3) Tastatur sperren, wenn ihr Spieler gerade nicht dran ist
            Type input = typeof(UnityXInput.Input);
            foreach (String name in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
                Patch(harmony, AccessTools.Method(input, name, new[] { typeof(KeyCode) }), nameof(Keyboard_Prefix));
            Patch(harmony, AccessTools.Method(typeof(HonoInputManager), "CheckPersistentInput"), nameof(Skip_IfKeyboardBlocked_Prefix));
            Patch(harmony, AccessTools.Method(typeof(HonoInputManager), "CheckPersistentDirectionInput"), nameof(Keyboard_Prefix));

            // 4) Achsen außer Horizontal/Vertical (Trigger, rechter Stick) liest das Spiel teils über
            //    UnityEngine.Input, das ALLE Joysticks zusammenführt. Bei aktivem Router nur den XInput-Wert nehmen.
            MethodInfo getXAxis = AccessTools.Method(input, "GetXAxis", new[] { typeof(String) });
            if (getXAxis != null)
            {
                s_getXAxis = (Func<String, Single>)Delegate.CreateDelegate(typeof(Func<String, Single>), getXAxis);
                Patch(harmony, AccessTools.Method(input, "GetAxis", new[] { typeof(String) }), nameof(Axis_Prefix));
                Patch(harmony, AccessTools.Method(input, "GetAxisRaw", new[] { typeof(String) }), nameof(Axis_Prefix));
            }
            else
            {
                CoopLog.Warning("UnityXInput.Input.GetXAxis nicht gefunden – Achsen-Filter inaktiv");
            }
        }

        private static void Patch(HarmonyInstance harmony, MethodBase original, String prefixName)
        {
            if (original == null)
            {
                CoopLog.Warning($"Patch-Ziel für {prefixName} nicht gefunden – übersprungen (andere Memoria-Version?)");
                return;
            }
            String label = $"{original.DeclaringType?.Name}.{original.Name}";
            s_step?.Invoke("Patch " + label);
            CoopLog.Message($"Patche {label} …");
            harmony.Patch(original, new HarmonyMethod(typeof(InputPatches).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic)), null);
            CoopLog.Message($"Patch OK: {label}");
        }

        private static Boolean RouterActiveOnThisThread =>
            CoopMod.Config.Enabled && Thread.CurrentThread.ManagedThreadId == InputRouter.MainThreadId;

        // ---------------- Prefixe ----------------

        private static void XInputManager_Update_Prefix()
        {
            try { InputRouter.Tick(); }
            catch (Exception ex) { CoopMod.ReportTickError(ex); }
        }

        private static Boolean GamePad_GetState_Prefix(ref GamePadState __result)
        {
            if (PadInput.IsRawQuery || !InputRouter.HasEffective || !RouterActiveOnThisThread)
                return true; // Original ausführen
            __result = InputRouter.EffectiveState;
            return false;
        }

        // Signatur passt für GetKey/GetKeyDown/GetKeyUp(KeyCode) und CheckPersistentDirectionInput(Control): Rückgabe Boolean.
        private static Boolean Keyboard_Prefix(ref Boolean __result)
        {
            if (InputRouter.KeyboardAllowed || !RouterActiveOnThisThread)
                return true;
            __result = false;
            return false;
        }

        private static Boolean Skip_IfKeyboardBlocked_Prefix()
        {
            return InputRouter.KeyboardAllowed || !RouterActiveOnThisThread;
        }

        private static Boolean Axis_Prefix(String axisName, ref Single __result)
        {
            if (axisName == "Horizontal" || axisName == "Vertical")
                return true; // Tastatur (bereits gefiltert) + XInput, wie im Original
            if (!InputRouter.HasEffective || !RouterActiveOnThisThread)
                return true;
            __result = s_getXAxis(axisName);
            return false;
        }
    }
}
