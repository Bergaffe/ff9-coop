using System;
using System.IO;
using System.Reflection;

namespace FF9Coop
{
    /// <summary>
    /// Einstiegspunkt der Mod.
    ///
    /// Memoria's ScriptsLoader lädt unsere DLL im Titelbildschirm und ruft für jeden Typ
    /// <c>type.GetCustomAttributes(false)</c> auf. Dabei wird dieses Attribut instanziiert,
    /// und der Konstruktor startet die Mod. So brauchen wir keinen Memoria-Fork und belegen
    /// keine der exklusiven IOverload*-Schnittstellen, die sich mit anderen Mods
    /// (z. B. AlternateFantasy) beißen könnten.
    ///
    /// WICHTIG: In dieser Datei keine Harmony-Typen verwenden. 0Harmony.dll liegt im Mod-Ordner
    /// und ist erst nach dem Registrieren des AssemblyResolve-Handlers auflösbar.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class CoopBootstrapAttribute : Attribute
    {
        public CoopBootstrapAttribute()
        {
            Bootstrap.Run();
        }
    }

    [CoopBootstrap]
    internal sealed class BootstrapAnchor
    {
    }

    internal static class Bootstrap
    {
        private static readonly Object s_lock = new Object();
        private static Boolean s_started;

        public static String ModDirectory { get; private set; }
        public static String ScriptsDirectory { get; private set; }

        public static void Run()
        {
            lock (s_lock)
            {
                if (s_started)
                    return;
                s_started = true;
            }

            try
            {
                ScriptsDirectory = Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location);
                // <Mod>/StreamingAssets/Scripts -> <Mod>
                ModDirectory = Path.GetFullPath(Path.Combine(ScriptsDirectory, Path.Combine("..", "..")));
                AppDomain.CurrentDomain.AssemblyResolve += ResolveFromModFolder;
                StartMod();
            }
            catch (Exception ex)
            {
                CoopLog.Error(ex, "Startup failed");
            }
        }

        // Eigene Methode, damit der JIT Harmony-Referenzen erst nach dem Registrieren des Resolvers auflöst.
        private static void StartMod()
        {
            CoopMod.Initialize();
        }

        private static Assembly ResolveFromModFolder(Object sender, ResolveEventArgs args)
        {
            String name = new AssemblyName(args.Name).Name;
            String path = Path.Combine(ScriptsDirectory, name + ".dll");
            if (!File.Exists(path))
                return null;
            CoopLog.Message($"Loading dependency {name} from mod folder");
            return Assembly.LoadFile(path);
        }
    }
}
