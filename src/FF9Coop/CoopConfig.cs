using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Memoria.Data;
using UnityEngine;

namespace FF9Coop
{
    internal enum MenuControlMode
    {
        Anyone,
        Leader
    }

    /// <summary>Einstellungen aus &lt;Mod&gt;/FF9Coop.ini. Fehlende Werte fallen auf Defaults zurück.</summary>
    internal sealed class CoopConfig
    {
        public const Int32 MaxPlayers = 4;
        public const Int32 MaxPads = 4;

        public Boolean Enabled = true;
        public Int32 PlayerCount = 2;
        public Int32 LeaderPlayer = 1;
        public Int32 KeyboardPlayer = 1;
        /// <summary>XInput-Slot (0–3) → Spieler (1–4).</summary>
        public readonly Int32[] PadPlayer = { 1, 2, 1, 2 };

        public MenuControlMode MenuControl = MenuControlMode.Anyone;
        public Boolean BattleTurnRequest = true;
        public Boolean GateKeyboard = true;
        public Boolean ShowHud = true;

        /// <summary>Tastatur-Taste für "Zug anfordern" im Kampf (Pad: Y).</summary>
        public KeyCode TurnRequestKey = KeyCode.T;
        /// <summary>Tastatur-Taste zum Übernehmen der Leader-Rolle, gehalten (Pad: Back/Select gehalten).</summary>
        public KeyCode LeaderKey = KeyCode.L;
        public Single LeaderHoldSeconds = 1f;

        /// <summary>Charakter → Spieler (1–4); 0 oder fehlend = automatisch nach Kampf-Slot.</summary>
        public readonly Dictionary<CharacterId, Int32> Owners = new Dictionary<CharacterId, Int32>();

        public static CoopConfig Load(String path)
        {
            CoopConfig cfg = new CoopConfig();
            if (!File.Exists(path))
            {
                CoopLog.Warning($"Keine Konfiguration gefunden ({path}), nutze Standardwerte.");
                return cfg;
            }

            Dictionary<String, Dictionary<String, String>> ini = ParseIni(File.ReadAllLines(path));

            cfg.Enabled = GetBool(ini, "Main", "Enabled", cfg.Enabled);
            cfg.PlayerCount = Clamp(GetInt(ini, "Players", "Count", cfg.PlayerCount), 1, MaxPlayers);
            cfg.LeaderPlayer = Clamp(GetInt(ini, "Players", "Leader", cfg.LeaderPlayer), 1, cfg.PlayerCount);
            cfg.KeyboardPlayer = Clamp(GetInt(ini, "Players", "Keyboard", cfg.KeyboardPlayer), 1, cfg.PlayerCount);
            for (Int32 i = 0; i < MaxPads; i++)
                cfg.PadPlayer[i] = Clamp(GetInt(ini, "Players", "Pad" + (i + 1), cfg.PadPlayer[i]), 1, cfg.PlayerCount);

            String menu = GetString(ini, "Control", "MenuControl", "Anyone");
            cfg.MenuControl = String.Equals(menu, "Leader", StringComparison.OrdinalIgnoreCase) ? MenuControlMode.Leader : MenuControlMode.Anyone;
            cfg.BattleTurnRequest = GetBool(ini, "Control", "BattleTurnRequest", cfg.BattleTurnRequest);
            cfg.GateKeyboard = GetBool(ini, "Control", "GateKeyboard", cfg.GateKeyboard);
            cfg.ShowHud = GetBool(ini, "Control", "ShowHud", cfg.ShowHud);
            cfg.TurnRequestKey = GetKey(ini, "Control", "TurnRequestKey", cfg.TurnRequestKey);
            cfg.LeaderKey = GetKey(ini, "Control", "LeaderKey", cfg.LeaderKey);
            if (Single.TryParse(GetString(ini, "Control", "LeaderHoldSeconds", null), NumberStyles.Float, CultureInfo.InvariantCulture, out Single hold))
                cfg.LeaderHoldSeconds = Math.Max(0f, hold);

            if (ini.TryGetValue("owners", out Dictionary<String, String> owners))
            {
                foreach (KeyValuePair<String, String> kv in owners)
                {
                    CharacterId id;
                    try { id = (CharacterId)Enum.Parse(typeof(CharacterId), kv.Key, true); }
                    catch { CoopLog.Warning($"[Owners] Unbekannter Charakter '{kv.Key}'"); continue; }
                    if (Int32.TryParse(kv.Value, out Int32 player) && player >= 1 && player <= cfg.PlayerCount)
                        cfg.Owners[id] = player;
                }
            }
            return cfg;
        }

        public override String ToString()
        {
            return $"Spieler={PlayerCount} Leader=P{LeaderPlayer} Tastatur=P{KeyboardPlayer} " +
                   $"Pads=[P{PadPlayer[0]},P{PadPlayer[1]},P{PadPlayer[2]},P{PadPlayer[3]}] Menüs={MenuControl} " +
                   $"Zuweisungen={Owners.Count}";
        }

        // ---- Mini-INI-Parser (Sektionen/Schlüssel case-insensitive, ';' und '#' sind Kommentare) ----

        private static Dictionary<String, Dictionary<String, String>> ParseIni(String[] lines)
        {
            var result = new Dictionary<String, Dictionary<String, String>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<String, String> section = null;
            foreach (String raw in lines)
            {
                String line = raw.Trim();
                Int32 comment = line.IndexOfAny(new[] { ';', '#' });
                if (comment >= 0)
                    line = line.Substring(0, comment).Trim();
                if (line.Length == 0)
                    continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    String name = line.Substring(1, line.Length - 2).Trim();
                    if (!result.TryGetValue(name, out section))
                        result[name] = section = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                Int32 eq = line.IndexOf('=');
                if (eq <= 0 || section == null)
                    continue;
                section[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Trim('"');
            }
            return result;
        }

        private static String GetString(Dictionary<String, Dictionary<String, String>> ini, String sec, String key, String def)
        {
            return ini.TryGetValue(sec, out var s) && s.TryGetValue(key, out String v) ? v : def;
        }

        private static Int32 GetInt(Dictionary<String, Dictionary<String, String>> ini, String sec, String key, Int32 def)
        {
            return Int32.TryParse(GetString(ini, sec, key, null), out Int32 v) ? v : def;
        }

        private static Boolean GetBool(Dictionary<String, Dictionary<String, String>> ini, String sec, String key, Boolean def)
        {
            String v = GetString(ini, sec, key, null);
            if (v == null) return def;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static KeyCode GetKey(Dictionary<String, Dictionary<String, String>> ini, String sec, String key, KeyCode def)
        {
            String v = GetString(ini, sec, key, null);
            if (String.IsNullOrEmpty(v))
                return def;
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), v, true); }
            catch { CoopLog.Warning($"[{sec}] {key}: unbekannte Taste '{v}', nutze {def}"); return def; }
        }

        private static Int32 Clamp(Int32 v, Int32 min, Int32 max) => v < min ? min : (v > max ? max : v);
    }
}
