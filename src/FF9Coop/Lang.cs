using System;
using Memoria.Assets;

namespace FF9Coop
{
    /// <summary>
    /// Display language of the mod: English (default) or German.
    /// "Auto" follows the game language (German if the game runs in German, otherwise English).
    /// Usage: Lang.T("English text", "Deutscher Text").
    /// </summary>
    internal static class Lang
    {
        public const Int32 Auto = 0;
        public const Int32 English = 1;
        public const Int32 German = 2;

        public static Boolean IsGerman
        {
            get
            {
                Int32 mode = CoopMod.Config.Language;
                if (mode == English)
                    return false;
                if (mode == German)
                    return true;
                try { return Localization.CurrentSymbol == "GR"; }
                catch (Exception) { return false; } // language system not ready yet (title screen)
            }
        }

        public static String T(String english, String german) => IsGerman ? german : english;

        public static String ToIni(Int32 mode) => mode == English ? "English" : mode == German ? "German" : "Auto";

        public static Int32 Parse(String value)
        {
            if (String.IsNullOrEmpty(value))
                return Auto;
            if (value.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                return English;
            if (value.StartsWith("de", StringComparison.OrdinalIgnoreCase) || value.StartsWith("ger", StringComparison.OrdinalIgnoreCase))
                return German;
            return Auto;
        }
    }
}
