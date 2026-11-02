using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>Offline import only: extract, align, downsample and palette-limit generated art.</summary>
    public static class FixedDemoArtBake
    {
        private const string Drafts = "ArtDirection/Drafts/FixedDemo/";
        private const string Root = "Assets/RuinRunner/Resources/";
        [MenuItem("Prototype/Art/Bake Fixed Demo")]
        public static void Bake()
        {
            BakeHero();
            foreach (var id in DemoContent.WeaponIds) BakeItem(id, InventoryIconKind.Weapon, 28);
            foreach (var id in DemoContent.AccessoryIds) BakeItem(id, InventoryIconKind.Accessory, 20);
            AssetDatabase.SaveAssets();
            Debug.Log("[Fixed Demo Art] PASS: 32 aligned direction frames, 2 weapons, 10 accessories; separate RGBA PNGs.");
        }
        private static Texture2D Load(string name)
        {
            var result = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!result.LoadImage(File.ReadAllBytes(Drafts + name + ".png"))) throw new InvalidOperationException(name);
            return result;
        }
        private static RectInt Bounds(Color32[] pixels, int width, RectInt cell)
        {
            int left = cell.xMax, right = -1, bottom = cell.yMax, top = -1;
            for (var y = cell.yMin; y < cell.yMax; y++) for (var x = cell.xMin; x < cell.xMax; x++)
            {
                if (pixels[y * width + x].a < 224) continue;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            if (right < left || left <= cell.xMin || right >= cell.xMax - 1 || bottom <= cell.yMin || top >= cell.yMax - 1)
                throw new InvalidOperationException("Empty/clipped/nontransparent source cell: " + cell);
            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }
        private static void BakeHero()
        {
            var source = Load("hero"); var input = source.GetPixels32();
            // Verified transparent gutters in this generated source; its rows are not evenly spaced.
            if (source.width != 1122 || source.height != 1402) throw new InvalidOperationException("Review hero extraction gutters after replacing source");
            var rowEdges = new[] { 0, 306, 566, 830, 1094, 1402 };
            var bounds = new RectInt[5, 4]; var maxHeight = 0; var maxWidth = 0;
            for (var row = 0; row < 5; row++) for (var col = 0; col < 4; col++)
            {
                var cell = new RectInt(col * source.width / 4, source.height - rowEdges[row + 1],
                    (col + 1) * source.width / 4 - col * source.width / 4,
                    rowEdges[row + 1] - rowEdges[row]);
                bounds[row, col] = Bounds(input, source.width, cell);
                maxHeight = Mathf.Max(maxHeight, bounds[row, col].height);
                maxWidth = Mathf.Max(maxWidth, bounds[row, col].width);
            }
            // One scale and one palette for the whole animation, not independently resized frames.
            var scale = Mathf.Min(28f / maxHeight, 26f / maxWidth);
            var palette = Palette(input, 14);
            for (var direction = 0; direction < 8; direction++) for (var frame = 0; frame < 4; frame++)
            {
                var row = direction <= 4 ? direction : 8 - direction;
                var b = bounds[row, frame];
                var w = Mathf.RoundToInt(b.width * scale); var h = Mathf.RoundToInt(b.height * scale);
                var output = Resample(input, source.width, b, 40, 40, w, h, (40 - w) / 2, 4, direction > 4, palette);
                Save(Root + IndividualArt.Root + "Demo/Player/move/" + direction.ToString("D2") + "/" + frame.ToString("D2") + ".png",
                    output, 40, 40, new Vector2(.5f, 11f / 40f), 24f);
            }
            UnityEngine.Object.DestroyImmediate(source);
        }
        private static void BakeItem(string id, InventoryIconKind kind, int contentSize)
        {
            var source = Load(id); var input = source.GetPixels32();
            var b = Bounds(input, source.width, new RectInt(0, 0, source.width, source.height));
            var scale = contentSize / (float)Mathf.Max(b.width, b.height);
            var w = Mathf.Max(1, Mathf.RoundToInt(b.width * scale)); var h = Mathf.Max(1, Mathf.RoundToInt(b.height * scale));
            var output = Resample(input, source.width, b, 32, 32, w, h, (32 - w) / 2, (32 - h) / 2, false, Palette(input, 10));
            Save(Root + ItemIconLibrary.ResourcePath(id, kind) + ".png", output, 32, 32, new Vector2(.5f, .5f), 24);
            UnityEngine.Object.DestroyImmediate(source);
        }
        private static Color32[] Resample(Color32[] input, int sourceWidth, RectInt b, int width, int height,
            int drawWidth, int drawHeight, int offsetX, int offsetY, bool mirror, Color32[] palette)
        {
            var result = new Color32[width * height];
            for (var y = 0; y < drawHeight; y++) for (var x = 0; x < drawWidth; x++)
            {
                var sx = b.x + Mathf.Min(b.width - 1, Mathf.FloorToInt((x + .5f) * b.width / drawWidth));
                var sy = b.y + Mathf.Min(b.height - 1, Mathf.FloorToInt((y + .5f) * b.height / drawHeight));
                var c = input[sy * sourceWidth + sx];
                if (c.a < 224) continue;
                var destinationX = offsetX + (mirror ? drawWidth - 1 - x : x);
                result[(offsetY + y) * width + destinationX] = palette[Nearest(c, palette)];
            }
            return result;
        }
        private static int Nearest(Color32 c, Color32[] palette)
        {
            var nearest = 0; var best = int.MaxValue;
            for (var i = 0; i < palette.Length; i++)
            {
                var dr = c.r - palette[i].r; var dg = c.g - palette[i].g; var db = c.b - palette[i].b;
                var distance = dr * dr + dg * dg + db * db;
                if (distance < best) { best = distance; nearest = i; }
            }
            return nearest;
        }
        private static Color32[] Palette(Color32[] input, int count)
        {
            var samples = new List<Color32>();
            for (var i = 0; i < input.Length; i += 41) if (input[i].a >= 224) samples.Add(input[i]);
            if (samples.Count == 0) throw new InvalidOperationException("No opaque source art");
            var palette = new Color32[count]; palette[0] = new Color32(20, 27, 43, 255);
            for (var k = 1; k < count; k++)
            {
                var best = -1; var selected = samples[0];
                foreach (var c in samples)
                {
                    var distance = int.MaxValue;
                    for (var j = 0; j < k; j++)
                    {
                        var dr = c.r - palette[j].r; var dg = c.g - palette[j].g; var db = c.b - palette[j].b;
                        distance = Mathf.Min(distance, dr * dr + dg * dg + db * db);
                    }
                    if (distance > best) { best = distance; selected = c; }
                }
                selected.a = 255; palette[k] = selected;
            }
            for (var iteration = 0; iteration < 8; iteration++)
            {
                var sums = new Vector3[count]; var totals = new int[count];
                foreach (var c in samples) { var j = Nearest(c, palette); sums[j] += new Vector3(c.r, c.g, c.b); totals[j]++; }
                for (var j = 1; j < count; j++) if (totals[j] > 0)
                { var c = sums[j] / totals[j]; palette[j] = new Color32((byte)c.x, (byte)c.y, (byte)c.z, 255); }
            }
            return palette;
        }
        private static void Save(string path, Color32[] pixels, int width, int height, Vector2 pivot, float ppu)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.isReadable = true; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
