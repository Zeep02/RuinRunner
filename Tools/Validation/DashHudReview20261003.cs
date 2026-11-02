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
public static class DashHudReview20261003
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
    static readonly HashSet<Sprite> phaseSprites=new HashSet<Sprite>();
    static bool sawMelee,sawMobileMelee;
    static readonly HashSet<string> phasePoses=new HashSet<string>();
    static float traveled;static Vector2 previousPosition;
    static void ResetObservations(){sawMelee=sawMobileMelee=false;phaseSprites.Clear();phasePoses.Clear();traveled=0;previousPosition=game.Player.transform.position;}
    static readonly HashSet<int>[] fxKeys={new HashSet<int>(),new HashSet<int>()};static readonly bool[] sawPeak={false,false};static bool sawEarly;static int observedContacts,initialContacts;
    static float handJump;static Vector2 lastHand;static bool hadHand;static MeleeCrossCutEffect trackedCross;
    static void Fresh(){ResetObservations();foreach(var keys in fxKeys)keys.Clear();sawPeak[0]=sawPeak[1]=sawEarly=false;initialContacts=observedContacts=game.Player.MeleeContactsResolved;handJump=0;hadHand=false;trackedCross=null;}
    static MeleeCrossCutEffect Cross(){foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeCrossCutEffect>())if(fx.FollowsCrossAction)return fx;return null;}
    static float socketStep; static Vector2 previousSocket; static bool hadSocket; static int expectedDirection;
    static bool attacked, gaitDuringAttack; static int maxReloadKey; static float maxReloadProgress; static readonly HashSet<Sprite> reloadSprites=new HashSet<Sprite>();
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();var pivot=p.transform.Find("HeldWeaponPivot");
        Check(body.sprite.rect.size==new Vector2(48,64)&&body.sprite.pivot==new Vector2(24,22),"unchanged native canvas/foot pivot "+phase);
        if(hadSocket)socketStep=Mathf.Max(socketStep,Vector2.Distance(previousSocket,a.HandLocalPosition)*32);
        previousSocket=a.HandLocalPosition;hadSocket=true;
        if(a.PoseName.StartsWith("reload-")){reloadSprites.Add(body.sprite);maxReloadKey=Math.Max(maxReloadKey,a.FrameIndex);maxReloadProgress=Math.Max(maxReloadProgress,a.ReloadPresentationProgress);}
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var camera=Camera.main;var origin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            a.ReloadPresentationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),origin.x.ToString("F6",CultureInfo.InvariantCulture),origin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-origin.x).ToString("F6",CultureInfo.InvariantCulture),(origin.y-down.y).ToString("F6",CultureInfo.InvariantCulture),"",-1,UnityEditorInternal.InternalEditorUtility.isApplicationActive,Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture)));
    }
    static void Check(bool okay,string label){if(!okay)throw new Exception(label);}
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
    static bool movingReviewOnly,transitionReviewOnly,cleanupOnly;
    public static string MainCleanup(){cleanupOnly=true;return Main();}
    public static string MainMovementReview(){movingReviewOnly=true;return Main();}
    public static string MainTransitions(){transitionReviewOnly=true;return Main();}
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-03-dash-hud/motion-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
        poses.Clear(); checks.Clear(); customSizes.Clear(); buffered.Clear(); sample = 0; recording.Clear(); recording.AppendLine("frame,realtime,phase"); telemetry.Clear();telemetry.AppendLine("frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts,angle,bodyAsset,originSX,originSY,scaleX,scaleY,fxClip,fxKey,editorForeground,gameFocused,delta");
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
        RestoreView(); File.WriteAllText(folder + "/result.txt", "PASS staged dash/HUD fixture; supplied equipment, actual Dash input path. Not normal campaign/native keyboard/performance evidence\n" + string.Join("\n", checks));
        Debug.Log("[Dash HUD] PASS " + folder);
    }

    static SpriteRenderer[] Ghosts() => GameObject.Find("DashAfterimages").GetComponentsInChildren<SpriteRenderer>(true);
    static int VisibleGhosts(){int n=0;foreach(var r in Ghosts())if(r.enabled)n++;return n;}
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        if(cleanupOnly)
        {
            Equip("storm_rifle");
            foreach(var mode in new[]{"teleport","session","disable"})
            {
                typeof(PlayerController).GetField("dashCharges",Private).SetValue(game.Player,2);yield return new WaitForSeconds(.1f);
                Pulse(GameAction.Dash);yield return new WaitForSeconds(.07f);yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();
                Require(VisibleGhosts()>0,"live echoes before "+mode);
                if(mode=="teleport")game.Player.Teleport(center);
                else if(mode=="session")typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
                else game.Player.GetComponent<DashAfterimageEffect>().enabled=false;
                Require(VisibleGhosts()==0,"immediate cleanup "+mode);
                yield return new WaitForSeconds(.4f);game.Player.GetComponent<DashAfterimageEffect>().enabled=true;
            }
            StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;yield return null;yield return null;
            Require(GameObject.Find("DashAfterimages")==null,"pool destroyed with run");yield break;
        }
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var id in new[]{"rust_sword","storm_rifle"})for(int direction=0;direction<8;direction++)
        {
            Equip(id);typeof(PlayerController).GetField("dashCharges",Private).SetValue(game.Player,2);
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();
            var root=GameObject.Find("DashAfterimages");Require(root!=null&&Ghosts().Length==8,"fixed four body/weapon echo pairs");
            var start=game.Player.transform.position; Pulse(GameAction.Dash); bool sawDash=false;int peak=0;
            var record=Record(.52f,id+"-dash-"+direction,.075f);
            while(record.MoveNext())
            {
                yield return record.Current;StageValidationInput.BeginFrame();sawDash|=game.Player.IsDashing;
                var source=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                peak=Math.Max(peak,VisibleGhosts());
                foreach(var r in Ghosts())if(r.enabled)
                { Check(r.sortingOrder<source.sortingOrder,"ghost behind live actor");Check(r.color.a<=.321f,"bounded afterimage alpha"); }
            }
            Require(sawDash&&peak>=4&&peak<=8,"multiple bounded silhouettes "+id+" direction "+direction+" peak="+peak);
            Require(Vector3.Distance(start,game.Player.transform.position)>2f,"unchanged responsive dash distance "+direction);
            Require(VisibleGhosts()==0,"all afterimages fade after dash "+direction);
            poses.Add(new JObject{{"weapon",id},{"direction",direction},{"peakRenderers",peak},{"distance",Vector3.Distance(start,game.Player.transform.position)}});Flush();
        }
        Equip("storm_rifle");typeof(PlayerController).GetField("dashCharges",Private).SetValue(game.Player,2);yield return null;
        Pulse(GameAction.Dash);yield return new WaitForSeconds(.07f);yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();
        Require(VisibleGhosts()>0,"pause fixture has live echoes");typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});
        yield return new WaitForEndOfFrame();var before=Ghosts();var colors=new Color[8];var locations=new Vector3[8];
        for(int i=0;i<8;i++){colors[i]=before[i].color;locations[i]=before[i].transform.position;}
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
        for(int i=0;i<8;i++)Require(before[i].color==colors[i]&&before[i].transform.position==locations[i],"pause freezes echo "+i);
        game.ResumeGame();yield return new WaitForSeconds(.2f);Pulse(GameAction.Dash);yield return new WaitForSeconds(.08f);StageValidationInput.BeginFrame();
        Require(Ghosts().Length==8&&VisibleGhosts()>0,"chained dash reuses pool");game.Player.SetStageClearSafety(true);yield return new WaitForEndOfFrame();
        Require(VisibleGhosts()==0,"stage clear removes live afterimages");game.Player.SetStageClearSafety(false);yield return new WaitForSeconds(.4f);
        // Full quickbar, wide and diagonal native silhouettes, actual reload state.
        Equip("rust_sword");game.Inventory.AddWeapon(WeaponLootTable.Find("storm_rifle"));game.Inventory.AddWeapon(WeaponLootTable.Find("dragon_cannon"));
        game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));game.Player.Teleport(center);
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1340,820)})
        {
            Resolution(size.x,size.y);yield return null;yield return new WaitForEndOfFrame();
            Require(Screen.width==size.x&&Screen.height==size.y,"requested native HUD resolution "+size);
            game.Inventory.ActiveSlot.Assign(game.Inventory.EquippedWeapon,12,0);yield return new WaitForEndOfFrame();
            Capture("hud-"+size.x+"x"+size.y);
            var ammo=game.Player.transform.Find("AmmoGauge");var dash=game.Player.transform.Find("DashCharges");
            var bounds=ammo.GetChild(0).GetComponent<SpriteRenderer>().bounds;
            var dashBounds=dash.GetComponent<MeshRenderer>().bounds;
            Require(bounds.max.y<dashBounds.min.y&&Mathf.Abs(ammo.position.x-dash.position.x)<.001f,"ammo below centered dash with visible gap "+size);
            Require(game.Inventory.StartReload(),"actual reload for HUD "+size);game.Inventory.Tick(game.Inventory.EquippedWeapon.ReloadSeconds*.4f);
            yield return new WaitForEndOfFrame();Capture("reload-"+size.x+"x"+size.y);
            typeof(GameController).GetProperty("IsInventoryOpen").GetSetMethod(true).Invoke(game,new object[]{true});
            yield return new WaitForEndOfFrame();Capture("inventory-"+size.x+"x"+size.y);
            typeof(GameController).GetProperty("IsInventoryOpen").GetSetMethod(true).Invoke(game,new object[]{false});
            game.Inventory.Tick(20f);
        }
        File.WriteAllText(folder+"/cases.json",poses.ToString(),new UTF8Encoding(false));
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;Time.timeScale=1;yield return null;yield return null;
        Require(GameObject.Find("DashAfterimages")==null,"menu destroys owned echo renderers");
    }
}
