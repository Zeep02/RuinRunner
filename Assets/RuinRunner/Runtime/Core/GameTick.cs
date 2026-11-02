using UnityEngine;

namespace RuinRunner
{
    public static class GameTick
    {
        public const int TicksPerSecond = 60;
        public const float Duration = 1f / TicksPerSecond;

        public static int FromSeconds(float seconds)
        {
            return Mathf.Max(0, Mathf.CeilToInt(seconds * TicksPerSecond));
        }
    }
}
