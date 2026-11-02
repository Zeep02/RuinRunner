using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>Slice the user-supplied 8x4 sheet; no generation, recoloring, mirroring or resampling.</summary>
    public static class UserHeroArtBake
    {
        private const string SourcePath = "ArtDirection/UserProvided/Hero-20260910/original.png";
        private const int Canvas = 256;

        [MenuItem("Prototype/Art/Bake User Hero")]
        public static void Bake()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(SourcePath)) || source.width != 887 || source.height != 1774)
                throw new InvalidOperationException("Review extraction gutters when changing the supplied sheet.");
            var pixels = source.GetPixels32();
            // Measured transparent row gutters, top-to-bottom: N, NE, E, SE, S, SW, W, NW.
            var edges = new[] { 0, 233, 445, 663, 883, 1102, 1321, 1539, 1774 };
            var report = new StringBuilder("direction,frame,sourceX,sourceYBottom,width,height,anchorX,canvas,ppu,pivotX,pivotY\n");
            for (var direction = 0; direction < 8; direction++) for (var frame = 0; frame < 4; frame++)
            {
                var cell = new RectInt(frame * source.width / 4, source.height - edges[direction + 1],
                    (frame + 1) * source.width / 4 - frame * source.width / 4, edges[direction + 1] - edges[direction]);
                var b = Bounds(pixels, source.width, cell);
                // Anchor on the lower body's center, excluding the scarf extending to one side.
                long sumX = 0; var count = 0;
                for (var y = b.y; y < b.y + b.height / 3; y++) for (var x = b.x; x < b.xMax; x++)
                    if (pixels[y * source.width + x].a >= 128) { sumX += x; count++; }
                var anchorX = Mathf.RoundToInt(sumX / (float)count);
                // Four source pixels of extra padding keep the supplied soft alpha edge intact.
                var crop = new RectInt(b.x - 4, b.y - 4, b.width + 8, b.height + 8);
                if (crop.xMin < cell.xMin || crop.xMax > cell.xMax || crop.yMin < cell.yMin || crop.yMax > cell.yMax)
                    throw new InvalidOperationException("Source frame crosses a gutter: " + direction + "/" + frame);
                var destinationX = 128 + crop.x - anchorX;
                if (destinationX < 1 || destinationX + crop.width >= Canvas || 24 + crop.height >= Canvas)
                    throw new InvalidOperationException("Frame does not fit its padded canvas.");
                var output = new Color32[Canvas * Canvas];
                for (var y = 0; y < crop.height; y++) for (var x = 0; x < crop.width; x++)
                    output[(24 + y) * Canvas + destinationX + x] = pixels[(crop.y + y) * source.width + crop.x + x];
                Save("Assets/RuinRunner/Resources/" + IndividualArt.Root + PlayerAnimationController.FrameRoot +
                    direction.ToString("D2") + "/" + frame.ToString("D2") + ".png", output);
                report.AppendLine($"{direction},{frame},{crop.x},{crop.y},{crop.width},{crop.height},{anchorX},256,176,128,76");
            }
            File.WriteAllText("ArtDirection/UserProvided/Hero-20260910/extraction.csv", report.ToString());
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.SaveAssets();
            Debug.Log("[User Hero Art] PASS: 32 unique source poses copied without resizing/recoloring; 256px canvases, common feet baseline, 176 PPU.");
        }

        private static RectInt Bounds(Color32[] pixels, int width, RectInt cell)
        {
            var left = cell.xMax; var bottom = cell.yMax; var right = -1; var top = -1;
            for (var y = cell.yMin; y < cell.yMax; y++) for (var x = cell.xMin; x < cell.xMax; x++)
            {
                if (pixels[y * width + x].a <= 32) continue;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            if (right < left) throw new InvalidOperationException("Empty source frame.");
            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
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
            importer.spritePixelsPerUnit = 176; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.isReadable = true; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, 76f / Canvas);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
