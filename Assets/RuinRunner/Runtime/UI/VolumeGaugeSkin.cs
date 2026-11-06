using UnityEngine;

namespace RuinRunner
{
    public static class VolumeGaugeSkin
    {
        public static Rect FillRect(Rect rect, float ratio) => new Rect(rect.x + 8, rect.y + 6,
            Mathf.Floor(Mathf.Max(0, rect.width - 16) * Mathf.Clamp01(ratio)), Mathf.Max(0, rect.height - 12));

        public static void Draw(Rect rect, float ratio)
        {
            var track = IndividualArt.Texture("ThrustOptions/VolumeTrack");
            GUI.DrawTexture(rect, track, ScaleMode.StretchToFill, true);
            var fill = FillRect(rect, ratio);
            if (fill.width <= 0)
                return;
            var previous = GUI.color;
            GUI.color = new Color(.65f, .49f, .30f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = new Color(.88f, .73f, .46f);
            GUI.DrawTexture(new Rect(fill.x, fill.y, fill.width, 2), Texture2D.whiteTexture);
            GUI.color = new Color(.36f, .25f, .20f);
            GUI.DrawTexture(new Rect(fill.x, fill.yMax - 2, fill.width, 2), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
