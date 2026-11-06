using UnityEngine;

namespace RuinRunner
{
    /// <summary>Two flooded rectangles partition the arena around one reachable dry corridor.</summary>
    public readonly struct TideLanePattern
    {
        public readonly Rect First;
        public readonly Rect Second;
        public readonly Rect Safe;
        public const float WarningSeconds = 1.4f;
        public TideLanePattern(Rect arena, Vector2 player, bool vertical, bool positive)
        {
            const float width = 4.6f;
            var low = vertical ? arena.xMin : arena.yMin;
            var high = vertical ? arena.xMax : arena.yMax;
            var axis = vertical ? player.x : player.y;
            var half = Mathf.Min(width * .5f, (high - low) * .3f);
            // Move the refuge toward the interior at edges, so every attack is escapable
            // without a dash even after an unlucky previous position.
            var sign = positive ? 1f : -1f;
            if (axis > high - half - 4.8f)
                sign = -1;
            else if (axis < low + half + 4.8f)
                sign = 1;
            var center = Mathf.Clamp(axis + sign * 4.8f, low + half + .4f, high - half - .4f);
            if (vertical)
            {
                First = Rect.MinMaxRect(arena.xMin, arena.yMin, center - half, arena.yMax);
                Safe = Rect.MinMaxRect(center - half, arena.yMin, center + half, arena.yMax);
                Second = Rect.MinMaxRect(center + half, arena.yMin, arena.xMax, arena.yMax);
            }
            else
            {
                First = Rect.MinMaxRect(arena.xMin, arena.yMin, arena.xMax, center - half);
                Safe = Rect.MinMaxRect(arena.xMin, center - half, arena.xMax, center + half);
                Second = Rect.MinMaxRect(arena.xMin, center + half, arena.xMax, arena.yMax);
            }
        }
    }
}
