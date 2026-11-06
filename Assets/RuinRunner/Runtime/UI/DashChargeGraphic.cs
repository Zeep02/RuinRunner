using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Native pixel-step chevrons. Fill is drawn as geometry.</summary>
    public sealed class DashChargeGraphic : MonoBehaviour
    {
        private static readonly string[] Rows = { "CCCW..", ".CCCW.", "..CCCW", ".CCCW.", "CCCW.." };
        private const float Pixel = .048f;
        private Mesh mesh;
        private Material material;
        private readonly List<Vector3> vertices = new List<Vector3>(400);
        private readonly List<Color32> colors = new List<Color32>(400);
        private readonly List<int> triangles = new List<int>(600);
        private int previousCharges = -1;
        private float previousProgress = -1;
        private int previousCapacity = -1;
        public float FirstFill { get; private set; }
        public float SecondFill { get; private set; }

        private void Awake()
        {
            mesh = new Mesh { name = "DashChevronGeometry" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default"));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 29;
        }

        public void SetCharges(int ready, float progress, int capacity = 2)
        {
            progress = Mathf.Clamp01(progress);
            if (ready == previousCharges && capacity == previousCapacity && Mathf.Approximately(progress, previousProgress))
                return;
            previousCharges = ready;
            previousProgress = progress;
            previousCapacity = capacity;
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            Quad(0, 0, 1 + capacity * 7, 7, new Color32(18, 22, 32, 255));
            FirstFill = ready > 0 ? 1 : progress;
            SecondFill = ready > 1 ? 1 : ready == 1 ? progress : 0;
            for (var charge = 0; charge < capacity; charge++)
            {
                var ratio = charge == 0 ? FirstFill : charge == 1 ? SecondFill : ready > charge ? 1 : ready == charge ? progress : 0;
                for (var y = 0; y < Rows.Length; y++)
                    for (var x = 0; x < Rows[y].Length; x++)
                    {
                        if (Rows[y][x] == '.')
                            continue;
                        var left = 1 + charge * 7 + x;
                        Quad(left, 1 + y, 1, 1, new Color32(41, 69, 79, 255));
                        var fill = Mathf.Clamp01(ratio * 6 - x);
                        if (fill > 0)
                            Quad(left, 1 + y, fill, 1,
                            Rows[y][x] == 'W' ? new Color32(224, 253, 255, 255) : new Color32(86, 218, 236, 255));
                    }
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void Quad(float x, float y, float width, float height, Color32 ink)
        {
            var start = vertices.Count;
            var left = (x - 7.5f) * Pixel;
            var top = (3.5f - y) * Pixel;
            vertices.Add(new Vector3(left, top));
            vertices.Add(new Vector3(left + width * Pixel, top));
            vertices.Add(new Vector3(left + width * Pixel, top - height * Pixel));
            vertices.Add(new Vector3(left, top - height * Pixel));
            for (var i = 0; i < 4; i++)
                colors.Add(ink);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
            if (material != null)
                Destroy(material);
        }
    }
}
