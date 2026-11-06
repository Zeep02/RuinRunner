using UnityEngine;

namespace RuinRunner
{
    public static class GameLayers
    {
        public const int Ground = 6;
        public const int Enemy = 7;
        public const int Player = 8;
        public const int Projectile = 9;

        private static PhysicsMaterial2D frictionless;
        // Walkers set their own speed every step, so the default friction only made them stick to walls and corners they brush past.
        public static PhysicsMaterial2D Frictionless => frictionless != null ? frictionless : frictionless = new PhysicsMaterial2D("Walker feet")
        {
            friction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine2D.Minimum
        };
    }
}
