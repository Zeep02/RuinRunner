using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Opt-in graphical evidence only. Never changes simulation time or game state.</summary>
    public sealed class RunEvidenceRecorder : MonoBehaviour
    {
        private GameController game;
        private string directory;
        private StreamWriter timeline;
        private MethodInfo screenshot;
        private float started, nextFrame, finishAt = -1;
        private int count;
        public void Initialize(GameController controller, string output)
        {
            game = controller;
            directory = Path.Combine(output, "frames");
            Directory.CreateDirectory(directory);
            timeline = new StreamWriter(Path.Combine(output, "recording.csv"), false);
            timeline.WriteLine("frame,realtime,stage,room,health,kills,weapon,pose,pose_frame,phase");
            screenshot = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            if (screenshot == null)
                throw new InvalidOperationException("ScreenCapture unavailable");
            started = Time.realtimeSinceStartup;
            nextFrame = started;
            File.WriteAllText(Path.Combine(output, "recording-info.txt"), "Normal GameAction input, fresh isolated profile. No fixed capture time or speedup. Target 10 screen samples/second; recording.csv retains actual timestamps. Capture overhead makes this unsuitable as a performance benchmark.");
            StartCoroutine(Record());
        }
        private IEnumerator Record()
        {
            while (game != null && Time.realtimeSinceStartup - started < 2800)
            {
                yield return new WaitForEndOfFrame();
                var now = Time.realtimeSinceStartup;
                if (now < nextFrame)
                    continue;
                nextFrame = now + .1f;
                var animation = game.Player == null ? null : game.Player.GetComponent<PlayerAnimationController>();
                screenshot.Invoke(null, new object[] { Path.Combine(directory, "frame-" + count.ToString("D6") + ".png"), 1 });
                timeline.WriteLine(string.Join(",", count, (now - started).ToString("F4", CultureInfo.InvariantCulture), game.CurrentStageIndex + 1, game.CurrentRoom + 1,
                    (game.Player?.Health ?? 0).ToString("F2", CultureInfo.InvariantCulture), game.Stats?.EnemiesDefeated ?? 0, game.Player?.EquippedWeapon?.Id, animation?.PoseName, animation?.FrameIndex ?? 0, game.Phase));
                if (count++ % 30 == 0)
                    timeline.Flush();
                var pilot = GetComponent<AutomatedStageValidation>();
                if ((pilot != null ? pilot.Complete : game.IsGameOver) && finishAt < 0)
                    finishAt = now + 1.4f;
                if (finishAt > 0 && now >= finishAt)
                    break;
            }
            timeline?.Dispose();
            timeline = null;
            Debug.Log("[Run Recording] FINISHED frames=" + count + " actual_seconds=" + (Time.realtimeSinceStartup - started).ToString("F2") + " directory=" + directory);
        }
        private void OnDestroy()
        {
            timeline?.Dispose();
            timeline = null;
        }
    }
}
