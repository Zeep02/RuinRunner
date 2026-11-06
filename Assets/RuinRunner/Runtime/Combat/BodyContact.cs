using UnityEngine;

namespace RuinRunner
{
    // The hero and the enemies do not collide in the physics world: a contact there shoves whoever gives way,
    // and a body squeezed against a wall gets pushed through it. Each one stops itself at the other's feet instead.
    public static class BodyContact
    {
        // Past this overlap (after a dash or a ram) the bodies are already through each other and may walk apart in any direction.
        const float DeepOverlap = .12f;

        public static Vector2 Stop(Collider2D mover, Collider2D other, Vector2 velocity, float step, out bool stopped)
        {
            stopped = false;
            var gap = Physics2D.Distance(mover, other);
            if (!gap.isValid || gap.distance < -DeepOverlap)
                return velocity;
            var toward = (Vector2)(other.bounds.center - mover.bounds.center);
            if (toward.sqrMagnitude < .0001f)
                return velocity;
            toward.Normalize();
            var excess = Vector2.Dot(velocity, toward) - Mathf.Max(0f, gap.distance) / step;
            if (excess <= 0f)
                return velocity;
            stopped = true;
            return velocity - toward * excess;
        }
    }
}
