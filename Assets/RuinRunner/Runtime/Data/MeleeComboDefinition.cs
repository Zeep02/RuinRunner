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
        private static MeleeAttackStep Step(float damage, float delay, float reach, float width, float height, float angle, float knockback,MeleeAttackMotion motion=MeleeAttackMotion.Sweep)
        {
            // The readable attack silhouette and its damage envelope grow together.
            // Keep the inner edge at the hand; extend outward instead of leaving a
            // dead zone next to the player when increasing the reach.
            var length = width * (motion == MeleeAttackMotion.Cleave ? 1.7f : motion == MeleeAttackMotion.Thrust ? 1.65f : 1.5f);
            var breadth = height * (motion == MeleeAttackMotion.Thrust ? 1.5f : motion == MeleeAttackMotion.CrossCut ? 3f : 2.5f);
            return new MeleeAttackStep(damage, delay, reach + (length-width)*.5f,
                new Vector2(length, breadth), angle, knockback,motion);
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
            new MeleeComboProfile("longsword", WeaponFamily.Longsword, .78f, .46f,
                new[]
                {
                    // Native forward / reverse wrist clips share their ready pose.
                    // The last cut opens a wider arc without taking over movement.
                    Step(.9f, .68f, .86f, 1.55f, 1.05f, -10f, 4.5f),
                    Step(.96f, .75f, .9f, 1.65f, 1.08f, 12f, 4.8f),
                    Step(1.28f, .9f, 1f, 2f, 1.35f, 0f, 7f)
                },
                Step(1.85f, 1.35f, 1.12f, 2.15f, 1.35f, 0f, 9f)),
            new MeleeComboProfile("mace", WeaponFamily.Mace, .9f, .52f,
                new[]
                {
                    Step(1f, 1.05f, .78f, 1.35f, 1.15f, -18f, 6f),
                    Step(1.08f, 1.08f, .82f, 1.45f, 1.2f, 18f, 6.5f),
                    Step(1.42f, 1.14f, .88f, 1.75f, 1.45f, 0f, 10f,MeleeAttackMotion.Impact)
                },
                Step(2.05f, 1.55f, .96f, 2.05f, 1.7f, -8f, 13f,MeleeAttackMotion.Impact)),
            new MeleeComboProfile("katana", WeaponFamily.Katana, .7f, .4f,
                new[]
                {
                    Step(.82f, .72f, .92f, 1.75f, .72f, -16f, 4f),
                    Step(.9f, .68f, .98f, 1.85f, .75f, 14f, 4.5f),
                    Step(1.38f, .88f, 1.1f, 2.05f, .82f, 0f, 7.5f,MeleeAttackMotion.CrossCut)
                },
                Step(1.95f, 1.15f, 1.35f, 2.5f, .85f, 0f, 10f,MeleeAttackMotion.CrossCut)),
            new MeleeComboProfile("greatsword", WeaponFamily.Greatsword, 1f, .62f,
                new[]
                {
                    Step(1.18f, 1.28f, 1f, 2.15f, 1.35f, -14f, 8f),
                    Step(1.58f, 1.24f, 1.2f, 2.35f, 1.55f, 0f, 12f,MeleeAttackMotion.Cleave)
                },
                Step(2.35f, 1.65f, 1.34f, 2.75f, 1.85f, 0f, 16f,MeleeAttackMotion.Cleave)),

            // Concept and high-rarity weapons can opt into a unique profile with ComboProfileId.
            new MeleeComboProfile("shadow_dance", WeaponFamily.Dagger, .72f, .3f,
                new[]
                {
                    Step(.62f, .42f, .8f, 1.35f, .8f, -20f, 3f),
                    Step(.68f, .4f, .85f, 1.4f, .82f, 20f, 3f),
                    Step(.76f, .42f, .9f, 1.5f, .88f, -8f, 4f),
                    Step(.84f, .45f, .95f, 1.6f, .92f, 8f, 4.5f),
                    Step(1.45f, .72f, 1.1f, 2f, 1.1f, 0f, 8f,MeleeAttackMotion.Thrust)
                },
                Step(1.8f, .95f, 1.2f, 2.25f, 1.2f, 0f, 9f)),
            new MeleeComboProfile("moon_iai", WeaponFamily.Katana, .82f, .32f,
                new[]
                {
                    Step(.95f, .58f, 1.05f, 1.95f, .7f, -5f, 4f),
                    Step(1.05f, .62f, 1.15f, 2.15f, .75f, 5f, 5f),
                    Step(1.62f, .82f, 1.42f, 2.7f, .85f, 0f, 9f,MeleeAttackMotion.CrossCut)
                },
                Step(2.15f, 1.05f, 1.65f, 3.1f, .9f, 0f, 12f)),
            new MeleeComboProfile("sun_oath", WeaponFamily.Longsword, .9f, .42f,
                new[]
                {
                    Step(1f, .72f, 1f, 1.85f, 1.1f, -12f, 5f),
                    Step(1.1f, .76f, 1.08f, 2f, 1.15f, 12f, 5.5f),
                    Step(1.25f, .8f, 1.15f, 2.15f, 1.2f, -6f, 6f),
                    Step(1.7f, .95f, 1.15f, 2.5f, 1.4f, 0f, 11f,MeleeAttackMotion.Sweep)
                },
                Step(2.3f, 1.3f, 1.5f, 2.9f, 1.6f, 0f, 14f))
        };

        public static MeleeComboProfile Get(WeaponDefinition weapon)
        {
            var family = weapon == null ? WeaponFamily.Longsword : weapon.Family;
            var requestedId = weapon == null ? string.Empty : weapon.ComboProfileId;
            if (!string.IsNullOrEmpty(requestedId))
                foreach (var profile in Profiles)
                    if (profile.Id == requestedId) return profile;

            foreach (var profile in Profiles)
                if (profile.Family == family && profile.Id == FamilyProfileId(family)) return profile;
            return Profiles[1];
        }

        private static string FamilyProfileId(WeaponFamily family)
        {
            switch (family)
            {
                case WeaponFamily.Dagger: return "dagger";
                case WeaponFamily.Mace: return "mace";
                case WeaponFamily.Katana: return "katana";
                case WeaponFamily.Greatsword: return "greatsword";
                default: return "longsword";
            }
        }
    }
}
