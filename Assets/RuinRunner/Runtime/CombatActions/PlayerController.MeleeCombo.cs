using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PlayerController
    {
        const float ComboHitStopSeconds = .055f, ComboHitStopScale = .08f, ComboFinisherStopSeconds = .11f, ComboFinisherStopScale = .03f;
        const float ComboFinisherShakeSeconds = .14f, ComboFinisherShake = .16f, ComboFinisherKick = .3f, ComboDustScale = .4f;
        int comboSwingSequence = -1;

        /// <summary>A light swing of a family's own combo, which is drawn with the whole body. Skills and charged swings keep their own poses.</summary>
        public bool IsBodyComboSwing => IsMeleeSwingActive && !heavyAttackPose && comboSwingSequence == attackAnimationSequence
            && MeleeComboFrames.Has(EquippedWeapon.Family, activeComboAnimationStep);

        // Blows of a heavier weapon hang longer on the target.
        static float ComboWeight(WeaponFamily family) => family == WeaponFamily.Greatsword ? 1.35f : family == WeaponFamily.Dagger ? .8f : 1f;

        // The stop is what makes a blow land: the swing hangs for a moment on the target, and the last blow of the combo hangs longest.
        void PlayComboImpact(bool finisher, Vector2 attackDirection)
        {
            var weight = ComboWeight(EquippedWeapon.Family);
            if (finisher)
            {
                CameraFollow.MeleeHitStop?.Invoke(ComboFinisherStopSeconds * weight, ComboFinisherStopScale);
                if (cameraFollow != null)
                {
                    cameraFollow.Shake(ComboFinisherShakeSeconds * weight, ComboFinisherShake * weight);
                    cameraFollow.Kick(attackDirection, ComboFinisherKick * weight);
                }
                return;
            }
            CameraFollow.MeleeHitStop?.Invoke(ComboHitStopSeconds * weight, ComboHitStopScale);
        }

        // Feet driving into the floor on the big moves of a combo, whether or not the blow lands.
        void PlayComboStepDust()
        {
            if (!MeleeComboFrames.RaisesDust(EquippedWeapon.Family, activeComboAnimationStep))
                return;
            SigFx.Play("GroundDust", (Vector2)transform.position + Vector2.down * .28f, 0, -1, true, false, ComboDustScale);
        }
    }
}
