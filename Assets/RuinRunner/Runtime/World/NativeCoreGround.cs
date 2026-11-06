using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Native stone paving with connected wear across continuous quiet mortar.</summary>
    public sealed class NativeCoreGround : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        public int StoneCount { get; private set; }
        public int VariantMask { get; private set; }
        void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh != null)
                    Destroy(mesh);
        }
        sealed class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<Color32> Colors = new List<Color32>();
            public readonly List<int> Triangles = new List<int>();
        }
        static Material material;
        public static Material Material
        {
            get
            {
                if (material == null)
                    material = Resources.Load<Material>("Materials/CoreStone");
                return material;
            }
        }
        readonly struct Strip
        {
            public readonly Vector2 A, B;
            public readonly float HalfWidth;
            public Strip(Vector2 a, Vector2 b, float halfWidth)
            {
                A = a;
                B = b;
                HalfWidth = halfWidth;
            }
            public float EdgeDistance(Vector2 point)
            {
                var delta = B - A;
                var t = Mathf.Clamp01(Vector2.Dot(point - A, delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
                return Vector2.Distance(point, A + delta * t) - HalfWidth;
            }
        }

        public static void Build(Transform parent, StageTerrainLayout terrain, HashSet<Vector2Int> materialFloor)
        {
            var steps = BuildSteps(parent, terrain, materialFloor, false);
            while (steps.MoveNext())
            { }
        }
        public static IEnumerator BuildProgressively(Transform parent, StageTerrainLayout terrain, HashSet<Vector2Int> materialFloor)
            => BuildSteps(parent, terrain, materialFloor, true);
        static IEnumerator BuildSteps(Transform parent, StageTerrainLayout terrain, HashSet<Vector2Int> materialFloor, bool progressive)
        {
            var budget = new TerrainBuildBudget(progressive);
            var strips = new List<Strip>();
            foreach (var paths in new[] { terrain.Connections, terrain.Trails, terrain.Exploration })
                foreach (var path in paths)
                {
                    float width = Mathf.Clamp(path.width * .27f, .75f, 1.3f);
                    for (int i = 1; i < path.points.Length; i++)
                        strips.Add(new Strip(path.points[i - 1], path.points[i], width));
                    if (path.points.Length == 0)
                        continue;
                    foreach (var end in new[] { path.points[0], path.points[path.points.Length - 1] })
                    {
                        int nearest = -1;
                        float best = float.PositiveInfinity;
                        for (int room = 0; room < terrain.Rooms.Length; room++)
                        {
                            var size = terrain.Rooms[room].Size + Vector2.one * 2;
                            if (!new Rect(terrain.Centers[room] - size * .5f, size).Contains(end))
                                continue;
                            float distance = (end - terrain.Centers[room]).sqrMagnitude;
                            if (distance < best)
                            {
                                best = distance;
                                nearest = room;
                            }
                        }
                        if (nearest < 0)
                            continue;
                        // Existing navigation supplies a passable approach around
                        // obelisks instead of drawing a road into their solid center.
                        var route = StageNavigation.FindPath(terrain, end, terrain.Centers[nearest]);
                        if (route == null)
                            continue;
                        for (int i = 1; i < route.Count; i++)
                            strips.Add(new Strip(route[i - 1], route[i], 1.15f));
                        if (budget.Exhausted)
                        {
                            yield return null;
                            budget.Resume();
                        }
                    }
                }
            // Sprite getters copy native arrays. Read each source once per stage,
            // rather than allocating vertices/UVs/triangles for every paving stone.
            var sprites = new PavingSource[16];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = new PavingSource(IndividualArt.Sprite("External/ChampiDungeon/Paving/" + i.ToString("D2")));
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            var root = new GameObject("Core stone paving");
            root.transform.SetParent(parent, false);
            var owner = root.AddComponent<NativeCoreGround>();
            var batches = new Dictionary<Texture2D, Batch>();
            foreach (var cell in materialFloor)
            {
                for (int y = 0; y < 2; y++)
                    for (int x = 0; x < 2; x++)
                    {
                        int px = cell.x * 2 + x, py = cell.y * 2 + y;
                        var point = new Vector2(px * .5f + .25f, py * .5f + .25f);
                        float edge = float.PositiveInfinity;
                        foreach (var strip in strips)
                            edge = Mathf.Min(edge, strip.EdgeDistance(point));
                        int hash = Hash(px, py);
                        // Intact paving surrounds connected, irregular worn patches.
                        // Traffic broadens the wear, never a hard rectangular court.
                        float erosion = Mathf.PerlinNoise(point.x * .23f + 8.1f, point.y * .23f + 28.7f);
                        float threshold = edge < -.2f ? .62f : .71f;
                        float opacity = Mathf.Clamp01((threshold + .06f - erosion) / .12f);
                        if (opacity < .025f || (erosion > threshold && hash % 5 == 0))
                            continue;
                        int variant = hash % 16;
                        var sprite = sprites[variant];
                        if (!batches.TryGetValue(sprite.Texture, out var batch))
                        {
                            batch = new Batch();
                            batches.Add(sprite.Texture, batch);
                        }
                        int first = batch.Vertices.Count;
                        foreach (var vertex in sprite.Vertices)
                            batch.Vertices.Add((Vector3)(point + vertex));
                        batch.Uv.AddRange(sprite.Uv);
                        for (int vertex = 0; vertex < sprite.Vertices.Length; vertex++)
                            batch.Colors.Add(new Color(1, 1, 1, opacity));
                        foreach (var index in sprite.Triangles)
                            batch.Triangles.Add(first + index);
                        owner.StoneCount++;
                        owner.VariantMask |= 1 << variant;
                    }
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
            // Use the imported Sprite geometry/UVs even after atlas packing.
            // One static mesh per source texture avoids thousands of components.
            foreach (var pair in batches)
            {
                var batch = pair.Value;
                var mesh = new Mesh { name = "Native core paving" };
                mesh.SetVertices(batch.Vertices);
                mesh.SetUVs(0, batch.Uv);
                mesh.SetColors(batch.Colors);
                mesh.SetTriangles(batch.Triangles, 0);
                mesh.RecalculateBounds();
                owner.meshes.Add(mesh);
                var obj = new GameObject("Core paving batch");
                obj.transform.SetParent(root.transform, false);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Material;
                renderer.sortingLayerName = "WorldFloor";
                renderer.sortingOrder = 1;
                var properties = new MaterialPropertyBlock();
                properties.SetTexture("_MainTex", pair.Key);
                renderer.SetPropertyBlock(properties);
                if (budget.Exhausted)
                {
                    yield return null;
                    budget.Resume();
                }
            }
        }
        readonly struct PavingSource
        {
            public readonly Texture2D Texture;
            public readonly Vector2[] Vertices, Uv;
            public readonly ushort[] Triangles;
            public PavingSource(Sprite sprite)
            {
                Texture = sprite.texture;
                Vertices = sprite.vertices;
                Uv = sprite.uv;
                Triangles = sprite.triangles;
            }
        }
        static int Hash(int x, int y)
        {
            unchecked
            {
                uint n = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                n ^= n >> 13;
                n *= 1274126177;
                return (int)(n & 0x7fffffff);
            }
        }
    }
}
