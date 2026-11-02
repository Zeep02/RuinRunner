using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public static class ThrustOptionsArtBake
    {
        private const string Source = "ArtDirection/Generated/ThrustOptions-20260910/";
        private const string Output = "Assets/RuinRunner/Resources/Art/Pixel/Separated/ThrustOptions/";

        [MenuItem("Prototype/Art/Bake Thrust And Options")]
        public static void Bake()
        {
            var sheet = Load("thrust-sheet.png", 1536, 1024);
            // Measured transparent gutters, not a blind equal grid: the longest thrust crosses x=1024.
            var lefts = new[] { 0, 416, 876, 0, 552, 1024 };
            var rights = new[] { 416, 876, 1536, 552, 1024, 1536 };
            var anchors = new[] { 188, 576, 1008, 108, 662, 1116 };
            var rows = new[] { 307, 307, 307, 757, 757, 757 };
            var pixels = sheet.GetPixels32();
            for (var frame = 0; frame < 6; frame++)
            {
                var output = new Color32[768 * 256];
                var top = frame < 3 ? 236 : 680; var bottom = frame < 3 ? 380 : 820;
                for (var y = top; y < bottom; y++) for (var x = lefts[frame]; x < rights[frame]; x++)
                {
                    var dx = 160 + x - anchors[frame]; var dy = 128 + rows[frame] - y;
                    var ink = pixels[(sheet.height - 1 - y) * sheet.width + x];
                    if (dx < 0 || dx >= 768 || dy < 0 || dy >= 256)
                    {
                        if (ink.a > 32) throw new InvalidOperationException("VFX silhouette clipped: " + frame);
                        continue;
                    }
                    output[dy * 768 + dx] = ink;
                }
                Save(Output + "Thrust/" + frame.ToString("D2") + ".png", output, 768, 256, 320, new Vector2(160f/768, .5f));
            }
            UnityEngine.Object.DestroyImmediate(sheet);
            var bar = Load("volume-track.png", 2172, 724);
            // Actual generated bar: x114..2057, y286..436 (top origin). Keep an extra 4px border.
            var barPixels = bar.GetPixels(110, 724 - 441, 1952, 159);
            var colors = new Color32[barPixels.Length];
            for (var i = 0; i < colors.Length; i++) colors[i] = barPixels[i];
            Save(Output + "VolumeTrack.png", colors, 1952, 159, 100, new Vector2(.5f, .5f));
            UnityEngine.Object.DestroyImmediate(bar);
            AssetDatabase.SaveAssets();
            Debug.Log("[Thrust Options Art] PASS six aligned generated VFX frames + bronze volume track; original alpha retained.");
        }

        private static Texture2D Load(string name, int width, int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(Source + name)) || texture.width != width || texture.height != height)
                throw new InvalidOperationException("Unexpected image dimensions: " + name);
            return texture;
        }

        private static void Save(string path, Color32[] pixels, int width, int height, float ppu, Vector2 pivot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.isReadable = true; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
