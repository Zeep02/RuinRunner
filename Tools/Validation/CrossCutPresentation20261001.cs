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
public static class CrossCutPresentation20261001
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
    static readonly HashSet<int>[] fxKeys={new HashSet<int>(),new HashSet<int>()};static readonly bool[] sawPeak={false,false};static bool sawEarly;static int observedContacts,initialContacts;
    static float handJump;static Vector2 lastHand;static bool hadHand;static MeleeCrossCutEffect trackedCross;
    static void Fresh(){ResetObservations();foreach(var keys in fxKeys)keys.Clear();sawPeak[0]=sawPeak[1]=sawEarly=false;initialContacts=observedContacts=game.Player.MeleeContactsResolved;handJump=0;hadHand=false;trackedCross=null;}
    static MeleeCrossCutEffect Cross(){foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeCrossCutEffect>())if(fx.FollowsCrossAction)return fx;return null;}
    static void Track(string phase)
    {
        var p=game.Player;var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var a=p.GetComponent<PlayerAnimationController>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var fx=Cross();string clip="";int key=-1;
        if(fx!=null)
        {
            if(fx!=trackedCross){trackedCross=fx;initialContacts=observedContacts=(int)typeof(MeleeCrossCutEffect).GetField("initialContacts",Private).GetValue(fx);hadHand=false;}
            if(Vector2.Distance(fx.transform.position,held.BladePoint(0))>.002f)throw new Exception("Cross arcs detached from actual grip");
            if(fx.StrokeRenderer(0).enabled&&fx.StrokeRenderer(1).enabled)throw new Exception("Two bright arcs overlap into a detached star");
            for(int stroke=0;stroke<2;stroke++)
            {
                var v=fx.StrokeRenderer(stroke);var originals=PixelArtAtlas.GetMeleeSlashFrames(WeaponFamily.Katana,false);
                if(v.sprite!=originals[fx.FrameIndex(stroke)])throw new Exception("Incomplete or non-native physical cross source");
                if(v.sortingOrder>=body.sortingOrder||Mathf.Abs(v.transform.lossyScale.x-v.transform.lossyScale.y)>.0001f)throw new Exception("Cross hides face or stretches source");
                if(v.enabled)fxKeys[stroke].Add(fx.FrameIndex(stroke));
            }
            int currentStroke=MeleeMotionTiming.CrossStroke(p.AttackAnimationProgress);clip=currentStroke==0?"CrossFirst":"CrossReturn";key=fx.FrameIndex(currentStroke);
            if(p.AttackAnimationProgress<MeleeMotionTiming.CrossFirstContact&&fx.StrokeRenderer(0).enabled)sawEarly=true;
            while(p.MeleeContactsResolved>observedContacts)
            {int stroke=observedContacts++-initialContacts;if(stroke>1)throw new Exception("Extra gameplay stroke in one cross action");if(!fx.StrokeRenderer(stroke).enabled||fx.FrameIndex(stroke)!=1)throw new Exception("Bright source not on actual cross contact "+stroke+" progress="+p.AttackAnimationProgress+" contacts="+p.MeleeContactsResolved+" key="+fx.FrameIndex(stroke)+" visible="+fx.StrokeRenderer(stroke).enabled);sawPeak[stroke]=true;}
            if(hadHand)handJump=Mathf.Max(handJump,Vector2.Distance(a.HandLocalPosition,lastHand)*32);lastHand=a.HandLocalPosition;hadHand=true;
        }
        else observedContacts=p.MeleeContactsResolved;
        phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);sawMelee|=p.IsMeleeSwingActive&&p.ActiveMeleeMotion==MeleeAttackMotion.CrossCut;sawMobileMelee|=p.IsMeleeSwingActive&&p.MovementInput.sqrMagnitude>.01f;
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
        folder = "F:/codex/LoglikeCap/Logs/Validation/2026-10-01-cross-cut/presentation-" + DateTime.Now.ToString("HHmmss"); Directory.CreateDirectory(folder + "/frames");
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
    static IEnumerator Prepare()
    {
        var ready=Ready();while(ready.MoveNext())yield return ready.Current;
        for(int i=0;i<2;i++){Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();ready=Ready();while(ready.MoveNext())yield return ready.Current;}
    }
    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.08f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static IEnumerator Case(string id,int direction,bool mobile=false)
    {
        Equip(id);yield return null;var p=game.Player;var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
        var prep=Prepare();while(prep.MoveNext())yield return prep.Current;var enemy=Enemy(aim*1.05f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
        // Let the actual target renderer upload before measuring the short action.
        // This is within the existing combo reset window and does not change attack time.
        for(int warm=0;warm<4;warm++){yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();}
        Fresh();int sequence=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;
        if(mobile)StageValidationInput.SetHeld(GameAction.MoveLeft,true);StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.Attack,false);
        yield return null;StageValidationInput.BeginFrame();float total=(float)typeof(PlayerController).GetField("pendingMeleeDamage",Private).GetValue(p);
        var phase=id+(mobile?"-mobile":"")+"-view-"+direction;var motion=Motion(.65f,phase,time=>{if(time>.3f)StageValidationInput.ReleaseAll();});while(motion.MoveNext())yield return motion.Current;
        Require(p.AttackAnimationSequence==sequence+1&&p.MeleeContactsResolved==contacts+2,"one input/two distinct contacts "+phase);
        float expected=2*Mathf.Max(1,total*.5f*100/(100+enemy.Armor));Require(Mathf.Abs(hp-enemy.Health-expected)<.003f,"unchanged aggregate damage/one critical snapshot "+phase);
        Require(sawEarly&&sawPeak[0]&&sawPeak[1]&&fxKeys[0].Count>=2&&fxKeys[1].Count>=3,"two native source preparations/real contacts/breakups "+phase);
        Require(phasePoses.Contains(mobile?"cross-first-move":"cross-first")&&phasePoses.Contains(mobile?"cross-return-move":"cross-return")&&phaseSprites.Count>=10,"two actual native wrist body clips "+phase);
        Require(handJump<=4.5f,"native hand continuity "+phase+" maxPixels="+handJump);
        if(mobile)Require(sawMobileMelee&&traveled>.5f,"independent moving gait and bound double cut "+phase);
        Require(Cross()==null&&!p.IsMeleeSwingActive,"body and both source arcs end together "+phase);StageValidationInput.ReleaseAll();Flush();
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();Require(Screen.width==1280&&Screen.height==720,"native cross composition");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        for(int warm=0;warm<8;warm++){yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();}
        foreach(var id in new[]{"training_katana","moon_blade","blood_moon_katana"})foreach(var direction in new[]{0,1,2,3,4,5,6,7})
        {var test=Case(id,direction);while(test.MoveNext())yield return test.Current;}
        foreach(var id in new[]{"training_katana","moon_blade","blood_moon_katana"}){var test=Case(id,2,true);while(test.MoveNext())yield return test.Current;}
        foreach(var id in new[]{"training_katana","moon_blade","blood_moon_katana"})
        {
            Equip(id);var ready=Ready();while(ready.MoveNext())yield return ready.Current;StageValidationInput.SetAim(Vector2.right);Fresh();StageValidationInput.SetHeld(GameAction.Attack,true);
            var show=Motion(3.4f,id+"-combo-1x",time=>{if(time>1.4f&&time<2)StageValidationInput.SetHeld(GameAction.MoveLeft,true);else StageValidationInput.SetHeld(GameAction.MoveLeft,false);if(time>2.7f)StageValidationInput.SetHeld(GameAction.Attack,false);});while(show.MoveNext())yield return show.Current;
            Require(sawPeak[0]&&sawPeak[1]&&Cross()==null,"full actual normal-speed cross combos "+id);StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var mode in new[]{0,1})
        {
            Equip("training_katana");yield return null;var prepare=Prepare();while(prepare.MoveNext())yield return prepare.Current;Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();
            var deadline=Time.realtimeSinceStartupAsDouble+2;int before=game.Player.MeleeContactsResolved;
            while(game.Player.MeleeContactsResolved==before){if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("first stroke deadline");yield return null;StageValidationInput.BeginFrame();}
            yield return new WaitForEndOfFrame();Require(Cross()!=null&&game.Player.AttackAnimationProgress<MeleeMotionTiming.CrossSecondContact,"live cross between contacts");var fx=Cross();int key=fx.FrameIndex(0);var pos=fx.transform.position;float progress=game.Player.AttackAnimationProgress;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Require(Cross()==fx&&fx.FrameIndex(0)==key&&fx.transform.position==pos&&game.Player.AttackAnimationProgress==progress,"pause freezes both source/body/action clocks");Time.timeScale=1;
            int contacts=game.Player.MeleeContactsResolved;if(mode==0)Pulse(GameAction.Dash);else{game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));}
            yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;Require(Cross()==null,"dash/swap removes both source arcs "+mode);yield return new WaitForSeconds(.35f);Require(game.Player.MeleeContactsResolved==contacts,"cancelled return cut has no late damage "+mode);StageValidationInput.ReleaseAll();
        }
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeCrossCutEffect>().Length==0,"menu clears owned physical cross arcs");
    }
}
