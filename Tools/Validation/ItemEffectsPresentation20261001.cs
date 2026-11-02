using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using RuinRunner;

// Staged visual review on the real first-region map. Items/targets supplied for review, not a normal run.
public static class ItemEffectsPresentation20261001
{
    static string folder; static GameController game; static Vector2 center;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
    static EditorWindow view; static object sizes; static int oldSize, oldCustomCount;
    static Type autoTick; static bool oldTickEnabled; static long oldTickInterval;
    static readonly List<int> customSizes = new List<int>();
    static readonly List<string> checks = new List<string>();
    static readonly StringBuilder recording = new StringBuilder();
    sealed class Frame { public int Index, Width, Height; public Color32[] Pixels; public string Showcase; }
    static readonly List<Frame> buffered = new List<Frame>(); static int sample;
    static int pending; static bool readbackError, flipReadback;
    static void Require(bool okay, string label) { if (!okay) throw new Exception(label); checks.Add("PASS " + label); }
    static void ClearEffects()
    {
        game.Player.ItemEffects.CancelTransient();
        foreach (var e in UnityEngine.Object.FindObjectsByType<EnemyController>()) { e.gameObject.SetActive(false); UnityEngine.Object.Destroy(e.gameObject); }
        foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) { p.gameObject.SetActive(false); UnityEngine.Object.Destroy(p.gameObject); }
    }
    static void Equip(string id, params AccessoryType[] accessories)
    {
        ClearEffects(); var bag = game.Inventory; bag.Reset(); bag.AddWeapon(WeaponLootTable.Find(id));
        foreach (var type in accessories) bag.AddAccessory(AccessoryLootTable.Find(type));
        game.Player.EquipWeapon(bag.EquippedWeapon); game.Player.Teleport(center + Vector2.left * 1.4f);
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll(); StageValidationInput.SetAim(Vector2.right);
    }
    static EnemyController Enemy(Vector2 offset)
    {
        var enemy = EnemyController.Create((EnemyType)0, (Vector2)game.Player.transform.position + offset, game.Player, () => false,
            dead => game.Player.OnEnemyDefeated(dead), game.CameraFollow);
        enemy.enabled = false; enemy.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        typeof(EnemyController).GetProperty("MaxHealth").SetValue(enemy, 220f); typeof(EnemyController).GetProperty("Health").SetValue(enemy, 220f);
        return enemy;
    }
    static void Pulse(GameAction action)
    { StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll(); StageValidationInput.SetHeld(action, true); StageValidationInput.SetHeld(action, false); }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-item-effects/presentation-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase");
        game = UnityEngine.Object.FindAnyObjectByType<GameController>(); game.ReturnToMainMenu(); game.SaveSlots.OpenTransientForTests();
        game.enabled = true; foreach (var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>()) hud.enabled = true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot", Private).Invoke(game, null);
        typeof(StageValidationInput).GetField("enabled", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        typeof(StageValidationInput).GetField("initialized", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        StageValidationInput.ReleaseAll(); Application.runInBackground = true; Application.targetFrameRate = 60; Time.captureFramerate = 0;
        view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")); view.Focus();
        oldSize = (int)view.GetType().GetProperty("selectedSizeIndex", All).GetValue(view);
        var t = Type.GetType("UnityEditor.GameViewSizes,UnityEditor"); var instance = t.GetProperty("instance", All).GetValue(null);
        sizes = t.GetMethod("GetGroup", All).Invoke(instance, new[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"), "Standalone") });
        oldCustomCount = (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { autoTick = assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand"); if (autoTick != null) break; }
        if (autoTick == null) throw new Exception("Installed editor auto-tick command not found");
        oldTickEnabled = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null);
        oldTickInterval = (long)autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null);
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
        game.CameraFollow.StartCoroutine(Guard(Run())); return folder;
    }
    static void Resolution(int width, int height)
    {
        var t = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var size = Activator.CreateInstance(t, new object[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"), width, height, "RuinRunner review " + width + "x" + height });
        int index = (int)sizes.GetType().GetMethod("GetTotalCount", All).Invoke(sizes, null);
        sizes.GetType().GetMethod("AddCustomSize", All).Invoke(sizes, new[] { size }); customSizes.Add(index);
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, index); view.Repaint();
    }
    static void RestoreView()
    {
        if (autoTick != null) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTickEnabled, (int)oldTickInterval, false });
        if (view == null || sizes == null) return;
        view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, oldSize);
        for (int i = customSizes.Count - 1; i >= 0; i--)
        {
            sizes.GetType().GetMethod("RemoveCustomSize", All).Invoke(sizes, new object[] { customSizes[i] });
        }
        customSizes.Clear();
        Require((int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null) == oldCustomCount,
            "review restores original Game View size selection and removes only its own temporary presets");
    }
    static void Capture(string name)
    { var texture = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(folder + "/" + name + ".png", texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture); }
    static IEnumerator Record(float seconds, string phase, float showcase)
    {
        double start = Time.realtimeSinceStartupAsDouble; bool taken = false;
        var target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32); target.Create();
        pending = 0; readbackError = false;
        while (Time.realtimeSinceStartupAsDouble - start < seconds)
        {
            yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble;
            string name = null;
            if (!taken && time - start >= showcase) { taken = true; name = phase + "-active"; }
            var frame = new Frame { Index = sample, Width = target.width, Height = target.height, Showcase = name }; buffered.Add(frame);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(target); pending++;
            AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
            {
                if (request.hasError) readbackError = true;
                else frame.Pixels = request.GetData<Color32>().ToArray();
                pending--;
            });
            recording.AppendLine(sample++ + "," + time.ToString("F6", CultureInfo.InvariantCulture) + "," + phase);
        }
        while (pending > 0) yield return null;
        target.Release(); UnityEngine.Object.Destroy(target);
        Require(!readbackError, "native asynchronous screen readback completed without dropped/failed frames " + phase);
    }
    static IEnumerator VerifyReadback()
    {
        Require(SystemInfo.supportsAsyncGPUReadback, "GPU supports asynchronous native screen readback");
        Time.timeScale = 0; yield return new WaitForEndOfFrame();
        var reference = ScreenCapture.CaptureScreenshotAsTexture(); var referencePixels = reference.GetPixels32();
        File.WriteAllBytes(folder + "/capture-reference.png", reference.EncodeToPNG()); UnityEngine.Object.Destroy(reference);
        var target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32); target.Create();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(target); Color32[] pixels = null; bool failed = false;
        AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, request =>
        { if (request.hasError) failed = true; else pixels = request.GetData<Color32>().ToArray(); });
        while (pixels == null && !failed) yield return null;
        Require(!failed, "native asynchronous reference capture succeeds");
        int normal = 0, flipped = 0, width = target.width, height = target.height;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var expected = referencePixels[y * width + x]; var a = pixels[y * width + x]; var b = pixels[(height - 1 - y) * width + x];
            if (a.r != expected.r || a.g != expected.g || a.b != expected.b) normal++;
            if (b.r != expected.r || b.g != expected.g || b.b != expected.b) flipped++;
        }
        flipReadback = flipped < normal;
        File.WriteAllText(folder + "/capture-readback-qa.txt", "normalDifferent=" + normal + " flippedDifferent=" + flipped + " verticalOrientationCorrection=" + flipReadback + "\nPixel comparison with synchronous native composited screen; no resampling.");
        Require(Mathf.Min(normal, flipped) < width * height * .01f, "asynchronous capture agrees with native composited screen pixels/orientation");
        target.Release(); UnityEngine.Object.Destroy(target); Time.timeScale = 1;
    }
    static void Flush()
    {
        // Compress between separately timestamped phases, never during the recorded motion.
        foreach (var f in buffered)
        {
            var texture = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false); texture.SetPixels32(f.Pixels); texture.Apply();
            if (flipReadback)
            {
                var oriented = new Color32[f.Pixels.Length];
                for (int y = 0; y < f.Height; y++) Array.Copy(f.Pixels, y * f.Width, oriented, (f.Height - 1 - y) * f.Width, f.Width);
                texture.SetPixels32(oriented); texture.Apply();
            }
            string path = folder + "/frames/frame-" + f.Index.ToString("D6") + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG());
            if (f.Showcase != null) File.Copy(path, folder + "/" + f.Showcase + ".png");
            UnityEngine.Object.DestroyImmediate(texture); f.Pixels = null;
        }
        buffered.Clear(); File.WriteAllText(folder + "/recording.csv", recording.ToString(), new UTF8Encoding(false));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object value = null; bool next;
            try { next = run.MoveNext(); if (next) value = run.Current; }
            catch (Exception error) { RestoreView(); Time.timeScale = 1; File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks)); Debug.LogException(error); yield break; }
            if (!next) break; yield return value;
        }
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged supplied-item/target visual review; not normal campaign, Windows build or 60fps performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Item effects presentation] PASS " + folder);
    }
    static IEnumerator Run()
    {
        yield return null; game.StopAllCoroutines(); game.enabled = false; typeof(GameController).GetProperty("StageIntroUntil").SetValue(game, 0f);
        foreach (var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>()) UnityEngine.Object.Destroy(e.gameObject);
        center = game.DungeonMap.GetRoomCenter(0); game.CameraFollow.FocusRoom(center, new Vector2(20, 14), true);
        Resolution(1920, 1080); yield return null; yield return new WaitForEndOfFrame();
        Require(Screen.width == 1920 && Screen.height == 1080, "motion capture is actual 1920x1080 output");
        var verify = VerifyReadback(); while (verify.MoveNext()) yield return verify.Current;
        Equip("scrap_pistol", AccessoryType.BerserkerEmblem, AccessoryType.VampireFang, AccessoryType.CrimsonRing);
        Enemy(new Vector2(1.5f, 0)); yield return null; Physics2D.SyncTransforms();
        StageValidationInput.BeginFrame(); StageValidationInput.SetHeld(GameAction.Attack, true);
        var motion = Record(2.4f, "blood", 1.9f); while (motion.MoveNext()) yield return motion.Current;
        StageValidationInput.ReleaseAll(); Require(game.Player.ItemEffects.ActiveShards > 0, "actual held attack creates visible recoverable blood shard"); Flush();
        Equip("storm_rifle", AccessoryType.SageSigil, AccessoryType.SoulCapacitor, AccessoryType.ArcaneLens, AccessoryType.ManaPrism);
        Enemy(new Vector2(.9f, .2f)); Enemy(new Vector2(1.2f, .95f)); Enemy(new Vector2(1.2f, -.9f)); yield return null; Physics2D.SyncTransforms();
        game.Player.Stats.RestoreMana(game.Player.MaxMana); Pulse(GameAction.Guard);
        motion = Record(2.4f, "arcane", .20f); while (motion.MoveNext()) yield return motion.Current;
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll();
        Require(game.Player.ItemEffects.ChainContactsResolved >= 3 && game.Player.ItemEffects.EchoContactsResolved >= 3 && game.Player.ItemEffects.OrbitContactsResolved > 0, "real paid rifle input displays chain, expanded echo and orbit on native map"); Flush();
        Equip("rust_sword", AccessoryType.StormFeather, AccessoryType.WingedBoots, AccessoryType.GuardianSeal, AccessoryType.GuardianPlate, AccessoryType.BulwarkBrooch);
        // Leave the actual travel lane open; a frozen enemy on the lane stops the player body
        // and makes every wake overlap at the starting point rather than showing a trail.
        Enemy(new Vector2(.75f, .65f)); yield return null; Physics2D.SyncTransforms();
        Vector2 dashStart = game.Player.transform.position; Pulse(GameAction.Dash);
        motion = Record(.75f, "storm", .24f); while (motion.MoveNext()) yield return motion.Current;
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll();
        Require(Vector2.Distance(dashStart, game.Player.transform.position) > 1.7f, "actual dash travels along an unobstructed native-map lane");
        Require(game.Player.ItemEffects.WakeContactsResolved > 0, "real dash input leaves native wind wake and contacts an off-lane enemy"); Flush();
        game.Player.Teleport(center + Vector2.left * 1.4f); yield return null;
        StageValidationInput.BeginFrame(); StageValidationInput.SetHeld(GameAction.Guard, true); yield return null;
        game.Player.TakeDamage(10f, Vector2.left);
        motion = Record(.8f, "guardian", .14f); while (motion.MoveNext()) yield return motion.Current;
        StageValidationInput.ReleaseAll(); Require(game.Player.ItemEffects.Shield > 0 && game.Player.ItemEffects.BashContactsResolved > 0, "actual held guard/damage produces native shield and retaliation"); Flush();
        ClearEffects(); game.Player.enabled = false;
        game.Inventory.Reset(); game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));
        foreach (var type in new[] { AccessoryType.BerserkerEmblem, AccessoryType.SageSigil, AccessoryType.ArcaneLens, AccessoryType.VampireFang, AccessoryType.SoulCapacitor }) game.Inventory.AddAccessory(AccessoryLootTable.Find(type));
        var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>(); hud.enabled = false;
        var paint = game.CameraFollow.gameObject.AddComponent<ItemTooltipPaint20261001>(); paint.Hud = hud;
        Pulse(GameAction.Inventory); game.enabled = true; yield return null; game.enabled = false;
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll(); Time.timeScale = 0;
        foreach (var resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1280, 800) })
        {
            Resolution(resolution.x, resolution.y); yield return null; yield return new WaitForEndOfFrame();
            Require(Screen.width == resolution.x && Screen.height == resolution.y, "actual tooltip output " + resolution);
            for (int i = 0; i < 5; i++)
            {
                paint.Key = "A:" + i; yield return new WaitForEndOfFrame();
                Capture("tooltip-" + game.Inventory.Accessories[i].Id + "-" + resolution.x + "x" + resolution.y);
            }
        }
        UnityEngine.Object.Destroy(paint); hud.enabled = true; Time.timeScale = 1;
    }
}

// Test-only native HUD call, in an actual OnGUI event. No runtime UI source or font replacement.
public sealed class ItemTooltipPaint20261001 : MonoBehaviour
{
    public PrototypeHud Hud; public string Key = "A:0";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    void OnGUI()
    {
        var matrix = GUI.matrix; var mouse = Event.current.mousePosition;
        float scale = GameTypography.LayoutScale; float width = Screen.width / scale, height = Screen.height / scale;
        Event.current.mousePosition = new Vector2(width - 490, height - 100);
        typeof(PrototypeHud).GetMethod("OnGUI", Private).Invoke(Hud, null);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        typeof(PrototypeHud).GetMethod("DrawItemTooltip", Private).Invoke(Hud, new object[] { width, height, Key });
        GUI.matrix = matrix; Event.current.mousePosition = mouse;
    }
}
