using UnityEngine;

namespace RuinRunner
{
    /// <summary>Shared runtime/UI contract for the ordinary sword's catch and riposte.</summary>
    public static class LongswordActionRules
    {
        public const float InputBufferSeconds = .16f;
        public const float ManaCost = 10f;
        public const float CatchSeconds = .18f;
        public const float SuccessfulCatchRecovery = .07f;
        public const float CooldownSeconds = .82f;
        public const float RiposteDelayMultiplier = .86f;
        public const float BreakthroughDamage = 1.3f;
        public const float CounterDamage = 1.9f;
        public const float FrontalDot = .35f;

        // A native broad sweep, not a detached projectile or an instant radial hit.
        public static readonly MeleeAttackStep Riposte = new MeleeAttackStep(
            1f, RiposteDelayMultiplier, 1.48f, new Vector2(3.15f, 3.4f), 0f, 8f);
    }
}
