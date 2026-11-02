using System;
using System.Text;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Bundled fonts only; no dependence on the player's installed fonts.</summary>
    public static class GameTypography
    {
        public const int BodySize = 24; // Galmuri11: 12px × 2 at 1280×720.
        public const int DetailSize = 20; // Galmuri9: 10px × 2.
        private static Font regular;
        private static Font emphasis;
        private static Font detail;
        private static bool subscribed;

        public static Font Regular => Load(ref regular, "Galmuri11");
        public static Font Emphasis => Load(ref emphasis, "Galmuri11-Bold");
        public static Font Detail => Load(ref detail, "Galmuri9");

        // Existing layouts use 1280×720 units. Half-integer scales correspond
        // to integer source pixels (2× at 720p, 3× at 1080p). Other sizes retain
        // extra layout space instead of stretching pixels by an arbitrary ratio.
        public static float LayoutScale => Mathf.Max(.5f,
            Mathf.Floor(Mathf.Min(Screen.width / 1280f, Screen.height / 720f) * 2f) * .5f);

        // IMGUI wraps Korean at any syllable, even before a final full stop.
        // Prefer spaces so a short card keeps words and their punctuation together.
        // Call when preparing content, not on every repaint. Overlong single words
        // still use the label's normal wrapping instead of being clipped.
        public static string WrapWords(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var result = new StringBuilder(text.Length + 16);
            var content = new GUIContent();
            foreach (var paragraph in text.Split('\n'))
            {
                if (result.Length > 0) result.Append('\n');
                string line = "";
                foreach (var word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    content.text = candidate;
                    if (line.Length > 0 && style.CalcSize(content).x > width)
                    {
                        result.Append(line).Append('\n');
                        line = word;
                    }
                    else line = candidate;
                }
                result.Append(line);
            }
            return result.ToString();
        }

        private static Font Load(ref Font font, string name)
        {
            if (font != null) return font;
            font = Resources.Load<Font>("Fonts/" + name);
            if (font == null) throw new InvalidOperationException("Missing bundled font: " + name);
            if (!subscribed)
            {
                Font.textureRebuilt += ApplyPointFilter;
                subscribed = true;
            }
            ApplyPointFilter(font);
            return font;
        }

        private static void ApplyPointFilter(Font font)
        {
            if (font != regular && font != emphasis && font != detail) return;
            if (font.material != null && font.material.mainTexture != null)
                font.material.mainTexture.filterMode = FilterMode.Point;
        }
    }
}
