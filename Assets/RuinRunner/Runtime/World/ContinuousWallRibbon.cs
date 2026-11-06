using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>A single joined cap footprint, with faces extruded from its lower outline.</summary>
    public sealed class ContinuousWallRibbon : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        public int CapCells { get; private set; }
        public int FaceCells { get; private set; }
        public static void Build(Transform parent, HashSet<Vector2Int> floor, HashSet<Vector2Int> obstacles, int stage)
        {
            var steps = BuildSteps(parent, floor, obstacles, stage, false);
            while (steps.MoveNext())
            { }
        }
        public static IEnumerator BuildProgressively(Transform parent, HashSet<Vector2Int> floor, HashSet<Vector2Int> obstacles, int stage)
            => BuildSteps(parent, floor, obstacles, stage, true);
        static IEnumerator BuildSteps(Transform parent, HashSet<Vector2Int> floor, HashSet<Vector2Int> obstacles, int stage, bool progressive)
        {
            var root = new GameObject("Continuous masonry ribbon");
            root.transform.SetParent(parent, false);
            var owner = root.AddComponent<ContinuousWallRibbon>();
            var caps = new HashSet<Vector2Int>();
            var footprint = FillFootprint(caps, floor, obstacles, progressive);
            while (footprint.MoveNext())
                yield return footprint.Current;
            owner.CapCells = caps.Count;
            var budget = new TerrainBuildBudget(progressive);
            var sprites = new SpriteRegion[8];
            for (var i = 0; i < 4; i++)
            {
                sprites[i] = new SpriteRegion(StageTerrainArt.Sprite(stage, "Ribbon/cap-" + i, Vector2.zero));
                sprites[i + 4] = new SpriteRegion(StageTerrainArt.Sprite(stage, "Ribbon/face-" + i, Vector2.zero));
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            var capBatches = new Dictionary<Texture2D, Batch>();
            var faceBatches = new Dictionary<Texture2D, Batch>();
            foreach (var c in caps)
            {
                var horizontal = caps.Contains(c + Vector2Int.left) || caps.Contains(c + Vector2Int.right);
                var index = Mod(horizontal ? c.x : c.y, 4);
                Add(capBatches, sprites[index], new Rect(c.x * .25f, c.y * .25f, .25f, .25f), !horizontal);
                // Only the exposed lower outline gets a front face. Interior edges
                // at straight/corner transitions cannot produce a seam or a stub.
                if (!caps.Contains(c + Vector2Int.down))
                {
                    Add(faceBatches, sprites[4 + Mod(c.x, 4)], new Rect(c.x * .25f, c.y * .25f - .5f, .25f, .5f), false);
                    owner.FaceCells++;
                }
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            var faces = owner.Emit(faceBatches, "Masonry faces", 18, progressive);
            while (faces.MoveNext())
                yield return faces.Current;
            var tops = owner.Emit(capBatches, "Masonry caps", 20, progressive);
            while (tops.MoveNext())
                yield return tops.Current;
        }
        public static HashSet<Vector2Int> Footprint(HashSet<Vector2Int> floor, HashSet<Vector2Int> obstacles)
        {
            var caps = new HashSet<Vector2Int>();
            var steps = FillFootprint(caps, floor, obstacles, false);
            while (steps.MoveNext())
            { }
            return caps;
        }
        static IEnumerator FillFootprint(HashSet<Vector2Int> caps, HashSet<Vector2Int> floor, HashSet<Vector2Int> obstacles, bool progressive)
        {
            var budget = new TerrainBuildBudget(progressive);
            foreach (var cell in floor)
            {
                for (var dy = -1; dy <= 4; dy++)
                    for (var dx = -1; dx <= 4; dx++)
                    {
                        if (dx >= 0 && dx < 4 && dy >= 0 && dy < 4)
                            continue;
                        var c = new Vector2Int(cell.x * 4 + dx, cell.y * 4 + dy);
                        var coarse = new Vector2Int(Mathf.FloorToInt(c.x / 4f), Mathf.FloorToInt(c.y / 4f));
                        if (!floor.Contains(coarse) && !obstacles.Contains(coarse))
                            caps.Add(c);
                    }
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
        }
        static int Mod(int x, int n) => (x % n + n) % n;
        static void Add(Dictionary<Texture2D, Batch> batches, SpriteRegion sprite, Rect bounds, bool rotate)
        {
            if (!batches.TryGetValue(sprite.Texture, out var batch))
            {
                batch = new Batch();
                batches.Add(sprite.Texture, batch);
            }
            var start = batch.vertices.Count;
            batch.vertices.Add(new Vector3(bounds.xMin, bounds.yMin));
            batch.vertices.Add(new Vector3(bounds.xMax, bounds.yMin));
            batch.vertices.Add(new Vector3(bounds.xMax, bounds.yMax));
            batch.vertices.Add(new Vector3(bounds.xMin, bounds.yMax));
            for (var i = 0; i < 4; i++)
                batch.uv.Add(sprite.Uv[(i + (rotate ? 1 : 0)) % 4]);
            batch.indices.Add(start);
            batch.indices.Add(start + 2);
            batch.indices.Add(start + 1);
            batch.indices.Add(start);
            batch.indices.Add(start + 3);
            batch.indices.Add(start + 2);
        }
        IEnumerator Emit(Dictionary<Texture2D, Batch> batches, string label, int order, bool progressive)
        {
            var budget = new TerrainBuildBudget(progressive);
            foreach (var pair in batches)
            {
                var mesh = new Mesh { name = label };
                mesh.SetVertices(pair.Value.vertices);
                mesh.SetUVs(0, pair.Value.uv);
                mesh.SetTriangles(pair.Value.indices, 0);
                mesh.RecalculateBounds();
                owned.Add(mesh);
                var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = pair.Key };
                owned.Add(material);
                var obj = new GameObject(label);
                obj.transform.SetParent(transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingLayerName = "WorldFloor";
                renderer.sortingOrder = order;
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
        }
        void OnDestroy()
        {
            foreach (var item in owned)
                if (item != null)
                    Destroy(item);
        }
        readonly struct SpriteRegion
        {
            public readonly Texture2D Texture;
            public readonly Vector2[] Uv;
            public SpriteRegion(Sprite sprite)
            {
                if (sprite == null)
                    throw new System.InvalidOperationException("Missing wall ribbon material");
                Texture = sprite.texture;
                var lo = Vector2.one * float.PositiveInfinity;
                var hi = Vector2.one * float.NegativeInfinity;
                foreach (var p in sprite.uv)
                {
                    lo = Vector2.Min(lo, p);
                    hi = Vector2.Max(hi, p);
                }
                Uv = new[] { lo, new Vector2(hi.x, lo.y), hi, new Vector2(lo.x, hi.y) };
            }
        }
        sealed class Batch
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> indices = new List<int>();
        }
    }
}
