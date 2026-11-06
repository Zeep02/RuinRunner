using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public static class CombatActionGeometry
    {
        public static bool Visible(Vector2 origin, EnemyController target)
        {
            return target != null && !target.IsDead && target.DamageCollider != null && Physics2D.Linecast(origin, target.DamageCollider.ClosestPoint(origin), 1 << GameLayers.Ground).collider == null;
        }
        public static Vector2 Clip(Vector2 origin, Vector2 direction, float length)
        {
            direction.Normalize();
            var hit = Physics2D.Raycast(origin, direction, length, 1 << GameLayers.Ground);
            return hit.collider == null ? origin + direction * length : hit.point + hit.normal * .08f;
        }
        public static List<EnemyController> Circle(Vector2 origin, float radius, int limit = 8)
        {
            var result = new List<EnemyController>();
            foreach (var col in Physics2D.OverlapCircleAll(origin, radius, 1 << GameLayers.Enemy))
            {
                var enemy = EnemyController.DamageTarget(col);
                if (!result.Contains(enemy) && Visible(origin, enemy))
                    result.Add(enemy);
            }
            result.Sort((a, b) => ((Vector2)a.transform.position - origin).sqrMagnitude.CompareTo(((Vector2)b.transform.position - origin).sqrMagnitude));
            if (result.Count > limit)
                result.RemoveRange(limit, result.Count - limit);
            return result;
        }
        public static List<EnemyController> Segment(Vector2 from, Vector2 to, float width, int limit = 8)
        {
            var delta = to - from;
            to = Clip(from, delta, delta.magnitude);
            delta = to - from;
            var result = new List<EnemyController>();
            foreach (var col in Physics2D.OverlapBoxAll((from + to) * .5f, new Vector2(delta.magnitude, width), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 1 << GameLayers.Enemy))
            {
                var enemy = EnemyController.DamageTarget(col);
                if (!result.Contains(enemy) && Visible(from, enemy))
                    result.Add(enemy);
            }
            if (result.Count > limit)
                result.RemoveRange(limit, result.Count - limit);
            return result;
        }
        public static void Trail(string clip, Vector2 from, Vector2 to, float width, SpriteRenderer actor)
        {
            to = Clip(from, to - from, Vector2.Distance(from, to));
            var delta = to - from;
            int count = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / .55f), 1, 10);
            for (int i = 0; i < count; i++)
                NativeCombatEffect.Create(clip, Vector2.Lerp(from, to, (i + .5f) / count), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90, width, .22f, Color.white)?.AboveWeapon(actor);
        }
    }
}
