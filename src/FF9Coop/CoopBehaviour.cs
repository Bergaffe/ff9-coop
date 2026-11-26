using System;
using FF9Coop.Game;
using FF9Coop.Input;
using UnityEngine;

namespace FF9Coop
{
    /// <summary>Unsichtbares GameObject: Koop-Logik pro Frame und kleines Status-HUD.</summary>
    internal sealed class CoopBehaviour : MonoBehaviour
    {
        private const Single NoticeSeconds = 3f;

        private static String s_notice;
        private static Single s_noticeUntil;

        private GUIStyle _style;
        private Texture2D _barTexture;
        private Boolean _updateErrorLogged;
        private Boolean _guiErrorLogged;

        /// <summary>Kurze Einblendung unter der Statuszeile.</summary>
        public static void Notify(String text)
        {
            s_notice = text;
            s_noticeUntil = Time.realtimeSinceStartup + NoticeSeconds;
        }

        private void Update()
        {
            try
            {
                Ui.CoopMenu.Update();
                if (Ui.CoopMenu.IsOpen)
                    return; // Spiel ist pausiert, die Koop-Logik ruht
                BattleTurnRequest.Update();
                LeaderControl.Update();
                Companion.Update(); // fängt eigene Fehler ab und schaltet sich nach 3 Fehlern ab
                InteractionOwner.Update();
                CompanionInteraction.Update();
            }
            catch (Exception ex)
            {
                if (!_updateErrorLogged)
                {
                    _updateErrorLogged = true;
                    CoopLog.Error(ex, "Fehler in CoopBehaviour.Update (weitere werden unterdrückt)");
                }
            }
        }

        private void OnGUI()
        {
            if (Ui.CoopMenu.IsOpen)
            {
                // Menü über allem anderen zeichnen
                GUI.depth = -1000;
                try { Ui.CoopMenu.Draw(); }
                catch (Exception ex)
                {
                    if (!_guiErrorLogged)
                    {
                        _guiErrorLogged = true;
                        CoopLog.Error(ex, "Fehler beim Zeichnen des Koop-Menüs");
                    }
                }
                return;
            }

            if (!CoopMod.Config.ShowHud || !CoopMod.Config.Enabled)
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(12, Screen.height / 50) };
                _style.normal.textColor = Color.white;
                _barTexture = new Texture2D(1, 1);
                _barTexture.SetPixel(0, 0, Color.white);
                _barTexture.Apply();
            }

            Single lineHeight = _style.fontSize * 1.6f;
            Single x = 8f, y = 8f, width = Screen.width * 0.6f;

            String text = $"Koop · P{InputRouter.ActivePlayer}";
            if (CoopContext.Description.Length > 0)
                text += " · " + CoopContext.Description;
            if (InputRouter.WaitingForRelease)
                text += " (Tasten loslassen)";
            ShadowLabel(new Rect(x, y, width, lineHeight), text);
            y += lineHeight;

            Single progress = LeaderControl.HoldProgress;
            if (progress > 0f)
            {
                ShadowLabel(new Rect(x, y, width, lineHeight), "Leader übernehmen …");
                y += lineHeight;
                Single barWidth = _style.fontSize * 10f;
                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(x, y, barWidth, _style.fontSize * 0.4f), _barTexture);
                GUI.color = new Color(1f, 0.85f, 0.3f, 1f);
                GUI.DrawTexture(new Rect(x, y, barWidth * progress, _style.fontSize * 0.4f), _barTexture);
                GUI.color = old;
                y += _style.fontSize * 0.8f;
            }

            if (s_notice != null && Time.realtimeSinceStartup < s_noticeUntil)
                ShadowLabel(new Rect(x, y, width, lineHeight), s_notice);
        }

        private void ShadowLabel(Rect rect, String text)
        {
            // Schatten für Lesbarkeit auf hellen Moguri-Hintergründen
            Color old = _style.normal.textColor;
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _style);
            _style.normal.textColor = old;
            GUI.Label(rect, text, _style);
        }
    }
}
