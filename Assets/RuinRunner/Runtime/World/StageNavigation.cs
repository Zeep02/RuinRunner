using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Shared world-space reachability on the actual stage mask; no movement or physics bypass.</summary>
    public static class StageNavigation
    {
        // A zero-width ray can slip past a wall while a character or projectile
        // still collides with its corner. Combat visibility needs clearance.
        public static bool HasCombatLane(Vector2 from, Vector2 to, float radius)
        {
            var delta = to - from;
            return delta.sqrMagnitude < .0001f ||
                   Physics2D.CircleCast(from, radius, delta.normalized, delta.magnitude,
                       1 << GameLayers.Ground).collider == null;
        }

        // Feet can legally rest closer to a wall than the wider combat corridor.
        // Routing toward another nearby actor then returns the same grid cell and
        // never opens the firing lane. Step away from the touching wall first.
        public static Vector2 WallClearanceDirection(Vector2 origin, float radius)
        {
            var wall = Physics2D.OverlapCircle(origin, radius, 1 << GameLayers.Ground);
            if (wall == null) return Vector2.zero;
            var away = origin - wall.ClosestPoint(origin);
            return away.sqrMagnitude > .000001f ? away.normalized : Vector2.zero;
        }

        public static bool IsClear(StageTerrainLayout terrain, Vector2 root)
        {
            var foot = root + RoomLayoutRules.FootOffset;
            var half = RoomLayoutRules.FootHalfSize + Vector2.one * .04f;
            for (var x = -1; x <= 1; x++) for (var y = -1; y <= 1; y++)
                if (!terrain.IsOpen(foot + new Vector2(x * half.x, y * half.y))) return false;
            return true;
        }
        public static List<Vector2> FindPath(StageTerrainLayout terrain, Vector2 from, Vector2 to, Bounds[] barriers = null)
        {
            const float step = .5f;
            var cols = Mathf.CeilToInt(terrain.Bounds.width / step); var rows = Mathf.CeilToInt(terrain.Bounds.height / step);
            var count = cols * rows; var previous = new int[count]; var valid = new sbyte[count];
            for (var i = 0; i < count; i++) previous[i] = -2;
            Vector2 Point(int id) => terrain.Bounds.min + new Vector2((id % cols + .5f) * step, (id / cols + .5f) * step);
            bool Clear(int id)
            {
                if (id < 0 || id >= count) return false;
                if (valid[id] != 0) return valid[id] > 0;
                var point = Point(id); var result = IsClear(terrain, point);
                if (barriers != null) foreach (var box in barriers)
                {
                    var inflated = box; inflated.Expand(new Vector3(.85f, .65f, 0));
                    if (inflated.Contains(point + RoomLayoutRules.FootOffset)) result = false;
                }
                valid[id] = (sbyte)(result ? 1 : -1); return result;
            }
            int Nearest(Vector2 point)
            {
                var x = Mathf.FloorToInt((point.x - terrain.Bounds.xMin) / step); var y = Mathf.FloorToInt((point.y - terrain.Bounds.yMin) / step);
                var best = float.PositiveInfinity; var chosen = -1;
                for (var dy = -3; dy <= 3; dy++) for (var dx = -3; dx <= 3; dx++)
                {
                    if (x + dx < 0 || x + dx >= cols || y + dy < 0 || y + dy >= rows) continue;
                    var id = (y + dy) * cols + x + dx; var distance = (Point(id) - point).sqrMagnitude;
                    if (distance >= best || !Clear(id)) continue;
                    chosen = id; best = distance;
                }
                return chosen;
            }
            var start = Nearest(from); var target = Nearest(to); if (start < 0 || target < 0) return null;
            var queue = new Queue<int>(); queue.Enqueue(start); previous[start] = -1;
            var offsets = new[] { 1, -1, cols, -cols };
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (id == target)
                {
                    var path = new List<Vector2>();
                    for (var cursor = id; cursor >= 0; cursor = previous[cursor]) path.Add(Point(cursor));
                    path.Reverse();
                    for (var i = 1; i + 1 < path.Count;)
                        if (Vector2.Dot((path[i] - path[i - 1]).normalized, (path[i + 1] - path[i]).normalized) > .999f) path.RemoveAt(i); else i++;
                    return path;
                }
                foreach (var offset in offsets)
                {
                    var next = id + offset;
                    if (next < 0 || next >= count || previous[next] != -2 || !Clear(next)) continue;
                    if (Mathf.Abs(next % cols - id % cols) + Mathf.Abs(next / cols - id / cols) != 1) continue;
                    if (!IsClear(terrain, (Point(id) + Point(next)) * .5f)) continue;
                    previous[next] = id; queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
