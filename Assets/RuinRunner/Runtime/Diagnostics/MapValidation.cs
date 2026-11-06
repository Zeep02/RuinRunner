using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Opt-in rendered camera/map fixtures. Transient profile, not stage-completion evidence.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class MapValidation : MonoBehaviour
    {
        private IEnumerator sequence;
        private GameController game;
        private string directory;
        private MethodInfo screenshot;
        private void Start()
        {
            Application.runInBackground = true;
            game = GetComponent<GameController>();
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--validation-capture-dir");
            directory = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : Path.Combine(Application.persistentDataPath, "MapValidation");
            Directory.CreateDirectory(directory);
            screenshot = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            Application.logMessageReceived += OnLog;
            sequence = Validate();
        }
        private void OnDestroy() => Application.logMessageReceived -= OnLog;
        private void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception) Application.Quit(3);
        }
        private void Update()
        {
            StageValidationInput.BeginFrame();
            if (sequence == null) return;
            try
            {
                if (sequence.MoveNext()) return;
                sequence = null;
                Debug.Log("[Map Validation] PASS fixed pixel camera, large-room tracking, real Map/Pause input, paused map and resume, room-local markers and geometry; rendered fixtures, not a normal run.");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Map Validation] FAIL " + exception);
                StageValidationInput.ReleaseAll();
                sequence = null;
                Application.Quit(3);
            }
        }
        private IEnumerator Validate()
        {
            var focusDeadline = Time.unscaledTime + 90f;
            while (!Application.isFocused)
            {
                if (Time.unscaledTime > focusDeadline) throw new InvalidOperationException("Visible game window required for rendered validation");
                yield return null;
            }
            game.SaveSlots.OpenTransientForTests();
            typeof(GameController).GetMethod("BeginRunForActiveSlot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            // The stage builds over several frames, and the run resets its state when that finishes.
            // The ink's reveal runs on the game's own coroutines: stopping them mid-reveal would hold every input forever.
            while (game.Player == null || game.IsStageTransitioning || InkTransition.LocksInput) yield return null;
            game.StopAllCoroutines(); // Isolated visual fixture: no wave or reward manipulation in a normal run.
            RuntimeSceneObjects.ClearSession();
            UnityEngine.Object.Destroy(game.DungeonMap.Root);
            // Replace the owner's map in this frame, before OnGUI can inspect
            // connections destroyed with the old terrain at end of frame.
            var small = Resources.Load<RoomTemplate>("Rooms/hall_broken");
            var large = Resources.Load<RoomTemplate>("Rooms/green_steps");
            var vertical = Resources.Load<RoomTemplate>("Rooms/twin_towers");
            var boss = Resources.Load<RoomTemplate>("Rooms/warden_throne");
            var map = DungeonMap.Build(new[] { small, large, vertical, boss }, _ => { });
            Set("DungeonMap", map);
            Set("CurrentRoom", 0);
            game.Player.Teleport(map.GetRoomCenter(0));
            game.CameraFollow.Target = game.Player.transform;
            game.CameraFollow.FocusRoom(map.GetRoomCenter(0), small.Size, true);
            var cam = game.CameraFollow.GetComponent<Camera>();
            // The fixed 640x360 pixel viewport now tracks even ordinary rooms;
            // zooming out to fit a complete room would violate the art scale.
            if (Mathf.Abs(cam.orthographicSize - CameraFollow.ViewHalfHeight) > .001f)
                throw new InvalidOperationException("Camera changed the fixed world pixel scale");
            var capture = Capture("01-small-room"); while (capture.MoveNext()) yield return null;
            Set("CurrentRoom", 1);
            var center = map.GetRoomCenter(1);
            game.Player.Teleport(center + large.Entrance(RoomTravelDirection.Left));
            game.CameraFollow.FocusRoom(center, large.Size, true);
            if (large.Size.x <= cam.orthographicSize * cam.aspect * 2 || large.Size.y <= cam.orthographicSize * 2)
                throw new InvalidOperationException("Large room does not exceed the viewport");
            var left = game.CameraFollow.transform.position;
            capture = Capture("02-large-west"); while (capture.MoveNext()) yield return null;
            game.Player.Teleport(center + large.Entrance(RoomTravelDirection.Right));
            game.CameraFollow.FocusRoom(center, large.Size, true);
            if (game.CameraFollow.transform.position.x - left.x < 8f) throw new InvalidOperationException("Camera does not track the large room");
            capture = Capture("03-large-east"); while (capture.MoveNext()) yield return null;
            var rect = new Rect(10, 20, 190, 130);
            if ((DungeonMapView.ToMap(Vector2.zero, large, rect) - rect.center).sqrMagnitude > .001f)
                throw new InvalidOperationException("Map center coordinate mismatch");
            // The arrival's ink holds all input until it has drained.
            var inkDeadline = Time.unscaledTime + 10f;
            while (InkTransition.LocksInput && Time.unscaledTime < inkDeadline) yield return null;
            StageValidationInput.SetHeld(GameAction.Map, true);
            yield return null;
            StageValidationInput.SetHeld(GameAction.Map, false);
            if (!game.IsMapOpen || !game.IsPaused || Time.timeScale != 0)
                throw new InvalidOperationException("Map did not pause the run: open=" + game.IsMapOpen + " paused=" + game.IsPaused + " scale=" + Time.timeScale + " ink=" + InkTransition.LocksInput);
            var start = game.Player.transform.position;
            var worldTime = Time.time;
            StageValidationInput.SetHeld(GameAction.MoveLeft, true);
            var until = Time.unscaledTime + .4f;
            while (Time.unscaledTime < until) yield return null;
            StageValidationInput.SetHeld(GameAction.MoveLeft, false);
            if (Vector2.Distance(start, game.Player.transform.position) > .001f || Time.time != worldTime)
                throw new InvalidOperationException("Map allowed world movement");
            capture = Capture("04-exploration-map"); while (capture.MoveNext()) yield return null;
            StageValidationInput.SetHeld(GameAction.Pause, true);
            yield return null;
            StageValidationInput.SetHeld(GameAction.Pause, false);
            if (game.IsMapOpen || game.IsManuallyPaused) throw new InvalidOperationException("Escape should close map first, without opening pause");
            yield return null; yield return null; // Closing frame is consumed before gameplay resumes.
            if (game.IsPaused || Time.timeScale <= 0) throw new InvalidOperationException("Map close did not resume gameplay");
            capture = Capture("05-map-closed"); while (capture.MoveNext()) yield return null;
            foreach (var room in Resources.LoadAll<RoomTemplate>("Rooms"))
            {
                var texture = DungeonMapView.CreateFootprint(room);
                File.WriteAllBytes(Path.Combine(directory, "footprint-" + room.roomId + ".png"), texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
                var issues = RoomLayoutRules.Validate(room);
                if (issues.Count > 0) throw new InvalidOperationException(room.roomId + ": " + string.Join(",", issues));
            }
        }
        private void Set(string name, object value) => typeof(GameController).GetProperty(name).GetSetMethod(true).Invoke(game, new[] { value });
        private IEnumerator Capture(string name)
        {
            var until = Time.unscaledTime + .3f;
            while (Time.unscaledTime < until) yield return null;
            screenshot.Invoke(null, new object[] { Path.Combine(directory, name + ".png"), 1 });
            until = Time.unscaledTime + .25f;
            while (Time.unscaledTime < until) yield return null;
        }
    }
}
