using UnityEngine;

namespace RuinRunner
{
    [System.Serializable]
    public sealed class MeleeAnimationSet
    {
        public WeaponFamily family;
        [Tooltip("비워두면 무기군 공통 세트, 입력하면 해당 ComboProfileId 전용 세트입니다.")]
        public string comboProfileId;
        public Sprite[] charge;
        public Sprite[] light1;
        public Sprite[] light2;
        public Sprite[] light3;
        public Sprite[] light4;
        public Sprite[] light5;
        public Sprite[] heavyAttack;
        public Sprite[] guard;

        public Sprite[] GetLightFrames(int step)
        {
            switch (step)
            {
                case 1: return light2;
                case 2: return light3;
                case 3: return light4;
                case 4: return light5;
                default: return light1;
            }
        }
    }

    [CreateAssetMenu(menuName = "Ruin Runner/Player Animation Library", fileName = "PlayerAnimationLibrary")]
    public sealed class PlayerAnimationLibrary : ScriptableObject
    {
        [Min(1f)] public float framesPerSecond = 12f;
        public Vector2 visualScale = new Vector2(.72f, 1.15f);
        public Vector2 visualOffset = Vector2.zero;
        public Sprite[] idle;
        public Sprite[] run;
        public Sprite[] brake;
        public Sprite[] jumpRise;
        public Sprite[] fall;
        public Sprite[] land;
        public Sprite[] charge;
        public Sprite[] attack;
        public Sprite[] heavyAttack;
        public Sprite[] guard;
        public Sprite[] dash;
        public Sprite[] hurt;
        [Tooltip("무기군 공통 세트와 ComboProfileId 전용 세트를 등록합니다.")]
        public MeleeAnimationSet[] meleeAnimationSets;

        public Sprite[] GetFrames(PlayerMotionState state, WeaponDefinition weapon, int comboStep)
        {
            var meleeSet = GetMeleeSet(weapon);
            if (meleeSet != null)
            {
                if (state == PlayerMotionState.Charge && HasFrames(meleeSet.charge)) return meleeSet.charge;
                if (state == PlayerMotionState.Attack)
                {
                    var lightFrames = meleeSet.GetLightFrames(comboStep);
                    if (HasFrames(lightFrames)) return lightFrames;
                }
                if (state == PlayerMotionState.HeavyAttack && HasFrames(meleeSet.heavyAttack)) return meleeSet.heavyAttack;
                if (state == PlayerMotionState.Guard && HasFrames(meleeSet.guard)) return meleeSet.guard;
            }

            switch (state)
            {
                case PlayerMotionState.Run: return run;
                case PlayerMotionState.Brake: return brake;
                case PlayerMotionState.JumpRise: return jumpRise;
                case PlayerMotionState.Fall: return fall;
                case PlayerMotionState.Land: return land;
                case PlayerMotionState.Charge: return charge;
                case PlayerMotionState.Attack: return attack;
                case PlayerMotionState.HeavyAttack: return heavyAttack;
                case PlayerMotionState.Guard: return guard;
                case PlayerMotionState.Dash: return dash;
                case PlayerMotionState.Hurt: return hurt;
                default: return idle;
            }
        }

        private MeleeAnimationSet GetMeleeSet(WeaponDefinition weapon)
        {
            if (meleeAnimationSets == null || (weapon != null && weapon.AttackType != WeaponAttackType.Melee)) return null;
            var family = weapon == null ? WeaponFamily.Longsword : weapon.Family;
            var profileId = weapon == null ? string.Empty : weapon.ComboProfileId;
            if (!string.IsNullOrEmpty(profileId))
                foreach (var set in meleeAnimationSets)
                    if (set != null && set.comboProfileId == profileId) return set;
            foreach (var set in meleeAnimationSets)
                if (set != null && set.family == family && string.IsNullOrEmpty(set.comboProfileId)) return set;
            return null;
        }

        private static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0;
        }

        public bool ShouldLoop(PlayerMotionState state)
        {
            return state == PlayerMotionState.Idle || state == PlayerMotionState.Run;
        }
    }
}
