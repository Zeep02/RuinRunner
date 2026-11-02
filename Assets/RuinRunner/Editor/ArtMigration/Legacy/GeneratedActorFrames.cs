using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.EditorTools.LegacyArt
{
    /// <summary>Isolate intact bodies in measured, irregular generated actor sheets.</summary>
    public static class GeneratedActorFrames
    {
        private static Sprite[][] bossRows;
        private static Sprite[][] enemyRows;
        private struct Component { public int id, area, left, right, top, bottom; }

        public static Sprite[] GetBoss(int row)
        {
            if (bossRows == null) bossRows = Build("Art/Pixel/Enemies/boss_animation_atlas",
                new[] { 145, 296, 445, 580, 716, 925, 1086 }, new[] { 8, 8, 8, 7, 7, 8, 8 }, 2300, 112f);
            return bossRows[Mathf.Clamp(row, 0, 6)];
        }

        public static Sprite[] GetEnemy(int row)
        {
            if (enemyRows == null) enemyRows = Build("Art/Pixel/Enemies/enemy_animation_atlas",
                new[] { 90, 180, 280, 365, 460, 548, 640, 730, 820, 903, 980, 1086 },
                new[] { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8 }, 800, 65f);
            return enemyRows[Mathf.Clamp(row, 0, 11)];
        }

        private static Sprite[][] Build(string path, int[] rowEnds, int[] expectedCounts, int minimumArea, float pixelsPerUnit)
        {
            var source = LegacySource.Load(path);
            if (source == null || !source.isReadable) throw new InvalidOperationException("Missing readable actor sheet: " + path);
            // These row bounds belong to the verified 1448x1086 source, not a guessed grid.
            if (source.width != 1448 || source.height != 1086) throw new InvalidOperationException("Actor sheet layout changed; remeasure frame metadata: " + path);
            var pixels = source.GetPixels32();
            for (var i = 0; i < pixels.Length; i++) pixels[i] = SpriteMatte.ChromaCutout(pixels[i]);
            var labels = new int[pixels.Length];
            var queue = new Queue<int>();
            var components = new List<Component>();
            var id = 0;
            for (var start = 0; start < pixels.Length; start++)
            {
                if (pixels[start].a == 0 || labels[start] != 0) continue;
                id++;
                var component = new Component { id = id, left = source.width, bottom = source.height, right = -1, top = -1 };
                labels[start] = id; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var x = current % source.width; var y = current / source.width;
                    component.area++;
                    component.left = Mathf.Min(component.left, x); component.right = Mathf.Max(component.right, x);
                    component.bottom = Mathf.Min(component.bottom, y); component.top = Mathf.Max(component.top, y);
                    Visit(x > 0 ? current - 1 : -1, id, pixels, labels, queue);
                    Visit(x + 1 < source.width ? current + 1 : -1, id, pixels, labels, queue);
                    Visit(y > 0 ? current - source.width : -1, id, pixels, labels, queue);
                    Visit(y + 1 < source.height ? current + source.width : -1, id, pixels, labels, queue);
                }
                // Isolate the connected body/weapon; detached crumbs and the next
                // pose's pixels must not become part of this frame or its foot pivot.
                if (component.area > minimumArea) components.Add(component);
            }
            var groups = new List<Component>[rowEnds.Length];
            for (var i = 0; i < groups.Length; i++) groups[i] = new List<Component>();
            foreach (var component in components)
            {
                var fromTop = source.height - 1 - component.top;
                var row = 0;
                while (row + 1 < rowEnds.Length && fromTop >= rowEnds[row]) row++;
                groups[row].Add(component);
            }
            var rows = new Sprite[groups.Length][];
            for (var row = 0; row < groups.Length; row++)
            {
                groups[row].Sort((a, b) => a.left.CompareTo(b.left));
                var expected = expectedCounts[row];
                if (groups[row].Count != expected) throw new InvalidOperationException(path + " row " + row + " expected " + expected + " intact bodies, found " + groups[row].Count);
                rows[row] = new Sprite[expected];
                for (var frame = 0; frame < expected; frame++)
                    rows[row][frame] = Extract(source.width, pixels, labels, groups[row][frame], source.name + "_" + row + "_" + frame, pixelsPerUnit);
            }
            return rows;
        }

        private static void Visit(int index, int id, Color32[] pixels, int[] labels, Queue<int> queue)
        {
            if (index < 0 || pixels[index].a == 0 || labels[index] != 0) return;
            labels[index] = id; queue.Enqueue(index);
        }

        private static Sprite Extract(int sourceWidth, Color32[] source, int[] labels, Component component, string name, float pixelsPerUnit)
        {
            const int padding = 2;
            var width = component.right - component.left + 1 + padding * 2;
            var height = component.top - component.bottom + 1 + padding * 2;
            var output = new Color32[width * height];
            var footLeft = width; var footRight = 0;
            for (var y = component.bottom; y <= component.top; y++)
            for (var x = component.left; x <= component.right; x++)
            {
                var sourceIndex = y * sourceWidth + x;
                if (labels[sourceIndex] != component.id) continue;
                var px = x - component.left + padding; var py = y - component.bottom + padding;
                output[py * width + px] = source[sourceIndex];
                if (py <= padding + 8 && source[sourceIndex].r + source[sourceIndex].g + source[sourceIndex].b < 390)
                { footLeft = Mathf.Min(footLeft, px); footRight = Mathf.Max(footRight, px); }
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(output); texture.Apply(false, false);
            var foot = footLeft <= footRight ? (footLeft + footRight) * .5f : width * .5f;
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(foot / width, padding / (float)height), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}

