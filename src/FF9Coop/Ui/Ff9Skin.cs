using System;
using System.Collections.Generic;
using System.Text;
using Assets.Sources.Scripts.UI.Common;
using Memoria.Assets;
using UnityEngine;

namespace FF9Coop.Ui
{
    /// <summary>
    /// Zeichnet im FF9-Stil mit IMGUI, nutzt dabei die Original-Bausteine des Spiels:
    /// - Fenster-Atlas (Grau/Blau je nach FF9-Option "Fenster"), Rahmen "dialog_frame_info"
    /// - Hand-Cursor "cursor_hand_choice" und Pfeile aus dem Icon-Atlas
    /// - Spielschrift (EncryptFontManager, inkl. Memoria/Moguri-Ersatzschrift)
    /// Fehlt etwas (andere Version/Mod), wird ein ähnlich aussehender Ersatz gezeichnet.
    /// </summary>
    internal static class Ff9Skin
    {
        private static Boolean s_initialized;
        private static UIAtlas s_window;
        private static UIAtlas s_icon;
        private static UISpriteData s_frame, s_body, s_highlight, s_hand, s_arrowLeft, s_arrowRight;
        private static Texture2D s_fallbackBody, s_white;
        private static GUIStyle s_text;

        /// <summary>Skalierung: FF9-UI ist auf 1080 Pixel Höhe ausgelegt.</summary>
        public static Single Scale => Screen.height / 1080f;
        public static Single FontSize => 36f * Scale;

        public static readonly Color TextColor = new Color(1f, 1f, 1f, 1f);
        public static readonly Color DimColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        public static readonly Color HeaderColor = new Color(0.75f, 0.8f, 1f, 1f);
        public static readonly Color AccentColor = new Color(1f, 0.92f, 0.55f, 1f);

        public static void EnsureInitialized()
        {
            if (s_initialized)
                return;
            s_initialized = true;

            try { s_window = FF9UIDataTool.WindowAtlas; } catch (Exception ex) { CoopLog.Error(ex, "Window atlas could not be loaded"); }
            try { s_icon = FF9UIDataTool.IconAtlas; } catch (Exception ex) { CoopLog.Error(ex, "Icon atlas could not be loaded"); }

            s_frame = Find(s_window, "dialog_frame_info", "dialog_frame_chat");
            s_body = Find(s_window, "dialog_bg", "dialog_body", "dialog_base", "window_bg", "dialog_back");
            if (s_body == null)
                s_body = FindContaining(s_window, "dialog", "bg");
            s_highlight = Find(s_window, "dialog_hilight");
            s_hand = Find(s_icon, "cursor_hand_choice");
            s_arrowLeft = Find(s_icon, "arrow_left");
            s_arrowRight = Find(s_icon, "arrow_right");

            CoopLog.Message($"Menu skin: frame={Name(s_frame)} body={Name(s_body)} highlight={Name(s_highlight)} hand={Name(s_hand)}");
            LogSpriteList(s_window, "Fenster-Atlas");

            s_white = new Texture2D(1, 1);
            s_white.SetPixel(0, 0, Color.white);
            s_white.Apply();

            // Ersatz-Hintergrund: FF9-typischer Blauverlauf (oben heller)
            s_fallbackBody = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
            for (Int32 y = 0; y < 64; y++)
            {
                Single t = y / 63f; // 0 = unten, 1 = oben
                s_fallbackBody.SetPixel(0, y, Color.Lerp(new Color(0.05f, 0.07f, 0.22f, 0.94f), new Color(0.22f, 0.30f, 0.58f, 0.94f), t));
            }
            s_fallbackBody.Apply();

            s_text = new GUIStyle { richText = false, wordWrap = false, clipping = TextClipping.Overflow };
            try
            {
                Font font = EncryptFontManager.SetDefaultFont();
                if (font != null)
                    s_text.font = font;
            }
            catch (Exception ex) { CoopLog.Error(ex, "Game font not available"); }
        }

