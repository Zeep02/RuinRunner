namespace RuinRunner
{
    /// <summary>Four diagonal body views using the existing catalog's stable direction indices.</summary>
    public static class DiagonalHeroFacing
    {
        public const int NorthEast = 1;
        public const int SouthEast = 3;
        public const int SouthWest = 5;
        public const int NorthWest = 7;

        // A small normalized axis dead zone prevents body flicker while aiming
        // near a cardinal axis. Continuous weapon aim is not quantized.
        public static int Resolve(float x, float y, int previous = SouthEast, float axisDeadZone = .08f)
        {
            if (previous != NorthEast && previous != SouthEast && previous != SouthWest && previous != NorthWest)
                previous = SouthEast;
            float magnitudeSquared = x * x + y * y;
            if (magnitudeSquared < .001f) return previous;
            float threshold = (float)System.Math.Sqrt(magnitudeSquared) * axisDeadZone;
            bool east = previous == NorthEast || previous == SouthEast;
            bool north = previous == NorthEast || previous == NorthWest;
            if (x > threshold) east = true;
            else if (x < -threshold) east = false;
            if (y > threshold) north = true;
            else if (y < -threshold) north = false;
            return north ? (east ? NorthEast : NorthWest) : (east ? SouthEast : SouthWest);
        }
    }
}
