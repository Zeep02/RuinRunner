namespace RuinRunner
{
    /// <summary>How far a thrust visibly reaches from the hand; the hit box of a thrust follows it.</summary>
    public static class MeleeThrustReach
    {
        static float OriginDistance(WeaponFamily family) => family == WeaponFamily.Dagger ? .25f : .40f;
        static float DisplayScale(WeaponFamily family) => family == WeaponFamily.Dagger ? 1.2f : 1.35f;
        public static float VisibleReach(WeaponFamily family) => family == WeaponFamily.Spear ? 2.35f : OriginDistance(family) + 25f / 32f * DisplayScale(family);
        public static float VisibleBreadth(WeaponFamily family) => family == WeaponFamily.Spear ? .55f : (family == WeaponFamily.Dagger ? 8f : 10f) / 32f * DisplayScale(family);
    }
}
