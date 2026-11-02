using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>Offline packing of generated source materials into editable, independently imported PNGs.</summary>
    public static class MeadowArtBake
    {
        private const string Input = "ArtDirection/Drafts/MeadowCohesion/";
        private const string Output = "Assets/RuinRunner/Resources/" + IndividualArt.Root + "Meadow/";
        [MenuItem("Prototype/Art/Bake Approved Meadow Art")]
        public static void Bake()
        {
            foreach (var tile in new[] { "Grass", "Dirt", "Cliff" })
            {
                var source = Load(tile.ToLowerInvariant() + "-source.png");
                var output = new Color32[32 * 32]; var pixels = source.GetPixels32();
                for (var y = 0; y < 32; y++)
                for (var x = 0; x < 32; x++)
                {
                    var sx = Mathf.Min(source.width - 1, Mathf.FloorToInt((x + .5f) * source.width / 32f));
                    var sy = Mathf.Min(source.height - 1, Mathf.FloorToInt((y + .5f) * source.height / 32f));
                    output[y * 32 + x] = pixels[sy * source.width + sx];
                    output[y * 32 + x].a = 255;
                }
                Write("Terrain/" + tile, output, new Vector2(.5f, .5f), tile == "Cliff" ? new Vector4(2, 8, 2, 2) : Vector4.zero);
                UnityEngine.Object.DestroyImmediate(source);
            }
            BakeCreatures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Meadow Bake] PASS: 3 individual terrain tiles, 24 individual creature frames; originals preserved, 32px canvas, 16 PPU.");
        }

        private static Texture2D Load(string file)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(Input + file), false)) throw new InvalidOperationException("Invalid source " + file);
            return texture;
        }

        private static void BakeCreatures()
        {
            var source = Load("creatures-source.png"); var pixels = source.GetPixels32();
            var names = new[] { "Grunt", "Shooter", "Charger", "Leaper" };
            for (var row = 0; row < 4; row++)
            {
                var bounds = new RectInt[6]; var anchors = new float[6];
                var maxHeight = 1f; var maxRadius = 1f;
                for (var frame = 0; frame < 6; frame++)
                {
                    var x0 = Mathf.RoundToInt(frame * source.width / 6f); var x1 = Mathf.RoundToInt((frame + 1) * source.width / 6f);
                    var y0 = Mathf.RoundToInt((3 - row) * source.height / 4f); var y1 = Mathf.RoundToInt((4 - row) * source.height / 4f);
                    var left = x1; var right = x0 - 1; var bottom = y1; var top = y0 - 1;
                    var keyCount = 0;
                    for (var y = y0; y < y1; y++)
                    for (var x = x0; x < x1; x++)
                    {
                        if (SpriteMatte.ChromaCutout(pixels[y * source.width + x]).a == 0) { keyCount++; continue; }
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
                    if (keyCount < (x1 - x0) * (y1 - y0) / 4 || right < left || left <= x0 || right >= x1 - 1 || bottom <= y0 || top >= y1 - 1)
                        throw new InvalidOperationException("Creature source needs visual review: cell " + row + "/" + frame);
                    bounds[frame] = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
                    var footLeft = right; var footRight = left;
                    for (var y = bottom; y <= Mathf.Min(top, bottom + (y1 - y0) / 16); y++)
                    for (var x = left; x <= right; x++)
                    {
                        var pixel = SpriteMatte.ChromaCutout(pixels[y * source.width + x]);
                        if (pixel.a == 0) continue;
                        footLeft = Mathf.Min(footLeft, x); footRight = Mathf.Max(footRight, x);
                    }
                    anchors[frame] = (footLeft + footRight + 1) * .5f;
                    maxRadius = Mathf.Max(maxRadius, anchors[frame] - left, right + 1 - anchors[frame]);
                    maxHeight = Mathf.Max(maxHeight, top - bottom + 1);
                }
                var scale = Mathf.Min(12f / maxRadius, 24f / maxHeight);
                for (var frame = 0; frame < 6; frame++)
                {
                    var output = new Color32[32 * 32]; var bound = bounds[frame]; var occupied = 0;
                    for (var y = 4; y < 29; y++)
                    for (var x = 2; x < 30; x++)
                    {
                        var sx = Mathf.FloorToInt(anchors[frame] + (x + .5f - 16) / scale);
                        var sy = Mathf.FloorToInt(bound.y + (y + .5f - 4) / scale);
                        if (!bound.Contains(new Vector2Int(sx, sy))) continue;
                        var pixel = SpriteMatte.ChromaCutout(pixels[sy * source.width + sx]);
                        output[y * 32 + x] = pixel;
                        if (pixel.a > 0) occupied++;
                    }
                    if (occupied < 50) throw new InvalidOperationException("Empty packed creature " + row + "/" + frame);
                    Write("Creatures/" + names[row] + "/" + frame.ToString("D2"), output, new Vector2(.5f, .125f), Vector4.zero);
                }
            }
            UnityEngine.Object.DestroyImmediate(source);
        }

        private static void Write(string id, Color32[] pixels, Vector2 pivot, Vector4 border)
        {
            var path = Output + id + ".png";
            // First bake only; artist replacements are never silently overwritten on later builds.
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = pivot; importer.spritePixelsPerUnit = 16; importer.spriteBorder = border;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }

        [MenuItem("Prototype/Art/Bake Individual Inventory Lock")]
        public static void BakeLock()
        {
            var source = Load("lock-source.png");
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++) pixels[i] = SpriteMatte.ChromaCutout(pixels[i]);
            source.SetPixels32(pixels); source.Apply();
            var bounds = ItemIconLibrary.ContentBounds(source);
            if (bounds.width == 0 || bounds.width >= source.width * .9f || bounds.height >= source.height * .95f)
                throw new InvalidOperationException("Padlock cutout requires review");
            var scale = Mathf.Min(10f / bounds.width, 12f / bounds.height);
            var width = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
            var output = new Color32[16 * 16];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sx = bounds.x + Mathf.Min(bounds.width - 1, Mathf.FloorToInt((x + .5f) * bounds.width / width));
                var sy = bounds.y + Mathf.Min(bounds.height - 1, Mathf.FloorToInt((y + .5f) * bounds.height / height));
                output[((16 - height) / 2 + y) * 16 + (16 - width) / 2 + x] = pixels[sy * source.width + sx];
            }
            Write("UI/InventoryLock", output, Vector2.one * .5f, Vector4.zero);
            UnityEngine.Object.DestroyImmediate(source);
            Debug.Log("[Meadow Bake] InventoryLock individual transparent 16x16 PNG.");
        }
    }
}
