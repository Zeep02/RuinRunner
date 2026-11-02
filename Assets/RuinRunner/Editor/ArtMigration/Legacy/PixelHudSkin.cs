using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
public static class PixelHudSkin
    {
        private const string ResourcePath = "Art/Pixel/HUD/status_bar_atlas";
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();

        public static bool Available => LegacySource.Load(ResourcePath) != null;
        public static Texture2D GetEmpty(PixelHudBarType type) => GetCell((int)type);
        public static Texture2D GetFilled(PixelHudBarType type) => GetCell(4 + (int)type);

        private static Texture2D GetCell(int index)
        {
            Texture2D cached;
            if (Cache.TryGetValue(index, out cached)) return cached;
            var source = LegacySource.Load(ResourcePath);
            if (source == null || !source.isReadable) return null;

            const int columns = 4;
            const int rows = 2;
            var column = index % columns;
            var row = index / columns;
            var xMin = Mathf.RoundToInt(column * source.width / (float)columns);
            var xMax = Mathf.RoundToInt((column + 1) * source.width / (float)columns);
            var yMin = Mathf.RoundToInt((rows - row - 1) * source.height / (float)rows);
            var yMax = Mathf.RoundToInt((rows - row) * source.height / (float)rows);
            var width = xMax - xMin;
            var height = yMax - yMin;
            var pixels = source.GetPixels(xMin, yMin, width, height);
            var left = width;
            var right = -1;
            var bottom = height;
            var top = -1;

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var pixelIndex = y * width + x;
                var color = (Color)SpriteMatte.ChromaCutout((Color32)pixels[pixelIndex]);
                pixels[pixelIndex] = color;
                if (color.a <= .02f)
                {
                    color.a = 0f;
                    pixels[pixelIndex] = color;
                    continue;
                }
                if (color.a <= .02f) continue;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }

            if (right < left || top < bottom) return null;
            left = Mathf.Max(0, left - 2);
            right = Mathf.Min(width - 1, right + 2);
            bottom = Mathf.Max(0, bottom - 2);
            top = Mathf.Min(height - 1, top + 2);
            var croppedWidth = right - left + 1;
            var croppedHeight = top - bottom + 1;
            var cropped = new Color[croppedWidth * croppedHeight];
            for (var y = 0; y < croppedHeight; y++)
                System.Array.Copy(pixels, (bottom + y) * width + left, cropped, y * croppedWidth, croppedWidth);

            cached = new Texture2D(croppedWidth, croppedHeight, TextureFormat.RGBA32, false);
            cached.name = "PixelHudBar_" + index;
            cached.filterMode = FilterMode.Point;
            cached.wrapMode = TextureWrapMode.Clamp;
            cached.SetPixels(cropped);
            cached.Apply(false, false);
            Cache[index] = cached;
            return cached;
        }
    }
}