        // ---------------- Fenster ----------------

        public static void Window(Rect r)
        {
            // Hintergrund: Atlas-Sprite oder Verlauf. Leicht eingerückt, damit der Rahmen darüber sauber abschließt.
            Rect inner = Inset(r, 4f * Scale);
            if (s_body != null && s_window != null)
                DrawSliced(s_window, s_body, inner);
            else
                GUI.DrawTexture(inner, s_fallbackBody, ScaleMode.StretchToFill);

            if (s_frame != null && s_window != null)
                DrawSliced(s_window, s_frame, r);
            else
                FallbackFrame(r);
        }

        public static void Highlight(Rect r)
        {
            if (s_highlight != null && s_window != null)
                DrawSliced(s_window, s_highlight, r);
            else
                Fill(r, new Color(1f, 1f, 1f, 0.12f));
        }

        /// <summary>FF9-Handcursor, zeigt nach rechts auf die Zeile. Leichtes Wippen wie im Original.</summary>
        public static void Hand(Single x, Single centerY)
        {
            Single bob = Mathf.Sin(Time.realtimeSinceStartup * 6f) * 3f * Scale;
            if (s_hand != null && s_icon != null)
            {
                Vector2 size = SpriteSize(s_icon, s_hand);
                DrawSprite(s_icon, s_hand, new Rect(x - size.x + bob, centerY - size.y / 2f, size.x, size.y));
            }
            else
            {
                Label(new Rect(x - FontSize + bob, centerY - FontSize * 0.6f, FontSize, FontSize * 1.2f), ">", AccentColor);
            }
        }

        public static void ArrowLeft(Single x, Single centerY) => Arrow(s_arrowLeft, "<", x, centerY);
        public static void ArrowRight(Single x, Single centerY) => Arrow(s_arrowRight, ">", x, centerY);

        private static void Arrow(UISpriteData sprite, String fallback, Single x, Single centerY)
        {
            if (sprite != null && s_icon != null)
            {
                Vector2 size = SpriteSize(s_icon, sprite) * 0.75f;
                DrawSprite(s_icon, sprite, new Rect(x - size.x / 2f, centerY - size.y / 2f, size.x, size.y));
            }
            else
            {
                Label(new Rect(x - FontSize * 0.3f, centerY - FontSize * 0.6f, FontSize, FontSize * 1.2f), fallback, DimColor, 0.7f);
            }
        }

        // ---------------- Text ----------------

        /// <summary>Text mit FF9-typischem dunklem Schatten.</summary>
        public static void Label(Rect r, String text, Color color, Single sizeFactor = 1f, TextAnchor align = TextAnchor.MiddleLeft)
        {
            s_text.fontSize = Mathf.Max(10, Mathf.RoundToInt(FontSize * sizeFactor));
            s_text.alignment = align;
            Single o = Mathf.Max(1f, 2f * Scale);
            s_text.normal.textColor = new Color(0f, 0f, 0f, 0.85f * color.a);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, s_text);
            s_text.normal.textColor = color;
            GUI.Label(r, text, s_text);
        }

        public static Single TextWidth(String text, Single sizeFactor = 1f)
        {
            s_text.fontSize = Mathf.Max(10, Mathf.RoundToInt(FontSize * sizeFactor));
            return s_text.CalcSize(new GUIContent(text)).x;
        }

