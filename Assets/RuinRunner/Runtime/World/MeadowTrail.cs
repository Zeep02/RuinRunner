using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Low-cost stepped ribbon geometry textured by one individual dirt tile. No runtime bitmap generation.</summary>
    public static class MeadowTrail
    {
        public static void Create(Transform parent, RoomTemplate room, Vector2 center, RoomTravelDirection exit)
        {
            var end = room.ExitPoint(exit);
            var outward = exit == RoomTravelDirection.Left ? Vector2.left : exit == RoomTravelDirection.Up ? Vector2.up : exit == RoomTravelDirection.Down ? Vector2.down : Vector2.right;
            var start = -outward * Mathf.Min(3f, room.Size.magnitude * .1f);
            end += outward * 2.4f;
            Ribbon(parent, center + start, center + end, .0625f, -902, new Color(.72f, .82f, .66f), "TrailGrassRim");
            Ribbon(parent, center + start, center + end, 0f, -900, Color.white, "OpenEarthTrail");
        }

        private static void Ribbon(Transform parent, Vector2 start, Vector2 end, float extraWidth, int order, Color tint, string name)
        {
            var axis = (end - start).normalized;
            var across = new Vector2(-axis.y, axis.x);
            var distance = Vector2.Distance(start, end);
            var count = Mathf.CeilToInt(distance * 4f);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var bend = Mathf.Sin(t * Mathf.PI * 2f) * .24f;
                var midpoint = Vector2.Lerp(start, end, t) + across * bend;
                var cap = Mathf.Sqrt(Mathf.Clamp01(t * distance / .8f));
                var halfWidth = (.95f + Mathf.Sin(i * .29f) * .07f + Mathf.Sin(i * .7f) * .06f) * cap + extraWidth;
                for (var edge = -1; edge <= 1; edge += 2)
                {
                    var point = midpoint + across * (halfWidth * edge);
                    point = new Vector2(Mathf.Round(point.x * 16f), Mathf.Round(point.y * 16f)) / 16f;
                    vertices.Add(point); uv.Add(point * .5f);
                }
                if (i == 0) continue;
                var n = i * 2;
                triangles.Add(n - 2); triangles.Add(n); triangles.Add(n - 1);
                triangles.Add(n - 1); triangles.Add(n); triangles.Add(n + 1);
            }
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sortingLayerName = "WorldFloor"; renderer.sortingOrder = order;
            var texture = EnvironmentArt.Dirt.texture;
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, color = tint };
            renderer.sharedMaterial = material;
            root.AddComponent<MeadowTrailLifetime>().Initialize(mesh, material);
        }
    }

    internal sealed class MeadowTrailLifetime : MonoBehaviour
    {
        private Mesh mesh; private Material material;
        public void Initialize(Mesh ownedMesh, Material ownedMaterial) { mesh = ownedMesh; material = ownedMaterial; }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
    }
}
