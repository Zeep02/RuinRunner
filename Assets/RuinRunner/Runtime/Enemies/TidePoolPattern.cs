using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Locked wells on clear floor, with a walkable gap between circles.</summary>
    public readonly struct TidePoolPattern
    {
        public const float Spacing = 4.75f;
        public readonly float Radius;
        public readonly Vector2[] Centers;
        static readonly Vector2[] Offsets =
        {
            Vector2.left, Vector2.right, Vector2.left * 2, Vector2.right * 2,
            Vector2.down, Vector2.up, Vector2.down * 2, Vector2.up * 2,
            new Vector2(-1,-1), new Vector2(1,-1), new Vector2(-1,1), new Vector2(1,1)
        };

        public TidePoolPattern(Rect arena, Vector2 target, bool boss, StageTerrainLayout terrain)
        {
            Radius = boss ? 1.9f : 1.4f;
            var positions = new List<Vector2>(boss ? 3 : 1);
            if (FindPrimary(arena, target, Radius, terrain, out var first))
            {
                positions.Add(first);
                if (boss)
                    foreach (var offset in Offsets)
                    {
                        // Reject a blocked location instead of clamping each well
                        // onto its neighbour. Near an edge the row extends inward.
                        var candidate = first + offset * Spacing;
                        if (!Fits(arena, candidate, Radius, terrain)) continue;
                        bool separate = true;
                        foreach (var placed in positions)
                            if ((candidate - placed).sqrMagnitude < Spacing * Spacing - .001f)
                            { separate = false; break; }
                        if (!separate) continue;
                        positions.Add(candidate);
                        if (positions.Count == 3) break;
                    }
            }
            Centers = positions.ToArray();
        }

        static bool FindPrimary(Rect arena, Vector2 target, float radius, StageTerrainLayout terrain, out Vector2 point)
        {
            float inset = radius + StageTerrainLayout.CellSize;
            point = target;
            if (arena.width < inset * 2 || arena.height < inset * 2) return false;
            var anchor = new Vector2(Mathf.Clamp(target.x, arena.xMin + inset, arena.xMax - inset),
                Mathf.Clamp(target.y, arena.yMin + inset, arena.yMax - inset));
            if (Fits(arena, anchor, radius, terrain)) { point = anchor; return true; }
            // Concave walls and cisterns are in the same fine mask as collision.
            // Search locally; no well may be created in a blocked fallback cell.
            float best = float.PositiveInfinity;
            for (int ring = 1; ring <= 8; ring++)
            {
                for (int y = -ring; y <= ring; y++)
                for (int x = -ring; x <= ring; x++)
                {
                    if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring) continue;
                    var candidate = anchor + new Vector2(x, y) * .5f;
                    float distance = (candidate - target).sqrMagnitude;
                    if (distance >= best || !Fits(arena, candidate, radius, terrain)) continue;
                    point = candidate; best = distance;
                }
                if (!float.IsPositiveInfinity(best)) return true;
            }
            return false;
        }

        public static bool Fits(Rect arena, Vector2 center, float radius, StageTerrainLayout terrain)
        {
            float clearance = radius + StageTerrainLayout.CellSize;
            if (center.x - clearance < arena.xMin - .0001f || center.x + clearance > arena.xMax + .0001f ||
                center.y - clearance < arena.yMin - .0001f || center.y + clearance > arena.yMax + .0001f) return false;
            // Encounter seals are temporary solid objects and are not cut into
            // the permanent terrain mask. Their whole footprint is reserved too.
            if (Physics2D.OverlapCircle(center, clearance, 1 << GameLayers.Ground) != null) return false;
            if (terrain == null) return true;
            float cell = StageTerrainLayout.CellSize;
            int x0 = Mathf.FloorToInt((center.x - clearance - terrain.Bounds.xMin) / cell);
            int x1 = Mathf.FloorToInt((center.x + clearance - terrain.Bounds.xMin) / cell);
            int y0 = Mathf.FloorToInt((center.y - clearance - terrain.Bounds.yMin) / cell);
            int y1 = Mathf.FloorToInt((center.y + clearance - terrain.Bounds.yMin) / cell);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (terrain.IsOpen(x, y)) continue;
                float left = terrain.Bounds.xMin + x * cell, bottom = terrain.Bounds.yMin + y * cell;
                var nearest = new Vector2(Mathf.Clamp(center.x, left, left + cell), Mathf.Clamp(center.y, bottom, bottom + cell));
                if ((nearest - center).sqrMagnitude < clearance * clearance) return false;
            }
            return true;
        }
    }
}
