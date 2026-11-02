using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    [DefaultExecutionOrder(-10000)]
    public sealed class DungeonGatheringValidation : MonoBehaviour
    {
        private DungeonGatheringTest map;
        private IEnumerator sequence;
        private int checks;
        private string output;
        public void Initialize(DungeonGatheringTest target)
        {
            map = target;
            Application.runInBackground = true;
            Time.captureFramerate = 60;
            QualitySettings.vSyncCount = 0;
            output = Path.Combine(Application.dataPath, "../Validation");
            Directory.CreateDirectory(output);
            sequence = Validate();
        }
        private void Update()
        {
            if (sequence == null) return;
            StageValidationInput.BeginFrame();
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Dungeon Gathering Validation] PASS " + checks + " assertions; real movement, wall/dash/water collision, connected layout and animation.");
                StageValidationInput.ReleaseAll(); sequence = null; Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Dungeon Gathering Validation] FAIL " + ex);
                StageValidationInput.ReleaseAll(); sequence = null; Application.Quit(3);
            }
        }
        private IEnumerator Validate()
        {
            for (var i = 0; i < 60; i++) yield return null;
            Require(StageValidationInput.Enabled, "Opt-in input driver enabled");
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>(); queue.Enqueue(Vector2Int.RoundToInt(map.Recipe.spawn));
            var neighbors = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); if (!visited.Add(cell)) continue;
                foreach (var direction in neighbors)
                    if (map.IsWalkable(cell + direction) && !visited.Contains(cell + direction)) queue.Enqueue(cell + direction);
            }
            var count = 0;
            foreach (var cell in map.Floor) if (map.IsWalkable(cell)) count++;
            Require(count == visited.Count, "All floor areas connected around water");
            foreach (var sprite in Resources.LoadAll<Sprite>(IndividualArt.Root + "DungeonGathering"))
                Require(sprite.rect.width == 16 && sprite.rect.height == 16 && sprite.texture.width == 16 && sprite.texture.height == 16 && sprite.pixelsPerUnit == 16 && sprite.texture.filterMode == FilterMode.Point, "Individual 16px tile: " + sprite.name);
            var animations = FindObjectsByType<DungeonGatheringAnimation>(FindObjectsSortMode.None);
            var firstFrames = new int[animations.Length];
            for (var i = 0; i < animations.Length; i++) firstFrames[i] = animations[i].FrameIndex;
            for (var i = 0; i < 10; i++) yield return null;
            for (var i = 0; i < animations.Length; i++) Require(animations[i].FrameIndex != firstFrames[i], "Animation advances: " + animations[i].name);
            Capture("01-entrance");
            for (var i = 0; i < 3; i++) yield return null;
            var move = MoveTo(new Vector2(2.2f, 6)); while (move.MoveNext()) yield return null;
            StageValidationInput.SetHeld(GameAction.MoveLeft, true);
            StageValidationInput.SetHeld(GameAction.Dash, true);
            for (var i = 0; i < 90; i++) yield return null;
            StageValidationInput.ReleaseAll();
            Require(map.Player.transform.position.x > 1.75f, "West wall stops walking and dash");
            foreach (var point in new[] { new Vector2(3, 6), new Vector2(3, 7.2f) })
            { move = MoveTo(point); while (move.MoveNext()) yield return null; }
            StageValidationInput.SetHeld(GameAction.Interact, true);
            yield return null; StageValidationInput.ReleaseAll(); yield return null;
            Require(map.OpenedUrns == 1, "Nearby urn interaction");
            foreach (var point in new[] { new Vector2(6, 6), new Vector2(14, 6), new Vector2(21, 6), new Vector2(21, 10), new Vector2(23, 10) })
            { move = MoveTo(point); while (move.MoveNext()) yield return null; }
            for (var i = 0; i < 60; i++) yield return null;
            Capture("02-cistern-hall");
            for (var i = 0; i < 3; i++) yield return null;
            move = MoveTo(new Vector2(23, 18)); while (move.MoveNext()) yield return null;
            for (var i = 0; i < 60; i++) yield return null;
            Require(map.Player.transform.position.y > 17.5f, "Sanctum reached with normal movement");
            Capture("03-sanctum");
            for (var i = 0; i < 3; i++) yield return null;
            StageValidationInput.SetHeld(GameAction.Map, true); yield return null;
            StageValidationInput.ReleaseAll(); for (var i = 0; i < 5; i++) yield return null;
            Require(map.OverviewVisible, "Tab opens overview");
            Capture("04-map");
            for (var i = 0; i < 3; i++) yield return null;
            StageValidationInput.SetHeld(GameAction.Map, true); yield return null;
            StageValidationInput.ReleaseAll(); yield return null;
            Require(!map.OverviewVisible, "Tab closes overview");
            foreach (var point in new[] { new Vector2(23, 10), new Vector2(23, 6) })
            { move = MoveTo(point); while (move.MoveNext()) yield return null; }
            StageValidationInput.SetHeld(GameAction.MoveRight, true);
            StageValidationInput.SetHeld(GameAction.Dash, true);
            for (var i = 0; i < 90; i++) yield return null;
            StageValidationInput.ReleaseAll();
            Require(map.Player.transform.position.x < 23.4f, "Water blocks walking and dash");
            foreach (var point in new[] { new Vector2(23, 10), new Vector2(18, 10), new Vector2(18, 13), new Vector2(15, 13) })
            { move = MoveTo(point); while (move.MoveNext()) yield return null; }
            Capture("05-side-alcove");
            for (var i = 0; i < 30; i++) yield return null;
            Require(map.Player.GetComponentInChildren<SpriteRenderer>().color.a > .9f, "Player remains visible after transitions and dashes");
        }
        private IEnumerator MoveTo(Vector2 point)
        {
            var frames = 0;
            while (Vector2.Distance(map.Player.transform.position, point) > .18f)
            {
                var delta = point - (Vector2)map.Player.transform.position;
                StageValidationInput.SetHeld(GameAction.MoveRight, delta.x > .12f);
                StageValidationInput.SetHeld(GameAction.MoveLeft, delta.x < -.12f);
                StageValidationInput.SetHeld(GameAction.MoveUp, delta.y > .12f);
                StageValidationInput.SetHeld(GameAction.MoveDown, delta.y < -.12f);
                Require(++frames < 480, "Route not blocked at " + point);
                yield return null;
            }
            StageValidationInput.ReleaseAll();
        }
        private void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + " player=" + map.Player.transform.position);
            if (!label.StartsWith("Route")) { checks++; Debug.Log("[Dungeon Gathering Check] " + label); }
        }
        private void Capture(string name)
        {
            var capture = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (capture == null) throw new InvalidOperationException("Screen capture unavailable");
            capture.Invoke(null, new object[] { Path.Combine(output, name + ".png"), 1 });
        }
    }
}
