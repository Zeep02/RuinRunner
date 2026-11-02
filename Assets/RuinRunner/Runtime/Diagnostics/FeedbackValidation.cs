using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Isolated visual/XP regression fixture, never loads or modifies a player's save.</summary>
    public sealed class FeedbackValidation : MonoBehaviour
    {
        private IEnumerator sequence;
        private Camera cameraView;
        private RenderTexture render;
        private Texture2D pixels;
        private int tick;
        private string output;
        private bool collect;
        private PlayerProgression target;

        private void Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0; Time.captureFramerate = 60; Time.timeScale = 1f;
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "--validation-capture-dir");
            if (index >= 0 && index + 1 < args.Length) { output = args[index + 1]; Directory.CreateDirectory(output); }
            cameraView = new GameObject("FeedbackCamera").AddComponent<Camera>();
            cameraView.orthographic = true; cameraView.orthographicSize = 4.5f;
            cameraView.transform.position = new Vector3(0, 0, -10);
            cameraView.backgroundColor = new Color(.29f, .52f, .33f);
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            var floor = new GameObject("MeadowBackdrop").AddComponent<SpriteRenderer>();
            floor.sprite = IndividualArt.Sprite("Meadow/Terrain/Grass"); floor.drawMode = SpriteDrawMode.Tiled;
            floor.size = new Vector2(20, 12); floor.sortingOrder = -100;
            var receiver = new GameObject("XPReceiver"); receiver.transform.position = new Vector3(0, -2f);
            target = receiver.AddComponent<PlayerProgression>(); target.Initialize(() => { });
            var body = receiver.AddComponent<SpriteRenderer>(); body.sprite = IndividualArt.Sprite("Meadow/Creatures/Grunt/00");
            body.sortingOrder = 8;
            sequence = Validate(); StartCoroutine(Capture());
        }

        private void Update()
        {
            if (sequence == null) return;
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Feedback Validation] PASS: denomination totals; 18 individual XP frames; combat gate; animated pickup; absorption; 4 spawn sequences. Isolated fixture.");
                sequence = null; Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("[Feedback Validation] FAIL " + error); sequence = null; Application.Quit(3); }
        }

        private IEnumerator Validate()
        {
            for (var total = 0; total <= 500; total++) for (var count = 1; count <= 15; count++)
            {
                var sum = 0;
                foreach (var amount in ExperienceOrb.SplitBurst(total, count)) { Require(amount > 0, "positive denomination"); sum += amount; }
                Require(sum == total, "XP sum preserved");
            }
            foreach (var size in new[] { "Small", "Medium", "Large" })
            {
                var frames = IndividualArt.Frames("Feedback/Experience/" + size, 6);
                foreach (var frame in frames)
                {
                    Require(frame.texture.width == 32 && frame.texture.height == 32, "fixed canvas");
                    var data = frame.texture.GetPixels32(); var occupied = 0;
                    for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
                    {
                        if (data[y * 32 + x].a == 0) continue;
                        occupied++; Require(x > 0 && y > 0 && x < 31 && y < 31, "transparent edge/no clipping");
                    }
                    Require(occupied > 8 && occupied < 300, "visible orb, no opaque matte");
                }
            }
            var orbs = new ExperienceOrb[3]; var amounts = new[] { 3, 10, 20 };
            for (var i = 0; i < 3; i++) orbs[i] = ExperienceOrb.Create(new Vector2(-2 + i * 2, 0), amounts[i], target, () => collect);
            var first = orbs[0].GetComponentInChildren<SpriteRenderer>().sprite;
            for (var i = 0; i < 8; i++) yield return null;
            Require(orbs[0].GetComponentInChildren<SpriteRenderer>().sprite != first, "frame animation advances");
            for (var i = 0; i < 112; i++) yield return null;
            Require(target.Level == 1 && target.Experience == 0, "combat blocks collection");
            var types = new[] { EnemyType.Grunt, EnemyType.Shooter, EnemyType.Charger, EnemyType.Leaper };
            var previews = new EnemySpawnTelegraph[4];
            for (var repeat = 0; repeat < 3; repeat++)
            {
                for (var i = 0; i < 4; i++) previews[i] = EnemySpawnTelegraph.Create(new Vector2(-4.5f + i * 3f, 2.4f), types[i]);
                for (var i = 0; i < 51; i++) yield return null;
                foreach (var preview in previews) preview.Complete();
                for (var i = 0; i < 20; i++) yield return null;
            }
            // Move only the fixture receiver closer so all real pickup homing paths are exercised.
            target.transform.position = Vector3.zero; collect = true;
            for (var i = 0; i < 180; i++) yield return null;
            Require(target.Level == 2 && target.Experience == 5, "33 XP awarded exactly once");
            Require(RuntimeSceneObjects.FindAll<ExperienceOrb>().Length == 0, "all pickups consumed");
            Require(RuntimeSceneObjects.FindAll<TransientSpriteEffect>().Length == 0, "short-lived echoes cleaned");
        }

        private IEnumerator Capture()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (string.IsNullOrEmpty(output) || (tick++ & 1) != 0) continue;
                if (render == null) { render = new RenderTexture(1280, 720, 24); pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); }
                var previous = RenderTexture.active; cameraView.targetTexture = render; cameraView.Render(); RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                cameraView.targetTexture = null; RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(output, "feedback-" + ((tick - 1) / 2).ToString("D4") + ".png"), pixels.EncodeToPNG());
            }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void OnDestroy() { if (render != null) { render.Release(); Destroy(render); } if (pixels != null) Destroy(pixels); }
    }
}
