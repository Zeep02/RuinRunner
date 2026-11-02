using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>Import the supplied five poses intact, with one scale and a common base anchor.</summary>
    public static class UserChestArtBake
    {
        private const int Canvas = 512;
        [MenuItem("Prototype/Art/Bake User Chest")]
        public static void Bake()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes("ArtDirection/UserProvided/Chest-20260910/original.png")) ||
                source.width != 2172 || source.height != 724) throw new InvalidOperationException("Unexpected chest sheet dimensions.");
            var input = source.GetPixels32();
            var report = new StringBuilder("frame,sourceX,sourceY,width,height,anchorX,baseY,canvas,ppu,pivotX,pivotY\n");
            var anchors = new[] { 228, 658, 1085, 1511, 1939 };
            const int baseY = 141;
            for (var frame = 0; frame < 5; frame++)
            {
                var left = frame * source.width / 5; var right = (frame + 1) * source.width / 5;
                var minX = right; var maxX = left; var minY = source.height; var maxY = 0;
                for (var y = 0; y < source.height; y++) for (var x = left; x < right; x++)
                {
                    if (input[y * source.width + x].a <= 32) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
                // Ignore isolated near-invisible sheet noise, retaining the full soft edge around each pose.
                minX -= 4; maxX += 4; minY -= 4; maxY += 4;
                var output = new Color32[Canvas * Canvas];
                for (var y = minY; y <= maxY; y++) for (var x = minX; x <= maxX; x++)
                {
                    var dx = 256 + x - anchors[frame]; var dy = 24 + y - baseY;
                    if (dx < 1 || dx >= Canvas - 1 || dy < 1 || dy >= Canvas - 1)
                        throw new InvalidOperationException("Chest frame exceeds padded canvas: " + frame);
                    output[dy * Canvas + dx] = input[y * source.width + x];
                }
                Save("Assets/RuinRunner/Resources/" + IndividualArt.Root + "UserChest/" + frame.ToString("D2") + ".png", output);
                report.AppendLine($"{frame},{minX},{minY},{maxX-minX+1},{maxY-minY+1},{anchors[frame]},{baseY},{Canvas},256,256,24");
            }
            File.WriteAllText("ArtDirection/UserProvided/Chest-20260910/extraction.csv", report.ToString());
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.SaveAssets();
            Debug.Log("[User Chest Art] PASS five original RGBA poses, no resizing/recoloring; common base and 256 PPU.");
        }

        private static void Save(string path, Color32[] pixels)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(Canvas, Canvas, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.isReadable = true; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, 24f / Canvas);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
