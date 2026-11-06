using UnityEngine;

namespace RuinRunner
{
    /// <summary>The katana's row of <see cref="MeleeComboFrames"/>, for skills that replay a katana's combo poses.</summary>
    public static class KatanaComboFrames
    {
        public static int StepCount => MeleeComboFrames.StepCount(WeaponFamily.Katana);
        public static Sprite Sample(int step, int facing, float progress, out Vector2 hand, out float bladeAngle, out int frame)
            => MeleeComboFrames.Sample(WeaponFamily.Katana, step, facing, progress, out hand, out bladeAngle, out frame);
        public static void Preload() => MeleeComboFrames.Preload(WeaponFamily.Katana);
    }
}
