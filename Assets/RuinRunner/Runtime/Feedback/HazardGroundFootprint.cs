using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Information layer. Geometry exactly matches the hazard test; attack art is separate.
    /// A second copy of the shape fills up over the warning and reaches the rim as the hit lands.</summary>
    public sealed class HazardGroundFootprint : MonoBehaviour
    {
        const float RimWidth = 2f / 32f;
        static Material material;
        Mesh mesh;
        Color tint;
        Color[] colors;
        Color[] growColors;
        Mesh growMesh;
        Transform grow;
        Vector2 size;
        bool round;
        bool thinLine;
        float arc;
        float[] vertexAngles;
        LineRenderer rim;
        public static HazardGroundFootprint Create(Transform parent, Vector2 size, bool round, Color tint, float arc = 360, bool capsule = false)
        {
            var root = new GameObject("CompleteDangerFootprint");
            root.transform.SetParent(parent, false);
            var shape = root.AddComponent<HazardGroundFootprint>();
            shape.tint = tint;
            shape.size = size;
            shape.round = round && !capsule;
            shape.arc = arc;
            shape.thinLine = !round && !capsule && size.y < .3f;
            var filter = root.AddComponent<MeshFilter>();
            shape.mesh = new Mesh { name = "LockedHazardFootprint" };
            filter.sharedMesh = shape.mesh;
            var view = root.AddComponent<MeshRenderer>();
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default")) { name = "HazardFootprintMaterial" };
            view.sharedMaterial = material;
            view.sortingLayerName = "WorldFloor";
            view.sortingOrder = 7;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            if (capsule)
            {
                float radius = size.y * .5f, halfSegment = Mathf.Max(0, size.x * .5f - radius);
                const int endSegments = 16;
                vertices.Add(Vector3.zero);
                for (int cap = 0; cap < 2; cap++)
                    for (int i = 0; i <= endSegments; i++)
                    {
                        float angle = (-90f + 180f * cap + 180f * i / endSegments) * Mathf.Deg2Rad;
                        vertices.Add(new Vector3((cap == 0 ? halfSegment : -halfSegment) + Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
                    }
                for (int i = 1; i < vertices.Count; i++)
                {
                    triangles.Add(0);
                    triangles.Add(i == vertices.Count - 1 ? 1 : i + 1);
                    triangles.Add(i);
                }
            }
            else if (round)
            {
                int segments = Mathf.Max(8, Mathf.CeilToInt(arc / 7.5f));
                vertices.Add(Vector3.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (-arc * .5f + arc * i / segments) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * size.x * .5f, Mathf.Sin(angle) * size.y * .5f, 0));
                    if (i > 0)
                    {
                        triangles.Add(0);
                        triangles.Add(i + 1);
                        triangles.Add(i);
                    }
                }
            }
            else
            {
                vertices.Add(new Vector3(-size.x * .5f, -size.y * .5f, 0));
                vertices.Add(new Vector3(-size.x * .5f, size.y * .5f, 0));
                vertices.Add(new Vector3(size.x * .5f, size.y * .5f, 0));
                vertices.Add(new Vector3(size.x * .5f, -size.y * .5f, 0));
                triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            }
            shape.mesh.SetVertices(vertices);
            shape.mesh.SetTriangles(triangles, 0);
            shape.mesh.RecalculateBounds();
            shape.colors = new Color[vertices.Count];
            if (!shape.thinLine)
            {
                var fill = new GameObject("GrowingDangerFill");
                fill.transform.SetParent(root.transform, false);
                shape.grow = fill.transform;
                shape.growMesh = new Mesh { name = "GrowingHazardFill" };
                shape.growMesh.SetVertices(vertices);
                shape.growMesh.SetTriangles(triangles, 0);
                shape.growMesh.RecalculateBounds();
                fill.AddComponent<MeshFilter>().sharedMesh = shape.growMesh;
                var fillView = fill.AddComponent<MeshRenderer>();
                fillView.sharedMaterial = material;
                fillView.sortingLayerName = "WorldFloor";
                fillView.sortingOrder = 7;
                shape.growColors = new Color[vertices.Count];
                if (shape.round && arc < 359)
                {
                    shape.vertexAngles = new float[vertices.Count];
                    for (int i = 1; i < vertices.Count; i++)
                        shape.vertexAngles[i] = Mathf.Atan2(vertices[i].y, vertices[i].x) * Mathf.Rad2Deg;
                }
                // Circles and rectangles already carry a contour sprite; sectors and rush routes have none.
                if (capsule || (round && arc < 359))
                {
                    var outline = new List<Vector3>();
                    for (int i = 1; i < vertices.Count; i++)
                        outline.Add(vertices[i]);
                    if (!capsule)
                        outline.Add(vertices[0]);
                    var line = root.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.loop = true;
                    line.widthMultiplier = RimWidth;
                    line.numCapVertices = 0;
                    line.sharedMaterial = material;
                    line.sortingLayerName = "WorldFloor";
                    line.sortingOrder = 8;
                    line.positionCount = outline.Count;
                    line.SetPositions(outline.ToArray());
                    shape.rim = line;
                }
            }
            shape.Sample(0, 1, false);
            return shape;
        }
        public void Sample(float elapsed, float warning, bool active, float activeSeconds = 1f)
        {
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, warning));
            var color = tint;
            color.a = active ? .4f : .08f + progress * .1f;
            for (int i = 0; i < colors.Length; i++)
                colors[i] = color;
            mesh.colors = colors;
            if (grow != null)
            {
                // Circles and sectors open from their centre; rectangles and rush routes run from the attacker's end.
                var eased = progress * progress * (3f - 2f * progress);
                if (round)
                    grow.localScale = new Vector3(eased, eased, 1f);
                else
                {
                    grow.localScale = new Vector3(eased, 1f, 1f);
                    grow.localPosition = new Vector3(-(1f - eased) * size.x * .5f, 0f, 0f);
                }
                var fill = tint;
                fill.a = active ? 0f : .16f + progress * .2f;
                for (int i = 0; i < growColors.Length; i++)
                    growColors[i] = fill;
                if (active && vertexAngles != null)
                {
                    // A bright edge runs across the wedge over the hit's own window.
                    float head = Mathf.Lerp(-arc * .5f, arc * .5f, Mathf.Clamp01((elapsed - warning) / Mathf.Max(.01f, activeSeconds)));
                    for (int i = 1; i < growColors.Length; i++)
                    {
                        float behind = head - vertexAngles[i];
                        float lit = behind < 0f ? 0f : 1f - Mathf.Clamp01(behind / 60f);
                        growColors[i] = Color.Lerp(tint, Color.white, lit * .75f);
                        growColors[i].a = lit * .85f;
                    }
                    growColors[0] = new Color(1f, 1f, 1f, .15f);
                }
                growMesh.colors = growColors;
            }
            if (rim != null)
            {
                var edge = tint;
                edge.a = active ? 1f : .55f + .4f * progress;
                rim.startColor = rim.endColor = edge;
            }
        }
        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
            if (growMesh != null)
                Destroy(growMesh);
        }
    }
}
