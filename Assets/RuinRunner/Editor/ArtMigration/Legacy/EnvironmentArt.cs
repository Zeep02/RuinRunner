using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
    public static class EnvironmentArt
    {
        private static readonly Dictionary<int, Sprite> Props = new Dictionary<int, Sprite>();
        private static Sprite ground;
        private static Sprite cliff;
        public static bool Available => LegacySource.Load("Art/Pixel/Environment/live_grass_cliff_v2") != null;
        public static Sprite Ground => ground != null ? ground : (ground = Terrain(0));
        public static Sprite Cliff => cliff != null ? cliff : (cliff = Terrain(1));

        private static Sprite Terrain(int cell)
        {
            var source = LegacySource.Load("Art/Pixel/Environment/live_grass_cliff_v2");
            if (source == null) return null;
            var texture = Pack(source, 2, 1, cell, 64, false);
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * .5f, cell == 0 ? 16f : 32f,
                0, SpriteMeshType.FullRect, cell == 0 ? Vector4.zero : new Vector4(12, 18, 12, 8));
        }

        public static Sprite Prop(int cell)
        {
            Sprite cached;
            if (Props.TryGetValue(cell, out cached)) return cached;
            var source = LegacySource.Load("Art/Pixel/Environment/ruins_props_atlas_v1");
            if (source == null) return null;
            var texture = Pack(source, 4, 3, cell, 32, true);
            cached = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .06f), 32f, 0, SpriteMeshType.FullRect);
            Props[cell] = cached;
            return cached;
        }

        private static bool IsKey(Color32 p) => p.a < 128 || SpriteMatte.IsMagentaMatte(p);

        private static Texture2D Pack(Texture2D source, int columns, int rows, int index, int size, bool cutout)
        {
            var x0 = Mathf.RoundToInt((index % columns) * source.width / (float)columns);
            var x1 = Mathf.RoundToInt((index % columns + 1) * source.width / (float)columns);
            var y0 = Mathf.RoundToInt((rows - 1 - index / columns) * source.height / (float)rows);
            var y1 = Mathf.RoundToInt((rows - index / columns) * source.height / (float)rows);
            var pixels = source.GetPixels32();
            var left = x0; var right = x1 - 1; var bottom = y0; var top = y1 - 1;
            if (cutout)
            {
                left = x1; right = x0; bottom = y1; top = y0;
                for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    if (IsKey(pixels[y * source.width + x])) continue;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            }
            var width = right - left + 1; var height = top - bottom + 1;
            var output = new Color32[size * size];
            var scale = cutout ? (size - 4f) / Mathf.Max(width, height) : size / (float)width;
            var drawWidth = cutout ? Mathf.RoundToInt(width * scale) : size;
            var drawHeight = cutout ? Mathf.RoundToInt(height * scale) : size;
            var offsetX = (size - drawWidth) / 2;
            var offsetY = cutout ? 2 : 0;
            for (var y = 0; y < drawHeight; y++)
            for (var x = 0; x < drawWidth; x++)
            {
                var sx = left + Mathf.Min(width - 1, Mathf.FloorToInt((x + .5f) * width / drawWidth));
                var sy = bottom + Mathf.Min(height - 1, Mathf.FloorToInt((y + .5f) * height / drawHeight));
                var pixel = pixels[sy * source.width + sx];
                pixel = cutout ? SpriteMatte.ChromaCutout(pixel) : pixel;
                output[(y + offsetY) * size + x + offsetX] = pixel;
            }
            var result = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            result.SetPixels32(output); result.Apply(false, false);
            return result;
        }
    }
}

