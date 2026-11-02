using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Real movement/collision through connected regions; encounter states are explicit fixtures, not combat victory.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ContinuousWorldValidation : MonoBehaviour
    {
        private DungeonMap map;
        private PlayerController player;
        private Camera cameraView;
        private IEnumerator sequence;
        private int current;
        private int enters;
        private int frame;
        private int renderTick;
        private Vector2 previousPosition;
        private string output;
        private RenderTexture render;
        private Texture2D pixels;

        private void Start()
        {
            Time.timeScale = 1; Time.captureFramerate = 60; Application.targetFrameRate = 60;
            Application.runInBackground = true; QualitySettings.vSyncCount = 0;
            var rooms = new RoomTemplate[3];
            for (var i = 0; i < rooms.Length; i++)
            {
                rooms[i] = ScriptableObject.CreateInstance<RoomTemplate>(); rooms[i].roomId = "connected_test_" + i;
                rooms[i].roomSize = new Vector2(20 + i * 4, 12 + i * 4);
                rooms[i].platforms = Array.Empty<RoomPlatformData>();
                rooms[i].decorations = Array.Empty<RoomDecorationData>();
                rooms[i].preferredExitDirection = i == 0 ? RoomTravelDirection.Right : RoomTravelDirection.Up;
            }
            map = DungeonMap.Build(rooms, room => { current = room; enters++; map.BeginEncounter(room); });
            var follow = CameraFollow.Create(); cameraView = follow.GetComponent<Camera>();
            var inventory = new InventorySystem(); inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
            player = PlayerController.Create(Vector2.zero, () => false, () => true, () => { }, follow, inventory, new GameSettingsService());
            player.EquipWeapon(inventory.EquippedWeapon); previousPosition = player.transform.position;
            follow.Target = player.transform; follow.FocusRoom(map.Terrain.Bounds.center, map.Terrain.Bounds.size, true);
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "--validation-capture-dir");
            if (index >= 0 && index + 1 < args.Length) { output = args[index + 1]; Directory.CreateDirectory(output); }
            sequence = Validate(); StartCoroutine(Capture());
        }

        private void Update()
        {
            if (sequence == null) return;
            StageValidationInput.BeginFrame();
            try
            {
                var position = (Vector2)player.transform.position;
                Require(Vector2.Distance(position, previousPosition) < .5f, "no transition teleport"); previousPosition = position;
                if (sequence.MoveNext()) return;
                Debug.Log("[Continuous World Validation] PASS: physical walk-through right/up; exit seal; seal closes behind; release/backtrack; no duplicate entry; no teleport. Isolated encounter-state fixture, not boss clear.");
                sequence = null; Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("[Continuous World Validation] FAIL " + error); sequence = null; Application.Quit(3); }
        }

        private IEnumerator Validate()
        {
            for (var i = 0; i < 130; i++) { Move(Vector2.right); yield return null; }
            var firstGate = map.GetRoom(0).ExitPoint(RoomTravelDirection.Right).x + .8f;
            Require(player.transform.position.x < firstGate - .2f && current == 0, "locked departure blocks movement");
            map.UnlockExit(0);
            for (var i = 0; i < 600 && current == 0; i++) { Move(Vector2.right); yield return null; }
            Require(current == 1 && enters == 1, "walk reaches next zone exactly once");
            var arrival = map.GetRoomCenter(1).x + map.GetRoom(1).ExitPoint(RoomTravelDirection.Left).x - .8f;
            for (var i = 0; i < 90; i++) { Move(Vector2.left); yield return null; }
            Require(player.transform.position.x > arrival + .2f, "entrance sealed behind player");
            map.UnlockExit(1);
            for (var i = 0; i < 60; i++) { Move(Vector2.left); yield return null; }
            Require(player.transform.position.x < arrival - 2f, "clear reopens visited connection for backtracking");
            for (var i = 0; i < 240 && player.transform.position.x < map.GetRoomCenter(1).x - .15f; i++) { Move(Vector2.right); yield return null; }
            Require(enters == 1, "returning across entrance does not retrigger encounter");
            for (var i = 0; i < 360 && current == 1; i++) { Move(Vector2.up); yield return null; }
            Require(current == 2 && enters == 2, "upward continuous connection works");
            for (var i = 0; i < 60; i++) { Move(Vector2.zero); yield return null; }
        }
        private static void Move(Vector2 direction)
        {
            StageValidationInput.SetHeld(GameAction.MoveLeft, direction.x < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveRight, direction.x > .1f);
            StageValidationInput.SetHeld(GameAction.MoveDown, direction.y < -.1f);
            StageValidationInput.SetHeld(GameAction.MoveUp, direction.y > .1f);
            StageValidationInput.SetAim(direction.sqrMagnitude > .01f ? direction : Vector2.right);
        }

        private IEnumerator Capture()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (string.IsNullOrEmpty(output) || (renderTick++ & 1) != 0) continue;
                if (render == null) { render = new RenderTexture(1280, 720, 24); pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); }
                var previous = RenderTexture.active; cameraView.targetTexture = render; cameraView.Render(); RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                cameraView.targetTexture = null; RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(output, "world-" + frame.ToString("D4") + ".png"), pixels.EncodeToPNG());
                File.AppendAllText(Path.Combine(output, "positions.csv"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2:F3},{3:F3}\n", frame++, current, player.transform.position.x, player.transform.position.y));
            }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private void OnDestroy() { if (render != null) { render.Release(); Destroy(render); } if (pixels != null) Destroy(pixels); }
    }
}
