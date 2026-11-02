using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
public static class PixelWorldArt
    {
        private const string ResourcePath = "Art/Pixel/UI/world_interaction_atlas_v1";
        private static readonly Dictionary<int, Sprite> Sprites = new Dictionary<int, Sprite>();

        public static bool Available => LegacySource.Load(ResourcePath) != null;

        public static Texture2D GetTexture(WorldInteractionArt art)
        {
            var sprite = GetSprite(art);
            return sprite != null ? sprite.texture : null;
        }

        public static Sprite GetSprite(WorldInteractionArt art)
        {
            var index = (int)art;
            Sprite cached;
            if (Sprites.TryGetValue(index, out cached)) return cached;

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
            var cellWidth = xMax - xMin;
            var cellHeight = yMax - yMin;
            var pixels = source.GetPixels32();
            // The halo deliberately uses translucent light; solid props do not.
            if (art != WorldInteractionArt.LootHalo)
                for (var i = 0; i < pixels.Length; i++) pixels[i] = SpriteMatte.AlphaCutout(pixels[i]);
            var left = cellWidth;
            var right = -1;
            var bottom = cellHeight;
            var top = -1;
            for (var y = 0; y < cellHeight; y++)
            for (var x = 0; x < cellWidth; x++)
            {
                if (pixels[(yMin + y) * source.width + xMin + x].a < 12) continue;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
            if (right < left || top < bottom) return null;

            left = Mathf.Max(0, left - 2);
            right = Mathf.Min(cellWidth - 1, right + 2);
            bottom = Mathf.Max(0, bottom - 2);
            top = Mathf.Min(cellHeight - 1, top + 2);
            var width = right - left + 1;
            var height = top - bottom + 1;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "WorldInteraction_" + art;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var output = new Color32[width * height];
            for (var y = 0; y < height; y++)
                System.Array.Copy(pixels, (yMin + bottom + y) * source.width + xMin + left, output, y * width, width);
            texture.SetPixels32(output);
            texture.Apply(false, false);

            var bottomAnchored = art == WorldInteractionArt.ChestClosed || art == WorldInteractionArt.ChestOpen ||
                                 art == WorldInteractionArt.PortalLocked || art == WorldInteractionArt.PortalActive;
            var pivot = bottomAnchored ? new Vector2(.5f, .06f) : new Vector2(.5f, .5f);
            cached = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, Mathf.Max(width, height), 0, SpriteMeshType.FullRect);
            cached.name = "WorldInteractionSprite_" + art;
            Sprites[index] = cached;
            return cached;
        }
    }
}

