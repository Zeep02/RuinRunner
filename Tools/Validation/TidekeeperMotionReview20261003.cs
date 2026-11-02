using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using RuinRunner;
using Newtonsoft.Json.Linq;

// Staged visual review on the real second-region map. Items/targets supplied for review, not a normal run.
public static class TidekeeperMotionReview20261003
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
    static EnemyController actor; static string actorName; static readonly HashSet<string> seen=new HashSet<string>();
    static readonly HashSet<string> observed=new HashSet<string>();
    static readonly HashSet<int> phases=new HashSet<int>();
    static void EnemyField(string field,object value)=>typeof(EnemyController).GetField(field,Private).SetValue(actor,value);
    static void Place(Vector2 position){var b=actor.GetComponent<Rigidbody2D>();b.position=position;b.linearVelocity=Vector2.zero;actor.transform.position=position;}
    static IEnumerator Settle(){yield return null;yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();}
    static void CountClip(string clip,int expected,string direction)
    {int actual=observed.Count(p=>p.Contains("/"+clip+"/"+direction+"/"));Require(actual==expected,"all authored "+clip+" poses "+direction+" "+actual+"/"+expected);}

    static void Track(string phase)
    {

        if(actor==null){telemetry.AppendLine(sample+","+phase+","+actorName+",destroyed,,,,,");return;}
        var body=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var path=AssetDatabase.GetAssetPath(body.sprite);seen.Add(path);observed.Add(path);phases.Add(actor.AttackVisualPhase);
        telemetry.AppendLine(string.Join(",",sample,phase,actor.Type,path,body.color.r,body.color.g,body.color.b,body.color.a,actor.Health));
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-03-tidekeeper/motion-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        poses.Clear(); checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; observed.Clear(); phases.Clear(); recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,actor,bodyAsset,red,green,blue,alpha,health");
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
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged real enemy TakeDamage and death sequences. Actors/contacts supplied. Not normal campaign/native keyboard/performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Tidekeeper Motion] PASS " + folder);
    }


    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        // Keep the old map valid until the HUD points to its replacement. A
        // frame between Destroy and reassignment exposes dead exits to OnGUI.
        var previousMap=game.DungeonMap;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,1);
        typeof(GameController).GetProperty("DungeonMap").SetValue(game,map);typeof(GameController).GetProperty("CurrentStageIndex").SetValue(game,1);
        UnityEngine.Object.Destroy(previousMap.Root);yield return null;
        center=map.GetRoomCenter(3);game.Player.SetStageClearSafety(true);game.Player.Teleport(center+Vector2.down*3);
        game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        int callbacks=0;actor=EnemyController.Create(EnemyType.Tidekeeper,center,game.Player,()=>game.IsPaused,_=>callbacks++,game.CameraFollow);actorName="Tidekeeper";
        EnemyField("specialTimer",10000f);EnemyField("speed",0f);
        var settle=Settle();while(settle.MoveNext())yield return settle.Current;
        var originalBox=actor.DamageCollider.bounds;
        var part=Record(1.4f,"idle",.5f);while(part.MoveNext())yield return part.Current;Flush();
        Require(observed.Count(p=>p.Contains("/idle/"))==4,"four unique native idle poses rendered");
        game.Player.Teleport(center+Vector2.down*8);EnemyField("speed",2f);
        settle=Settle();while(settle.MoveNext())yield return settle.Current;
        part=Record(.9f,"glide",.4f);while(part.MoveNext())yield return part.Current;Flush();
        Require(Vector2.Distance(actor.transform.position,center)>.6f,"native hovering body moves through actual AI velocity");
        EnemyField("speed",0f);Place(center);game.Player.Teleport(center+Vector2.down*3);
        settle=Settle();while(settle.MoveNext())yield return settle.Current;
        var body=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        Require(originalBox.Intersects(body.bounds),"visible floating body and damage envelope overlap");
        Require(originalBox.center.y>actor.transform.position.y+.8f,"damage envelope follows hovering body instead of empty space at feet");
        settle=Settle();while(settle.MoveNext())yield return settle.Current;
        actor.TakeDamage(1,Vector2.zero);part=Record(.4f,"recoil",.11f);while(part.MoveNext())yield return part.Current;Flush();
        Require(observed.Count(p=>p.Contains("/immune/"))==4,"four native partial-fold recoil poses rendered");
        Require(actor.DamageCollider.bounds==originalBox,"recoil keeps fixed body damage bounds");
        foreach(bool lanes in new[]{true,false})
        {
            EnemyField("specialTimer",10000f);observed.Clear();phases.Clear();
            settle=Settle();while(settle.MoveNext())yield return settle.Current;
            var action=(IEnumerator)typeof(EnemyController).GetMethod(lanes?"TideLanes":"CallTide",Private).Invoke(actor,lanes?null:new object[]{true});
            double start=Time.timeAsDouble;actor.StartCoroutine(action);
            part=Record(lanes?3.65f:3.05f,lanes?"lanes":"pools",lanes?1.57f:1.11f);while(part.MoveNext())yield return part.Current;
            Require(phases.Contains(0)&&phases.Contains(1)&&phases.Contains(2),"real warning active recovery phases "+lanes);
            Require(!actor.IsActing,"original action duration preserved "+lanes);
            Require(observed.Count(p=>p.Contains("/glow/"))==8,"all eight native glow source frames rendered "+lanes);
            Require(observed.Count(p=>p.Contains(lanes?"/shield_cast/":"/laser_cast/"))==(lanes?10:7),"complete distinct native cast vocabulary "+lanes);
            Require(actor.DamageCollider.bounds==originalBox,"attack extremities never resize damage bounds "+lanes);
            Flush();EnemyField("specialTimer",10000f);
        }
        // Hit during anticipation must not replace the dangerous cast by recoil.
        actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("TideLanes",Private).Invoke(actor,null));
        yield return new WaitForSeconds(.2f);actor.TakeDamage(1,Vector2.zero);yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
        Require(!AssetDatabase.GetAssetPath(body.sprite).Contains("/immune/"),"nonlethal hit preserves attack anticipation");
        Capture("anticipation-after-hit");actor.StopAllCoroutines();typeof(EnemyController).GetMethod("EndAction",Private).Invoke(actor,null);EnemyField("specialTimer",10000f);
        foreach(var hazard in UnityEngine.Object.FindObjectsByType<ArenaHazard>())UnityEngine.Object.Destroy(hazard.gameObject);
        settle=Settle();while(settle.MoveNext())yield return settle.Current;
        observed.Clear();actor.TakeDamage(100000,Vector2.zero);Require(actor.IsDead&&callbacks==0,"boss disables combat before delayed death settlement");
        Require(!actor.DamageCollider.enabled&&!actor.GetComponent<Rigidbody2D>().simulated,"dead boss explicitly disables body contact and physics simulation");
        actor.TakeDamage(100000,Vector2.zero);part=Record(1.2f,"collapse",.64f);while(part.MoveNext())yield return part.Current;Flush();
        Require(observed.Count(p=>p.Contains("/death/"))==14,"all fourteen authored collapse frames rendered including source hold");
        Require(actor==null&&callbacks==1,"normal boss collapse settles and removes actor exactly once");
        actor=EnemyController.Create(EnemyType.Tidekeeper,center,game.Player,()=>game.IsPaused,_=>callbacks++,game.CameraFollow);actor.TakeDamage(100000,Vector2.zero);
        yield return new WaitForSeconds(.12f);typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});yield return new WaitForEndOfFrame();
        body=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var frame=body.sprite;float hp=actor.Health;
        yield return new WaitForSecondsRealtime(.3f);
        Require(actor!=null&&body.sprite==frame&&actor.Health==hp&&callbacks==1,"pause freezes native collapse health drain and reward callback");Capture("paused-collapse");
        game.ResumeGame();yield return new WaitForSeconds(1.15f);Require(actor==null&&callbacks==2,"resume finishes native collapse once");
        game.ReturnToMainMenu();game.enabled=true;yield return null;
        Require(UnityEngine.Object.FindObjectsByType<EnemyController>().Length==0,"menu clears reviewed native bosses");
        Require(UnityEngine.Object.FindObjectsByType<ArenaHazard>().Length==0,"menu clears boss hazards");
    }
}
