using System;
using System.IO;
using FF9Coop.Patches;
using Harmony;
using UnityEngine;

namespace FF9Coop
{
    internal static class CoopMod
    {
        public const String Version = "0.9.0";

        public static CoopConfig Config { get; private set; } = new CoopConfig();

        private static GameObject s_host;
        private static Boolean s_tickErrorLogged;

        /// <summary>Läuft im ScriptsLoader-Thread (Titelbildschirm). Hier keine Unity-Objekte erzeugen.</summary>
        public static void Initialize()
        {
            CoopLog.Message($"Version {Version} loaded from {Bootstrap.ModDirectory}");
            Config = CoopConfig.Load(Path.Combine(Bootstrap.ModDirectory, "FF9Coop.ini"));
            CoopLog.Message("Configuration: " + Config);

            if (!Config.Enabled)
            {
                CoopLog.Message("Disabled (FF9Coop.ini: [Main] Enabled = 0)");
                return;
            }

            // Absturzschutz: Ein nativer Absturz beim Patchen lässt sich nicht abfangen. Deshalb vorher eine
            // Marker-Datei anlegen und nach Erfolg löschen. Existiert sie beim Start noch, ist der letzte Start
            // beim Patchen abgestürzt. Dann wird nicht gepatcht, damit das Spiel spielbar bleibt.
            String marker = Path.Combine(Bootstrap.ModDirectory, "FF9Coop.patching");
            if (File.Exists(marker))
            {
                CoopLog.Warning("The last start crashed while patching (" + File.ReadAllText(marker).Trim() + "). " +
                                "Mod stays disabled. Delete the file FF9Coop.patching to try again.");
                Config.Enabled = false;
                return;
            }

            File.WriteAllText(marker, "Harmony-Init");
            CoopLog.Message("Creating Harmony instance …");
            HarmonyInstance harmony = HarmonyInstance.Create("ff9coop");
            CoopLog.Message("Harmony ready, applying patches …");
            InputPatches.Apply(harmony, step => File.WriteAllText(marker, step));
            File.WriteAllText(marker, "Patch BattleHUD.IsNativeEnableAtb");
            Game.BattleCoop.Apply(harmony);
            File.WriteAllText(marker, "Patch BubbleUI.FilterFlags");
            Ui.PlayerMarkers.Apply(harmony);
            File.Delete(marker);
            CoopLog.Message("Initialization complete");
        }

        /// <summary>Erzeugt beim ersten Frame-Tick (Hauptthread) das Host-GameObject.</summary>
        public static void EnsureBehaviour()
        {
            if (s_host != null)
                return;
            s_host = new GameObject("FF9Coop");
            UnityEngine.Object.DontDestroyOnLoad(s_host);
            s_host.AddComponent<CoopBehaviour>();
            CoopLog.Message("Runtime object created");
        }

        public static void ReportTickError(Exception ex)
        {
            if (s_tickErrorLogged)
                return;
            s_tickErrorLogged = true;
            CoopLog.Error(ex, "Error in input tick (further errors suppressed)");
        }
    }
}
