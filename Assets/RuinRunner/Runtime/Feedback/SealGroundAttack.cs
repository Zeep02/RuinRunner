using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Low ground energy on a 32px world grid. One continuous attack, below every actor.</summary>
    public sealed class SealGroundAttack : MonoBehaviour
    {
        const float Pixel = 1f / 32;
        static Material material;
        Mesh mesh;
        MeshRenderer view;
        Color[] colors;
        float[] radii;
        byte[] bands;
        bool sweep;
        float radius;
        static readonly Color Violet = new Color(62 / 255f, 43 / 255f, 85 / 255f, .48f);
        static readonly Color Teal = new Color(25 / 255f, 179 / 255f, 193 / 255f, .65f);
        static readonly Color Ivory = new Color(218 / 255f, 224 / 255f, 193 / 255f, .85f);

        public static SealGroundAttack Create(Transform parent, Vector2 size, bool sweep)
        {
            var root = new GameObject(sweep ? "SealContinuousWave" : "SealGroundFissure");
            root.transform.SetParent(parent, false);
            var effect = root.AddComponent<SealGroundAttack>();
            effect.sweep = sweep;
            effect.radius = size.x * .5f;
            effect.mesh = new Mesh { name = sweep ? "SealWavePixelGrid" : "SealRiftPixelGrid" };
            root.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            effect.view = root.AddComponent<MeshRenderer>();
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default")) { name = "SealGroundEnergyMaterial" };
            effect.view.sharedMaterial = material;
            effect.view.sortingLayerName = "WorldFloor";
            effect.view.sortingOrder = 9;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var distances = new List<float>();
            var strips = new List<byte>();
            int left = Mathf.FloorToInt(-size.x * .5f / Pixel), right = Mathf.CeilToInt(size.x * .5f / Pixel);
            int low = Mathf.FloorToInt(-size.y * .5f / Pixel), high = Mathf.CeilToInt(size.y * .5f / Pixel);
            for (int y = low; y < high; y++)
                for (int x = left; x < right; x++)
                {
                    Vector2 center = new Vector2((x + .5f) * Pixel, (y + .5f) * Pixel);
                    if (Mathf.Abs(center.x) > size.x * .5f || Mathf.Abs(center.y) > size.y * .5f)
                        continue;
                    byte strip = 0;
                    if (sweep)
                    {
                        if (center.sqrMagnitude > effect.radius * effect.radius || Mathf.Abs(Mathf.Atan2(center.y, center.x) * Mathf.Rad2Deg) > 67.5f)
                            continue;
                    }
                    else
                    {
                        // Jagged central seam and short connected branches use the
                        // same palette as the core. The full-width danger footprint
                        // remains visible; these floor marks never become tall props.
                        int zig = ((x % 24 + 24) % 24);
                        int centerY = zig < 6 ? 0 : zig < 12 ? 1 : zig < 18 ? 0 : -1;
                        int dy = Mathf.Abs(y - centerY), edge = Mathf.RoundToInt(size.y * .5f / Pixel) - 1;
                        int branch = (x % 40 + 40) % 40;
                        bool fracture = branch < 11 && Mathf.Abs(Mathf.Abs(y) - branch) <= 1;
                        if (dy <= 1)
                            strip = 2;
                        else if (dy <= 3 || fracture)
                            strip = 1;
                        else if (Mathf.Abs(y) >= edge - 1)
                            strip = 1;
                        else
                            continue;
                    }
                    int index = vertices.Count;
                    float px = x * Pixel, py = y * Pixel;
                    vertices.Add(new Vector3(px, py));
                    vertices.Add(new Vector3(px, py + Pixel));
                    vertices.Add(new Vector3(px + Pixel, py + Pixel));
                    vertices.Add(new Vector3(px + Pixel, py));
                    triangles.Add(index);
                    triangles.Add(index + 1);
                    triangles.Add(index + 2);
                    triangles.Add(index);
                    triangles.Add(index + 2);
                    triangles.Add(index + 3);
                    distances.Add(center.magnitude);
                    strips.Add(strip);
                }
            if (vertices.Count > 65535)
                effect.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            effect.mesh.SetVertices(vertices);
            effect.mesh.SetTriangles(triangles, 0);
            effect.mesh.RecalculateBounds();
            effect.mesh.MarkDynamic();
            effect.radii = distances.ToArray();
            effect.bands = strips.ToArray();
            effect.colors = new Color[vertices.Count];
            effect.mesh.colors = effect.colors;
            effect.view.enabled = false;
            return effect;
        }
        public void Sample(float elapsed, float warning, float active)
        {
            float release = elapsed - warning;
            view.enabled = release >= 0;
            if (!view.enabled)
                return;
            float progress = Mathf.Clamp01(release / Mathf.Max(.01f, active));
            float front = Mathf.Lerp(.65f, radius - .15f, progress);
            float fade = release < active ? 1 : 1 - Mathf.Clamp01((release - active) / ArenaHazardArt.TailSeconds);
            for (int i = 0; i < radii.Length; i++)
            {
                Color color;
                if (sweep)
                {
                    float distance = Mathf.Abs(radii[i] - front);
                    color = distance < Pixel * 2 ? Ivory : distance < .24f ? Teal : Violet;
                    // Same moving .48-unit band as ArenaHazard.ContainsActive;
                    // raster edges differ by at most one source pixel.
                    if (distance > .48f)
                        color.a = 0;
                }
                else
                    color = bands[i] == 2 ? Ivory : Teal;
                color.a *= fade;
                for (int corner = 0; corner < 4; corner++)
                    colors[i * 4 + corner] = color;
            }
            mesh.colors = colors;
        }
        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
