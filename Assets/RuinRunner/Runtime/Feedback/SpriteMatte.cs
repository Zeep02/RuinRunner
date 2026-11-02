using UnityEngine;

namespace RuinRunner
{
    /// <summary>Explicit import policies. Never chroma-key equipment or opaque terrain.</summary>
    public static class SpriteMatte
    {
        // Generated key backgrounds include dark/antialiased purple, not just #ff00ff.
        // Apply ONLY to sheets authored against a magenta matte. White/cyan highlights
        // have a high green channel and must stay; purple equipment never uses this policy.
        public static bool IsMagentaMatte(Color32 p) => p.r > 70 && p.b > 70 &&
            p.r - p.g > 28 && p.b - p.g > 28;

        public static Color32 ChromaCutout(Color32 pixel)
        {
            if (pixel.a < 128 || IsMagentaMatte(pixel)) return new Color32(0, 0, 0, 0);
            pixel.a = 255;
            return pixel;
        }

        public static Color32 AlphaCutout(Color32 pixel)
        {
            // Solid pixel sprites: discard extraction residue and retain the actual
            // source colors. Do not erase white pixels by RGB (weapons/eyes use them).
            if (pixel.a < 128) return new Color32(0, 0, 0, 0);
            pixel.a = 255;
            return pixel;
        }
    }
}
