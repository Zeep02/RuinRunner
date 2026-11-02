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
public static class ThrustCancel20261001
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
    static readonly StringBuilder telemetry=new StringBuilder();
    static readonly HashSet<Sprite> phaseSprites=new HashSet<Sprite>();
    static bool sawMelee,sawMobileMelee;
    static readonly HashSet<string> phasePoses=new HashSet<string>();
    static float traveled;static Vector2 previousPosition;
    static void ResetObservations(){sawMelee=sawMobileMelee=false;phaseSprites.Clear();phasePoses.Clear();traveled=0;previousPosition=game.Player.transform.position;}
    static readonly HashSet<int> fxKeys=new HashSet<int>();static bool sawPeak,sawEarly;static int observedContacts;
    static void Fresh(){ResetObservations();fxKeys.Clear();sawPeak=sawEarly=false;observedContacts=game.Player.MeleeContactsResolved;}
    static MeleeThrustEffect Thrust(){foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())if(fx.FollowsThrustAction)return fx;return null;}
    static void Track(string phase)
    {
        var p=game.Player;var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var a=p.GetComponent<PlayerAnimationController>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var fx=Thrust();string clip="";int key=-1;
        if(fx!=null)
        {
            var v=fx.GetComponent<SpriteRenderer>();var family=p.EquippedWeapon.Family;clip=family==WeaponFamily.Dagger?"DaggerStab":"LongswordStab";key=fx.FrameIndex;
            if(!AssetDatabase.GetAssetPath(v.sprite).Contains("External/CombatFX/"+clip+"/"))throw new Exception("Non-native physical thrust cell");
            if(v.sortingOrder>=body.sortingOrder)throw new Exception("Thrust hides native body");
            if(Vector2.Distance(fx.transform.position,held.BladePoint(MeleeThrustEffect.OriginDistance(family)))>.002f)throw new Exception("Thrust detached from displayed blade");
            if(Mathf.Abs(fx.transform.lossyScale.x-fx.transform.lossyScale.y)>.0001f)throw new Exception("Nonuniform thrust stretch");
            if(v.enabled&&p.AttackAnimationProgress>=.16f)fxKeys.Add(key);
            if(v.enabled&&p.AttackAnimationProgress<MeleeMotionTiming.ThrustContact)sawEarly=true;
            if(p.MeleeContactsResolved>observedContacts){if(!v.enabled||key!=(family==WeaponFamily.Dagger?1:2))throw new Exception("Native bright key not on actual damage contact");sawPeak=true;observedContacts=p.MeleeContactsResolved;}
        }
        else observedContacts=p.MeleeContactsResolved;
        phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);sawMelee|=p.IsThrustActive;sawMobileMelee|=p.IsThrustActive&&p.MovementInput.sqrMagnitude>.01f;
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture),clip,key,UnityEditorInternal.InternalEditorUtility.isApplicationActive,Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture)));
    }
    static void Require(bool okay, string label) { if (!okay) throw new Exception(label); checks.Add("PASS " + label); }
    static void ClearEffects()
    {
        game.Player.ItemEffects.CancelTransient();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())UnityEngine.Object.Destroy(fx.gameObject);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<WeaponSecondaryParticles>())UnityEngine.Object.Destroy(fx.gameObject);
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
        typeof(EnemyController).GetProperty("MaxHealth").SetValue(enemy, 3000f); typeof(EnemyController).GetProperty("Health").SetValue(enemy, 3000f);
        return enemy;
    }
    static void Pulse(GameAction action)
    { StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll(); StageValidationInput.SetHeld(action, true); StageValidationInput.SetHeld(action, false); }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-thrust-fx/cancel-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts,angle,bodyAsset,originSX,originSY,scaleX,scaleY,fxClip,fxKey,editorForeground,gameFocused,delta");
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
            if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive){var focus=autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor",All);focus.Invoke(null,null);view.Focus();}view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble; Track(phase);
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
            if(f.Pixels==null)continue;var texture = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false); texture.SetPixels32(f.Pixels); texture.Apply();
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
        buffered.Clear(); File.WriteAllText(folder + "/recording.csv", recording.ToString(), new UTF8Encoding(false));File.WriteAllText(folder+"/body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            object value = null; bool next;
            try { next = run.MoveNext(); if (next) value = run.Current; }
            catch (Exception error) { File.WriteAllText(folder+"/partial-recording.csv",recording.ToString(),new UTF8Encoding(false));File.WriteAllText(folder+"/partial-body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));try{Flush();}catch{} RestoreView(); Time.timeScale = 1; File.WriteAllText(folder + "/result.txt", "FAIL " + error + "\n" + string.Join("\n", checks)); Debug.LogException(error); yield break; }
            if (!next) break; yield return value;
        }
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged supplied-weapon/target motion review; not normal campaign, Windows build or 60fps performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Bound thrust presentation] PASS " + folder);
    }
    static IEnumerator Ready()
    {
        var deadline=Time.realtimeSinceStartupAsDouble+5;
        while((float)typeof(PlayerController).GetField("attackTimer",Private).GetValue(game.Player)>0)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("readiness timeout");yield return null;StageValidationInput.BeginFrame();}
    }
    static IEnumerator Prepare(int mode)
    {
        var ready=Ready();while(ready.MoveNext())yield return ready.Current;
        for(int i=0;i<mode;i++)
        {Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();ready=Ready();while(ready.MoveNext())yield return ready.Current;}
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        foreach(var death in new[]{false,true})
        {
            Equip("rust_sword");yield return null;var ready=Prepare(2);while(ready.MoveNext())yield return ready.Current;
            var p=game.Player;int contacts=p.MeleeContactsResolved;Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
            Require(Thrust()!=null&&p.AttackAnimationProgress<MeleeMotionTiming.ThrustContact,"live native thrust before "+(death?"death":"guard"));
            Capture(death?"death-before":"guard-before");
            if(death)p.TakeDamage(99999,-p.AimDirection);else StageValidationInput.SetHeld(GameAction.Guard,true);
            yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
            if(death)
            {
                Require(Thrust()==null&&p.IsDead,"actual death removes bound source immediately");yield return new WaitForEndOfFrame();Capture("death-after");
                yield return new WaitForSecondsRealtime(.35f);Require(p.MeleeContactsResolved==contacts,"no cancelled stab contact after death");
            }
            else
            {
                Require(Thrust()!=null&&!p.IsGuarding,"early guard input preserves the already committed stab until existing recovery timing");
                yield return new WaitForSeconds(.4f);
                Require(Thrust()==null&&p.IsGuarding&&p.MeleeContactsResolved==contacts+1,"existing one-contact stab recovers into held guard with no residual source");yield return new WaitForEndOfFrame();Capture("guard-after");
            }
            StageValidationInput.ReleaseAll();
        }
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>().Length==0,"final native thrust cleanup");
    }
}
