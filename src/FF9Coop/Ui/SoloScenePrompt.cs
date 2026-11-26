using System;
using FF9Coop.Game;
using FF9Coop.Input;
using Memoria.Data;
using UnityEngine;
using XInputDotNetPure;

namespace FF9Coop.Ui
{
    /// <summary>
    /// "Wer spielt …?": Wechselt das Spiel per Skript die gesteuerte Figur (z. B. Zidane → Vivi in Alexandria,
    /// Steiner-/Freya-Abschnitte), erscheint ein kleines FF9-Fenster. Wer zuerst A (Tastatur: Bestätigen) drückt,
    /// wird Leader und spielt den Abschnitt. B/Esc oder 15 s ohne Eingabe: alles bleibt wie bisher.
    /// Das Spiel ist währenddessen pausiert und bekommt keine Eingaben (wie im Koop-Menü).
    /// </summary>
    internal static class SoloScenePrompt
    {
        private const Single TimeoutSeconds = 15f;

        private static String s_lastCode;
        private static Int32 s_lastMap = -1;
        private static Boolean s_open;
        private static String s_name;
        private static Single s_openedAt;
        private static Single s_prevTimeScale = 1f;

        public static Boolean IsOpen => s_open;

        public static void Update()
        {
            if (s_open)
            {
                HandleInput();
                return;
            }

            CoopConfig cfg = CoopMod.Config;
            UIManager ui = PersistenSingleton<UIManager>.Instance;
            EventEngine events = PersistenSingleton<EventEngine>.Instance;
            FieldMapActorController ctrl = events?.fieldmap?.playerController;
            if (ui == null || ui.State != UIManager.UIState.FieldHUD || ctrl?.originalActor == null)
                return;

            String code = Companion.ModelCodeOf(ctrl.originalActor);
            if (code == null)
                return;
            Int32 map = FF9StateSystem.Common.FF9.fldMapNo;

            // Erste Figur nach dem Laden/Start merken, ohne nachzufragen
            if (s_lastCode == null)
            {
                s_lastCode = code;
                s_lastMap = map;
                return;
            }
            if (code == s_lastCode)
                return;
            // Erst fragen, wenn der Spieler die neue Figur wirklich steuern soll (nicht mitten in der Cutscene)
            if (!events.GetUserControl())
                return;

            s_lastCode = code;
            s_lastMap = map;
            if (cfg.PlayerCount < 2 || cfg.SoloSceneMode == CoopConfig.SoloKeepLeader)
                return;
            if (cfg.SoloSceneMode == CoopConfig.SoloAsk)
            {
                Open(code);
                return;
            }

            // Fester Spieler aus dem Koop-Menü: ohne Fenster übernehmen
            s_name = NameForCode(code);
            Int32 fixedPlayer = Math.Min(cfg.SoloSceneMode, cfg.PlayerCount);
            if (fixedPlayer != cfg.LeaderPlayer)
            {
                cfg.LeaderPlayer = fixedPlayer;
                InputRouter.ForceRelease();
                CoopLog.Message($"Solo section: P{fixedPlayer} plays {s_name} (fixed setting)");
            }
            CoopBehaviour.Notify(Lang.T($"P{cfg.LeaderPlayer} plays {s_name}", $"P{cfg.LeaderPlayer} spielt {s_name}"));
        }

        private static void Open(String code)
        {
            s_name = NameForCode(code);
            s_open = true;
            s_openedAt = Time.realtimeSinceStartup;
            s_prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            InputRouter.ForceRelease();
            FF9Sfx.FF9SFX_Play(103);
            CoopLog.Message($"Solo section: who plays {s_name}?");
        }

