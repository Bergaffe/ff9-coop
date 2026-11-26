using System;
using System.Collections.Generic;
using System.IO;
using FF9Coop.Game;
using FF9Coop.Input;
using Memoria.Data;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Ui
{
    /// <summary>
    /// Koop-Einstellungen im FF9-Stil (Taste K / Controller L3+R3).
    ///
    /// Aufbau wie das Konfig-Menü von FF9: Titel-Fenster oben, Liste mit Hand-Cursor, Werte
    /// nebeneinander (aktiver Wert hell) bzw. mit Pfeilen bei langen Listen, Hilfe-Fenster unten.
    /// Änderungen wirken sofort; "Speichern" schreibt die FF9Coop.ini, "Abbrechen" stellt den
    /// Zustand vom Öffnen wieder her. Während das Menü offen ist, ist das Spiel pausiert
    /// (Time.timeScale = 0, wie im Pause-Menü des Spiels) und bekommt keine Eingaben.
    /// </summary>
    internal static class CoopMenu
    {
        private sealed class Row
        {
            public String Label;
            public String Help;
            public Boolean Header;
            public Func<String[]> Options;
            public Func<Int32> Get;
            public Action<Int32> Set;
            public Action Confirm;   // Aktions-Zeile (Speichern/Abbrechen)
            public Int32 Pad = -1;   // Controller-Zeile: Slot für die "drückt gerade"-Anzeige
            public Boolean Rebuild;  // changing the value rebuilds all rows (player count, language)
            public Boolean IsAction => Confirm != null;
            public Boolean Selectable => !Header;
        }

        private static readonly List<Row> s_rows = new List<Row>();
        private static Boolean s_open;
        private static Int32 s_cursor;
        private static Single s_scroll;
        private static CoopConfig s_snapshot;
        private static Single s_prevTimeScale = 1f;
        private static Single s_openedAt;

        // Wiederholung beim Halten einer Richtung
        private static Int32 s_heldDir;
        private static Single s_nextRepeat;
        private static Boolean s_comboWasDown;

        public static Boolean IsOpen => s_open;

        // ---------------- Öffnen / Schließen ----------------

        public static void Update()
        {
            CoopConfig cfg = CoopMod.Config;
            Boolean combo = ComboDown();
            Boolean comboPressed = combo && !s_comboWasDown;
            s_comboWasDown = combo;

            if (!s_open)
            {
                if (UnityEngine.Input.GetKeyDown(cfg.MenuKey) || comboPressed)
                    Open();
                return;
            }

            // Das Menü kann sich nicht im selben Frame wieder schließen, in dem es aufging
            if (Time.realtimeSinceStartup - s_openedAt < 0.2f)
                return;
            if (UnityEngine.Input.GetKeyDown(cfg.MenuKey) || comboPressed)
            {
                SaveAndClose();
                return;
            }
            HandleInput();
        }

        private static void Open()
        {
            Ff9Skin.EnsureInitialized();
            s_snapshot = CoopMod.Config.Clone();
            BuildRows();
            s_cursor = NextSelectable(-1, +1);
            s_scroll = 0f;
            s_open = true;
            s_openedAt = Time.realtimeSinceStartup;
            s_prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            InputRouter.ForceRelease();
            FF9Sfx.FF9SFX_Play(103);
        }

        private static void Close()
        {
            s_open = false;
            Time.timeScale = s_prevTimeScale > 0f ? s_prevTimeScale : 1f;
            // Taste, mit der geschlossen wurde, darf nicht im Spiel ankommen
            InputRouter.ForceRelease();
        }

        private static void SaveAndClose()
        {
            try
            {
                CoopMod.Config.Save(Path.Combine(Bootstrap.ModDirectory, "FF9Coop.ini"));
                CoopLog.Message("Settings saved: " + CoopMod.Config);
                CoopBehaviour.Notify(Lang.T("Co-op settings saved", "Koop-Einstellungen gespeichert"));
            }
            catch (Exception ex)
            {
                CoopLog.Error(ex, "Saving FF9Coop.ini failed");
                CoopBehaviour.Notify(Lang.T("Saving failed (see Memoria.log)", "Speichern fehlgeschlagen (siehe Memoria.log)"));
            }
            FF9Sfx.FF9SFX_Play(103);
            Close();
        }

        private static void CancelAndClose()
        {
            if (s_snapshot != null)
                CoopMod.Config.CopyFrom(s_snapshot);
            FF9Sfx.FF9SFX_Play(101);
            CoopBehaviour.Notify(Lang.T("Changes discarded", "Änderungen verworfen"));
            Close();
        }

        // ---------------- Zeilen ----------------

        private static String[] PlayerOptions()
        {
            Int32 n = CoopMod.Config.PlayerCount;
            String[] o = new String[n];
            for (Int32 i = 0; i < n; i++)
                o[i] = "P" + (i + 1);
            return o;
        }

        private static void BuildRows()
        {
            s_rows.Clear();
            CoopConfig cfg = CoopMod.Config;

            Header(Lang.T("Players", "Spieler"));
            Row language = Add("Language / Sprache", Lang.T(
                    "Language of the co-op menu and messages. \"Auto\" follows the game language.",
                    "Sprache von Koop-Menü und Meldungen. „Auto“ richtet sich nach der Spielsprache."),
                () => new[] { "Auto", "English", "Deutsch" }, () => cfg.Language, v => cfg.Language = v);
            language.Rebuild = true;
            Row count = Add(Lang.T("Number of players", "Anzahl Spieler"), Lang.T("How many players play together.", "Wie viele Spieler gemeinsam spielen."),
                () => new[] { "1", "2", "3", "4" }, () => cfg.PlayerCount - 1, v => SetPlayerCount(v + 1));
            count.Rebuild = true; // option lists depend on the player count
            Add("Leader", Lang.T(
                    "Controls the main character in towns, dungeons and on the world map and triggers story events.",
                    "Steuert die Hauptfigur in Städten, Dungeons und auf der Weltkarte und löst Story-Ereignisse aus."),
                PlayerOptions, () => cfg.LeaderPlayer - 1, v => cfg.LeaderPlayer = v + 1);
            Add(Lang.T("Menus", "Menüs"), Lang.T("Who may use the main menu, shops and the like.", "Wer das Hauptmenü, Shops und Ähnliches bedienen darf."),
                () => new[] { Lang.T("Everyone", "Alle"), "Leader" }, () => cfg.MenuControl == MenuControlMode.Leader ? 1 : 0,
                v => cfg.MenuControl = v == 1 ? MenuControlMode.Leader : MenuControlMode.Anyone);
            Add(Lang.T("Solo sections", "Solo-Abschnitte"), Lang.T(
                    "Who plays when the game only lets you control one character (e.g. Vivi alone in Alexandria). \"Ask\": a window, whoever presses A first takes over. \"Keep leader\": continue without asking. P1/P2…: this player always takes over.",
                    "Wer spielt, wenn das Spiel nur eine Figur steuern lässt (z. B. Vivi allein in Alexandria). „Fragen“: Fenster, wer zuerst A drückt, übernimmt. „Leader bleibt“: ohne Nachfrage weiter. P1/P2…: dieser Spieler übernimmt immer."),
                () =>
                {
                    String[] po = PlayerOptions();
                    String[] o = new String[po.Length + 2];
                    o[0] = Lang.T("Ask", "Fragen");
                    o[1] = Lang.T("Keep leader", "Leader bleibt");
                    Array.Copy(po, 0, o, 2, po.Length);
                    return o;
                },
                // Optionsindex: 0 = Fragen (-1), 1 = Leader bleibt (0), 2.. = P1..
                () => cfg.SoloSceneMode == CoopConfig.SoloAsk ? 0 : cfg.SoloSceneMode + 1,
                v => cfg.SoloSceneMode = v == 0 ? CoopConfig.SoloAsk : v - 1);

            Header(Lang.T("Input devices", "Eingabegeräte"));
            Add(Lang.T("Keyboard", "Tastatur"), Lang.T("Which player uses the keyboard.", "Welcher Spieler die Tastatur benutzt."),
                PlayerOptions, () => cfg.KeyboardPlayer - 1, v => cfg.KeyboardPlayer = v + 1);
            for (Int32 i = 0; i < CoopConfig.MaxPads; i++)
            {
                Int32 pad = i;
                Row r = Add($"Controller {pad + 1}", Lang.T(
                        "Which player uses this controller. Press a button on a controller to identify it.",
                        "Welcher Spieler diesen Controller benutzt. Drück eine Taste am Controller, um ihn zu erkennen."),
                    PlayerOptions, () => cfg.PadPlayer[pad] - 1, v => cfg.PadPlayer[pad] = v + 1);
                r.Pad = pad;
            }

            Header(Lang.T("Battle: who controls whom", "Kampf: Wer steuert wen"));
            PLAYER[] members = FF9StateSystem.Common.FF9?.party?.member ?? new PLAYER[0];
            Boolean anyMember = false;
            for (Int32 i = 0; i < members.Length; i++)
            {
                PLAYER p = members[i];
                if (p == null)
                    continue;
                anyMember = true;
                CharacterId id = p.Index;
                Int32 slot = i; // eigene Kopie pro Zeile, sonst teilen sich alle Lambdas die Schleifenvariable
                Add(CharacterName(id), Lang.T(
                        "Whose controller operates this character's command menu in battle. \"Auto\" assigns by battle position.",
                        "Wessen Controller im Kampf das Kommandomenü dieses Charakters bedient. „Auto“ verteilt nach Kampfposition."),
                    () =>
                    {
                        String[] po = PlayerOptions();
                        String[] o = new String[po.Length + 1];
                        o[0] = $"Auto ({"P" + ((slot % CoopMod.Config.PlayerCount) + 1)})";
                        Array.Copy(po, 0, o, 1, po.Length);
                        return o;
                    },
                    () => cfg.Owners.TryGetValue(id, out Int32 owner) ? owner : 0,
                    v => { if (v == 0) cfg.Owners.Remove(id); else cfg.Owners[id] = v; });
            }
            if (!anyMember)
                Header(Lang.T("(no party loaded)", "(keine Party geladen)"));
            Func<String[]> onOff = () => new[] { Lang.T("On", "An"), Lang.T("Off", "Aus") };
            Add(Lang.T("Turn request", "Zug anfordern"), Lang.T(
                    "When one of your characters is ready, the waiting player takes it over with Y or T.",
                    "Ist ein eigener Charakter bereit, holt ihn der wartende Spieler mit Y bzw. T."),
                onOff, () => cfg.BattleTurnRequest ? 0 : 1, v => cfg.BattleTurnRequest = v == 0);
            Add(Lang.T("ATB while choosing", "ATB bei Befehlswahl"), Lang.T(
                    "\"Pause\": time stands still while a player is choosing in the command menu. Fair for coordinating, online delay and hard mods.",
                    "„Anhalten“: Die Zeit steht still, solange ein Spieler im Kommandomenü wählt. Fair bei Absprachen, Online-Verzögerung und schweren Mods."),
                () => new[] { Lang.T("As in game", "Wie im Spiel"), Lang.T("Pause", "Anhalten") }, () => cfg.BattlePauseWhileChoosing ? 1 : 0, v => cfg.BattlePauseWhileChoosing = v == 1);
            Add(Lang.T("Ready signal", "Bereit-Signal"), Lang.T(
                    "Short vibration on your own controller as soon as one of your characters is ready.",
                    "Kurze Vibration am eigenen Controller, sobald einer deiner Charaktere bereit ist."),
                () => new[] { "Vibration", Lang.T("Off", "Aus") }, () => cfg.ReadySignal ? 0 : 1, v => cfg.ReadySignal = v == 0);
            Add(Lang.T("P1/P2 markers", "P1/P2-Markierung"), Lang.T(
                    "Shows next to each name in battle which player controls the character.",
                    "Zeigt im Kampf neben jedem Namen, welcher Spieler den Charakter steuert."),
                onOff, () => cfg.BattleMarkers ? 0 : 1, v => cfg.BattleMarkers = v == 0);

            Header(Lang.T("Companion", "Begleiter"));
            Add(Lang.T("Companion", "Begleiter"), Lang.T(
                    "Second visible character, controlled by the player who is not the leader. Can talk to NPCs (A), open chests and shop.",
                    "Zweite sichtbare Figur, gesteuert vom Spieler, der nicht Leader ist. Kann NPCs ansprechen (A), Truhen öffnen und handeln."),
                onOff, () => cfg.CompanionEnabled ? 0 : 1, v => cfg.CompanionEnabled = v == 0);
            Add(Lang.T("Player hand", "Spieler-Hand"), Lang.T(
                    "The \"I'm here\" hand above the characters, coloured per player and with the player number. \"On enter\": like the game, shortly after a screen change. \"Original\": only the normal hand above the leader.",
                    "Die „Ich bin hier“-Hand über den Figuren, eingefärbt je Spieler und mit Spielernummer. „Beim Betreten“: wie im Spiel kurz nach einem Bildschirmwechsel. „Original“: nur die normale Hand über dem Leader."),
                () => new[] { "Original", Lang.T("On enter", "Beim Betreten"), Lang.T("Always", "Immer") }, () => cfg.PlayerHands, v => cfg.PlayerHands = v);
            Add(Lang.T("On the world map", "Auf der Weltkarte"), Lang.T(
                    "Show the companion on the world map too (on foot only, not on chocobo or airship).",
                    "Begleiter auch auf der Weltkarte zeigen (nur zu Fuß, nicht auf Chocobo oder Luftschiff)."),
                onOff, () => cfg.WorldCompanionEnabled ? 0 : 1, v => cfg.WorldCompanionEnabled = v == 0);
            List<CharacterId> companions = new List<CharacterId> { CharacterId.NONE };
            foreach (PLAYER p in members)
                if (p != null && Game.Companion.TryGetModelCode(p.Index, out _))
                    companions.Add(p.Index);
            if (cfg.CompanionCharacter != CharacterId.NONE && !companions.Contains(cfg.CompanionCharacter))
                companions.Add(cfg.CompanionCharacter);
            Add(Lang.T("Companion character", "Begleiter-Figur"), Lang.T(
                    "Which party member accompanies the leader. \"Auto\" takes the first party member who is not the leader.",
                    "Welcher Charakter aus der Gruppe den Leader begleitet. „Auto“ nimmt das erste Gruppenmitglied, das nicht Leader ist."),
                () => companions.ConvertAll(c => c == CharacterId.NONE ? "Auto" : CharacterName(c)).ToArray(),
                () => Math.Max(0, companions.IndexOf(cfg.CompanionCharacter)),
                v => cfg.CompanionCharacter = companions[v]);

            Header("");
            Action(Lang.T("Save and close", "Speichern und schließen"), Lang.T(
                    "Apply the settings and save them to FF9Coop.ini. (Also: K or L3+R3)",
                    "Einstellungen übernehmen und in FF9Coop.ini speichern. (Auch: K bzw. L3+R3)"), SaveAndClose);
            Action(Lang.T("Cancel", "Abbrechen"), Lang.T(
                    "Discard all changes since opening. (Also: B or Esc)",
                    "Alle Änderungen seit dem Öffnen verwerfen. (Auch: B bzw. Esc)"), CancelAndClose);
        }

        private static void SetPlayerCount(Int32 n)
        {
            CoopConfig cfg = CoopMod.Config;
            cfg.PlayerCount = n;
            cfg.LeaderPlayer = Math.Min(cfg.LeaderPlayer, n);
            cfg.KeyboardPlayer = Math.Min(cfg.KeyboardPlayer, n);
            if (cfg.SoloSceneMode > n)
                cfg.SoloSceneMode = CoopConfig.SoloAsk;
            for (Int32 i = 0; i < CoopConfig.MaxPads; i++)
                cfg.PadPlayer[i] = Math.Min(cfg.PadPlayer[i], n);
            foreach (CharacterId id in new List<CharacterId>(cfg.Owners.Keys))
                if (cfg.Owners[id] > n)
                    cfg.Owners.Remove(id);
        }

        internal static String CharacterName(CharacterId id)
        {
            try
            {
                PLAYER p = FF9StateSystem.Common.FF9.GetPlayer(id);
                if (p != null && !String.IsNullOrEmpty(p.Name))
                    return p.Name;
            }
            catch (Exception)
            {
                // Charakter (noch) nicht verfügbar
            }
            return id.ToString();
        }

        private static void Header(String text) => s_rows.Add(new Row { Label = text, Header = true });

        private static Row Add(String label, String help, Func<String[]> options, Func<Int32> get, Action<Int32> set)
        {
            Row r = new Row { Label = label, Help = help, Options = options, Get = get, Set = set };
            s_rows.Add(r);
            return r;
        }

        private static void Action(String label, String help, Action confirm) =>
            s_rows.Add(new Row { Label = label, Help = help, Confirm = confirm });

        // ---------------- Eingabe ----------------

        /// <summary>L3+R3 auf irgendeinem Controller.</summary>
        private static Boolean ComboDown()
        {
            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (!PadInput.IsConnected(pad))
                    continue;
                GamePadButtons b = PadInput.Current(pad).Buttons;
                if (b.LeftStick == ButtonState.Pressed && b.RightStick == ButtonState.Pressed)
                    return true;
            }
            return false;
        }

        private static void HandleInput()
        {
            // Richtungen: alle Controller + Tastatur (roh; das Spiel bekommt währenddessen nichts)
            Int32 dirY = 0, dirX = 0;
            Boolean confirm = false, cancel = false;
            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (!PadInput.IsConnected(pad))
                    continue;
                GamePadState s = PadInput.Current(pad), prev = PadInput.Previous(pad);
                if (s.DPad.Up == ButtonState.Pressed || s.ThumbSticks.Left.Y > 0.6f) dirY = -1;
                if (s.DPad.Down == ButtonState.Pressed || s.ThumbSticks.Left.Y < -0.6f) dirY = 1;
                if (s.DPad.Left == ButtonState.Pressed || s.ThumbSticks.Left.X < -0.6f) dirX = -1;
                if (s.DPad.Right == ButtonState.Pressed || s.ThumbSticks.Left.X > 0.6f) dirX = 1;
                confirm |= s.Buttons.A == ButtonState.Pressed && prev.Buttons.A != ButtonState.Pressed;
                cancel |= s.Buttons.B == ButtonState.Pressed && prev.Buttons.B != ButtonState.Pressed;
            }
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow) || UnityEngine.Input.GetKey(KeyCode.W)) dirY = -1;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow) || UnityEngine.Input.GetKey(KeyCode.S)) dirY = 1;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.A)) dirX = -1;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow) || UnityEngine.Input.GetKey(KeyCode.D)) dirX = 1;
            confirm |= UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) || UnityEngine.Input.GetKeyDown(KeyCode.Space);
            cancel |= UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.Backspace);

            if (cancel)
            {
                CancelAndClose();
                return;
            }

            Int32 dir = dirY != 0 ? dirY * 10 : dirX; // ±10 = hoch/runter, ±1 = links/rechts
            if (dir != 0 && Repeat(dir))
            {
                if (dirY != 0)
                {
                    Int32 next = NextSelectable(s_cursor, dirY);
                    if (next != s_cursor)
                    {
                        s_cursor = next;
                        FF9Sfx.FF9SFX_Play(103);
                    }
                }
                else
                {
                    ChangeValue(dirX);
                }
            }
            if (dir == 0)
                s_heldDir = 0;

            if (confirm)
            {
                Row r = s_rows[s_cursor];
                if (r.IsAction)
                    r.Confirm();
                else
                    ChangeValue(+1);
            }
        }

        /// <summary>Sofort beim Drücken, dann nach 0,35 s alle 0,1 s.</summary>
        private static Boolean Repeat(Int32 dir)
        {
            Single now = Time.realtimeSinceStartup;
            if (dir != s_heldDir)
            {
                s_heldDir = dir;
                s_nextRepeat = now + 0.35f;
                return true;
            }
            if (now >= s_nextRepeat)
            {
                s_nextRepeat = now + 0.1f;
                return true;
            }
            return false;
        }

        private static void ChangeValue(Int32 delta)
        {
            Row r = s_rows[s_cursor];
            if (r.Options == null)
                return;
            Int32 count = r.Options().Length;
            if (count <= 1)
            {
                FF9Sfx.FF9SFX_Play(102);
                return;
            }
            Int32 v = ((r.Get() + delta) % count + count) % count;
            r.Set(v);
            FF9Sfx.FF9SFX_Play(103);
            if (r.Rebuild)
            {
                Int32 keep = s_cursor;
                BuildRows(); // option lists / texts depend on this value
                s_cursor = keep;
            }
        }

        private static Int32 NextSelectable(Int32 from, Int32 dir)
        {
            for (Int32 i = from + dir; i >= 0 && i < s_rows.Count; i += dir)
                if (s_rows[i].Selectable)
                    return i;
            return from < 0 ? 0 : from;
        }

        // ---------------- Zeichnen ----------------

        public static void Draw()
        {
            if (!s_open)
                return;
            Ff9Skin.EnsureInitialized();
            Single s = Ff9Skin.Scale;

            // Leichte Abdunkelung des Spiels dahinter
            Ff9Skin.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f));

            Single width = Math.Min(Screen.width - 80f * s, 1240f * s);
            Single left = (Screen.width - width) / 2f;
            Single rowH = 52f * s;
            Single pad = 28f * s;

            // Titel-Fenster (wie "Konfig." oben links im FF9-Menü)
            Rect title = new Rect(left, 40f * s, 420f * s, 76f * s);
            Ff9Skin.Window(title);
            Ff9Skin.Label(Ff9Skin.Inset(title, pad * 0.8f), Lang.T("Co-op Settings", "Koop-Einstellungen"), Ff9Skin.TextColor, 1.05f);

            // Hilfe-Fenster unten
            Single helpH = 120f * s;
            Rect help = new Rect(left, Screen.height - helpH - 40f * s, width, helpH);

            // Liste
            Rect list = new Rect(left, title.yMax + 12f * s, width, help.y - title.yMax - 24f * s);
            Ff9Skin.Window(list);
            Rect content = new Rect(list.x + pad + 40f * s, list.y + pad * 0.7f, list.width - 2f * pad - 40f * s, list.height - pad * 1.4f);

            // Scrollen: Cursor-Zeile im sichtbaren Bereich halten
            Single cursorTop = s_cursor * rowH;
            if (cursorTop - s_scroll > content.height - rowH * 1.5f)
                s_scroll = cursorTop - content.height + rowH * 1.5f;
            if (cursorTop - s_scroll < rowH * 0.5f)
                s_scroll = Math.Max(0f, cursorTop - rowH * 0.5f);

            GUI.BeginGroup(content);
            Single labelW = content.width * 0.42f;
            for (Int32 i = 0; i < s_rows.Count; i++)
            {
                Row r = s_rows[i];
                Single y = i * rowH - s_scroll;
                if (y < -rowH || y > content.height)
                    continue;
                Rect rowRect = new Rect(0f, y, content.width, rowH);

                if (r.Header)
                {
                    if (r.Label.Length > 0)
                    {
                        Ff9Skin.Label(new Rect(0f, y, content.width, rowH), r.Label, Ff9Skin.HeaderColor, 0.85f);
                        Ff9Skin.Fill(new Rect(0f, y + rowH * 0.86f, content.width, Math.Max(1f, 2f * s)), new Color(0.75f, 0.8f, 1f, 0.35f));
                    }
                    continue;
                }

                Boolean selected = i == s_cursor;
                if (selected)
                    Ff9Skin.Highlight(new Rect(-12f * s, y + 3f * s, content.width + 12f * s, rowH - 6f * s));

                String label = r.Label;
                Color labelColor = Ff9Skin.TextColor;
                if (r.Pad >= 0)
                {
                    if (!PadInput.IsConnected(r.Pad))
                        labelColor = Ff9Skin.DimColor;
                    else if (PadInput.IsActive(PadInput.Current(r.Pad)))
                        label += "  ●";
                }
                Ff9Skin.Label(new Rect(24f * s, y, labelW, rowH), label, labelColor);

                if (r.Options != null)
                    DrawOptions(r, new Rect(labelW, y, content.width - labelW, rowH), selected);
            }
            GUI.EndGroup();

            // Hand-Cursor links neben der Zeile (außerhalb der Gruppe, damit er nicht abgeschnitten wird)
            Single cy = content.y + s_cursor * rowH - s_scroll + rowH / 2f;
            if (cy > content.y && cy < content.yMax)
                Ff9Skin.Hand(content.x + 14f * s, cy);

            // Hilfe
            Ff9Skin.Window(help);
            String helpText = s_cursor >= 0 && s_cursor < s_rows.Count ? s_rows[s_cursor].Help : "";
            if (s_rows[s_cursor].Pad >= 0 && !PadInput.IsConnected(s_rows[s_cursor].Pad))
                helpText += Lang.T(" (Not connected)", " (Nicht verbunden)");
            Rect helpInner = Ff9Skin.Inset(help, pad);
            Ff9Skin.Label(helpInner, Wrap(helpText, helpInner.width), Ff9Skin.TextColor, 0.8f, TextAnchor.UpperLeft);

            // Bedienhinweis rechts im Titel
            Ff9Skin.Label(new Rect(title.xMax + 20f * s, title.y, width - title.width - 20f * s, title.height),
                Lang.T("↑↓ Select   ←→ Change   A/Enter   B/Esc Cancel", "↑↓ Auswahl   ←→ Ändern   A/Enter   B/Esc Abbrechen"), Ff9Skin.DimColor, 0.7f, TextAnchor.MiddleRight);
        }

        private static void DrawOptions(Row r, Rect area, Boolean selected)
        {
            String[] options = r.Options();
            Int32 value = Mathf.Clamp(r.Get(), 0, options.Length - 1);
            Single s = Ff9Skin.Scale;

            // Kurze Listen nebeneinander (wie im FF9-Konfigmenü), lange mit Pfeilen
            Single total = 0f;
            foreach (String o in options)
                total += Ff9Skin.TextWidth(o) + 40f * s;
            if (total <= area.width && options.Length <= 5)
            {
                Single x = area.x;
                for (Int32 i = 0; i < options.Length; i++)
                {
                    Single w = Ff9Skin.TextWidth(options[i]) + 40f * s;
                    Boolean on = i == value;
                    Ff9Skin.Label(new Rect(x, area.y, w, area.height), options[i], on ? (selected ? Ff9Skin.AccentColor : Ff9Skin.TextColor) : Ff9Skin.DimColor);
                    x += w;
                }
            }
            else
            {
                String text = options[value];
                Single w = Ff9Skin.TextWidth(text);
                Single cy = area.y + area.height / 2f;
                if (selected)
                    Ff9Skin.ArrowLeft(area.x + 14f * s, cy);
                Ff9Skin.Label(new Rect(area.x + 40f * s, area.y, w + 10f, area.height), text, selected ? Ff9Skin.AccentColor : Ff9Skin.TextColor);
                if (selected)
                    Ff9Skin.ArrowRight(area.x + 40f * s + w + 26f * s, cy);
            }
        }

        /// <summary>Einfacher Zeilenumbruch, da der Skin-Text ohne WordWrap arbeitet (für den Schatten-Effekt).</summary>
        private static String Wrap(String text, Single width)
        {
            if (String.IsNullOrEmpty(text))
                return "";
            String[] words = text.Split(' ');
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            String line = "";
            foreach (String word in words)
            {
                String test = line.Length == 0 ? word : line + " " + word;
                if (Ff9Skin.TextWidth(test, 0.8f) > width && line.Length > 0)
                {
                    sb.Append(line).Append('\n');
                    line = word;
                }
                else
                {
                    line = test;
                }
            }
            sb.Append(line);
            return sb.ToString();
        }
    }
}