        public static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, s_white);
            GUI.color = old;
        }

        // ---------------- Atlas-Zeichnen ----------------

        private static UISpriteData Find(UIAtlas atlas, params String[] names)
        {
            if (atlas == null)
                return null;
            foreach (String n in names)
            {
                UISpriteData s = atlas.GetSprite(n);
                if (s != null)
                    return s;
            }
            return null;
        }

        private static UISpriteData FindContaining(UIAtlas atlas, params String[] parts)
        {
            if (atlas?.spriteList == null)
                return null;
            foreach (UISpriteData s in atlas.spriteList)
            {
                Boolean all = true;
                foreach (String p in parts)
                    all &= s.name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0;
                if (all)
                    return s;
            }
            return null;
        }

        private static String Name(UISpriteData s) => s?.name ?? "—(Ersatz)";

        private static void LogSpriteList(UIAtlas atlas, String label)
        {
            if (atlas?.spriteList == null)
                return;
            StringBuilder sb = new StringBuilder();
            foreach (UISpriteData s in atlas.spriteList)
                sb.Append(s.name).Append(' ');
            CoopLog.Message($"{label}: {sb}");
        }

        private static Vector2 SpriteSize(UIAtlas atlas, UISpriteData s)
        {
            Single k = atlas.pixelSize * Scale;
            return new Vector2(s.width * k, s.height * k);
        }

        private static Rect Uv(Texture tex, Single x, Single y, Single w, Single h)
        {
            // NGUI: Pixelkoordinaten von oben links; Unity-UV: von unten links
            return new Rect(x / tex.width, 1f - (y + h) / tex.height, w / tex.width, h / tex.height);
        }

        private static void DrawSprite(UIAtlas atlas, UISpriteData s, Rect r)
        {
            Texture tex = atlas.texture;
            if (tex == null)
                return;
            GUI.DrawTextureWithTexCoords(r, tex, Uv(tex, s.x, s.y, s.width, s.height));
        }

        /// <summary>9-Slice: Ecken unverzerrt, Kanten und Mitte gestreckt (wie NGUI "Sliced").</summary>
        private static void DrawSliced(UIAtlas atlas, UISpriteData s, Rect r)
        {
            Texture tex = atlas.texture;
            if (tex == null)
                return;
            if (!s.hasBorder)
            {
                DrawSprite(atlas, s, r);
                return;
            }

            Single k = atlas.pixelSize * Scale;
            Single l = s.borderLeft * k, rr = s.borderRight * k, t = s.borderTop * k, b = s.borderBottom * k;
            // Bei kleinen Rechtecken Ränder proportional verkleinern
            Single fx = Math.Min(1f, r.width / Math.Max(1f, l + rr)), fy = Math.Min(1f, r.height / Math.Max(1f, t + b));
            l *= fx; rr *= fx; t *= fy; b *= fy;

            Single[] xs = { r.x, r.x + l, r.xMax - rr, r.xMax };
            Single[] ys = { r.y, r.y + t, r.yMax - b, r.yMax };
            Single[] us = { s.x, s.x + s.borderLeft, s.x + s.width - s.borderRight, s.x + s.width };
            Single[] vs = { s.y, s.y + s.borderTop, s.y + s.height - s.borderBottom, s.y + s.height };

            for (Int32 iy = 0; iy < 3; iy++)
            {
                for (Int32 ix = 0; ix < 3; ix++)
                {
                    Rect dst = new Rect(xs[ix], ys[iy], xs[ix + 1] - xs[ix], ys[iy + 1] - ys[iy]);
                    if (dst.width <= 0f || dst.height <= 0f)
                        continue;
                    GUI.DrawTextureWithTexCoords(dst, tex, Uv(tex, us[ix], vs[iy], us[ix + 1] - us[ix], vs[iy + 1] - vs[iy]));
                }
            }
        }

        private static void FallbackFrame(Rect r)
        {
            Single w = Mathf.Max(2f, 3f * Scale);
            Color outer = new Color(0.86f, 0.88f, 0.95f, 1f), inner = new Color(0.30f, 0.34f, 0.50f, 1f);
            Fill(new Rect(r.x, r.y, r.width, w), outer);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), outer);
            Fill(new Rect(r.x, r.y, w, r.height), outer);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), outer);
            Rect i = Inset(r, w);
            Fill(new Rect(i.x, i.y, i.width, w * 0.6f), inner);
            Fill(new Rect(i.x, i.y, w * 0.6f, i.height), inner);
        }

        public static Rect Inset(Rect r, Single d) => new Rect(r.x + d, r.y + d, r.width - 2f * d, r.height - 2f * d);
    }
}
