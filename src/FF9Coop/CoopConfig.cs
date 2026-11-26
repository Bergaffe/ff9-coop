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
        /// <summary>Display language: Lang.Auto / Lang.English / Lang.German.</summary>
        public Int32 Language = FF9Coop.Lang.Auto;
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

        /// <summary>Sichtbare Begleiter-Figur im Feld für den Nicht-Leader.</summary>
        public Boolean CompanionEnabled = true;
        /// <summary>Bevorzugter Begleiter; NONE = automatisch (erstes Party-Mitglied außer dem Leader).</summary>
        public CharacterId CompanionCharacter = CharacterId.NONE;
        /// <summary>Maximaler Abstand zum Leader, danach wird der Begleiter zurückgeholt (Feld-Einheiten).</summary>
        public Single CompanionLeash = 2000f;
        public KeyCode CompanionRunKey = KeyCode.LeftShift;
        /// <summary>ATB anhalten, solange ein Spieler im Kommandomenü wählt.</summary>
        public Boolean BattlePauseWhileChoosing = false;
        /// <summary>Kurze Vibration am Controller des Besitzers, wenn sein Charakter bereit ist.</summary>
        public Boolean ReadySignal = true;
        /// <summary>P1/P2-Markierung neben den Namen in der Kampf-Gruppenanzeige.</summary>
        public Boolean BattleMarkers = true;
        /// <summary>"Ich bin hier"-Hand je Spieler: 0 = Original, 1 = beim Betreten (wie im Spiel), 2 = immer.</summary>
        public Int32 PlayerHands = Ui.PlayerMarkers.ModeOnEnter;

        /// <summary>"Wer spielt …?"-Fenster, wenn das Spiel die gesteuerte Figur wechselt.</summary>
        public Int32 SoloSceneMode = SoloAsk;

        /// <summary>Solo-Abschnitte: Fenster "Wer spielt …?" zeigen.</summary>
        public const Int32 SoloAsk = -1;
        /// <summary>Solo-Abschnitte: der aktuelle Leader spielt weiter, ohne Nachfrage. Werte 1–4 = fester Spieler.</summary>
        public const Int32 SoloKeepLeader = 0;
        /// <summary>Begleiter auch auf der Weltkarte (nur zu Fuß).</summary>
        public Boolean WorldCompanionEnabled = true;
        /// <summary>Leine auf der Weltkarte in Welt-Einheiten (ein Gelände-Block = 64).</summary>
        public Single WorldLeash = 12f;

        /// <summary>Öffnet das Koop-Menü (Controller: L3+R3).</summary>
        public KeyCode MenuKey = KeyCode.K;

        /// <summary>Charakter → Spieler (1–4); 0 oder fehlend = automatisch nach Kampf-Slot.</summary>
        public readonly Dictionary<CharacterId, Int32> Owners = new Dictionary<CharacterId, Int32>();

        public static CoopConfig Load(String path)
        {
            CoopConfig cfg = new CoopConfig();
            if (!File.Exists(path))
            {
                CoopLog.Warning($"No configuration found ({path}), using defaults.");
                return cfg;
            }

            Dictionary<String, Dictionary<String, String>> ini = ParseIni(File.ReadAllLines(path));

            cfg.Enabled = GetBool(ini, "Main", "Enabled", cfg.Enabled);
            cfg.Language = FF9Coop.Lang.Parse(GetString(ini, "Main", "Language", "Auto"));
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

            cfg.CompanionEnabled = GetBool(ini, "Companion", "Enabled", cfg.CompanionEnabled);
            String comp = GetString(ini, "Companion", "Character", "Auto");
            if (!String.Equals(comp, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                try { cfg.CompanionCharacter = (CharacterId)Enum.Parse(typeof(CharacterId), comp, true); }
                catch { CoopLog.Warning($"[Companion] Character: unknown '{comp}', using Auto"); }
            }
            if (Single.TryParse(GetString(ini, "Companion", "Leash", null), NumberStyles.Float, CultureInfo.InvariantCulture, out Single leash))
                cfg.CompanionLeash = Math.Max(200f, leash);
            cfg.CompanionRunKey = GetKey(ini, "Companion", "RunKey", cfg.CompanionRunKey);
            cfg.MenuKey = GetKey(ini, "Control", "MenuKey", cfg.MenuKey);
            cfg.WorldCompanionEnabled = GetBool(ini, "Companion", "WorldMap", cfg.WorldCompanionEnabled);
            cfg.BattlePauseWhileChoosing = GetBool(ini, "Control", "BattlePause", cfg.BattlePauseWhileChoosing);
            cfg.ReadySignal = GetBool(ini, "Control", "ReadySignal", cfg.ReadySignal);
            cfg.BattleMarkers = GetBool(ini, "Control", "BattleMarkers", cfg.BattleMarkers);
            cfg.PlayerHands = Clamp(GetInt(ini, "Control", "PlayerHands", cfg.PlayerHands), 0, 2);
            cfg.SoloSceneMode = ParseSoloMode(GetString(ini, "Control", "SoloScenePrompt", null), cfg.PlayerCount);
            if (Single.TryParse(GetString(ini, "Companion", "WorldLeash", null), NumberStyles.Float, CultureInfo.InvariantCulture, out Single wleash))
                cfg.WorldLeash = Math.Max(2f, wleash);

            // Zusätzliche/abweichende Begleiter-Modelle, z. B. für Charaktere aus anderen Mods
            if (ini.TryGetValue("CompanionModels", out Dictionary<String, String> models))
            {
                foreach (KeyValuePair<String, String> kv in models)
                {
                    try
                    {
                        CharacterId id = (CharacterId)Enum.Parse(typeof(CharacterId), kv.Key, true);
                        if (kv.Value.StartsWith("GEO_", StringComparison.OrdinalIgnoreCase))
                            Game.Companion.SetModel(id, kv.Value.ToUpperInvariant());
                    }
                    catch { CoopLog.Warning($"[CompanionModels] Unknown character '{kv.Key}'"); }
                }
            }

            if (ini.TryGetValue("owners", out Dictionary<String, String> owners))
            {
                foreach (KeyValuePair<String, String> kv in owners)
                {
                    CharacterId id;
                    try { id = (CharacterId)Enum.Parse(typeof(CharacterId), kv.Key, true); }
                    catch { CoopLog.Warning($"[Owners] Unknown character '{kv.Key}'"); continue; }
                    if (Int32.TryParse(kv.Value, out Int32 player) && player >= 1 && player <= cfg.PlayerCount)
                        cfg.Owners[id] = player;
                }
            }
            return cfg;
        }

        /// <summary>Tiefe Kopie (für "Abbrechen" im Menü).</summary>
        public CoopConfig Clone()
        {
            CoopConfig c = (CoopConfig)MemberwiseClone();
            CopyArraysInto(c);
            return c;
        }

        /// <summary>Übernimmt alle Werte aus <paramref name="src"/> in dieses Objekt (die Referenz bleibt gleich).</summary>
        public void CopyFrom(CoopConfig src)
        {
            foreach (System.Reflection.FieldInfo f in typeof(CoopConfig).GetFields())
                if (!f.IsInitOnly && !f.IsLiteral)
                    f.SetValue(this, f.GetValue(src));
            Array.Copy(src.PadPlayer, PadPlayer, MaxPads);
            Owners.Clear();
            foreach (KeyValuePair<CharacterId, Int32> kv in src.Owners)
                Owners[kv.Key] = kv.Value;
        }

        private void CopyArraysInto(CoopConfig c)
        {
            // readonly-Felder per Reflection neu setzen, damit die Kopie eigene Arrays/Dictionaries hat
            typeof(CoopConfig).GetField(nameof(PadPlayer)).SetValue(c, (Int32[])PadPlayer.Clone());
            typeof(CoopConfig).GetField(nameof(Owners)).SetValue(c, new Dictionary<CharacterId, Int32>(Owners));
        }

        /// <summary>Schreibt die im Menü änderbaren Werte in die INI. Kommentare und Reihenfolge bleiben erhalten.</summary>
        public void Save(String path)
        {
            List<String> lines = File.Exists(path) ? new List<String>(File.ReadAllLines(path)) : new List<String>();
            Set(lines, "Main", "Language", FF9Coop.Lang.ToIni(Language));
            Set(lines, "Players", "Count", PlayerCount.ToString());
            Set(lines, "Players", "Leader", LeaderPlayer.ToString());
            Set(lines, "Players", "Keyboard", KeyboardPlayer.ToString());
            for (Int32 i = 0; i < MaxPads; i++)
                Set(lines, "Players", "Pad" + (i + 1), PadPlayer[i].ToString());
            Set(lines, "Control", "MenuControl", MenuControl.ToString());
            Set(lines, "Control", "BattleTurnRequest", BattleTurnRequest ? "1" : "0");
            Set(lines, "Control", "BattlePause", BattlePauseWhileChoosing ? "1" : "0");
            Set(lines, "Control", "ReadySignal", ReadySignal ? "1" : "0");
            Set(lines, "Control", "BattleMarkers", BattleMarkers ? "1" : "0");
            Set(lines, "Control", "PlayerHands", PlayerHands.ToString());
            Set(lines, "Control", "SoloScenePrompt",
                SoloSceneMode == SoloAsk ? "Ask" : SoloSceneMode == SoloKeepLeader ? "Keep" : "P" + SoloSceneMode);
            Set(lines, "Companion", "Enabled", CompanionEnabled ? "1" : "0");
            Set(lines, "Companion", "Character", CompanionCharacter == CharacterId.NONE ? "Auto" : CompanionCharacter.ToString());
            Set(lines, "Companion", "WorldMap", WorldCompanionEnabled ? "1" : "0");
            foreach (CharacterId id in Enum.GetValues(typeof(CharacterId)))
            {
                if (id == CharacterId.NONE)
                    continue;
                Set(lines, "Owners", id.ToString(), Owners.TryGetValue(id, out Int32 p) ? p.ToString() : "0");
            }
            File.WriteAllText(path, String.Join("\r\n", lines.ToArray()) + "\r\n", new System.Text.UTF8Encoding(false));
        }

        private static void Set(List<String> lines, String section, String key, String value)
        {
            Int32 sectionLine = -1, end = lines.Count;
            for (Int32 i = 0; i < lines.Count; i++)
            {
                String t = lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    if (sectionLine >= 0) { end = i; break; }
                    if (String.Equals(t.Substring(1, t.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase))
                        sectionLine = i;
                }
            }
            if (sectionLine < 0)
            {
                lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(key + " = " + value);
                return;
            }
            for (Int32 i = sectionLine + 1; i < end; i++)
            {
                String t = lines[i].TrimStart();
                if (t.StartsWith(";") || t.StartsWith("#"))
                    continue;
                Int32 eq = t.IndexOf('=');
                if (eq > 0 && String.Equals(t.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = key + " = " + value;
                    return;
                }
            }
            // Schlüssel fehlt: vor eventuelle Leerzeilen am Sektionsende einfügen
            Int32 insert = end;
            while (insert > sectionLine + 1 && lines[insert - 1].Trim().Length == 0)
                insert--;
            lines.Insert(insert, key + " = " + value);
        }

        public override String ToString()
        {
            return $"Players={PlayerCount} Leader=P{LeaderPlayer} Keyboard=P{KeyboardPlayer} " +
                   $"Pads=[P{PadPlayer[0]},P{PadPlayer[1]},P{PadPlayer[2]},P{PadPlayer[3]}] Menus={MenuControl} Language={FF9Coop.Lang.ToIni(Language)} " +
                   $"Assignments={Owners.Count}";
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

        /// <summary>"Ask"/"1" = fragen, "Keep"/"0" = Leader bleibt, "P1".."P4" = fester Spieler.</summary>
        private static Int32 ParseSoloMode(String v, Int32 playerCount)
        {
            if (String.IsNullOrEmpty(v) || v == "1" || v.Equals("Ask", StringComparison.OrdinalIgnoreCase))
                return SoloAsk;
            if (v == "0" || v.Equals("Keep", StringComparison.OrdinalIgnoreCase))
                return SoloKeepLeader;
            if (v.Length == 2 && (v[0] == 'P' || v[0] == 'p') && Int32.TryParse(v.Substring(1), out Int32 p) && p >= 1 && p <= playerCount)
                return p;
            CoopLog.Warning($"[Control] SoloScenePrompt: unknown value '{v}', using Ask");
            return SoloAsk;
        }

        private static KeyCode GetKey(Dictionary<String, Dictionary<String, String>> ini, String sec, String key, KeyCode def)
        {
            String v = GetString(ini, sec, key, null);
            if (String.IsNullOrEmpty(v))
                return def;
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), v, true); }
            catch { CoopLog.Warning($"[{sec}] {key}: unknown key '{v}', using {def}"); return def; }
        }

        private static Int32 Clamp(Int32 v, Int32 min, Int32 max) => v < min ? min : (v > max ? max : v);
    }
}
