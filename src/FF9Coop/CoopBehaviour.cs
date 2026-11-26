using System;
using FF9Coop.Game;
using FF9Coop.Input;
using UnityEngine;

namespace FF9Coop
{
    /// <summary>Unsichtbares GameObject: Kampf-Logik pro Frame und kleines Status-HUD.</summary>
    internal sealed class CoopBehaviour : MonoBehaviour
    {
        private GUIStyle _style;
        private Boolean _updateErrorLogged;

        private void Update()
        {
            try
            {
                BattleTurnRequest.Update();
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
            if (!CoopMod.Config.ShowHud || !CoopMod.Config.Enabled)
                return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(12, Screen.height / 50) };
                _style.normal.textColor = Color.white;
            }

            String text = $"Koop · P{InputRouter.ActivePlayer}";
            if (CoopContext.Description.Length > 0)
                text += " · " + CoopContext.Description;
            if (InputRouter.WaitingForRelease)
                text += " (Tasten loslassen)";

            Single h = _style.fontSize * 1.8f;
            Rect rect = new Rect(8, 8, Screen.width * 0.5f, h);
            // Einfacher Schatten für Lesbarkeit auf hellen Moguri-Hintergründen
            Color old = _style.normal.textColor;
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _style);
            _style.normal.textColor = old;
            GUI.Label(rect, text, _style);
        }
    }
}
