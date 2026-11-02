using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    /// <summary>Separates the supplied poses by connected alpha, including arms/jumps crossing grid cells.</summary>
    public static class UserBossArtBake
    {
        private const int Canvas = 256;
        private sealed class Pose
        {
            public int label, row, frame;
            public RectInt bounds;
            public readonly List<int> pixels = new List<int>();
        }

        [MenuItem("Prototype/Art/Bake User Boss")]
        public static void Bake()
        {
            const string sourcePath = "ArtDirection/UserProvided/Boss-20260910/original.png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath)) || source.width != 1254 || source.height != 1254)
                throw new InvalidOperationException("Review boss extraction when replacing source.");
            var input = source.GetPixels32(); var width = source.width; var height = source.height;
            var labels = new int[input.Length]; var queue = new Queue<int>(); var poses = new List<Pose>();
            var component = 0;
            for (var seed = 0; seed < input.Length; seed++)
            {
                if (input[seed].a <= 32 || labels[seed] != 0) continue;
                var pose = new Pose { label = ++component };
                var left = width; var right = -1; var bottom = height; var top = -1;
                labels[seed] = component; queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var index = queue.Dequeue(); var x = index % width; var y = index / width;
                    pose.pixels.Add(index);
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx; var ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        var next = ny * width + nx;
                        if (labels[next] != 0 || input[next].a <= 32) continue;
                        labels[next] = component; queue.Enqueue(next);
                    }
                }
                if (pose.pixels.Count < 1000) continue;
                pose.bounds = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
                pose.row = Mathf.FloorToInt((height - (bottom + top + 1) * .5f) * 8f / height);
                pose.frame = Mathf.FloorToInt((left + right + 1) * .5f * 8f / width);
                poses.Add(pose);
            }
            if (poses.Count != 64) throw new InvalidOperationException("Expected 64 separate bodies, got " + poses.Count);
            var occupied = new HashSet<int>(); var owners = new int[input.Length]; var distances = new byte[input.Length];
            foreach (var pose in poses)
            {
                if (!occupied.Add(pose.row * 8 + pose.frame)) throw new InvalidOperationException("Ambiguous boss pose assignment");
                foreach (var index in pose.pixels) { owners[index] = pose.label; queue.Enqueue(index); }
            }
            // Retain the original soft alpha around each silhouette, without borrowing neighboring poses.
            while (queue.Count > 0)
            {
                var index = queue.Dequeue(); if (distances[index] >= 4) continue;
                var x = index % width; var y = index / width;
                for (var dy = -1; dy <= 1; dy++) for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx; var ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    var next = ny * width + nx;
                    if (owners[next] != 0) continue;
                    owners[next] = owners[index]; distances[next] = (byte)(distances[index] + 1); queue.Enqueue(next);
                }
            }
            poses.Sort((a, b) => (a.row * 8 + a.frame).CompareTo(b.row * 8 + b.frame));
            var report = new StringBuilder("row,frame,sourceX,sourceYBottom,width,height,anchorX,visiblePixels,canvas,ppu,pivotX,pivotY\n");
            foreach (var pose in poses)
            {
                var b = pose.bounds; long sum = 0; var count = 0;
                foreach (var index in pose.pixels)
                    if (index / width < b.y + b.height / 3 && input[index].a >= 128) { sum += index % width; count++; }
                var anchor = Mathf.RoundToInt(sum / (float)count);
                var crop = new RectInt(Mathf.Max(0, b.x - 4), Mathf.Max(0, b.y - 4), b.width + 8, b.height + 8);
                crop.xMax = Mathf.Min(width, crop.xMax); crop.yMax = Mathf.Min(height, crop.yMax);
                var dx = 128 + crop.x - anchor;
                if (dx < 1 || dx + crop.width >= Canvas || 24 + crop.height >= Canvas)
                    throw new InvalidOperationException("Boss pose does not fit the padded canvas");
                var output = new Color32[Canvas * Canvas]; var copiedVisible = 0;
                for (var y = 0; y < crop.height; y++) for (var x = 0; x < crop.width; x++)
                {
                    var index = (crop.y + y) * width + crop.x + x;
                    if (owners[index] != pose.label) continue;
                    if (input[index].a > 32 && labels[index] != pose.label) continue;
                    output[(24 + y) * Canvas + dx + x] = input[index];
                    if (input[index].a > 32) copiedVisible++;
                }
                if (copiedVisible != pose.pixels.Count) throw new InvalidOperationException($"Missing/foreign visible pixels in {pose.row}/{pose.frame}: {copiedVisible}/{pose.pixels.Count}");
                Save("Assets/RuinRunner/Resources/" + IndividualArt.Root + BossSpriteAnimator.FrameRoot +
                    pose.row.ToString("D2") + "/" + pose.frame.ToString("D2") + ".png", output);
                report.AppendLine($"{pose.row},{pose.frame},{crop.x},{crop.y},{crop.width},{crop.height},{anchor},{copiedVisible},256,64,128,28");
            }
            File.WriteAllText("ArtDirection/UserProvided/Boss-20260910/extraction.csv", report.ToString());
            UnityEngine.Object.DestroyImmediate(source); AssetDatabase.SaveAssets();
            Debug.Log("[User Boss Art] PASS: 64 aligned poses; all connected visible pixels retained, no recoloring/resampling; 256px canvas, 64 PPU.");
        }

        private static void Save(string path, Color32[] pixels)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(Canvas, Canvas, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.isReadable = true; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, 28f / Canvas);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
    }
}
