using System;
using System.IO;
using FF9Coop.Patches;
using Harmony;
using UnityEngine;

namespace FF9Coop
{
    internal static class CoopMod
    {
        public const String Version = "0.6.0";

        public static CoopConfig Config { get; private set; } = new CoopConfig();

        private static GameObject s_host;
        private static Boolean s_tickErrorLogged;

        /// <summary>LÃ¤uft im ScriptsLoader-Thread (Titelbildschirm). Hier keine Unity-Objekte erzeugen.</summary>
        public static void Initialize()
        {
            CoopLog.Message($"Version {Version} geladen aus {Bootstrap.ModDirectory}");
            Config = CoopConfig.Load(Path.Combine(Bootstrap.ModDirectory, "FF9Coop.ini"));
            CoopLog.Message("Konfiguration: " + Config);

            if (!Config.Enabled)
            {
                CoopLog.Message("Deaktiviert (FF9Coop.ini: [Main] Enabled = 0)");
                return;
            }

            // Absturzschutz: Ein nativer Absturz beim Patchen lÃ¤sst sich nicht abfangen. Deshalb vorher eine
            // Marker-Datei anlegen und nach Erfolg lÃ¶schen. Existiert sie beim Start noch, ist der letzte Start
            // beim Patchen abgestÃ¼rzt. Dann wird nicht gepatcht, damit das Spiel spielbar bleibt.
            String marker = Path.Combine(Bootstrap.ModDirectory, "FF9Coop.patching");
            if (File.Exists(marker))
            {
                CoopLog.Warning("Letzter Start ist beim Patchen abgestÃ¼rzt (" + File.ReadAllText(marker).Trim() + "). " +
                                "Mod bleibt deaktiviert. Zum erneuten Versuch die Datei FF9Coop.patching lÃ¶schen.");
                Config.Enabled = false;
                return;
            }

            File.WriteAllText(marker, "Harmony-Init");
            CoopLog.Message("Erzeuge Harmony-Instanz â€¦");
            HarmonyInstance harmony = HarmonyInstance.Create("ff9coop");
            CoopLog.Message("Harmony bereit, wende Patches an â€¦");
            InputPatches.Apply(harmony, step => File.WriteAllText(marker, step));
            File.Delete(marker);
            CoopLog.Message("Initialisierung abgeschlossen");
        }

        /// <summary>Erzeugt beim ersten Frame-Tick (Hauptthread) das Host-GameObject.</summary>
        public static void EnsureBehaviour()
        {
            if (s_host != null)
                return;
            s_host = new GameObject("FF9Coop");
            UnityEngine.Object.DontDestroyOnLoad(s_host);
            s_host.AddComponent<CoopBehaviour>();
            CoopLog.Message("Laufzeit-Objekt erstellt");
        }

        public static void ReportTickError(Exception ex)
        {
            if (s_tickErrorLogged)
                return;
            s_tickErrorLogged = true;
            CoopLog.Error(ex, "Fehler im Input-Tick (weitere werden unterdrÃ¼ckt)");
        }
    }
}
