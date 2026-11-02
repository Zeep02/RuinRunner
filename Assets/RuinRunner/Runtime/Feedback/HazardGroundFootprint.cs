using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Information layer. Geometry exactly matches the hazard test; attack art is separate.</summary>
    public sealed class HazardGroundFootprint : MonoBehaviour
    {
        static Material material;
        Mesh mesh; MeshRenderer view; Color tint;Color[] colors;
        public static HazardGroundFootprint Create(Transform parent, Vector2 size, bool round, Color tint, float arc = 360, bool capsule = false)
        {
            var root = new GameObject("CompleteDangerFootprint"); root.transform.SetParent(parent, false);
            var shape = root.AddComponent<HazardGroundFootprint>(); shape.tint = tint;
            var filter = root.AddComponent<MeshFilter>(); shape.mesh = new Mesh { name = "LockedHazardFootprint" };
            filter.sharedMesh = shape.mesh; shape.view = root.AddComponent<MeshRenderer>();
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "HazardFootprintMaterial" };
            shape.view.sharedMaterial = material; shape.view.sortingLayerName = "WorldFloor"; shape.view.sortingOrder = 7;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            if (capsule)
            {
                float radius=size.y*.5f, halfSegment=Mathf.Max(0,size.x*.5f-radius);
                const int endSegments=16;
                vertices.Add(Vector3.zero);
                for(int cap=0;cap<2;cap++)
                    for(int i=0;i<=endSegments;i++)
                    {
                        float angle=(-90f+180f*cap+180f*i/endSegments)*Mathf.Deg2Rad;
                        vertices.Add(new Vector3((cap==0?halfSegment:-halfSegment)+Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0));
                    }
                for(int i=1;i<vertices.Count;i++)
                {triangles.Add(0);triangles.Add(i==vertices.Count-1?1:i+1);triangles.Add(i);}
            }
            else if (round)
            {
                int segments = Mathf.Max(8, Mathf.CeilToInt(arc / 7.5f)); vertices.Add(Vector3.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (-arc * .5f + arc * i / segments) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * size.x * .5f, Mathf.Sin(angle) * size.y * .5f, 0));
                    if (i > 0) { triangles.Add(0); triangles.Add(i + 1); triangles.Add(i); }
                }
            }
            else
            {
                vertices.Add(new Vector3(-size.x*.5f,-size.y*.5f,0)); vertices.Add(new Vector3(-size.x*.5f,size.y*.5f,0));
                vertices.Add(new Vector3(size.x*.5f,size.y*.5f,0)); vertices.Add(new Vector3(size.x*.5f,-size.y*.5f,0));
                triangles.AddRange(new[] {0,1,2,0,2,3});
            }
            shape.mesh.SetVertices(vertices); shape.mesh.SetTriangles(triangles, 0); shape.mesh.RecalculateBounds();
            shape.colors=new Color[vertices.Count];
            shape.Sample(0, 1, false); return shape;
        }
        public void Sample(float elapsed, float warning, bool active)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, warning));
            var color = tint; color.a = active ? .32f : .08f + progress * .13f;
            for (int i=0;i<colors.Length;i++) colors[i]=color;
            mesh.colors=colors;
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
