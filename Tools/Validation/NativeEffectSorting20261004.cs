using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using Newtonsoft.Json.Linq;
using RuinRunner;

// Supplied five native melee actors and stationary high-HP targets, actual GameAction
// combo input in eight directions. Defense uses explicitly supplied frontal damage.
// Compatibility Impact/Cleave are manual renderer probes, not current gameplay paths.
// Static original-size screenshots and per-render telemetry; no motion/audio/campaign claim.
public static class NativeEffectSorting20261004
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    const int Width = 1280, Height = 720;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static readonly Dictionary<GameObject, bool> roots = new Dictionary<GameObject, bool>();
    static readonly Dictionary<string, GameAction[]> inputs = new Dictionary<string, GameAction[]>();
    static readonly List<Sample> buffered = new List<Sample>();
    static readonly JArray clips = new JArray();
    static readonly StringBuilder recording = new StringBuilder(), telemetry = new StringBuilder(), warmup = new StringBuilder(), checks = new StringBuilder();
    static readonly StringBuilder cleaveTelemetry = new StringBuilder();
    sealed class Sample { public int Index; public Color32[] Pixels; public double Wall, Sim; public string Label; }
    static Scene originalScene, scene;
    static PlayerController player; static CameraFollow camera;
    static readonly List<PlayerController> actors = new List<PlayerController>();
    static readonly List<EnemyController> targets = new List<EnemyController>();
    static readonly string[] WeaponIds = { "worn_dagger", "rust_sword", "iron_mace", "training_katana", "iron_greatsword" };
    static readonly Vector2[] Stations = { new Vector2(-6,2), new Vector2(0,2), new Vector2(6,2), new Vector2(-3,-2), new Vector2(3,-2) };
    static readonly HashSet<string> seen = new HashSet<string>();
    static readonly HashSet<string> captures = new HashSet<string>();
    static readonly JArray captureManifest = new JArray();
    static int observed, oldAntialiasing; static AnisotropicFiltering oldAniso;
    static int currentDirection; static string phase; static double phaseStart;
    static GameSettingsService oldCombatSettings; static bool audioOverridden;
    static RenderTexture screen, crop;
    static EditorWindow view, oldFocus; static object sizes; static int oldSize, customSize = -1, oldCustomCount;
    static Type autoTick; static bool oldTick, tickSaved, madeView; static int oldTickInterval;
    static bool oldInputEnabled, oldInputInitialized, oldBackground, flip, error, running, restored;
    static Vector2 oldAim; static int oldRate, oldCapture, oldVsync, index, pending;
    static float oldScale; static UnityEngine.Random.State oldRandom; static double started; static string folder, outcome;
    static string F(double value) => value.ToString("F6", Inv);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); checks.AppendLine("PASS " + message); }
    static FieldInfo InputField(string name) => typeof(StageValidationInput).GetField(name, All);

    public static string Main()
    {
        if (running) throw new Exception("Review already running");
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (game == null || game.IsRunActive) throw new Exception("Start this supplied-actor fixture from the main menu, after the isolated regression finishes.");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-04-melee-sorting/" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder + "/frames");
        actors.Clear(); targets.Clear(); seen.Clear(); captures.Clear(); captureManifest.Clear(); observed = 0;
        roots.Clear(); inputs.Clear(); buffered.Clear(); clips.Clear(); checks.Clear(); recording.Clear(); telemetry.Clear(); warmup.Clear(); cleaveTelemetry.Clear();
        cleaveTelemetry.AppendLine("unityFrame,realtime,direction,sourceFrame,progress,tipX,tipY,leadingX,leadingY,tipGap,gripOffset,rotation,scaleX,scaleY,contactCount");
        warmup.AppendLine("nextClip,realtime,simulation,unityFrame,delta,stableFrames,editorActive,autoTickInterval");
        recording.AppendLine("frame,realtime,phase,simulation,unityFrame,timeScale");
        telemetry.AppendLine("unityFrame,realtime,simulation,direction,phase,weaponId,effect,sourceFrame,bodyOrder,weaponOrder,effectOrder,bodyLayer,weaponLayer,effectLayer,foreground,sequence,step,motion,progress,gripError,asset");
        index = pending = 0; error = restored = audioOverridden = false; customSize = -1; tickSaved = false; running = true; started = EditorApplication.timeSinceStartup;
        originalScene = SceneManager.GetActiveScene(); oldFocus = EditorWindow.focusedWindow;
        oldScale = Time.timeScale; oldCapture = Time.captureFramerate; oldRate = Application.targetFrameRate;
        oldBackground = Application.runInBackground; oldVsync = QualitySettings.vSyncCount;
        oldAntialiasing = QualitySettings.antiAliasing; oldAniso = QualitySettings.anisotropicFiltering;
        oldRandom = UnityEngine.Random.state;
        oldInputEnabled = (bool)InputField("enabled").GetValue(null); oldInputInitialized = (bool)InputField("initialized").GetValue(null);
        oldAim = StageValidationInput.AimDirection;
        foreach (var name in new[] { "Held", "Pressed", "Released" })
        { var set = (HashSet<GameAction>)InputField(name).GetValue(null); inputs[name] = new List<GameAction>(set).ToArray(); set.Clear(); }
        try
        {
            InputField("enabled").SetValue(null, true); InputField("initialized").SetValue(null, true);
            ConfigureView();
            // Silence through a review-only settings object before PlayClip can
            // select a pooled source beneath the hidden original audio root.
            // The user's settings object/file is never edited.
            oldCombatSettings = (GameSettingsService)typeof(CombatAudio).GetField("settings", All).GetValue(null);
            var mutedSettings = new GameSettingsService(folder + "/muted-settings.json", false);
            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel))) mutedSettings.ChangeVolume(channel, -1);
            CombatAudio.Configure(mutedSettings); audioOverridden = true;
            // Do not rebuild the user's world, inventory, save slot or front end.
            for (int s = 0; s < SceneManager.sceneCount; s++)
                foreach (var root in SceneManager.GetSceneAt(s).GetRootGameObjects()) roots[root] = root.activeSelf;
            foreach (var pair in roots) if (pair.Value) pair.Key.SetActive(false);
            scene = SceneManager.CreateScene("NativeEffectSorting-Temporary"); SceneManager.SetActiveScene(scene);
            camera = CameraFollow.Create(); camera.FocusRoom(Vector2.zero, new Vector2(100, 100), true);
            camera.GetComponent<Camera>().backgroundColor = new Color(.23f, .25f, .23f);
            if (GraphicsSettings.currentRenderPipeline != null) throw new Exception("This native Built-in pixel camera fixture requires the project's existing Built-in pipeline.");
            for (int i = 0; i < WeaponIds.Length; i++)
            {
                var bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find(WeaponIds[i]));
                var actor = PlayerController.Create(Stations[i], () => false, () => false, () => { }, camera, bag, game.Settings);
                actor.EquipWeapon(bag.EquippedWeapon); actors.Add(actor);
                var target = EnemyController.Create(EnemyType.Grunt, Stations[i] + Vector2.up * 1.1f, actor, () => false, _ => { }, null);
                typeof(EnemyController).GetField("acting", All).SetValue(target, true);
                typeof(EnemyController).GetField("collisionAttackActive", All).SetValue(target, false);
                typeof(EnemyController).GetProperty("MaxHealth", All).SetValue(target, 10000f);
                typeof(EnemyController).GetProperty("Health", All).SetValue(target, 10000f);
                target.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll; targets.Add(target);
            }
            player = actors[1]; camera.Target = null;
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Time.captureFramerate = 0; Time.timeScale = 1;
            EditorApplication.update += Watchdog; EditorApplication.playModeStateChanged += PlayState;
            camera.StartCoroutine(Guard(Run()));
        }
        catch (Exception e) { Finish("FAIL setup " + e); }
        return folder;
    }

    static void ConfigureView()
    {
        var type = Type.GetType("UnityEditor.GameView,UnityEditor"); madeView = Resources.FindObjectsOfTypeAll(type).Length == 0;
        view = EditorWindow.GetWindow(type); oldSize = (int)type.GetProperty("selectedSizeIndex", All).GetValue(view);
        var groups = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        sizes = groups.GetMethod("GetGroup", All).Invoke(groups.GetProperty("instance", All).GetValue(null),
            new[] { Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"), "Standalone") });
        oldCustomCount = (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null);
        var size = Activator.CreateInstance(Type.GetType("UnityEditor.GameViewSize,UnityEditor"), new object[] {
            Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"),1280,720,"Melee sorting temporary 1280x720" });
        customSize = (int)sizes.GetType().GetMethod("GetTotalCount", All).Invoke(sizes, null);
        sizes.GetType().GetMethod("AddCustomSize", All).Invoke(sizes, new[] { size });
        type.GetProperty("selectedSizeIndex", All).SetValue(view, customSize); view.Focus(); view.Repaint();
        autoTick = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { autoTick = assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand"); if (autoTick != null) break; }
        if (autoTick == null) throw new Exception("Installed AutoTick command missing");
        oldTick = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null);
        oldTickInterval = Convert.ToInt32(autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null)); tickSaved = true;
        autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(run); string failure = null;
        while (stack.Count > 0 && running)
        {
            object value = null; bool next;
            try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
            catch (Exception e) { failure = e.ToString(); break; }
            if (!next) { stack.Pop(); continue; }
            if (value is IEnumerator child) { stack.Push(child); continue; }
            yield return value;
        }
        if (running) Finish(failure == null ? "PASS supplied-actor melee sorting capture" : "FAIL " + failure);
    }
    static IEnumerator Run()
    {
        for (int i = 0; i < 5; i++) yield return new WaitForEndOfFrame();
        Require(Screen.width == Width && Screen.height == Height, "native Game View 1280x720; full-screen pixel copy without resampling");
        Require(SystemInfo.supportsAsyncGPUReadback, "asynchronous GPU readback supported");
        screen = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32); screen.Create();
        crop = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32); crop.Create();
        yield return VerifyReadback();
        for (currentDirection = 0; currentDirection < 8; currentDirection++)
        {
            StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
            yield return SettleCaptureClock(currentDirection);
            var aim = new Vector2(Mathf.Sin(currentDirection * Mathf.PI / 4), Mathf.Cos(currentDirection * Mathf.PI / 4));
            StageValidationInput.SetAim(aim);
            for (int i = 0; i < actors.Count; i++)
            {
                actors[i].enabled = true; actors[i].Teleport(Stations[i]); actors[i].Stats.RestoreMana(actors[i].MaxMana);
                targets[i].transform.position = Stations[i] + aim * 1.1f;
            }
            Physics2D.SyncTransforms(); yield return new WaitForEndOfFrame();
            yield return Direction(aim);
            while (pending > 0) yield return null;
            Require(!error, "all original pixel screenshot readbacks completed direction " + currentDirection);
            Flush();
        }
        Require(clips.Count == 8, "eight directions completed for all five representative melee families");
        Require(observed > 0, "actual visible effects were observed and checked");
    }
    static IEnumerator SettleCaptureClock(int nextClip)
    {
        // PNG encoding runs synchronously between clips. Force the following
        // Update to consume that elapsed time, then observe two normal frames
        // before starting either the input schedule or the recorded clip clock.
        int stable = 0; double limit = Time.realtimeSinceStartupAsDouble + 2;
        while (stable < 2 && Time.realtimeSinceStartupAsDouble < limit)
        {
            StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll();
            // Encoding can outlast the foreground editor's focus. Reacquire it
            // here as well as during capture; a background 20Hz loop is not a
            // settled recording clock and must not be accepted by a wider limit.
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            { autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor", All).Invoke(null, null); view.Focus(); }
            view.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
            yield return null; yield return new WaitForEndOfFrame();
            stable = Time.deltaTime > 0 && Time.deltaTime < .05f ? stable + 1 : 0;
            warmup.AppendLine(string.Join(",", nextClip, F(Time.realtimeSinceStartupAsDouble), F(Time.timeAsDouble), Time.frameCount, F(Time.deltaTime), stable,
                UnityEditorInternal.InternalEditorUtility.isApplicationActive, autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null)));
        }
        Require(stable == 2, "recording clock settled after between-clip encoding " + nextClip);
    }
    static void CopyScreen()
    {
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
        Graphics.CopyTexture(screen, 0, 0, (Screen.width - Width) / 2, (Screen.height - Height) / 2, Width, Height, crop, 0, 0, 0, 0);
    }
    static IEnumerator VerifyReadback()
    {
        Time.timeScale = 0; yield return new WaitForEndOfFrame();
        var reference = ScreenCapture.CaptureScreenshotAsTexture();
        var full = reference.GetPixels32(); UnityEngine.Object.Destroy(reference);
        CopyScreen(); Color32[] pixels = null; bool failed = false;
        AsyncGPUReadback.Request(crop, 0, TextureFormat.RGBA32, r => { if (r.hasError) failed = true; else pixels = r.GetData<Color32>().ToArray(); });
        while (pixels == null && !failed) yield return null;
        Require(!failed, "readback reference succeeds");
        int normal = 0, reversed = 0, x0 = (Screen.width - Width) / 2, y0 = (Screen.height - Height) / 2;
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            var expected = full[(y + y0) * Screen.width + x + x0];
            if (!expected.Equals(pixels[y * Width + x])) normal++;
            if (!expected.Equals(pixels[(Height - 1 - y) * Width + x])) reversed++;
        }
        flip = reversed < normal;
        Require(Math.Min(normal, reversed) < Width * Height / 100, "captured crop matches original screen pixels and vertical orientation");
        File.WriteAllText(folder + "/capture-qa.txt", "normalDifferences=" + normal + " flippedDifferences=" + reversed + " flip=" + flip, Utf8);
        Time.timeScale = 1;
    }
    static IEnumerator Direction(Vector2 aim)
    {
        var masks = new int[actors.Count]; var supplied = new bool[actors.Count];
        var initialHealth = new float[actors.Count]; for (int i = 0; i < targets.Count; i++) initialHealth[i] = targets[i].Health;
        phaseStart = Time.timeAsDouble; phase = "actual-combo";
        bool released = false, special = false, compatibility = false;
        double wallStart = Time.realtimeSinceStartupAsDouble;
        while (Time.timeAsDouble - phaseStart < 3.25 && Time.realtimeSinceStartupAsDouble - wallStart < 5)
        {
            StageValidationInput.BeginFrame(); double t = Time.timeAsDouble - phaseStart;
            StageValidationInput.SetHeld(GameAction.Attack, t >= .06 && t < 1.60);
            if (!released && t >= 1.60) { StageValidationInput.ReleaseAll(); released = true; }
            if (t >= 2.05 && !special)
            {
                phase = "actual-defense-supplied-frontal-hit";
                // Only these two families receive the shared RMB input. All actors
                // and their state are isolated supplied fixtures, never the user's run.
                for (int i = 0; i < actors.Count; i++) if (i != 1 && i != 3) actors[i].enabled = false;
                StageValidationInput.SetHeld(GameAction.Guard, true); special = true;
            }
            else if (special) StageValidationInput.SetHeld(GameAction.Guard, false);
            for (int i = 0; i < actors.Count; i++)
            {
                if (phase == "actual-combo" && actors[i].IsMeleeSwingActive && actors[i].ActiveComboAnimationStep >= 0)
                    masks[i] |= 1 << actors[i].ActiveComboAnimationStep;
                if (special && !supplied[i] && (i == 1 && actors[i].IsLongswordCatchActive || i == 3 && actors[i].IsParrying))
                { actors[i].TakeDamage(10, -aim); supplied[i] = true; }
            }
            if (!compatibility && t >= 2.87)
            {
                phase = "manual-compatibility-probe"; compatibility = true;
                float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                // These two fallback paths are not selected by current melee combo
                // profiles. This probe is explicitly not actual GameAction coverage.
                ExternalCombatEffect.Create("Impact", (Vector2)actors[2].transform.position + aim, angle, 2.3f, -3000, Body(actors[2]));
                ExternalCombatEffect.Create("Cleave", (Vector2)actors[4].transform.position + aim, angle + 90, 2.3f, -3000, Body(actors[4]));
            }
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            { autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor", All).Invoke(null, null); view.Focus(); }
            view.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); yield return new WaitForEndOfFrame();
            ObserveEffects();
        }
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        for (int i = 0; i < actors.Count; i++)
        {
            int wanted = (1 << MeleeComboCatalog.Get(actors[i].EquippedWeapon).LightSteps.Length) - 1;
            Require(masks[i] == wanted, "actual complete combo " + WeaponIds[i] + " direction " + currentDirection);
            Require(targets[i].Health < initialHealth[i], "actual contact on supplied fixed target " + WeaponIds[i] + " direction " + currentDirection);
            Need(i, "Slash", "actual-combo");
        }
        Need(0, "Thrust", "actual-combo"); Need(3, "Cross0", "actual-combo"); Need(3, "Cross1", "actual-combo");
        Need(4, "Native-GreatswordCut", "actual-combo"); Need(2, "Native-GroundDust", "actual-combo");
        Need(1, "Native-CutStreak", "actual-combo"); Need(3, "Native-ParryCross", "actual-combo");
        Require(supplied[1] && supplied[3], "actual RMB catch/parry accepted supplied frontal hit direction " + currentDirection);
        Need(1, "Native-MetalSpark", "actual-defense-supplied-frontal-hit");
        Need(3, "Native-ParryCross", "actual-defense-supplied-frontal-hit"); Need(3, "Native-TwinCrescent", "actual-defense-supplied-frontal-hit");
        Need(2, "External-ImpactVFX", "manual-compatibility-probe"); Need(4, "External-CleaveVFX", "manual-compatibility-probe");
        clips.Add(new JObject { ["direction"] = currentDirection, ["observedWallSeconds"] = Time.realtimeSinceStartupAsDouble - wallStart,
            ["scope"] = "five simultaneous native actors; actual combo and defense inputs; manually supplied frontal damage; explicitly separate fallback renderer probes" });
    }
    static SpriteRenderer Body(PlayerController actor) => actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
    static string Key(int actor, string kind, string stage) => currentDirection + "/" + WeaponIds[actor] + "/" + stage + "/" + kind;
    static void Need(int actor, string kind, string stage) => Require(seen.Contains(Key(actor, kind, stage)), "visible " + Key(actor, kind, stage));
    static PlayerController Source(Component effect, string actorField)
    {
        var parent = effect.GetComponentInParent<PlayerController>(); if (parent != null) return parent;
        var info = effect.GetType().GetField(actorField, All); var renderer = info == null ? null : info.GetValue(effect) as SpriteRenderer;
        return renderer == null ? null : renderer.GetComponentInParent<PlayerController>();
    }
    static void ObserveEffects()
    {
        foreach (var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())
            Observe(fx.GetComponent<SpriteRenderer>(), Source(fx, "caster"), "Slash", fx.FrameIndex, true);
        foreach (var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())
            Observe(fx.GetComponent<SpriteRenderer>(), Source(fx, "caster"), "Thrust", fx.FrameIndex, true);
        foreach (var fx in UnityEngine.Object.FindObjectsByType<MeleeCrossCutEffect>())
            for (int i = 0; i < 2; i++) Observe(fx.StrokeRenderer(i), Source(fx, "caster"), "Cross" + i, fx.FrameIndex(i), true);
        foreach (var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())
            if (fx.IsAboveWeapon || fx.IsBelowActor)
            {
                Observe(fx.GetComponent<SpriteRenderer>(), Source(fx, "foregroundActor"), "Native-" + fx.ClipId, fx.FrameIndex, fx.IsAboveWeapon);
                if (fx.ClipId == "GreatswordCut") ObserveCleave(fx);
            }
        foreach (var fx in UnityEngine.Object.FindObjectsByType<ExternalCombatEffect>())
            Observe(fx.GetComponent<SpriteRenderer>(), Source(fx, "caster"), "External-" + fx.name, -1, true);
    }
    static void Observe(SpriteRenderer fx, PlayerController actor, string kind, int sourceFrame, bool foreground)
    {
        if (fx == null || actor == null || !fx.enabled || !fx.gameObject.activeInHierarchy || fx.sprite == null || fx.color.a <= .02f || fx.gameObject.scene != scene) return;
        int a = actors.IndexOf(actor); if (a < 0) return;
        var body = Body(actor); var held = actor.GetComponent<HeldWeaponView>();
        var weapon = (SpriteRenderer)typeof(HeldWeaponView).GetProperty("VisualRenderer", All).GetValue(held);
        var animator = actor.GetComponent<PlayerAnimationController>();
        float gripError = Vector2.Distance(actor.transform.Find("HeldWeaponPivot").localPosition, animator.HandLocalPosition);
        if (gripError > .001f) throw new Exception("Native grip detached " + WeaponIds[a]);
        bool correct = foreground ? fx.sortingLayerID == weapon.sortingLayerID && fx.sortingLayerID == body.sortingLayerID && fx.sortingOrder > weapon.sortingOrder && fx.sortingOrder < body.sortingOrder
                                  : fx.sortingLayerID == body.sortingLayerID && fx.sortingOrder < body.sortingOrder;
        telemetry.AppendLine(string.Join(",", Time.frameCount, F(Time.realtimeSinceStartupAsDouble), F(Time.timeAsDouble), currentDirection, phase, WeaponIds[a], kind,
            sourceFrame, body.sortingOrder, weapon.sortingOrder, fx.sortingOrder, body.sortingLayerName, weapon.sortingLayerName, fx.sortingLayerName, foreground,
            actor.AttackAnimationSequence, actor.ActiveComboAnimationStep, actor.ActiveMeleeMotion, F(actor.AttackAnimationProgress), F(gripError), AssetDatabase.GetAssetPath(fx.sprite)));
        observed++; var key = Key(a, kind, phase); seen.Add(key);
        if (!correct) { Capture("FAIL-" + key); throw new Exception("Effect sorting regression " + key + " body=" + body.sortingOrder + " weapon=" + weapon.sortingOrder + " effect=" + fx.sortingOrder); }
        // Representative contact / mature silhouettes, not transparent anticipation.
        bool ready = sourceFrame >= 1 || kind.StartsWith("External-");
        if (ready && captures.Add(key)) Capture(key);
    }
    static Vector3 PixelPoint(SpriteRenderer renderer, Vector2 pixel)
    {
        var sprite = renderer.sprite;
        var p = new Vector3((pixel.x - sprite.pivot.x) / sprite.pixelsPerUnit, (sprite.rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit);
        if (renderer.flipX) p.x = -p.x; if (renderer.flipY) p.y = -p.y;
        return renderer.transform.TransformPoint(p);
    }
    static void ObserveCleave(NativeCombatEffect fx)
    {
        var actor = Source(fx, "foregroundActor"); var effect = fx.GetComponent<SpriteRenderer>();
        if (actor == null || !effect.enabled || effect.sprite == null) return;
        var held = actor.GetComponent<HeldWeaponView>(); var weapon = (SpriteRenderer)typeof(HeldWeaponView).GetProperty("VisualRenderer", All).GetValue(held);
        NativeWeaponArt.TryGet(actor.EquippedWeapon.Id, out var pose); var tip = PixelPoint(weapon, pose.Muzzle);
        var anchors = new[] { new Vector2(93.5f,91.5f),new Vector2(100.5f,74.5f),new Vector2(102.5f,49.5f),new Vector2(62.5f,34.5f) };
        var leading = fx.FrameIndex < 4 ? PixelPoint(effect, anchors[fx.FrameIndex]) : tip;
        float gap = Vector2.Distance(tip, leading), offset = Vector2.Distance(fx.transform.position, held.BladePoint(0));
        cleaveTelemetry.AppendLine(string.Join(",", Time.frameCount, F(Time.realtimeSinceStartupAsDouble), currentDirection, fx.FrameIndex,
            F(actor.AttackAnimationProgress), F(tip.x), F(tip.y), F(leading.x), F(leading.y), F(gap), F(offset), F(fx.transform.eulerAngles.z),
            F(fx.transform.lossyScale.x), F(fx.transform.lossyScale.y), actor.MeleeContactsResolved));
        if (fx.FrameIndex < 3 && gap > .002f) throw new Exception("Thin cleave intro detached from native metal tip: " + gap);
        if (fx.FrameIndex >= 4 && offset > .002f) throw new Exception("Cleave contact/recovery moved away from original grip envelope: " + offset);
        string key = Key(actors.IndexOf(actor), "GreatswordSource" + fx.FrameIndex, phase);
        if (captures.Add(key)) Capture(key);
    }
    static void Capture(string label)
    {
        var frame = new Sample { Index = index++, Wall = Time.realtimeSinceStartupAsDouble, Sim = Time.timeAsDouble, Label = label };
        buffered.Add(frame); CopyScreen(); pending++;
        AsyncGPUReadback.Request(crop, 0, TextureFormat.RGBA32, r => { if (r.hasError) error = true; else frame.Pixels = r.GetData<Color32>().ToArray(); pending--; });
        recording.AppendLine(string.Join(",", frame.Index, F(frame.Wall), label, F(frame.Sim), Time.frameCount, F(Time.timeScale)));
        captureManifest.Add(new JObject { ["file"] = "frames/frame-" + frame.Index.ToString("D6") + ".png", ["label"] = label, ["wall"] = frame.Wall, ["simulation"] = frame.Sim, ["unityFrame"] = Time.frameCount });
    }
    static void Flush()
    {
        foreach (var frame in buffered)
        {
            if (frame.Pixels == null) continue;
            var pixels = frame.Pixels;
            if (flip) { pixels = new Color32[frame.Pixels.Length]; for (int y = 0; y < Height; y++) Array.Copy(frame.Pixels, y * Width, pixels, (Height - 1 - y) * Width, Width); }
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false); texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(folder + "/frames/frame-" + frame.Index.ToString("D6") + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); frame.Pixels = null;
        }
        buffered.Clear();
        File.WriteAllText(folder + "/recording.csv", recording.ToString(), Utf8);
        File.WriteAllText(folder + "/sorting-telemetry.csv", telemetry.ToString(), Utf8);
        File.WriteAllText(folder + "/cleave-anchor-telemetry.csv", cleaveTelemetry.ToString(), Utf8);
        File.WriteAllText(folder + "/warmup.csv", warmup.ToString(), Utf8);
        File.WriteAllText(folder + "/manifest.json", new JObject { ["scope"] = "Supplied native actors and stationary high-HP targets. Actual GameAction combo and defense; explicit frontal damage. Separate manual fallback renderer probes. Static captures, not a motion, campaign, performance or audio pass.",
            ["viewport"] = "1280x720 original pixels", ["layout"] = "top: dagger, longsword, mace; bottom: katana, greatsword", ["directions"] = clips, ["observations"] = observed, ["captures"] = captureManifest }.ToString(), Utf8);
        File.WriteAllText(folder + "/review.html", Viewer, Utf8);
    }
    static void Watchdog() { if (running && EditorApplication.timeSinceStartup - started > 60) Finish("FAIL 60 second watchdog; partial capture retained"); }
    static void PlayState(PlayModeStateChange state) { if (running && state == PlayModeStateChange.ExitingPlayMode) Finish("INTERRUPTED Play Mode ended"); }
    static void Restore(Action action) { try { action(); } catch (Exception e) { checks.AppendLine("RESTORE ERROR " + e.Message); outcome = "FAIL restoration"; } }
    static void Finish(string result)
    {
        if (!running || restored) return; restored = true; running = false; outcome = result;
        EditorApplication.update -= Watchdog; EditorApplication.playModeStateChanged -= PlayState;
        Restore(() => { AsyncGPUReadback.WaitAllRequests(); Flush(); });
        Restore(() => { foreach (var actor in actors) if (actor != null) { actor.ItemEffects.CancelTransient(); actor.gameObject.SetActive(false); } });
        Restore(() => { if (screen != null) { screen.Release(); UnityEngine.Object.DestroyImmediate(screen); } if (crop != null) { crop.Release(); UnityEngine.Object.DestroyImmediate(crop); } });
        Restore(() => { if (scene.IsValid() && scene.isLoaded) foreach (var root in scene.GetRootGameObjects()) root.SetActive(false); });
        Restore(() => { if (audioOverridden) CombatAudio.Configure(oldCombatSettings); });
        Restore(() => { if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene); foreach (var pair in roots) if (pair.Key != null) pair.Key.SetActive(pair.Value); });
        Restore(() => { foreach (var pair in inputs) { var set = (HashSet<GameAction>)InputField(pair.Key).GetValue(null); set.Clear(); set.UnionWith(pair.Value); }
            typeof(StageValidationInput).GetProperty("AimDirection", All).SetValue(null, oldAim); InputField("enabled").SetValue(null, oldInputEnabled); InputField("initialized").SetValue(null, oldInputInitialized); });
        Restore(() => { Application.runInBackground = oldBackground; Application.targetFrameRate = oldRate; QualitySettings.vSyncCount = oldVsync; QualitySettings.antiAliasing = oldAntialiasing; QualitySettings.anisotropicFiltering = oldAniso; Time.captureFramerate = oldCapture; Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom; });
        Restore(() => { if (view != null) view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, oldSize); if (customSize >= 0 && sizes != null) sizes.GetType().GetMethod("RemoveCustomSize", All).Invoke(sizes, new object[] { customSize });
            if (sizes != null && (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null) != oldCustomCount) throw new Exception("Game View preset count changed"); if (madeView && view != null) view.Close(); if (oldFocus != null) oldFocus.Focus(); });
        Restore(() => { if (tickSaved) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTick, oldTickInterval, false }); });
        if (scene.IsValid() && scene.isLoaded) { SceneManager.sceneUnloaded += Unloaded; SceneManager.UnloadSceneAsync(scene); }
        else WriteResult();
    }
    static void Unloaded(Scene unloaded) { if (unloaded.handle != scene.handle) return; SceneManager.sceneUnloaded -= Unloaded; checks.AppendLine("PASS temporary scene unloaded; original world/input/view/time/AutoTick restored"); WriteResult(); }
    static void WriteResult() { File.WriteAllText(folder + "/result.txt", outcome + "\n" + checks + "\nStatic screenshots and sorting telemetry only; not a motion, campaign or audio quality claim.\n", Utf8); Debug.Log("[Melee sorting] " + outcome + " " + folder); }
    const string Viewer = @"<!doctype html><meta charset='utf-8'><title>Melee sorting native screenshots</title><style>body{background:#202020;color:#eee;font:14px sans-serif}button{margin:3px}img{display:block;width:1280px;height:720px;image-rendering:pixelated}</style><p>Supplied five actors. Top: dagger / longsword / mace. Bottom: katana / greatsword. Original screen pixels. Static samples, not motion proof.</p><div id='buttons'></div><pre id='info'></pre><img id='frame'><script>fetch('manifest.json').then(r=>r.json()).then(m=>{for(const f of m.captures){const b=document.createElement('button');b.textContent=f.label;b.onclick=()=>{document.querySelector('#frame').src=f.file;document.querySelector('#info').textContent=f.label+' / actual Unity frame '+f.unityFrame};document.querySelector('#buttons').append(b)}if(m.captures.length)document.querySelector('button').click()});</script>";
}
