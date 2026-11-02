using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>Source sheets are editor-only; the player loads individually imported frame files.</summary>
    public static class CombatFeedbackBake
    {
        private const string Source = "ArtDirection/Drafts/CombatFeedback/";
        private const string Output = "Assets/RuinRunner/Resources/" + IndividualArt.Root + "Feedback/";

        [MenuItem("Prototype/Art/Bake Combat Feedback")]
        public static void Bake()
        {
            BakeSequence("xp-Small.png", "Experience/Small", 7, true);
            BakeSequence("xp-Medium-final.png", "Experience/Medium", 10, true);
            BakeSequence("xp-Large-final.png", "Experience/Large", 14, true);
            foreach (var type in new[] { "Grunt", "Shooter", "Charger", "Leaper" })
                BakeSequence("spawnGen-" + type + ".png", "SpawnV3/" + type, 24, false, type == "Charger" ? 580 : 512);
            Debug.Log("[Combat Feedback Bake] PASS: 18 experience and 24 spawn frames, individual transparent PNGs.");
        }

        private static void BakeSequence(string file, string id, int contentSize, bool centerEach, int rowSplit = 512)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(Source + file))) throw new InvalidOperationException(file);
            var pixels = source.GetPixels32();
            var alphaThreshold = centerEach ? 128 : 240;
            var bounds = new RectInt[6]; var maxSize = 1;
            for (var i = 0; i < 6; i++)
            {
                var x0 = i % 3 * source.width / 3; var x1 = (i % 3 + 1) * source.width / 3;
                var split = centerEach ? source.height / 2 : source.height - rowSplit;
                var y0 = i < 3 ? split : 0; var y1 = i < 3 ? source.height : split;
                var left = x1; var right = x0 - 1; var bottom = y1; var top = y0 - 1;
                for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++)
                {
                    if (pixels[y * source.width + x].a < alphaThreshold) continue;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
                if (right < left || left <= x0 || right >= x1 - 1 || bottom <= y0 || top >= y1 - 1)
                    throw new InvalidOperationException("Clipped/empty source frame " + file + ":" + i);
                bounds[i] = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
                maxSize = Mathf.Max(maxSize, bounds[i].width, bounds[i].height);
            }
            var scale = contentSize / (float)maxSize;
            for (var i = 0; i < 6; i++)
            {
                var path = Output + id + "/" + i.ToString("D2") + ".png";
                if (File.Exists(path)) continue;
                const int size = 32;
                var output = new Color32[size * size]; var b = bounds[i];
                var centerX = b.center.x;
                // Fixed ground anchor avoids per-frame scaling or floating as an effect grows.
                var centerY = centerEach ? b.center.y : b.yMin + 12f / scale;
                for (var y = 1; y < size - 1; y++) for (var x = 1; x < size - 1; x++)
                {
                    var sx = Mathf.FloorToInt(centerX + (x + .5f - size / 2f) / scale);
                    var sy = Mathf.FloorToInt(centerY + (y + .5f - size / 2f) / scale);
                    if (!b.Contains(new Vector2Int(sx, sy))) continue;
                    var color = pixels[sy * source.width + sx];
                    // Discard generated soft glow, retain the visible pixel silhouette without a matte.
                    if (color.a < alphaThreshold) continue;
                    color.a = 255; output[y * size + x] = color;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels32(output); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, centerEach ? .5f : .125f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            UnityEngine.Object.DestroyImmediate(source);
        }
    }
}
