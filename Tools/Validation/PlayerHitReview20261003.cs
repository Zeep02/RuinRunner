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
using Newtonsoft.Json.Linq;

// Staged visual review on the real first-region map. Items/targets supplied for review, not a normal run.
public static class PlayerHitReview20261003
{
    static readonly JArray poses=new JArray(); static string folder; static GameController game; static Vector2 center;
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
    static void Check(bool okay,string label){if(!okay)throw new Exception(label);}
    static void Require(bool okay,string label){Check(okay,label);checks.Add("PASS "+label);}
    static void Field(string key,object value)=>typeof(PlayerController).GetField(key,Private).SetValue(game.Player,value);
    static T Field<T>(string key)=>(T)typeof(PlayerController).GetField(key,Private).GetValue(game.Player);
    static readonly HashSet<int> contactFrames=new HashSet<int>();
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var pivot=p.transform.Find("HeldWeaponPivot");Check(Vector2.Distance(pivot.localPosition,a.HandLocalPosition)<.001f,"unchanged exact hand/weapon attachment");
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())
        {
            if(fx.ClipId!="PlayerHit"&&fx.ClipId!="ItemShield")continue;
            Check(fx.GetComponent<SpriteRenderer>().sortingOrder<body.sortingOrder,"hit/shield remains behind live face");
            if(fx.ClipId=="PlayerHit")contactFrames.Add(fx.FrameIndex);
        }
        var camera=Camera.main;var origin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));
        var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,a.PoseName,a.FacingDirection,a.FrameIndex,AssetDatabase.GetAssetPath(body.sprite),origin.x,origin.y,across.x-origin.x,origin.y-down.y,body.color.r,body.color.g,body.color.b,body.color.a,p.Health,p.InvincibilityTicksRemaining));
    }
    static void ClearEffects()
    {
        CombatAudio.StopCombat();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())UnityEngine.Object.Destroy(fx.gameObject);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<FadingSpark>())UnityEngine.Object.Destroy(fx.gameObject);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemyController>()){e.gameObject.SetActive(false);UnityEngine.Object.Destroy(e.gameObject);}
        foreach(var p in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())UnityEngine.Object.Destroy(p.gameObject);
    }
    static void NewCase(string weapon,int protection)
    {
        ClearEffects();var previous=game.Player;previous.gameObject.SetActive(false);UnityEngine.Object.Destroy(previous.gameObject);
        var bag=new InventorySystem();bag.AddWeapon(WeaponLootTable.Find(weapon));
        if(protection==1){bag.AddAccessory(AccessoryLootTable.Find("guardian_seal"));bag.AddAccessory(AccessoryLootTable.Find("bulwark_brooch"));}
        if(protection==2)bag.AddAccessory(AccessoryLootTable.Find("last_light_pendant"));
        var player=PlayerController.Create(center,()=>game.IsPaused,()=>false,()=>{},game.CameraFollow,bag,game.Settings);player.EquipWeapon(bag.EquippedWeapon);
        typeof(GameController).GetProperty("Inventory").SetValue(game,bag);typeof(GameController).GetProperty("Player").SetValue(game,player);game.CameraFollow.Target=player.transform;
        StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();StageValidationInput.SetAim(Vector2.right);contactFrames.Clear();
    }
    static int HitCount(){int n=0;foreach(var f in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(f.ClipId=="PlayerHit")n++;return n;}
    static bool Playing(string id){var clip=CombatAudio.Clip(id);foreach(var s in UnityEngine.Object.FindObjectsByType<AudioSource>())if(s.isPlaying&&s.clip==clip)return true;return false;}
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-03-player-hit/motion-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        poses.Clear(); checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,pose,direction,key,bodyAsset,originSX,originSY,scaleX,scaleY,r,g,b,a,health,immunity");
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
        double start = Time.timeAsDouble; bool taken = false;
        var target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32); target.Create();
        pending = 0; readbackError = false;
        // Duration follows the same simulation clock as ammo. Each capture still
        // retains its actual wall timestamp; render/tool stalls are not cut out.
        while (Time.timeAsDouble - start < seconds)
        {
            if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive){var focus=autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor",All);focus.Invoke(null,null);view.Focus();}view.Repaint();EditorApplication.QueuePlayerLoopUpdate();if(!game.IsPaused)game.Inventory.Tick(Time.deltaTime);yield return new WaitForEndOfFrame(); double time = Time.realtimeSinceStartupAsDouble; Track(phase);
            string name = null;
            if (!taken && Time.timeAsDouble - start >= showcase) { taken = true; name = phase + "-active"; }
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
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged real TakeDamage/shield/guard/pause API and normal attack/dash input fixture. Equipment/health/contact supplied. Not normal campaign/native keyboard/performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Player Hit] PASS " + folder);
    }


    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        for(int protection=0;protection<3;protection++)for(int direction=0;direction<8;direction++)
        {
            NewCase(direction%2==0?"rust_sword":"storm_rifle",protection);
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();
            if(protection==1)game.Player.ItemEffects.OnDefenseSuccess(aim);
            if(protection==2)game.Player.Stats.ReceiveDamage(game.Player.MaxHealth*.75f);
            CombatAudio.StopCombat();float hp=game.Player.Health;var start=game.Player.transform.position;
            game.Player.TakeDamage(4,-aim);var ink=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().color;
            Require(game.Player.InvincibilityTicksRemaining==PlayerController.HitInvincibilityTicks,"existing 1-second immunity starts "+protection+"/"+direction);
            if(protection==0)
            {
                Require(game.Player.Health<hp&&Field<float>("hurtPoseTimer")>0&&Field<Vector2>("knockbackVelocity").magnitude>4.9f,"unshielded damage/timer/knockback retained");
                Require(ink.r>ink.b&&Playing("hurt")&&!Playing("block")&&HitCount()==1,"real health loss has red tint/hurt sound/one native contact");
            }
            else
            {
                Require(game.Player.Health==hp&&Field<float>("hurtPoseTimer")==0&&Field<Vector2>("knockbackVelocity")==Vector2.zero,"full shield has no HP loss/flinch/shove "+protection);
                Require(ink.b>ink.r&&Playing("block")&&!Playing("hurt")&&HitCount()==0,"full shield has cool tint/block sound/no injury contact "+protection);
            }
            float after=game.Player.Health;game.Player.TakeDamage(30,aim);Require(game.Player.Health==after,"same-frame repeated contact ignored");
            Require(UnityEngine.Object.FindObjectsByType<FadingSpark>().Length==0,"no square injury particle burst");
            var record=Record(.42f,"response-"+protection+"-direction-"+direction,.035f);while(record.MoveNext()){yield return record.Current;StageValidationInput.BeginFrame();}
            if(protection==0)Require(contactFrames.Count==5,"all five actual contact poses observed");
            else Require(Vector3.Distance(start,game.Player.transform.position)<.001f,"full shield preserves position");
            Require(HitCount()==0,"short contact effect completes without residue");Flush();
            poses.Add(new JObject{{"protection",protection},{"direction",direction},{"healthBefore",hp},{"healthAfter",game.Player.Health},{"contactFrames",contactFrames.Count}});
        }
        foreach(int protection in new[]{1,2})
        {
            NewCase("storm_rifle",protection);yield return new WaitForSeconds(.1f);
            if(protection==1)game.Player.ItemEffects.OnDefenseSuccess(Vector2.right);else game.Player.Stats.ReceiveDamage(game.Player.MaxHealth*.75f);
            var hp=game.Player.Health;float absorbed=protection==1?8:10;float raw=absorbed+2;
            float expected=Mathf.Max(1,(raw-absorbed)*100/(100+game.Player.Stats.Armor));CombatAudio.StopCombat();
            game.Player.TakeDamage(raw,Vector2.left);
            Require(Mathf.Abs(hp-game.Player.Health-expected)<.001f&&HitCount()==1&&Playing("hurt"),"partial shield retains only remaining armored damage and injury feedback "+protection);
            yield return new WaitForSeconds(.4f);
        }
        NewCase("rust_sword",0);yield return new WaitForSeconds(.1f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;yield return new WaitForEndOfFrame();
        Require(game.Player.IsGuarding,"normal guard input enters guard");CombatAudio.StopCombat();float beforeGuard=game.Player.Health;
        game.Player.TakeDamage(10,Vector2.left);var guardColor=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().color;
        Require(game.Player.Health<beforeGuard&&Field<float>("hurtPoseTimer")==0&&Field<Vector2>("knockbackVelocity").magnitude<1.41f,"guard damage/reduced shove retained");
        Require(guardColor.r>guardColor.b&&guardColor.g>.8f&&Playing("block")&&!Playing("hurt")&&HitCount()==0,"guard has amber/block feedback");
        var part=Record(.35f,"guard",.025f);while(part.MoveNext()){yield return part.Current;StageValidationInput.BeginFrame();}Flush();StageValidationInput.ReleaseAll();
        NewCase("storm_rifle",0);yield return new WaitForSeconds(.1f);CombatAudio.StopCombat();float hp0=game.Player.Health;
        game.Player.TakeDamage(0,Vector2.left);game.Player.TakeDamage(-10,Vector2.left);
        Require(game.Player.Health==hp0&&game.Player.InvincibilityTicksRemaining==0&&HitCount()==0&&!Playing("hurt")&&!Playing("block"),"nonpositive contacts produce no fake hurt or immunity");
        StageValidationInput.SetHeld(GameAction.Attack,true);yield return new WaitForSeconds(.05f);int shot=game.Player.AttackAnimationSequence;
        game.Player.TakeDamage(2,Vector2.left);StageValidationInput.SetHeld(GameAction.MoveDown,true);
        part=Record(.35f,"shoot-through-hit",.035f);while(part.MoveNext()){yield return part.Current;StageValidationInput.BeginFrame();}Flush();
        Require(game.Player.AttackAnimationSequence>shot,"damage does not add an attack lock");StageValidationInput.ReleaseAll();
        NewCase("rust_sword",0);yield return new WaitForSeconds(.1f);game.Player.TakeDamage(2,Vector2.left);yield return new WaitForSeconds(.025f);
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});yield return new WaitForEndOfFrame();
        var body=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var color0=body.color;var position0=game.Player.transform.position;int immune0=game.Player.InvincibilityTicksRemaining;
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
        Require(body.color==color0&&game.Player.transform.position==position0&&game.Player.InvincibilityTicksRemaining==immune0,"pause freezes impact/tint/position/immunity");
        game.ResumeGame();StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,true);StageValidationInput.SetHeld(GameAction.Dash,false);
        bool dash=false;part=Record(.4f,"dash-after-hit",.08f);while(part.MoveNext()){yield return part.Current;dash|=game.Player.IsDashing;StageValidationInput.BeginFrame();}Flush();
        Require(dash,"dash responds immediately after damage/pause");
        Field("invincibilityTicks",0);game.Player.TakeDamage(10000,Vector2.left);Require(game.Player.IsDead,"lethal damage still enters death");
        yield return new WaitForSeconds(.85f);Require(game.Player.DeathProgress>=.99f&&!game.Player.GetComponent<HeldWeaponView>().IsVisible,"death clears weapon and settles actual native death sequence");
        File.WriteAllText(folder+"/cases.json",poses.ToString(),new UTF8Encoding(false));
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;Time.timeScale=1;yield return null;yield return null;
        Require(HitCount()==0,"menu clears player contact effects");
    }
}
