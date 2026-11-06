using UnityEngine;

namespace RuinRunner
{
    public enum MeleeAttackMotion { Sweep, Thrust, Impact, CrossCut, Cleave }

    public sealed class MeleeAttackStep
    {
        public float DamageMultiplier { get; }
        public float DelayMultiplier { get; }
        public float Reach { get; }
        public Vector2 HitboxSize { get; }
        public float AngleOffset { get; }
        public float Knockback { get; }
        public MeleeAttackMotion Motion { get; }

        public MeleeAttackStep(float damage, float delay, float reach, Vector2 hitboxSize, float angleOffset, float knockback,
            MeleeAttackMotion motion = MeleeAttackMotion.Sweep)
        {
            DamageMultiplier = damage;
            DelayMultiplier = delay;
            Reach = reach;
            HitboxSize = hitboxSize;
            AngleOffset = angleOffset;
            Knockback = knockback;
            Motion = motion;
        }
    }

    public sealed class MeleeComboProfile
    {
        public string Id { get; }
        public WeaponFamily Family { get; }
        public float ResetWindow { get; }
        public float ChargeSeconds { get; }
        public MeleeAttackStep[] LightSteps { get; }
        public MeleeAttackStep HeavyStep { get; }

        public MeleeComboProfile(string id, WeaponFamily family, float resetWindow, float chargeSeconds, MeleeAttackStep[] lightSteps, MeleeAttackStep heavyStep)
        {
            Id = id;
            Family = family;
            ResetWindow = resetWindow;
            ChargeSeconds = chargeSeconds;
            LightSteps = lightSteps;
            HeavyStep = heavyStep;
        }
    }

    public static class MeleeComboCatalog
    {
        private static MeleeAttackStep Step(float damage, float delay, float reach, float width, float height, float angle, float knockback, MeleeAttackMotion motion = MeleeAttackMotion.Sweep)
        {
            // The readable attack silhouette and its damage envelope grow together.
            // Keep the inner edge at the hand; extend outward instead of leaving a
            // dead zone next to the player when increasing the reach.
            var length = width * (motion == MeleeAttackMotion.Cleave ? 1.7f : motion == MeleeAttackMotion.Thrust ? 1.65f : 1.5f);
            var breadth = height * (motion == MeleeAttackMotion.Thrust ? 1.5f : motion == MeleeAttackMotion.CrossCut ? 3f : 2.5f);
            return new MeleeAttackStep(damage, delay, reach + (length - width) * .5f,
                new Vector2(length, breadth), angle, knockback, motion);
        }

        private static readonly MeleeComboProfile[] Profiles =
        {
            new MeleeComboProfile("dagger", WeaponFamily.Dagger, .62f, .34f,
                new[]
                {
                    Step(.68f, .56f, .7f, 1.15f, .75f, -12f, 2.8f),
                    Step(.72f, .52f, .72f, 1.2f, .78f, 12f, 3f),
                    Step(.82f, .58f, .78f, 1.3f, .82f, -6f, 3.5f),
                    Step(1.12f, .72f, .86f, 1.45f, .9f, 0f, 5.5f,MeleeAttackMotion.Thrust)
                },
                Step(1.55f, 1.05f, 1f, 1.8f, 1.05f, 0f, 7f,MeleeAttackMotion.Thrust)),
            new MeleeComboProfile("katana", WeaponFamily.Katana, .7f, .4f,
                new[]
                {
                    // Rising cut, falling cut, a full turn, then the driving cross.
                    Step(.9f, .72f, .92f, 1.75f, .72f, -16f, 4f),
                    Step(.95f, .68f, .98f, 1.85f, .75f, 14f, 4.5f),
                    Step(1.1f, .85f, 1f, 1.95f, .85f, 0f, 5.5f),
                    Step(1.6f, 1.15f, 1.1f, 2.05f, .82f, 0f, 7.5f,MeleeAttackMotion.CrossCut)
                },
                Step(1.95f, 1.15f, 1.35f, 2.5f, .85f, 0f, 10f,MeleeAttackMotion.CrossCut)),
            new MeleeComboProfile("spear", WeaponFamily.Spear, .85f, .45f,
                new[]
                {
                    // Thrust, a wide swing of the shaft, a low driving thrust, then a leap down on the point.
                    Step(.95f, .9f, 1.1f, 1.7f, .55f, 0f, 5f,MeleeAttackMotion.Thrust),
                    Step(1f, .88f, 1f, 2f, .95f, 0f, 5.5f),
                    Step(.95f, .8f, 1.15f, 1.8f, .55f, 0f, 6f,MeleeAttackMotion.Thrust),
                    Step(1.6f, 1.2f, 1.2f, 1.9f, .6f, 0f, 9.5f,MeleeAttackMotion.Thrust)
                },
                Step(1.9f, 1.3f, 1.3f, 2.1f, .65f, 0f, 11f,MeleeAttackMotion.Thrust)),
            new MeleeComboProfile("greatsword", WeaponFamily.Greatsword, 1f, .62f,
                new[]
                {
                    // A heavy swing, the swing back, then a leap and a crash.
                    Step(1.1f, 1.2f, 1f, 2.15f, 1.35f, -14f, 8f),
                    Step(1.2f, 1.15f, 1.05f, 2.2f, 1.4f, 10f, 9f),
                    Step(1.75f, 1.4f, 1.2f, 2.35f, 1.55f, 0f, 13f,MeleeAttackMotion.Cleave)
                },
                Step(2.35f, 1.65f, 1.34f, 2.75f, 1.85f, 0f, 16f,MeleeAttackMotion.Cleave)),

            // Concept and high-rarity weapons can opt into a unique profile with ComboProfileId.
        };

        public static MeleeComboProfile Get(WeaponDefinition weapon)
        {
            var family = weapon == null ? WeaponFamily.Katana : weapon.Family;
            var requestedId = weapon == null ? string.Empty : weapon.ComboProfileId;
            if (!string.IsNullOrEmpty(requestedId))
                foreach (var profile in Profiles)
                    if (profile.Id == requestedId)
                        return profile;

            foreach (var profile in Profiles)
                if (profile.Family == family && profile.Id == FamilyProfileId(family))
                    return profile;
            return Profiles[0];
        }

        private static string FamilyProfileId(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Dagger:
                    return "dagger";
                case WeaponFamily.Katana:
                    return "katana";
                case WeaponFamily.Greatsword:
                    return "greatsword";
                case WeaponFamily.Spear:
                    return "spear";
                default:
                    return "katana";
            }
        }
    }
}
