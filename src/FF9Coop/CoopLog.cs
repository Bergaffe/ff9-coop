using System;
using Memoria.Prime;

namespace FF9Coop
{
    /// <summary>Schreibt mit Präfix [FF9Coop] in Memoria.log.</summary>
    internal static class CoopLog
    {
        // Memoria's Log nutzt String.Format, deshalb wird der Text als Argument übergeben (Klammern im Text sind sicher).
        public static void Message(String text) => Log.Message("[FF9Coop] {0}", text);
        public static void Warning(String text) => Log.Warning("[FF9Coop] {0}", text);
        public static void Error(Exception ex, String text) => Log.Error(ex, "[FF9Coop] {0}", text);
    }
}
