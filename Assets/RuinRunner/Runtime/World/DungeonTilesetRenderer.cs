using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Stage-specific terrain artwork over shared authored floor and collision geometry.</summary>
    public static class DungeonTilesetRenderer
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static GameObject Build(Transform parent, StageTerrainLayout terrain, int stageIndex = 0, Generation.GeneratedStage graph = null)
        {
            var root = new GameObject(StageTerrainArt.Theme(stageIndex) + " continuous terrain");
            root.transform.SetParent(parent, false);
            var steps = BuildSteps(root, terrain, stageIndex, graph, false);
            while (steps.MoveNext())
            { }
            return root;
        }
        public static IEnumerator BuildProgressively(Transform parent, StageTerrainLayout terrain, int stageIndex, Generation.GeneratedStage graph)
        {
            var root = new GameObject(StageTerrainArt.Theme(stageIndex) + " continuous terrain");
            root.transform.SetParent(parent, false);
            return BuildSteps(root, terrain, stageIndex, graph, true);
        }
        // Both paths create exactly the same geometry, in the same order, on Unity's main thread.
        static IEnumerator BuildSteps(GameObject root, StageTerrainLayout terrain, int stageIndex, Generation.GeneratedStage graph, bool progressive)
        {
            var budget = new TerrainBuildBudget(progressive);
            var floor = new HashSet<Vector2Int>();
            for (var y = Mathf.FloorToInt(terrain.Bounds.yMin); y < terrain.Bounds.yMax; y++)
            {
                for (var x = Mathf.FloorToInt(terrain.Bounds.xMin); x < terrain.Bounds.xMax; x++)
                    if (terrain.IsOpen(new Vector2(x + .5f, y + .5f)))
                        floor.Add(new Vector2Int(x, y));
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            var obstacleCells = new HashSet<Vector2Int>();
            for (var region = 0; region < terrain.Rooms.Length; region++)
                foreach (var obstacle in terrain.Rooms[region].platforms)
                    obstacleCells.UnionWith(ObstacleCells(terrain, terrain.Centers[region] + obstacle.position, obstacle.size));
            var border = new HashSet<Vector2Int>();

            foreach (var cell in floor)
            {
                var point = Center(cell);
                var surface = DrawGround(root.transform, cell, stageIndex, terrain, floor);
                // The authored wall silhouettes have 2–4 transparent pixels at the
                // floor-facing edge. Continue the same ground underneath that lip.
                // Copies retain the original pixel density and sit below all floors.
                for (var dy = -1; dy <= 1; dy++)
                    for (var dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && !floor.Contains(cell + new Vector2Int(dx, dy)))
                            GroundContact(root.transform, surface, point + new Vector2(dx, dy) * .125f);
                foreach (var d in Directions)
                    if (!floor.Contains(cell + d))
                        border.Add(cell + d);
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            foreach (var cell in border)
            {
                var wall = new GameObject("Wall collision");
                wall.transform.SetParent(root.transform);
                wall.transform.position = Center(cell);
                wall.layer = GameLayers.Ground;
                wall.AddComponent<BoxCollider2D>().size = Vector2.one;
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            // A diagonal-only floor neighbor needs a concave corner in the black masonry.
            var corners = new HashSet<Vector2Int>();
            foreach (var cell in floor)
                foreach (var dx in new[] { -1, 1 })
                    foreach (var dy in new[] { -1, 1 })
                    {
                        var corner = cell + new Vector2Int(dx, dy);
                        if (floor.Contains(corner) || border.Contains(corner) || !corners.Add(corner))
                            continue;
                        if (floor.Contains(cell + new Vector2Int(dx, 0)) || floor.Contains(cell + new Vector2Int(0, dy)))
                            continue;
                        var wall = new GameObject("Corner collision");
                        wall.transform.SetParent(root.transform);
                        wall.transform.position = Center(corner);
                        wall.layer = GameLayers.Ground;
                        wall.AddComponent<BoxCollider2D>();
                        if (budget.Exhausted)
                        {
                            yield return null;
                            budget.Resume();
                        }
                    }
            for (var region = 0; region < terrain.Rooms.Length; region++)
                foreach (var obstacle in terrain.Rooms[region].platforms)
                {
                    var cells = ObstacleCells(terrain, terrain.Centers[region] + obstacle.position, obstacle.size);
                    BuildObstacleBed(root.transform, cells, stageIndex, terrain, floor);
                    if (budget.Exhausted)
                    {
                        yield return null;
                        budget.Resume();
                    }
                }
            if (progressive)
                yield return ContinuousWallRibbon.BuildProgressively(root.transform, floor, obstacleCells, stageIndex);
            else
                ContinuousWallRibbon.Build(root.transform, floor, obstacleCells, stageIndex);
            if (stageIndex == 2)
            {
                // The floor material continues beneath solid props. Collision
                // islands must not create a different ring of wall-edge tiles.
                var materialFloor = new HashSet<Vector2Int>(floor);
                materialFloor.UnionWith(obstacleCells);
                if (progressive)
                    yield return NativeCoreGround.BuildProgressively(root.transform, terrain, materialFloor);
                else
                    NativeCoreGround.Build(root.transform, terrain, materialFloor);
            }
            var placement = new EnvironmentPlacementRules(terrain, graph);
            if (progressive)
                yield return CampaignEnvironmentArt.BuildProgressively(root.transform, terrain, stageIndex, placement);
            else
                CampaignEnvironmentArt.Build(root.transform, terrain, stageIndex, placement);
        }

        private static Vector2 Center(Vector2Int cell) => (Vector2)cell + Vector2.one * .5f;
        private static SpriteRenderer DrawGround(Transform parent, Vector2Int cell, int stage, StageTerrainLayout terrain, HashSet<Vector2Int> floor)
        {
            var obj = new GameObject("Continuous ground");
            obj.transform.SetParent(parent, false);
            obj.transform.position = Center(cell);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = StageTerrainArt.Ground(stage, cell, terrain, floor);
            renderer.color = NativeStageGround.Tint(stage);
            if (stage == 2)
                renderer.sharedMaterial = NativeCoreGround.Material;
            renderer.sortingLayerName = "WorldFloor";
            renderer.sortingOrder = 0;
            return renderer;
        }
        private static void GroundContact(Transform parent, SpriteRenderer source, Vector2 point)
        {
            var obj = new GameObject("Floor contact underlay");
            obj.transform.SetParent(parent, false);
            obj.transform.position = point;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.color = source.color;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerName = "WorldFloor";
            renderer.sortingOrder = -1;
        }
        private static HashSet<Vector2Int> ObstacleCells(StageTerrainLayout terrain, Vector2 center, Vector2 size)
        {
            var cells = new HashSet<Vector2Int>();
            for (var y = Mathf.FloorToInt(center.y - size.y * .5f); y < Mathf.CeilToInt(center.y + size.y * .5f); y++)
                for (var x = Mathf.FloorToInt(center.x - size.x * .5f); x < Mathf.CeilToInt(center.x + size.x * .5f); x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!terrain.IsOpen(Center(cell)))
                        cells.Add(cell);
                }
            return cells;
        }
        private static void BuildObstacleBed(Transform parent, HashSet<Vector2Int> cells, int stage, StageTerrainLayout terrain, HashSet<Vector2Int> floor)
        {
            foreach (var cell in cells)
            {
                // Ground continues beneath each object, including the rounded cistern corners.
                DrawGround(parent, cell, stage, terrain, floor);
            }
        }
    }

    // A cooperative budget, never a worker thread. Resume restarts after the yielded frame,
    // so display/vsync time is not mistaken for work performed by the next batch.
    internal sealed class TerrainBuildBudget
    {
        readonly System.Diagnostics.Stopwatch clock;
        public TerrainBuildBudget(bool progressive)
        {
            if (progressive)
                clock = System.Diagnostics.Stopwatch.StartNew();
        }
        public bool Exhausted => clock != null && clock.Elapsed.TotalMilliseconds >= 4;
        public void Resume() => clock?.Restart();
    }
}