        private static void Close(Int32 newLeader)
        {
            s_open = false;
            Time.timeScale = s_prevTimeScale > 0f ? s_prevTimeScale : 1f;
            InputRouter.ForceRelease();
            CoopConfig cfg = CoopMod.Config;
            if (newLeader > 0 && newLeader != cfg.LeaderPlayer)
            {
                cfg.LeaderPlayer = newLeader;
                CoopLog.Message($"P{newLeader} plays {s_name}");
            }
            CoopBehaviour.Notify(Lang.T($"P{cfg.LeaderPlayer} plays {s_name}", $"P{cfg.LeaderPlayer} spielt {s_name}"));
        }

        private static void HandleInput()
        {
            CoopConfig cfg = CoopMod.Config;
            Single age = Time.realtimeSinceStartup - s_openedAt;
            if (age < 0.3f)
                return; // gehaltene Tasten aus der Szene nicht sofort werten
            if (age > TimeoutSeconds)
            {
                Close(0);
                return;
            }

            for (Int32 pad = 0; pad < PadInput.PadCount; pad++)
            {
                if (!PadInput.IsConnected(pad))
                    continue;
                GamePadButtons now = PadInput.Current(pad).Buttons, before = PadInput.Previous(pad).Buttons;
                if (now.A == ButtonState.Pressed && before.A != ButtonState.Pressed)
                {
                    FF9Sfx.FF9SFX_Play(103);
                    Close(cfg.PadPlayer[pad]);
                    return;
                }
                if (now.B == ButtonState.Pressed && before.B != ButtonState.Pressed)
                {
                    FF9Sfx.FF9SFX_Play(101);
                    Close(0);
                    return;
                }
            }

            KeyCode[] keys = PersistenSingleton<HonoInputManager>.Instance?.InputKeysPrimary;
            Boolean kbConfirm = UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Space)
                                || (keys != null && keys.Length > 0 && UnityEngine.Input.GetKeyDown(keys[(Int32)Control.Confirm]));
            if (kbConfirm)
            {
                FF9Sfx.FF9SFX_Play(103);
                Close(cfg.KeyboardPlayer);
                return;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
            {
                FF9Sfx.FF9SFX_Play(101);
                Close(0);
            }
        }

        public static void Draw()
        {
            if (!s_open)
                return;
            Ff9Skin.EnsureInitialized();
            Single s = Ff9Skin.Scale;
            CoopConfig cfg = CoopMod.Config;

            Ff9Skin.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.3f));

            Single w = 900f * s, h = 300f * s;
            Rect win = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2.4f, w, h);
            Ff9Skin.Window(win);
            Rect inner = Ff9Skin.Inset(win, 32f * s);

            Single line = 56f * s;
            Ff9Skin.Label(new Rect(inner.x, inner.y, inner.width, line), Lang.T($"Who plays {s_name}?", $"Wer spielt {s_name}?"), Ff9Skin.TextColor, 1.15f, TextAnchor.MiddleCenter);
            Ff9Skin.Label(new Rect(inner.x, inner.y + line * 1.2f, inner.width, line),
                Lang.T("Whoever presses A first (keyboard: Enter) takes over this section.", "Wer zuerst A drückt (Tastatur: Enter), übernimmt diesen Abschnitt."), Ff9Skin.TextColor, 0.8f, TextAnchor.MiddleCenter);

            Single remaining = Mathf.Max(0f, TimeoutSeconds - (Time.realtimeSinceStartup - s_openedAt));
            Ff9Skin.Label(new Rect(inner.x, inner.y + line * 2.3f, inner.width, line),
                Lang.T($"B / Esc: continue with P{cfg.LeaderPlayer}   ({remaining:0} s)", $"B / Esc: weiter mit P{cfg.LeaderPlayer}   ({remaining:0} s)"), Ff9Skin.DimColor, 0.75f, TextAnchor.MiddleCenter);
        }

        private static String NameForCode(String code)
        {
            foreach (CharacterId id in Enum.GetValues(typeof(CharacterId)))
                if (Companion.TryGetModelCode(id, out String c) && c == code)
                    return CoopMenu.CharacterName(id);
            return code;
        }
    }
}
