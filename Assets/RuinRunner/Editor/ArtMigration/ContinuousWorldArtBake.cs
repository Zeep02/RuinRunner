using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public static class ContinuousWorldArtBake
    {
        [MenuItem("Prototype/Art/Bake Continuous World Materials")]
        public static void Bake()
        {
            Export("plateau-top", "PlateauTop", 32, 32, false);
            Export("cliff-face", "CliffFace", 32, 16, false);
            Export("hud-bar", "HudFrame", 128, 12, true);
            Export("grass-calm", "GrassCalm", 32, 32, false);
            Export("dirt-calm", "DirtCalm", 32, 32, false);
            Export("meadow-tree", "MeadowTree", 48, 52, true, true);
            Debug.Log("[Continuous World Art] PASS: individual plateau/face/HUD assets.");
        }
        private static void Export(string sourceName, string outputName, int width, int height, bool cropAlpha, bool cutout = false)
        {
            var path = "Assets/RuinRunner/Resources/" + IndividualArt.Root + "ContinuousWorld/" + outputName + ".png";
            if (File.Exists(path)) return;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes("ArtDirection/Drafts/ContinuousWorld/" + sourceName + ".png"))) throw new InvalidOperationException(sourceName);
            var input = source.GetPixels32(); var bounds = new RectInt(0, 0, source.width, source.height);
            if (cropAlpha)
            {
                var left = source.width; var right = -1; var bottom = source.height; var top = -1;
                for (var y = 0; y < source.height; y++) for (var x = 0; x < source.width; x++)
                    if (input[y * source.width + x].a >= 128)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
                if (right < left || !cutout && right - left < (top - bottom) * 5) throw new InvalidOperationException("Source needs review");
                if (cutout && (left == 0 || bottom == 0 || right == source.width - 1 || top == source.height - 1))
                    throw new InvalidOperationException("Tree must have genuine transparent padding");
                bounds = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            }
            var output = new Color32[width * height];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var drawWidth = width; var drawHeight = height; var offsetX = 0; var offsetY = 0;
                if (cutout)
                {
                    var scale = Mathf.Min((width - 4f) / bounds.width, (height - 4f) / bounds.height);
                    drawWidth = Mathf.RoundToInt(bounds.width * scale); drawHeight = Mathf.RoundToInt(bounds.height * scale);
                    offsetX = (width - drawWidth) / 2; offsetY = 2;
                    if (x < offsetX || x >= offsetX + drawWidth || y < offsetY || y >= offsetY + drawHeight) continue;
                }
                var sx = bounds.x + Mathf.Min(bounds.width - 1, Mathf.FloorToInt((x - offsetX + .5f) / drawWidth * bounds.width));
                var sy = bounds.y + Mathf.Min(bounds.height - 1, Mathf.FloorToInt((y - offsetY + .5f) / drawHeight * bounds.height));
                var color = input[sy * source.width + sx]; color.a = cutout && color.a < 128 ? (byte)0 : (byte)255;
                output[y * width + x] = color;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false); texture.SetPixels32(output); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            if (cutout) { importer.spritePivot = new Vector2(.5f, 2f / height); var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = importer.spritePivot; importer.SetTextureSettings(settings); }
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
    }
}
