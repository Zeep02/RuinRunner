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
public static class CoreFloorMotionReview20261003
{
    static readonly JArray poses=new JArray(); static string folder; static GameController game;
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
    static EnemyController actor; static readonly HashSet<string> seen=new HashSet<string>();
    static readonly HashSet<string> observed=new HashSet<string>();
    static readonly HashSet<int> phases=new HashSet<int>();
    static void EnemyField(string field,object value)=>typeof(EnemyController).GetField(field,Private).SetValue(actor,value);
    static void Place(Vector2 position){var b=actor.GetComponent<Rigidbody2D>();b.position=position;b.linearVelocity=Vector2.zero;actor.transform.position=position;}
    static IEnumerator Settle(){yield return null;yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();}
    static void CountClip(string clip,int expected,string direction)
    {int actual=observed.Count(p=>p.Contains("/"+clip+"/"+direction+"/"));Require(actual==expected,"all authored "+clip+" poses "+direction+" "+actual+"/"+expected);}

    static List<Vector2> route; static int waypoint; static double travel;
    static Vector2 lastPosition;
    static void Track(string phase)
    {
        Vector2 position=game.Player.transform.position;travel+=Vector2.Distance(position,lastPosition);lastPosition=position;
        telemetry.AppendLine(string.Join(",",sample,phase,position.x,position.y,game.CameraFollow.transform.position.x,game.CameraFollow.transform.position.y));
        StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
        if(route==null)return;
        while(waypoint<route.Count && Vector2.Distance(position,route[waypoint])<.3f)waypoint++;
        if(waypoint>=route.Count)return;
        var delta=route[waypoint]-position;StageValidationInput.SetAim(delta);
        if(Mathf.Abs(delta.x)>.14f)StageValidationInput.SetHeld(delta.x>0?GameAction.MoveRight:GameAction.MoveLeft,true);
        if(Mathf.Abs(delta.y)>.14f)StageValidationInput.SetHeld(delta.y>0?GameAction.MoveUp:GameAction.MoveDown,true);
        if(phase=="guardian-floor")StageValidationInput.SetHeld(GameAction.Attack,true);
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-03-core-floor/motion-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        poses.Clear(); checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; observed.Clear(); phases.Clear(); recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,playerX,playerY,cameraX,cameraY");
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
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged actual core-floor motion on production terrain. Stage, actor, start positions and safety supplied; movement/attack use GameAction. Not normal campaign/native keyboard/performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Core Floor Motion] PASS " + folder);
    }



    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        var previous=game.DungeonMap;UnityEngine.Random.InitState(9100);
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,2);
        typeof(GameController).GetProperty("DungeonMap").SetValue(game,map);typeof(GameController).GetProperty("CurrentStageIndex").SetValue(game,2);
        UnityEngine.Object.Destroy(previous.Root);yield return null;
        // Staged post-clear traversal: production exits otherwise remain sealed.
        // This review is not evidence of earning those encounter clears.
        for(int room=0;room<map.RoomCount;room++)map.UnlockExit(room);
        game.Player.SetStageClearSafety(true);game.CameraFollow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        var starts=new[]{map.GetRoomCenter(0)+new Vector2(-6,-3),map.Terrain.Connections[0].points[0],map.GetRoomCenter(3)+new Vector2(-7,-1)};
        var goals=new[]{map.GetRoomCenter(0)+new Vector2(6,-3),map.Terrain.Connections[0].points.Last(),map.GetRoomCenter(3)+new Vector2(7,-1)};
        var labels=new[]{"worn-court","wall-connection","guardian-floor"};
        var report=new JArray();
        for(int index=0;index<starts.Length;index++)
        {
            Require(StageNavigation.IsClear(map.Terrain,starts[index]),"reachable start "+labels[index]);
            route=StageNavigation.FindPath(map.Terrain,starts[index],goals[index]);waypoint=0;
            Require(route!=null&&route.Count>1,"reachable route "+labels[index]);
            game.Player.Teleport(starts[index]);game.CameraFollow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
            lastPosition=starts[index];travel=0;
            if(index==2)
            {
                var room=map.GetRoom(3);var c=map.GetRoomCenter(3);
                actor=EnemyController.Create(EnemyType.SealHeart,c+Vector2.up*3,game.Player,()=>false,_=>{},game.CameraFollow);
                actor.ConfigureArena(new Rect(c-room.Size*.5f,room.Size));actor.ConfigureNavigation(map.Terrain);
            }
            yield return null;yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
            Capture(labels[index]+"-start");
            var record=Record(4f,labels[index],1.7f);while(record.MoveNext())yield return record.Current;
            StageValidationInput.ReleaseAll();route=null;
            Require(travel>7,"actual GameAction travel >7 world units "+labels[index]);
            yield return new WaitForEndOfFrame();Capture(labels[index]+"-end");report.Add(new JObject{{"phase",labels[index]},{"distance",travel},{"waypoints",waypoint}});
            Flush();
        }
        var ground=map.Root.GetComponentsInChildren<SpriteRenderer>().Where(v=>v.name=="Continuous ground").ToArray();
        var paving=map.Root.GetComponentInChildren<NativeCoreGround>();
        Require(ground.All(v=>v.sharedMaterial==NativeCoreGround.Material),"all core ground uses shared palette material");
        Require(paving!=null&&paving.StoneCount>0&&paving.MeshCount<=16&&paving.GetComponentsInChildren<MeshRenderer>().All(v=>v.sharedMaterial==NativeCoreGround.Material&&v.transform.lossyScale==Vector3.one),"paving batched at native scale with shared material");
        Require(paving.VariantMask==65535,"all sixteen authored paving variants present");
        File.WriteAllText(folder+"/motion.json",new JObject{{"seed",9100},{"scope","staged actual Game View; supplied stage, starts, boss, safety"},{"phases",report},{"ground",ground.Length},{"paving",paving.StoneCount},{"pavingMeshes",paving.MeshCount}}.ToString(),new UTF8Encoding(false));
        game.ReturnToMainMenu();game.enabled=true;yield return null;
        Require(UnityEngine.Object.FindObjectsByType<NativeCoreGround>().Length==0,"menu removes all map paving");
    }
}
