using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuinRunner
{
    /// <summary>Fused land surfaces with world-space UVs: no full-block tile borders or room perimeter props.</summary>
    public sealed class ContinuousTerrainRenderer : MonoBehaviour
    {
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Material> ownedMaterials = new List<Material>();

        public static void Build(Transform parent, StageTerrainLayout layout)
        {
            var root = new GameObject("ContinuousMeadowTerrain");
            root.transform.SetParent(parent, false);
            var renderer = root.AddComponent<ContinuousTerrainRenderer>();
            renderer.Surface("MeadowFloor", layout, 0, IndividualArt.Texture("ContinuousWorld/GrassCalm"), -1000);
            renderer.Surface("DirtClearings", layout, 1, IndividualArt.Texture("ContinuousWorld/DirtCalm"), -990);
            renderer.Surface("FusedRaisedLand", layout, 2, IndividualArt.Texture("ContinuousWorld/PlateauTop"), -600);
            renderer.Edges(layout);
            renderer.PlantGroves(layout);
        }

        private void PlantGroves(StageTerrainLayout layout)
        {
            // Sparse, asymmetric land-side groves. No collider and no continuous
            // row of bushes masquerading as a boundary. Gate mouths stay clear.
            for (var i = 0; i < layout.Rooms.Length; i++)
            {
                var center = layout.Centers[i];
                var half = layout.Rooms[i].Size * .5f;
                var anchors = new[] { new Vector2(-half.x * .64f, half.y * .87f),
                    new Vector2(half.x * .73f, -half.y * .88f), new Vector2(half.x * .92f, half.y * .6f) };
                for (var k = 0; k < anchors.Length; k++)
                {
                    var point = center + anchors[k];
                    // Move outward until the trunk is on raised land, not a path.
                    for (var n = 0; n < 18 && layout.IsOpen(point); n++)
                        point += anchors[k].normalized * .25f;
                    if (layout.IsOpen(point))
                        continue;
                    EnvironmentProp.Create(transform, point, EnvironmentPropKind.Tree, 1f);
                    if (k == 0)
                        EnvironmentProp.Create(transform, point + new Vector2(-2f, .7f), EnvironmentPropKind.Tree, 1f);
                }
                // A few responsive grass tufts in authored clear areas.
                for (var k = 0; k < 3; k++)
                {
                    var point = center + new Vector2((k - 1) * half.x * .55f, half.y * .47f - k * .8f);
                    if (layout.IsOpen(point))
                        EnvironmentProp.Create(transform, point, EnvironmentPropKind.Grass, 1f);
                }
            }
        }

        private void Surface(string name, StageTerrainLayout layout, int mode, Texture2D texture, int order)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            for (var y = 0; y < layout.Height; y++)
            {
                var start = -1;
                for (var x = 0; x <= layout.Width; x++)
                {
                    var filled = x < layout.Width && (mode == 0 || (mode == 1 ? layout.Path[y * layout.Width + x] : !layout.IsOpen(x, y)));
                    if (filled && start < 0)
                        start = x;
                    if (filled || start < 0)
                        continue;
                    var min = layout.Bounds.min + new Vector2(start, y) * StageTerrainLayout.CellSize;
                    var max = layout.Bounds.min + new Vector2(x, y + 1) * StageTerrainLayout.CellSize;
                    Quad(vertices, uvs, indices, min, max, 2f);
                    start = -1;
                }
            }
            DrawMesh(name, vertices, uvs, indices, texture, order);
        }

        private void Edges(StageTerrainLayout layout)
        {
            var faceVertices = new List<Vector3>();
            var faceUvs = new List<Vector2>();
            var faceIndices = new List<int>();
            var rimVertices = new List<Vector3>();
            var rimUvs = new List<Vector2>();
            var rimIndices = new List<int>();
            var physics = new GameObject("WalkableContour");
            physics.transform.SetParent(transform, false);
            physics.layer = GameLayers.Ground;
            // Merge each collinear boundary into one edge collider; independent of drawing and gate state.
            for (var y = 0; y <= layout.Height; y++)
            {
                var start = -1;
                var previousSide = 0;
                for (var x = 0; x <= layout.Width; x++)
                {
                    var side = x < layout.Width ? (layout.IsOpen(x, y - 1) ? 1 : 0) - (layout.IsOpen(x, y) ? 1 : 0) : 0;
                    if (start >= 0 && side != previousSide)
                    {
                        var from = layout.Bounds.min + new Vector2(start, y) * StageTerrainLayout.CellSize;
                        var to = layout.Bounds.min + new Vector2(x, y) * StageTerrainLayout.CellSize;
                        AddEdge(physics, from, to);
                        if (previousSide == 1)
                            Quad(faceVertices, faceUvs, faceIndices, from, to + Vector2.up * .55f, 2f);
                        var lipHeight = previousSide == 1 ? .55f : 0f;
                        Quad(rimVertices, rimUvs, rimIndices, from + Vector2.up * (lipHeight - .03125f), to + Vector2.up * (lipHeight + .03125f), 2f);
                        start = -1;
                    }
                    if (side != 0 && start < 0)
                        start = x;
                    previousSide = side;
                }
            }
            for (var x = 0; x <= layout.Width; x++)
            {
                var start = -1;
                var previousSide = 0;
                for (var y = 0; y <= layout.Height; y++)
                {
                    var side = y < layout.Height ? (layout.IsOpen(x - 1, y) ? 1 : 0) - (layout.IsOpen(x, y) ? 1 : 0) : 0;
                    if (start >= 0 && side != previousSide)
                    {
                        var from = layout.Bounds.min + new Vector2(x, start) * StageTerrainLayout.CellSize;
                        var to = layout.Bounds.min + new Vector2(x, y) * StageTerrainLayout.CellSize;
                        AddEdge(physics, from, to);
                        // A narrow soil rim on east/west edges, not another complete cliff block.
                        Quad(faceVertices, faceUvs, faceIndices, from - Vector2.right * .08f, to + Vector2.right * .08f, 2f);
                        Quad(rimVertices, rimUvs, rimIndices, from - Vector2.right * .03125f, to + Vector2.right * .03125f, 2f);
                        start = -1;
                    }
                    if (side != 0 && start < 0)
                        start = y;
                    previousSide = side;
                }
            }
            DrawMesh("ContinuousCliffFaces", faceVertices, faceUvs, faceIndices, IndividualArt.Texture("ContinuousWorld/CliffFace"), -599);
            DrawMesh("GrassLip", rimVertices, rimUvs, rimIndices, IndividualArt.Texture("ContinuousWorld/GrassCalm"), -598, new Color(.65f, .73f, .5f));
        }

        private static void AddEdge(GameObject root, Vector2 from, Vector2 to)
        {
            var edge = root.AddComponent<EdgeCollider2D>();
            edge.points = new[] { from, to };
            edge.edgeRadius = .04f;
        }

        private static void Quad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector2 min, Vector2 max, float period)
        {
            var index = vertices.Count;
            vertices.Add(new Vector3(min.x, min.y));
            vertices.Add(new Vector3(min.x, max.y));
            vertices.Add(new Vector3(max.x, max.y));
            vertices.Add(new Vector3(max.x, min.y));
            uvs.Add(min / period);
            uvs.Add(new Vector2(min.x, max.y) / period);
            uvs.Add(max / period);
            uvs.Add(new Vector2(max.x, min.y) / period);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        private void DrawMesh(string name, List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Texture2D texture, int order, Color? tint = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var colors = new Color32[vertices.Count];
            for (var i = 0; i < colors.Length; i++)
                colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors;
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            ownedMeshes.Add(mesh);
            texture.wrapMode = TextureWrapMode.Repeat;
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, color = tint ?? Color.white };
            ownedMaterials.Add(material);
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = "WorldFloor";
            renderer.sortingOrder = order;
        }

        private void OnDestroy()
        {
            foreach (var mesh in ownedMeshes)
                Destroy(mesh);
            foreach (var material in ownedMaterials)
                Destroy(material);
        }
    }
}
