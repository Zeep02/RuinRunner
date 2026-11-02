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

// Supplied rust_sword actor in an isolated review scene. Real GameAction input,
// native body/grip/effects, and one explicitly supplied frontal hit per clip.
// No campaign/AI/performance/audio claim; no source sprite edits or resampling.
public static class LongswordMotionReview20261004
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    const int Width = 512, Height = 384;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static readonly Dictionary<GameObject, bool> roots = new Dictionary<GameObject, bool>();
    static readonly Dictionary<string, GameAction[]> inputs = new Dictionary<string, GameAction[]>();
    static readonly List<Sample> buffered = new List<Sample>();
    static readonly JArray clips = new JArray();
    static readonly StringBuilder recording = new StringBuilder(), telemetry = new StringBuilder(), warmup = new StringBuilder(), checks = new StringBuilder();
    sealed class Sample { public int Index; public Color32[] Pixels; public double Wall, Sim; }
    static Scene originalScene, scene;
    static PlayerController player; static CameraFollow camera;
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-04-sword-motion/" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder + "/frames");
        roots.Clear(); inputs.Clear(); buffered.Clear(); clips.Clear(); checks.Clear(); recording.Clear(); telemetry.Clear(); warmup.Clear();
        warmup.AppendLine("nextClip,realtime,simulation,unityFrame,delta,stableFrames");
        recording.AppendLine("frame,realtime,phase,simulation,unityFrame,timeScale");
        telemetry.AppendLine("frame,phase,realtime,simulation,unityFrame,delta,sequence,step,motion,progress,pose,key,direction,handX,handY,gripError,angle,contacts,catch,caught,moving,x,y,bodyAsset,weaponAsset");
        index = pending = 0; error = restored = audioOverridden = false; customSize = -1; tickSaved = false; running = true; started = EditorApplication.timeSinceStartup;
        originalScene = SceneManager.GetActiveScene(); oldFocus = EditorWindow.focusedWindow;
        oldScale = Time.timeScale; oldCapture = Time.captureFramerate; oldRate = Application.targetFrameRate;
        oldBackground = Application.runInBackground; oldVsync = QualitySettings.vSyncCount;
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
            scene = SceneManager.CreateScene("LongswordMotionReview-Temporary"); SceneManager.SetActiveScene(scene);
            camera = CameraFollow.Create(); camera.FocusRoom(Vector2.zero, new Vector2(100, 100), true);
            camera.GetComponent<Camera>().backgroundColor = new Color(.23f, .25f, .23f);
            var bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find("rust_sword"));
            player = PlayerController.Create(Vector2.zero, () => false, () => false, () => { }, camera, bag, game.Settings);
            player.EquipWeapon(bag.EquippedWeapon); camera.Target = player.transform;
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
            Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"), "FixedResolution"),1280,720,"Sword motion temporary 1280x720" });
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
        if (running) Finish(failure == null ? "PASS supplied-actor motion capture" : "FAIL " + failure);
    }
    static IEnumerator Run()
    {
        for (int i = 0; i < 5; i++) yield return new WaitForEndOfFrame();
        Require(Screen.width == 1280 && Screen.height == 720, "native Game View 1280x720; fixed 512x384 pixel crop, no zoom/resampling");
        Require(SystemInfo.supportsAsyncGPUReadback, "asynchronous GPU readback supported");
        screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32); screen.Create();
        crop = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32); crop.Create();
        yield return VerifyReadback();
        for (int moving = 0; moving < 2; moving++) for (int direction = 0; direction < 8; direction++)
        {
            StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
            yield return SettleCaptureClock(moving * 8 + direction);
            player.Teleport(Vector2.zero); player.Stats.RestoreMana(player.MaxMana);
            camera.FocusRoom(Vector2.zero, new Vector2(100, 100), true);
            var aim = new Vector2(Mathf.Sin(direction * Mathf.PI / 4), Mathf.Cos(direction * Mathf.PI / 4));
            StageValidationInput.SetAim(aim);
            // Previous actions finished before this boundary; no timers or animation keys are forced.
            yield return new WaitForEndOfFrame();
            yield return Clip(direction, moving != 0, aim);
            Flush();
        }
        Require(clips.Count == 16, "all eight directions captured both stationary and moving");
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
            view.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
            yield return null; yield return new WaitForEndOfFrame();
            stable = Time.deltaTime > 0 && Time.deltaTime < .05f ? stable + 1 : 0;
            warmup.AppendLine(string.Join(",", nextClip, F(Time.realtimeSinceStartupAsDouble), F(Time.timeAsDouble), Time.frameCount, F(Time.deltaTime), stable));
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
    static IEnumerator Clip(int direction, bool moving, Vector2 aim)
    {
        string name = "rust_sword-" + (moving ? "moving" : "stationary") + "-direction-" + direction;
        double wallStart = Time.realtimeSinceStartupAsDouble, simStart = Time.timeAsDouble;
        int first = index, initialSequence = player.AttackAnimationSequence, initialContacts = player.MeleeContactsResolved;
        bool primary = false, released = false, special = false, suppliedHit = false; int swingMask = 0; double previous = 0, maxGap = 0;
        var clipFrames = new JArray(); var poses = new HashSet<string>(); float maxGripError = 0;
        while (Time.timeAsDouble - simStart < 1.72 && Time.realtimeSinceStartupAsDouble - wallStart < 3.0)
        {
            StageValidationInput.BeginFrame();
            double sim = Time.timeAsDouble - simStart;
            if (moving) StageValidationInput.SetHeld(GameAction.MoveRight, true);
            if (!primary && sim >= .06) { StageValidationInput.SetHeld(GameAction.Attack, true); primary = true; }
            if (!released && player.AttackAnimationSequence >= initialSequence + 3)
            { StageValidationInput.SetHeld(GameAction.Attack, false); released = true; }
            if (!special && sim >= 1.00) { StageValidationInput.SetHeld(GameAction.Guard, true); special = true; }
            else if (special) StageValidationInput.SetHeld(GameAction.Guard, false);
            if (!suppliedHit && player.IsLongswordCatchActive)
            {
                // Explicit test stimulus, not enemy AI footage. No instant outgoing damage.
                player.TakeDamage(10, -aim); suppliedHit = true;
            }
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            { autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor", All).Invoke(null, null); view.Focus(); }
            view.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); yield return new WaitForEndOfFrame();
            double wall = Time.realtimeSinceStartupAsDouble, simulation = Time.timeAsDouble;
            var a = player.GetComponent<PlayerAnimationController>(); var held = player.GetComponent<HeldWeaponView>();
            var body = player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            var grip = Vector2.Distance(player.transform.Find("HeldWeaponPivot").localPosition, a.HandLocalPosition);
            maxGripError = Mathf.Max(maxGripError, grip); poses.Add(a.PoseName);
            if (player.IsMeleeSwingActive && player.AttackAnimationSequence <= initialSequence + 3) swingMask |= 1 << player.ActiveComboAnimationStep;
            if (a.FacingDirection != direction) throw new Exception("Body/aim direction mismatch " + name);
            if (body.sprite.rect.size != new Vector2(48, 64) || body.sprite.pivot != new Vector2(24, 22)) throw new Exception("Native body canvas or pivot changed");
            if (grip > .001f) throw new Exception("Hand/grip detached " + name);
            var frame = new Sample { Index = index++, Wall = wall, Sim = simulation }; buffered.Add(frame);
            CopyScreen(); pending++;
            AsyncGPUReadback.Request(crop, 0, TextureFormat.RGBA32, r => { if (r.hasError) error = true; else frame.Pixels = r.GetData<Color32>().ToArray(); pending--; });
            recording.AppendLine(string.Join(",", frame.Index, F(wall), name, F(simulation), Time.frameCount, F(Time.timeScale)));
            telemetry.AppendLine(string.Join(",", frame.Index, name, F(wall), F(simulation), Time.frameCount, F(Time.deltaTime), player.AttackAnimationSequence,
                player.ActiveComboAnimationStep, player.ActiveMeleeMotion, F(player.AttackAnimationProgress), a.PoseName, a.FrameIndex, a.FacingDirection,
                F(a.HandLocalPosition.x), F(a.HandLocalPosition.y), F(grip), F(held.DisplayedActionRotation), player.MeleeContactsResolved,
                player.IsLongswordCatchActive, player.LongswordCatchSucceeded, player.MovementInput.sqrMagnitude > .01f,
                F(player.transform.position.x), F(player.transform.position.y), AssetDatabase.GetAssetPath(body.sprite), AssetDatabase.GetAssetPath(held.RendererSprite)));
            clipFrames.Add(new JObject { ["file"] = "frames/frame-" + frame.Index.ToString("D6") + ".png", ["wall"] = wall, ["simulation"] = simulation });
            if (previous > 0) maxGap = Math.Max(maxGap, wall - previous); previous = wall;
        }
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        while (pending > 0) yield return null;
        var duration = clipFrames.Count > 1 ? (double)clipFrames[clipFrames.Count - 1]["wall"] - (double)clipFrames[0]["wall"] : 0;
        clips.Add(new JObject { ["name"] = name, ["direction"] = direction, ["moving"] = moving, ["frames"] = clipFrames,
            ["wallSeconds"] = duration, ["observedCaptureHz"] = duration > 0 ? (clipFrames.Count - 1) / duration : 0,
            ["maxCaptureGapSeconds"] = maxGap, ["frameCount"] = index - first, ["nativeBody"] = "48x64 pivot 24,22",
            ["maxHandGripDistance"] = maxGripError, ["contacts"] = player.MeleeContactsResolved - initialContacts,
            ["poses"] = new JArray(poses), ["suppliedFrontalHit"] = suppliedHit, ["sweepStepMask"] = swingMask });
        Require(!error, "readback complete " + name);
        Require(swingMask == 7 && suppliedHit && player.MeleeContactsResolved - initialContacts == 4, "three ordinary cuts and one catch-counter " + name);
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
        File.WriteAllText(folder + "/body-telemetry.csv", telemetry.ToString(), Utf8);
        File.WriteAllText(folder + "/warmup.csv", warmup.ToString(), Utf8);
        File.WriteAllText(folder + "/manifest.json", new JObject { ["scope"] = "Supplied native actor, real GameAction combo/counter, supplied frontal hit; neutral temporary scene. No enemy AI, campaign, performance, audio or production hit-stop claim.",
            ["playback"] = "1x actual wall timestamps; hold each captured frame until next; no interpolation or speedup.",
            ["originalViewport"] = "1280x720", ["crop"] = new JArray(384, 168, Width, Height), ["clips"] = clips }.ToString(), Utf8);
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
        Restore(() => { if (player != null) { player.ItemEffects.CancelTransient(); player.gameObject.SetActive(false); } });
        Restore(() => { if (screen != null) { screen.Release(); UnityEngine.Object.DestroyImmediate(screen); } if (crop != null) { crop.Release(); UnityEngine.Object.DestroyImmediate(crop); } });
        Restore(() => { if (scene.IsValid() && scene.isLoaded) foreach (var root in scene.GetRootGameObjects()) root.SetActive(false); });
        Restore(() => { if (audioOverridden) CombatAudio.Configure(oldCombatSettings); });
        Restore(() => { if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene); foreach (var pair in roots) if (pair.Key != null) pair.Key.SetActive(pair.Value); });
        Restore(() => { foreach (var pair in inputs) { var set = (HashSet<GameAction>)InputField(pair.Key).GetValue(null); set.Clear(); set.UnionWith(pair.Value); }
            typeof(StageValidationInput).GetProperty("AimDirection", All).SetValue(null, oldAim); InputField("enabled").SetValue(null, oldInputEnabled); InputField("initialized").SetValue(null, oldInputInitialized); });
        Restore(() => { Application.runInBackground = oldBackground; Application.targetFrameRate = oldRate; QualitySettings.vSyncCount = oldVsync; Time.captureFramerate = oldCapture; Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom; });
        Restore(() => { if (view != null) view.GetType().GetProperty("selectedSizeIndex", All).SetValue(view, oldSize); if (customSize >= 0 && sizes != null) sizes.GetType().GetMethod("RemoveCustomSize", All).Invoke(sizes, new object[] { customSize });
            if (sizes != null && (int)sizes.GetType().GetMethod("GetCustomCount", All).Invoke(sizes, null) != oldCustomCount) throw new Exception("Game View preset count changed"); if (madeView && view != null) view.Close(); if (oldFocus != null) oldFocus.Focus(); });
        Restore(() => { if (tickSaved) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTick, oldTickInterval, false }); });
        if (scene.IsValid() && scene.isLoaded) { SceneManager.sceneUnloaded += Unloaded; SceneManager.UnloadSceneAsync(scene); }
        else WriteResult();
    }
    static void Unloaded(Scene unloaded) { if (unloaded.handle != scene.handle) return; SceneManager.sceneUnloaded -= Unloaded; checks.AppendLine("PASS temporary scene unloaded; original world/input/view/time/AutoTick restored"); WriteResult(); }
    static void WriteResult() { File.WriteAllText(folder + "/result.txt", outcome + "\n" + checks + "\nActual capture frequency is in manifest.json; not a 60fps claim.\n", Utf8); Debug.Log("[Sword motion] " + outcome + " " + folder); }
    const string Viewer = @"<!doctype html><meta charset='utf-8'><title>장검 실제 프레임 · 1×</title><style>body{background:#181824;color:#eee;font:14px sans-serif}button{margin:3px;padding:6px}canvas{display:block;width:512px;height:384px;image-rendering:pixelated}pre{white-space:pre-wrap}</style><p>공급된 캐릭터 · 실제 입력 3타 및 반격 · 실제 캡처 시간 그대로 1× · 무보간 · 무음</p><div id='buttons'></div><button id='replay'>1× 다시 재생</button><pre id='info'></pre><canvas width='512' height='384'></canvas><script>let current,frames=[],start=0,token=0;const c=document.querySelector('canvas'),ctx=c.getContext('2d'),info=document.querySelector('#info');function play(){if(!current)return;const id=++token;start=performance.now();function draw(t){if(id!==token)return;let seconds=(t-start)/1000,i=0;while(i+1<frames.length&&current.frames[i+1].wall-current.frames[0].wall<=seconds)i++;ctx.drawImage(frames[i],0,0);if(seconds<current.wallSeconds)requestAnimationFrame(draw)}requestAnimationFrame(draw)}async function select(clip){token++;current=clip;info.textContent=clip.name+'\n'+clip.frameCount+' actual frames / '+clip.wallSeconds.toFixed(3)+'s / '+clip.observedCaptureHz.toFixed(1)+' Hz / max gap '+clip.maxCaptureGapSeconds.toFixed(3)+'s';frames=await Promise.all(clip.frames.map(f=>new Promise((resolve,reject)=>{const im=new Image;im.onload=()=>resolve(im);im.onerror=reject;im.src=f.file})));play()}fetch('manifest.json').then(r=>r.json()).then(m=>{for(const clip of m.clips){let b=document.createElement('button');b.textContent=clip.name;b.onclick=()=>select(clip);document.querySelector('#buttons').append(b)}if(m.clips.length)select(m.clips[0])});document.querySelector('#replay').onclick=play;</script>";
}
