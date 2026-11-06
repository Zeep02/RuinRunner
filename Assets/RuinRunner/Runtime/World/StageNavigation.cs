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
            if (wall == null)
                return Vector2.zero;
            var away = origin - wall.ClosestPoint(origin);
            return away.sqrMagnitude > .000001f ? away.normalized : Vector2.zero;
        }

        public static bool IsClear(StageTerrainLayout terrain, Vector2 root)
        {
            var foot = root + RoomLayoutRules.FootOffset;
            var half = RoomLayoutRules.FootHalfSize + Vector2.one * .04f;
            for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                    if (!terrain.IsOpen(foot + new Vector2(x * half.x, y * half.y)))
                        return false;
            return true;
        }
        // A blocked enemy asks for a route about once a second, so the stage-sized search grid is kept and reused.
        // Walkability is remembered only briefly across searches: seals close and pots break.
        const float PathStep = .5f, WalkableMemorySeconds = .5f;
        // Room for any regular walker's feet, measured from its root: the hero's sit lower and enemies' are wider.
        static readonly Vector2 WalkerFootOffset = new Vector2(0f, -.24f), WalkerFootSize = new Vector2(.86f, .58f);
        static StageTerrainLayout gridTerrain;
        static int[] cameFrom, visitedOn;
        static sbyte[] walkable;
        static int searchNumber;
        static float walkableUntil;
        static readonly Queue<int> frontier = new Queue<int>();

        // Barriers are hypothetical closed seals, for checks that nothing can get around one; real ones are colliders.
        public static List<Vector2> FindPath(StageTerrainLayout terrain, Vector2 from, Vector2 to, Bounds[] barriers = null)
        {
            var cols = Mathf.CeilToInt(terrain.Bounds.width / PathStep);
            var rows = Mathf.CeilToInt(terrain.Bounds.height / PathStep);
            var count = cols * rows;
            if (walkable == null || walkable.Length != count)
            {
                walkable = new sbyte[count];
                cameFrom = new int[count];
                visitedOn = new int[count];
                searchNumber = 0;
                walkableUntil = 0f;
            }
            if (barriers != null || terrain != gridTerrain || Time.unscaledTime >= walkableUntil)
            {
                System.Array.Clear(walkable, 0, count);
                gridTerrain = terrain;
                walkableUntil = barriers != null ? 0f : Time.unscaledTime + WalkableMemorySeconds;
            }
            var search = ++searchNumber;
            Vector2 Point(int id) => terrain.Bounds.min + new Vector2((id % cols + .5f) * PathStep, (id / cols + .5f) * PathStep);
            bool Clear(int id)
            {
                if (id < 0 || id >= count)
                    return false;
                if (walkable[id] != 0)
                    return walkable[id] > 0;
                var point = Point(id);
                // Pots, closed seals and field services are solid but not part of the terrain mask.
                var result = IsClear(terrain, point) &&
                    Physics2D.OverlapBox(point + WalkerFootOffset, WalkerFootSize, 0f, 1 << GameLayers.Ground) == null;
                if (barriers != null)
                    foreach (var box in barriers)
                    {
                        var inflated = box;
                        inflated.Expand(new Vector3(.85f, .65f, 0));
                        if (inflated.Contains(point + RoomLayoutRules.FootOffset))
                            result = false;
                    }
                walkable[id] = (sbyte)(result ? 1 : -1);
                return result;
            }
            int Nearest(Vector2 point)
            {
                var x = Mathf.FloorToInt((point.x - terrain.Bounds.xMin) / PathStep);
                var y = Mathf.FloorToInt((point.y - terrain.Bounds.yMin) / PathStep);
                var best = float.PositiveInfinity;
                var chosen = -1;
                for (var dy = -3; dy <= 3; dy++)
                    for (var dx = -3; dx <= 3; dx++)
                    {
                        if (x + dx < 0 || x + dx >= cols || y + dy < 0 || y + dy >= rows)
                            continue;
                        var id = (y + dy) * cols + x + dx;
                        var distance = (Point(id) - point).sqrMagnitude;
                        if (distance >= best || !Clear(id))
                            continue;
                        chosen = id;
                        best = distance;
                    }
                return chosen;
            }
            var start = Nearest(from);
            var target = Nearest(to);
            if (start < 0 || target < 0)
                return null;
            frontier.Clear();
            frontier.Enqueue(start);
            visitedOn[start] = search;
            cameFrom[start] = -1;
            while (frontier.Count > 0)
            {
                var id = frontier.Dequeue();
                if (id == target)
                {
                    var path = new List<Vector2>();
                    for (var cursor = id; cursor >= 0; cursor = cameFrom[cursor])
                        path.Add(Point(cursor));
                    path.Reverse();
                    for (var i = 1; i + 1 < path.Count;)
                        if (Vector2.Dot((path[i] - path[i - 1]).normalized, (path[i + 1] - path[i]).normalized) > .999f)
                            path.RemoveAt(i);
                        else
                            i++;
                    return path;
                }
                for (var side = 0; side < 4; side++)
                {
                    var next = id + (side == 0 ? 1 : side == 1 ? -1 : side == 2 ? cols : -cols);
                    if (next < 0 || next >= count || visitedOn[next] == search || !Clear(next))
                        continue;
                    if (Mathf.Abs(next % cols - id % cols) + Mathf.Abs(next / cols - id / cols) != 1)
                        continue;
                    if (!IsClear(terrain, (Point(id) + Point(next)) * .5f))
                        continue;
                    visitedOn[next] = search;
                    cameFrom[next] = id;
                    frontier.Enqueue(next);
                }
            }
            return null;
        }
    }
}
