using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuinRunner
{
    /// <summary>Cached geometry measurement only; never creates, slices or modifies an image.</summary>
    public static class SpriteVisibleBounds
    {
        private static readonly Dictionary<Sprite, Bounds> Cache = new Dictionary<Sprite, Bounds>();
        public static Color32[] ReadPixels(Texture2D texture)
        {
            if (texture.isReadable) return texture.GetPixels32();
            // This path is used by explicit validation/geometry diagnostics.
            // Keep production imports non-readable instead of retaining a CPU
            // copy of every animation texture just for occasional inspection.
            if (!SystemInfo.supportsAsyncGPUReadback)
                throw new System.InvalidOperationException("GPU pixel inspection is unavailable for " + texture.name);
            var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32);
            request.WaitForCompletion();
            if (request.hasError) throw new System.InvalidOperationException("GPU pixel inspection failed for " + texture.name);
            return request.GetData<Color32>().ToArray();
        }
        public static Bounds Local(Sprite sprite)
        {
            if (Cache.TryGetValue(sprite, out var bounds)) return bounds;
            var texture = sprite.texture;
            var colors = ReadPixels(texture);
            int left=texture.width,bottom=texture.height,right=-1,top=-1;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
            {
                if(colors[y*texture.width+x].a<128)continue;
                left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);
            }
            var pixels = right<left?new RectInt():new RectInt(left,bottom,right-left+1,top-bottom+1);
            if (pixels.width == 0) return sprite.bounds;
            var center = ((Vector2)pixels.position + (Vector2)pixels.size * .5f - sprite.pivot) / sprite.pixelsPerUnit;
            bounds = new Bounds(center, (Vector2)pixels.size / sprite.pixelsPerUnit);
            Cache.Add(sprite, bounds);
            return bounds;
        }
        public static Bounds World(SpriteRenderer renderer)
        {
            var local = Local(renderer.sprite);
            var center = local.center;
            if (renderer.flipX) center.x = -center.x;
            if (renderer.flipY) center.y = -center.y;
            var bounds = new Bounds(renderer.transform.TransformPoint(center), Vector3.zero);
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
                bounds.Encapsulate(renderer.transform.TransformPoint(center + new Vector3(local.extents.x * x, local.extents.y * y)));
            return bounds;
        }
    }
}
