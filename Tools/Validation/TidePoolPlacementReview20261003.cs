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
public static class TidePoolPlacementReview20261003
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

    static ArenaHazard[] locked; static Vector2[] origins; static int activeSamples;
    static readonly HashSet<string> effectFrames = new HashSet<string>();
    static void Track(string phase)
    {
        if(locked!=null)for(int i=0;i<locked.Length;i++)if(locked[i]!=null)
        {
            Check((Vector2)locked[i].transform.position==origins[i],"Warning position followed moving target "+phase);
            if(locked[i].IsActive){activeSamples++;Check(locked[i].Contains(origins[i]),"Active center missing from damage shape");}
        }
        foreach (var hazard in UnityEngine.Object.FindObjectsByType<ArenaHazard>())
            foreach(var renderer in hazard.GetComponentsInChildren<SpriteRenderer>())
                if(renderer.enabled && renderer.name.StartsWith("HazardFX_"))
                    effectFrames.Add(AssetDatabase.GetAssetPath(renderer.sprite));

        if(actor==null){telemetry.AppendLine(sample+","+phase+","+actorName+",destroyed,,,,,");return;}
        var body=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var path=AssetDatabase.GetAssetPath(body.sprite);seen.Add(path);observed.Add(path);phases.Add(actor.AttackVisualPhase);
        telemetry.AppendLine(string.Join(",",sample,phase,actor.Type,path,body.color.r,body.color.g,body.color.b,body.color.a,actor.Health));
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-03-tide-pool-placement/motion-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
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
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged actual Tidekeeper pool placement on production arena. Actors/start positions/protection supplied; movement uses GameAction. Not normal campaign/native keyboard/performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Tide Pool Placement] PASS " + folder);
    }



    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
        var previous=game.DungeonMap;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,1);
        typeof(GameController).GetProperty("DungeonMap").SetValue(game,map);typeof(GameController).GetProperty("CurrentStageIndex").SetValue(game,1);
        UnityEngine.Object.Destroy(previous.Root);yield return null;
        center=map.GetRoomCenter(3);var size=map.GetRoom(3).Size;var arena=new Rect(center-size*.5f,size);
        game.Player.SetStageClearSafety(true);game.Player.Teleport(center);
        game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        actor=EnemyController.Create(EnemyType.Tidekeeper,center+Vector2.up*3,game.Player,()=>game.IsPaused,_=>{},game.CameraFollow);
        actor.ConfigureArena(arena);actor.ConfigureNavigation(map.Terrain);actorName=actor.Type.ToString();
        EnemyField("specialTimer",10000f);EnemyField("speed",0f);Physics2D.SyncTransforms();
        var targets=new[]{Vector2.zero,new Vector2(-12,0),new Vector2(12,0),new Vector2(0,10),new Vector2(0,-10),new Vector2(-9,8),new Vector2(9,8),new Vector2(-9,-8),new Vector2(9,-8)};
        var labels=new[]{"center","west-wall","east-wall","north-wall","south-wall","northwest-corner","northeast-corner","southwest-corner","southeast-corner"};
        effectFrames.Clear();var placement=new JArray();
        for(int index=0;index<targets.Length;index++)
        {
            var target=center+targets[index];string phase=labels[index];
            Require(StageNavigation.IsClear(map.Terrain,target),"supplied target is reachable "+phase);
            actor.StopAllCoroutines();typeof(EnemyController).GetMethod("EndAction",Private).Invoke(actor,null);EnemyField("specialTimer",10000f);
            StageValidationInput.ReleaseAll();game.Player.Teleport(target);game.CameraFollow.FocusRoom(target,new Vector2(20,14),true);
            var settle=Settle();while(settle.MoveNext())yield return settle.Current;
            actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod("CallTide",Private).Invoke(actor,new object[]{true}));
            locked=UnityEngine.Object.FindObjectsByType<ArenaHazard>();origins=locked.Select(h=>(Vector2)h.transform.position).ToArray();activeSamples=0;
            Require(locked.Length==3&&locked.All(h=>!h.IsActive),"three harmless locked warnings "+phase);
            float minimumGap=float.PositiveInfinity;
            for(int i=0;i<origins.Length;i++)
            {
                Require(Physics2D.OverlapCircle(origins[i],1.9f,1<<GameLayers.Ground)==null,"full disk avoids actual wall/cover/seal "+phase+" "+i);
                for(int j=i+1;j<origins.Length;j++)
                {
                    var gap=Vector2.Distance(origins[i],origins[j])-3.8f;minimumGap=Mathf.Min(minimumGap,gap);
                    Require(gap>=.949f,"separate circle borders "+phase+" "+i+" "+j);
                    Require(!locked[i].Contains((origins[i]+origins[j])*.5f)&&!locked[j].Contains((origins[i]+origins[j])*.5f),"midpoint outside both damage circles "+phase+" "+i+" "+j);
                }
            }
            var fx=locked.SelectMany(h=>h.GetComponentsInChildren<SpriteRenderer>()).Where(v=>v.name.StartsWith("HazardFX_")).ToArray();
            Require(fx.Length==12&&fx.All(v=>v.sortingLayerName=="WorldFloor"&&v.sortingOrder==9&&v.transform.lossyScale==Vector3.one),"twelve native scale effects below actors "+phase);
            yield return new WaitForEndOfFrame();Capture(phase+"-warning");
            var move=targets[index].x>0?GameAction.MoveLeft:targets[index].x<0?GameAction.MoveRight:targets[index].y>0?GameAction.MoveDown:GameAction.MoveUp;
            StageValidationInput.SetHeld(move,true);
            var part=Record(2f,phase,1.12f);while(part.MoveNext())yield return part.Current;
            StageValidationInput.ReleaseAll();EnemyField("specialTimer",10000f);
            Require(activeSamples>0,"actual active samples rendered "+phase);
            Require(UnityEngine.Object.FindObjectsByType<ArenaHazard>().Length==0,"native tails completely expire "+phase);
            var moved=Vector2.Distance(target,game.Player.transform.position);
            Require(moved>1f,"normal movement input changes target while locked centers stay fixed "+phase);
            placement.Add(new JObject{{"phase",phase},{"target",new JArray(target.x,target.y)},{"centers",new JArray(origins.Select(p=>new JArray(p.x,p.y)))},{"minimumBorderGap",minimumGap},{"moved",moved},{"activeSamples",activeSamples}});
            locked=null;Flush();
        }
        Require(effectFrames.Count(p=>p.Contains("/TidePillar/"))==9,"all nine native water pillar files rendered across scenarios");
        Require(effectFrames.Count(p=>p.Contains("/TideSplash/"))==11,"all eleven native water splash files rendered across scenarios");
        File.WriteAllText(folder+"/placement.json",placement.ToString(),new UTF8Encoding(false));
        actor.StopAllCoroutines();typeof(EnemyController).GetMethod("EndAction",Private).Invoke(actor,null);EnemyField("specialTimer",10000f);
        game.ReturnToMainMenu();game.enabled=true;yield return null;
        Require(UnityEngine.Object.FindObjectsByType<ArenaHazard>().Length==0,"menu clears every ground effect");
        Require(UnityEngine.Object.FindObjectsByType<SpriteRenderer>().All(v=>!v.name.StartsWith("HazardFX_")),"no detached ground art survives menu");
    }
}
