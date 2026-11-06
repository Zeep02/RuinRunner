using UnityEngine;

namespace RuinRunner
{
    /// <summary>pixelfx clips hold colour and additive light in one premultiplied image and need this material to draw them.</summary>
    public static class PixelFxMaterial
    {
        private static Material shared;
        public static Material Shared
        {
            get
            {
                if (shared == null) shared = new Material(Resources.Load<Shader>("Shaders/PixelFxPremultiplied")) { name = "pixelfx premultiplied" };
                return shared;
            }
        }
        // Exports are named Fx..., E..., W... and Warden...; older hand-placed clips such as Ember or Wind are ordinary sprites.
        public static bool IsPixelFxClip(string clip) => clip.Length > 1 && (clip.StartsWith("Warden", System.StringComparison.Ordinal) || clip.StartsWith("Fx", System.StringComparison.Ordinal) ||
            (clip[0] == 'E' || clip[0] == 'W') && char.IsUpper(clip[1]));
        // The presentation clips from Tools/Art/presentation-fx.mjs, whose names say what they are for instead.
        static readonly string[] PresentationClips = { "Amb", "Break", "Spawn", "DeathPuff", "Ink", "Stain", "Step", "Banner", "BossFall", "Ui", "LootBeam", "LevelUpBurst", "RoomClearRing" };
        static bool IsPresentationClip(string clip)
        {
            foreach (var prefix in PresentationClips) if (clip.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
            return false;
        }
        public static void Apply(SpriteRenderer view, string clip)
        {
            if (IsPixelFxClip(clip) || IsPresentationClip(clip)) view.sharedMaterial = Shared;
        }
    }
}
