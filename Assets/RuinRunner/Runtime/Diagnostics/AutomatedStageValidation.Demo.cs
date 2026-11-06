using System;
using System.Diagnostics;
using System.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RuinRunner
{
    /// <summary>Presentation run, enabled only with --validation-demo: the same pilot, invulnerable, moving without
    /// stopping at waypoints, and holding each menu long enough to read. Never evidence of balance or normal play.</summary>
    public sealed partial class AutomatedStageValidation
    {
        const float DemoMenuSeconds = 1.5f, DemoChoiceSeconds = 1.6f, DemoResultSeconds = 4f;
        readonly bool demoRun = Array.IndexOf(Environment.GetCommandLineArgs(), "--validation-demo") >= 0;
        float demoChoiceAt, demoResultAt, demoKeyAt;
        string demoFacilityShown;

        float MenuPause(float normal) => demoRun ? DemoMenuSeconds : normal;

        void TickDemo()
        {
            if (!demoRun) return;
            telegraphMovementChecked = true;
            if (game.Player != null) game.Player.SandboxInvulnerable = true;
        }

        bool HoldDemoChoice()
        {
            if (!demoRun) return false;
            if (demoChoiceAt <= 0f) demoChoiceAt = Time.unscaledTime + DemoChoiceSeconds;
            if (Time.unscaledTime < demoChoiceAt) return true;
            demoChoiceAt = 0f;
            return false;
        }

        bool HoldDemoResult()
        {
            if (!demoRun) return false;
            if (demoResultAt <= 0f) demoResultAt = Time.unscaledTime + DemoResultSeconds;
            return Time.unscaledTime < demoResultAt;
        }

        // Inventory keys one at a time, slowly enough to follow the cursor.
        bool HoldDemoInventory()
        {
            if (!demoRun || !game.IsInventoryOpen) return false;
            if (Time.unscaledTime < demoKeyAt) { StageValidationInput.ReleaseAll(); return true; }
            demoKeyAt = Time.unscaledTime + .35f;
            return false;
        }

        // Cut straight to the furthest waypoint in view and keep the keys down, instead of stopping at each one.
        Vector2 DemoNavigate(Vector2 destination)
        {
            var position = game.Player.GetComponent<Rigidbody2D>().position;
            if (routeRoom != game.CurrentRoom || Vector2.Distance(routeDestination, destination) > .75f ||
                (route.Count == 0 || stalledFor > .4f) && Time.unscaledTime >= nextRouteAt)
            {
                routeRoom = game.CurrentRoom; routeDestination = destination; nextRouteAt = Time.unscaledTime + .4f;
                var path = StageNavigation.FindPath(game.DungeonMap.Terrain, position, destination);
                route.Clear(); if (path != null) route.AddRange(path);
            }
            for (var i = Mathf.Min(route.Count - 1, 10); i > 0; i--)
                if (StageNavigation.HasCombatLane(position, route[i], .36f)) { route.RemoveRange(0, i); break; }
            while (route.Count > 0 && Vector2.Distance(position, route[0]) < .35f) route.RemoveAt(0);
            if (route.Count == 0) return Vector2.zero;
            var heading = (route[0] - position).normalized;
            return new Vector2(Mathf.Abs(heading.x) > .38f ? Mathf.Sign(heading.x) : 0f, Mathf.Abs(heading.y) > .38f ? Mathf.Sign(heading.y) : 0f);
        }

        void DashWhileTravelling(float distanceLeft)
        {
            if (!demoRun || distanceLeft < 4f || Time.unscaledTime < nextDashAt) return;
            Tap(GameAction.Dash);
            nextDashAt = Time.unscaledTime + .8f;
        }
    }

    /// <summary>Writes every rendered frame to ffmpeg at a steady 30 frames of game time: --demo-video out.mp4 ffmpeg.exe</summary>
    public sealed class DemoVideoRecorder : MonoBehaviour
    {
        const int Fps = 30;
        Process encoder;
        byte[] buffer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "--demo-video");
            if (at < 0 || at + 2 >= args.Length) return;
            var root = new GameObject("DemoVideoRecorder");
            DontDestroyOnLoad(root);
            root.AddComponent<DemoVideoRecorder>().Begin(args[at + 1], args[at + 2]);
        }

        void Begin(string output, string ffmpeg)
        {
            Time.captureFramerate = Fps;
            encoder = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = "-y -loglevel error -f rawvideo -pix_fmt rgba -s " + Screen.width + "x" + Screen.height + " -framerate " + Fps +
                    " -i - -an -vf vflip -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \"" + output + "\"",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            });
            StartCoroutine(Record());
        }

        IEnumerator Record()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (encoder != null && !encoder.HasExited)
            {
                // Rendering faster than the capture rate would run the game faster than real time, while the pilot and
                // the server keep real time. Settings can rewrite these at any point, so they are held every frame.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Fps;
                yield return endOfFrame;
                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                var data = frame.GetRawTextureData<byte>();
                if (buffer == null || buffer.Length != data.Length) buffer = new byte[data.Length];
                data.CopyTo(buffer);
                encoder.StandardInput.BaseStream.Write(buffer, 0, buffer.Length);
                Destroy(frame);
            }
        }

        void OnApplicationQuit()
        {
            if (encoder == null || encoder.HasExited) return;
            encoder.StandardInput.BaseStream.Flush();
            encoder.StandardInput.Close();
            encoder.WaitForExit(60000);
            Debug.Log("[Demo Video] finished");
        }
    }
}
