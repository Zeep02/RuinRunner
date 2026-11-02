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
public static class WeaponStandingDefense20261001
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
    static readonly HashSet<int> fxKeys=new HashSet<int>();static bool sawContactPeak,sawCut;static int observedContacts;
    static void Fresh(){ResetObservations();fxKeys.Clear();sawContactPeak=sawCut=false;observedContacts=game.Player.MeleeContactsResolved;}
    static NativeCombatEffect Cut(){foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.FollowsMeleeAction)return fx;return null;}
    static float nativeHandJump;static Vector2 priorNativeHand;static bool hadNativeHand;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var fx=Cut();
        if(fx!=null)
        {
            var v=fx.GetComponent<SpriteRenderer>();bool charged=p.IsChargedCleaveActive;
            if(v.sortingOrder>=body.sortingOrder)throw new Exception("cleave hides source head/hand "+phase);
            if(v.sprite.rect.size!=new Vector2(128,128)||v.sprite.pixelsPerUnit!=32||v.sprite.pivot!=new Vector2(64,64))throw new Exception("source cleave geometry "+phase);
            if(fx.ClipId!="GreatswordCut"||!v.flipY)throw new Exception("wrong source silhouette/orientation "+phase);
            if(Vector2.Distance(fx.transform.position,held.BladePoint(0))>.002f)throw new Exception("moving cut/grip anchor drift "+phase);
            if(v.enabled){sawCut=true;fxKeys.Add(fx.FrameIndex);}
            if(p.MeleeContactsResolved>observedContacts)
            {
                if(fx.FrameIndex!=4)throw new Exception("first actual damage contact misses native bright peak "+phase+" key="+fx.FrameIndex+" p="+p.AttackAnimationProgress);
                sawContactPeak=true;
            }
        }
        observedContacts=p.MeleeContactsResolved;
        if(p.IsMeleeSwingActive){phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);}
        var pivot=p.transform.Find("HeldWeaponPivot");var animation=p.GetComponent<PlayerAnimationController>();
        if(Vector2.Distance(pivot.localPosition,animation.HandLocalPosition)>.001f)throw new Exception("Native charged hand detached "+phase);
        if(hadNativeHand)nativeHandJump=Mathf.Max(nativeHandJump,Vector2.Distance(animation.HandLocalPosition,priorNativeHand)*32);
        priorNativeHand=animation.HandLocalPosition;hadNativeHand=true;
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture),fx==null?"":fx.ClipId,fx==null?-1:fx.FrameIndex,UnityEditorInternal.InternalEditorUtility.isApplicationActive,Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture)));
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-weapon-carry/defense-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
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
        Debug.Log("[Greatsword FX presentation] PASS " + folder);
    }
    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.11f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 composited screen");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var direction in new[]{0,1,2,3,4,5,6,7})foreach(var mode in new[]{0,1,2})
        {
            Equip("iron_greatsword");yield return new WaitForSeconds(.9f);var p=game.Player;
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            var enemy=Enemy(aim*1.3f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            Fresh();nativeHandJump=0;hadNativeHand=false;int sequence=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;bool started=false,stopped=false;float hold=mode==2?1.32f:.23f;
            var phase=(mode==0?"normal":mode==1?"charge-partial":"charge-full")+"-view-"+direction;
            var motion=Motion(mode==0?1.5f:hold+.68f,phase,time=>
            {
                if(time>.025f&&!started){started=true;StageValidationInput.SetHeld(mode==0?GameAction.Attack:GameAction.Guard,true);}
                if(started&&!stopped&&(mode==0?p.AttackAnimationSequence>=sequence+2:time>hold&&p.IsGreatswordCharging))
                {stopped=true;StageValidationInput.SetHeld(mode==0?GameAction.Attack:GameAction.Guard,false);}
            });
            while(motion.MoveNext())yield return motion.Current;
            Require(nativeHandJump<=6.5f,"actual standing/charge/release/return hand continuity "+phase+" maxNativePixels="+nativeHandJump);
            Require(stopped&&p.AttackAnimationSequence==sequence+(mode==0?2:1),"real input reaches only requested combo/release "+phase);
            Require(sawCut&&fxKeys.Count>=5,"distinct native trajectory and breakup "+phase+" observed="+fxKeys.Count);
            Require(sawContactPeak,"source bright pose coincides with actual .42 contact "+phase);
            Require(p.MeleeContactsResolved==contacts+(mode==0?2:1)&&enemy.Health<hp,"original contact count and actual target damage "+phase);
            Require(Cut()==null&&!p.IsMeleeSwingActive,"action and bound trail recover together "+phase);StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var id in new[]{"iron_greatsword","dragon_slayer","blood_reaver"})
        {
            Equip(id);yield return new WaitForSeconds(.9f);StageValidationInput.SetAim(Vector2.right);Fresh();bool started=false;
            var motion=Motion(1.65f,id+"-mobile-combo",t=>{if(!started){started=true;StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.MoveRight,true);}if(t>.72f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>1.4f)StageValidationInput.ReleaseAll();});
            while(motion.MoveNext())yield return motion.Current;
            Require(sawCut&&traveled>3&&phaseSprites.Count>=8,"independent native gait with moving source grip anchor "+id);StageValidationInput.ReleaseAll();Flush();
        }
        Equip("iron_greatsword");yield return new WaitForSeconds(.9f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.25f);StageValidationInput.SetHeld(GameAction.Guard,false);
        while(!game.Player.IsChargedCleaveActive)yield return null;
        yield return new WaitForEndOfFrame();var cut=Cut();Require(cut!=null,"pause scenario has bound charged source");
        var savedKey=cut.FrameIndex;var savedPosition=cut.transform.position;var savedProgress=game.Player.AttackAnimationProgress;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.16f);yield return new WaitForEndOfFrame();Require(cut.FrameIndex==savedKey&&cut.transform.position==savedPosition&&game.Player.AttackAnimationProgress==savedProgress,"pause freezes source trajectory/body/contact clock");Time.timeScale=1;
        int before=game.Player.MeleeContactsResolved;Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
        Require(Cut()==null&&!game.Player.IsChargedCleaveActive,"real dash cancels prepared cut immediately");yield return new WaitForSeconds(.35f);Require(game.Player.MeleeContactsResolved==before,"cancelled source creates no late damage");
        Equip("iron_greatsword");yield return new WaitForSeconds(.9f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.25f);StageValidationInput.SetHeld(GameAction.Guard,false);yield return null;StageValidationInput.BeginFrame();
        game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));yield return new WaitForEndOfFrame();yield return null;
        Require(Cut()==null&&!game.Player.IsMeleeSwingActive,"swap removes bound cut without altering firearm stance");StageValidationInput.ReleaseAll();
        Equip("iron_greatsword");yield return new WaitForSeconds(.9f);StageValidationInput.SetHeld(GameAction.Guard,true);
        var chargeDeadline=Time.realtimeSinceStartupAsDouble+3;
        while(game.Player.GreatswordChargeProgress<.98f)
        {if(Time.realtimeSinceStartupAsDouble>chargeDeadline)throw new Exception("full-charge input fixture timeout");yield return null;StageValidationInput.BeginFrame();}
        yield return new WaitForEndOfFrame();var preparation=game.Player.GetComponent<WeaponPreparationEffect>();
        var readyField=typeof(WeaponPreparationEffect).GetField("readyFlash",All);var ready=(NativeCombatEffect)readyField.GetValue(preparation);
        Require(ready!=null&&ready.IsBelowActor,"completed-charge glint follows the blade below the native face");
        before=game.Player.MeleeContactsResolved;Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
        Require(!game.Player.IsGreatswordCharging&&readyField.GetValue(preparation)==null&&Cut()==null,"full-charge dash clears glow and completion glint together");
        yield return new WaitForSeconds(.4f);Require(game.Player.MeleeContactsResolved==before,"cancelled full charge creates no late contact");StageValidationInput.ReleaseAll();
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0,"menu removes all source trails and owned player");
    }
}
