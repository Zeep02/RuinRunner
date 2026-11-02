using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using RuinRunner;

// Isolated supplied actors/items; real action coroutines, GameAction input and rendered
// sockets. This does not enter, complete or claim a normal campaign or an audio review.
public static class GoalHeroHurtMotion20261009
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    static readonly Dictionary<GameObject, bool> roots = new Dictionary<GameObject, bool>();
    static readonly Dictionary<string, GameAction[]> inputs = new Dictionary<string, GameAction[]>();
    static readonly List<string> checks = new List<string>();
    static Scene originalScene, scene;
    static CameraFollow camera;
    static PlayerController player;
    static EnemyController actor;
    static InventorySystem bag;
    static GameSettingsService settings, oldAudio;
    static bool running, paused, oldEnabled, oldInitialized, oldBackground, motionCapture, smoke, directionTransitions, priorityOnly, lowRateRetest;
    static float oldScale;
    static int oldRate, oldInterval, oldAA;static AnisotropicFiltering oldAniso;
    static Vector2 oldAim;
    static UnityEngine.Random.State oldRandom;
    static Type autoTick;
    static bool oldTick;
    static EditorWindow oldFocus;
    static string folder;
    static double began;
    static FieldInfo Input(string name) => typeof(StageValidationInput).GetField(name, All);
    static T Read<T>(object instance, string name) => (T)instance.GetType().GetField(name, All).GetValue(instance);
    static void Set(object instance, string name, object value) => instance.GetType().GetField(name, All).SetValue(instance, value);
    static void Check(bool pass, string label)
    { checks.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) throw new Exception(label); }
    static IEnumerator Frame()
    {
        yield return new WaitForEndOfFrame();
        if (!paused && bag != null) bag.Tick(Time.deltaTime);
        StageValidationInput.BeginFrame();
    }
    static IEnumerator Wait(float seconds)
    {
        float until=Time.time+seconds;double wallUntil=Time.realtimeSinceStartupAsDouble+seconds;
        while(Time.time<until||Time.realtimeSinceStartupAsDouble<wallUntil)yield return Frame();
    }
    static void Remove(Component value)
    { if (value != null) { value.gameObject.SetActive(false); UnityEngine.Object.Destroy(value.gameObject); } }
    static void Capture(string name)
    {
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
    }
    static void Fresh(string weapon = "rust_sword", bool protect = false)
    {
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        Remove(actor); actor = null; Remove(player);
        foreach (var hazard in UnityEngine.Object.FindObjectsByType<ArenaHazard>()) Remove(hazard);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<BossRuneProjectile>()) Remove(shot);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<EnemyProjectile>()) Remove(shot);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) Remove(shot);
        bag = new InventorySystem(); if(weapon!=null)bag.AddWeapon(WeaponLootTable.Find(weapon));
        player = PlayerController.Create(Vector2.zero, () => paused, () => false, () => { }, camera, bag, settings);
        player.IsTrainingCombat = true; player.EquipWeapon(bag.EquippedWeapon);
        if (protect) player.SetStageClearSafety(true);
        camera.Target = player.transform;
        StageValidationInput.SetAim(Vector2.right); Physics2D.SyncTransforms();
    }
    public static string Main()=>Start(false);
    public static string MainMotion()=>Start(true);
    public static string MainSmoke()=>Start(true,true);
    public static string MainDirectionTransitions()=>Start(true,false,true);
    public static string MainPriorityMotion()=>Start(true,false,false,true);
    public static string MainLowRateRetest()=>Start(true,false,false,false,true);
    static string Start(bool motion,bool quick=false,bool transitions=false,bool priorities=false,bool lowRate=false)
    {
        if (running || !Application.isPlaying) throw new Exception("Play Mode required; run one fixture at a time.");
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (game == null || game.IsRunActive) throw new Exception("Start at main menu; existing campaign must not be replaced.");
        motionCapture=motion;smoke=quick;directionTransitions=transitions;priorityOnly=priorities;lowRateRetest=lowRate;
        if(motion&&Time.captureFramerate!=0)throw new Exception("Actual-clock capture requires Time.captureFramerate == 0.");
        folder = Path.Combine("F:/codex/LoglikeCap/Logs/Validation/2026-10-09-goal/hero-hurt", (priorities?"priority-":transitions?"transitions-":smoke?"smoke-":motion?"motion-":"functional-")+DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(folder); checks.Clear(); roots.Clear(); inputs.Clear();
        originalScene = SceneManager.GetActiveScene(); oldFocus = EditorWindow.focusedWindow;
        oldScale = Time.timeScale; oldRate = Application.targetFrameRate; oldBackground = Application.runInBackground;
        oldAA=QualitySettings.antiAliasing;oldAniso=QualitySettings.anisotropicFiltering;oldRandom = UnityEngine.Random.state; oldEnabled = (bool)Input("enabled").GetValue(null);
        oldInitialized = (bool)Input("initialized").GetValue(null); oldAim = StageValidationInput.AimDirection;
        foreach (var name in new[] { "Held", "Pressed", "Released" })
        { var set = (HashSet<GameAction>)Input(name).GetValue(null); inputs[name] = new List<GameAction>(set).ToArray(); set.Clear(); }
        oldAudio = (GameSettingsService)typeof(CombatAudio).GetField("settings", All).GetValue(null);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        { autoTick = assembly.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand"); if (autoTick != null) break; }
        if (autoTick != null)
        {
            oldTick = (bool)autoTick.GetProperty("IsEnabled", All).GetValue(null);
            oldInterval = Convert.ToInt32(autoTick.GetProperty("CurrentIntervalMs", All).GetValue(null));
        }
        running = true; began = EditorApplication.timeSinceStartup;
        try
        {
            settings = new GameSettingsService(Path.Combine(folder, "fixture-settings.json"), false);
            foreach (AudioChannel channel in Enum.GetValues(typeof(AudioChannel))) settings.ChangeVolume(channel, -1);
            CombatAudio.Configure(settings); paused = false;
            Input("enabled").SetValue(null, true); Input("initialized").SetValue(null, true);
            Application.runInBackground = true; Application.targetFrameRate = 60; Time.timeScale = 1;
            if (autoTick != null) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { true, 0, false });
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects()) roots[root] = root.activeSelf;
            foreach (var root in roots)
                if (root.Value && root.Key.GetComponent<PrototypeAudioController>() == null && root.Key.GetComponent<CombatAudio>() == null)
                    root.Key.SetActive(false);
            scene = SceneManager.CreateScene("GoalHeroHurt-SuppliedFixture"); SceneManager.SetActiveScene(scene);
            camera = CameraFollow.Create(); camera.FocusRoom(Vector2.zero, new Vector2(100, 100), true);
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            checks.Add("DATA render pipeline=" + (pipeline == null ? "Built-in" : pipeline.GetType().FullName));
            EditorApplication.update += Watchdog; EditorApplication.playModeStateChanged += PlayState;
            camera.StartCoroutine(Guard(Run()));
        }
        catch (Exception error) { Finish("FAIL setup " + error); }
        return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(run); string error = null;
        while (stack.Count > 0 && running)
        {
            bool next; object value = null;
            try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
            catch (Exception exception) { error = exception.ToString(); break; }
            if (!next) { stack.Pop(); continue; }
            if (value is IEnumerator child) { stack.Push(child); continue; }
            yield return value;
        }
        if (running) Finish(error == null ? "PASS" : "FAIL " + error);
    }
    static void NativeReviewFloor()
    {
        if(GameObject.Find("SuppliedNativeBossFloor")!=null||GameObject.Find("SuppliedNativeCoreFloor")!=null)return;
        var floor=new GameObject("SuppliedNativeBossFloor");var ground=floor.AddComponent<SpriteRenderer>();
        ground.sprite=IndividualArt.Sprite("External/ChampiDungeon/Ground");ground.drawMode=SpriteDrawMode.Tiled;
        ground.size=new Vector2(30,18);ground.sortingLayerName="WorldFloor";ground.sortingOrder=0;ground.sharedMaterial=NativeCoreGround.Material;
    }

    static PlayerAnimationController Animation=>player.GetComponent<PlayerAnimationController>();
    static SpriteRenderer Body=>player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
    static readonly HashSet<int> keys=new HashSet<int>();
    static readonly HashSet<Sprite> bodies=new HashSet<Sprite>();
    static double firstHurt,lastHurt;static float maxHurtProgress,maxHandStep;static Vector2 lastHand;
    static bool haveHand;static int expectedDirection;static string currentClip;
    static int Variant=>player.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude>.04f?8+Mathf.FloorToInt(Read<float>(Animation,"stride")*8):PlayerAnimationController.IdleFrame(Read<float>(Animation,"idleTime"));
    static void Ensure(bool okay,string label){if(!okay)throw new Exception(label);}
    static string Observe(string clip)
    {
        var animation=Animation;var body=Body;var held=player.GetComponent<HeldWeaponView>();
        Ensure(body.sprite!=null&&body.sprite.rect.size==new Vector2(48,64)&&body.sprite.pivot==new Vector2(24,22),"Native body geometry changed "+clip);
        if(player.EquippedWeapon!=null)
        {
            var pivot=player.transform.Find("HeldWeaponPivot");
            Ensure(Vector2.Distance(pivot.localPosition,animation.HandLocalPosition)<.001f,"Held pivot detached from visible hand "+clip);
            var renderer=Read<SpriteRenderer>(held,"weaponRenderer");NativeWeaponArt.Pose pose;
            Ensure(NativeWeaponArt.TryGet(player.EquippedWeapon.Id,out pose),"Missing reviewed native weapon grip "+clip);
            var local=new Vector2(pose.Grip.x-renderer.sprite.pivot.x,renderer.sprite.rect.height-pose.Grip.y-renderer.sprite.pivot.y)/renderer.sprite.pixelsPerUnit;
            if(renderer.flipY)local.y=-local.y;
            Ensure(Vector2.Distance(renderer.transform.TransformPoint(local),player.transform.TransformPoint(animation.HandLocalPosition))<.035f,"Actual rendered grip is more than one source pixel from hand "+clip);
        }
        else Ensure(!held.IsVisible,"Empty hand renders a weapon "+clip);
        if(animation.PoseName.StartsWith("hurt-",StringComparison.Ordinal))
        {
            Ensure(animation.FacingDirection==expectedDirection,"Hurt body changed aimed direction "+clip);
            int profile=player.EquippedWeapon==null?6:WeaponStandingFrames.Profile(player.EquippedWeapon.Family);
            Vector2 hand;bool behind;var expected=SscaryHurtFrames.Sample(profile,expectedDirection,Variant,animation.FrameIndex,out hand,out behind);
            Ensure(body.sprite==expected&&animation.HandBehindBody==behind,"Runtime hurt did not use current native variant/socket metadata "+clip);
            Ensure(animation.FrameIndex==SscaryHurtFrames.PhaseIndex(player.HurtPresentationProgress),"Hurt sampled an independent clock "+clip);
            if(keys.Count==0)firstHurt=Time.timeAsDouble;lastHurt=Time.timeAsDouble;
            keys.Add(animation.FrameIndex);bodies.Add(body.sprite);maxHurtProgress=Mathf.Max(maxHurtProgress,player.HurtPresentationProgress);
        }
        if(haveHand)maxHandStep=Mathf.Max(maxHandStep,Vector2.Distance(lastHand,animation.HandLocalPosition)*32);lastHand=animation.HandLocalPosition;haveHand=true;
        return string.Join(",",animation.PoseName,animation.FacingDirection,player.HurtPresentationProgress.ToString("F5",CultureInfo.InvariantCulture),player.Health.ToString("F3",CultureInfo.InvariantCulture),
            animation.HandLocalPosition.x.ToString("F5",CultureInfo.InvariantCulture),animation.HandLocalPosition.y.ToString("F5",CultureInfo.InvariantCulture),animation.HandBehindBody,
            player.AttackAnimationSequence,Read<int>(player,"actionCancelSequence"),player.MeleeContactsResolved,bag.ActiveSlot.Ammo,bag.ActiveSlot.ReloadRemaining.ToString("F5",CultureInfo.InvariantCulture),
            player.transform.position.x.ToString("F5",CultureInfo.InvariantCulture),player.transform.position.y.ToString("F5",CultureInfo.InvariantCulture),held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture));
    }
    static void Move(Vector2 direction)
    {
        StageValidationInput.SetHeld(GameAction.MoveLeft,direction.x<-.25f);StageValidationInput.SetHeld(GameAction.MoveRight,direction.x>.25f);
        StageValidationInput.SetHeld(GameAction.MoveDown,direction.y<-.25f);StageValidationInput.SetHeld(GameAction.MoveUp,direction.y>.25f);
    }
    static IEnumerator HurtCase(string id,int direction,bool moving,bool reset=true,bool settle=true)
    {
        if(reset)Fresh(id);expectedDirection=direction;
        var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
        if(moving)Move(aim);if(settle)yield return Wait(.25f);
        string clip=(id??"unarmed")+"-d"+direction+(moving?"-moving":"-standing");
        keys.Clear();bodies.Clear();haveHand=false;maxHandStep=maxHurtProgress=0;firstHurt=lastHurt=0;
        int sequence=player.AttackAnimationSequence,cancel=Read<int>(player,"actionCancelSequence"),ammo=bag.ActiveSlot.Ammo;
        float health=player.Health;Vector2 origin=player.transform.position;bool hit=false;double hitTime=0;
        yield return RecordMotion(clip,.5f,t=>{
            if(!hit&&t>=.075f){hit=true;hitTime=Time.timeAsDouble;player.TakeDamage(1,Vector2.zero);}
            Ensure(player.AttackAnimationSequence==sequence&&Read<int>(player,"actionCancelSequence")==cancel&&bag.ActiveSlot.Ammo==ammo,"Hurt changed attack commitment/ammo "+clip);
        });
        FlushMotion(clip);
        Check(hit&&player.Health<health,"Real damage reached health "+clip);
        Check(keys.Count>=3&&bodies.Count>=3&&maxHurtProgress>.7f,"Authored hurt visible at several actual timer phases "+clip+" keys="+string.Join("/",keys)+" sprites="+bodies.Count);
        Check(lastHurt-hitTime<=PlayerController.HurtPresentationSeconds+.035f&&!Animation.PoseName.StartsWith("hurt-"),"Hurt ended on the shared .18-second real timer "+clip);
        Vector2 hand;bool behind;Sprite expected;
        if(id==null)expected=Read<Sprite[][]>(Animation,Variant>=8?"move":"idle")[Animation.FacingDirection][Variant%8];
        else expected=WeaponStandingFrames.Sample(WeaponStandingFrames.Profile(player.EquippedWeapon.Family),Animation.FacingDirection,Variant,8,out hand,out behind);
        Check(Body.sprite==expected,"Returned to the current native idle/gait variant "+clip);
        if(moving)Check(Vector2.Distance(origin,player.transform.position)>.15f,"Normal movement continued through damage "+clip);
        checks.Add("DATA "+clip+" maxHandStepSourcePixels="+maxHandStep.ToString("F3",CultureInfo.InvariantCulture)+" firstHurtDelay="+(firstHurt-hitTime).ToString("F5",CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(folder,"progress.txt"),clip+"\n"+string.Join("\n",checks),new UTF8Encoding(false));
        StageValidationInput.ReleaseAll();
    }
    static IEnumerator AttackPriority(string id)
    {
        Fresh(id);expectedDirection=2;yield return Wait(.25f);int contacts=player.MeleeContactsResolved;
        StageValidationInput.SetHeld(GameAction.Attack,true);yield return Frame();StageValidationInput.SetHeld(GameAction.Attack,false);
        int sequence=player.AttackAnimationSequence,cancel=Read<int>(player,"actionCancelSequence");
        Check(sequence>0,"Actual GameAction began attack "+id);float health=player.Health;bool hit=false;bool activeSeen=false;
        yield return RecordMotion("priority-attack-"+id,.55f,t=>{
            if(!hit){hit=true;int ammo=bag.ActiveSlot.Ammo;float reload=bag.ActiveSlot.ReloadRemaining;player.TakeDamage(1,Vector2.zero);Ensure(bag.ActiveSlot.Ammo==ammo&&bag.ActiveSlot.ReloadRemaining==reload,"Damage instantly reset ammo/reload "+id);}
            bool committed=player.IsMeleeSwingActive||player.IsRangedPresentationActive;
            if(committed){activeSeen=true;Ensure(!Animation.PoseName.StartsWith("hurt-"),"Hurt concealed committed attack "+id);}
            Ensure(player.AttackAnimationSequence==sequence&&Read<int>(player,"actionCancelSequence")==cancel,"Hurt reset or recommitted attack "+id);
        });FlushMotion("priority-attack-"+id);
        Check(activeSeen&&player.Health<health,"Damage preserves active attack presentation "+id);
        if(player.EquippedWeapon.AttackType==WeaponAttackType.Melee)Check(player.MeleeContactsResolved==contacts+1,"Existing pending melee contact resolved once "+id);
        else Check(player.AttackAnimationSequence==sequence,"Damage did not fire an additional shot "+id);
    }
    static IEnumerator ReloadPriority(string id)
    {
        Fresh(id);expectedDirection=2;yield return Wait(.25f);StageValidationInput.SetHeld(GameAction.Attack,true);yield return Frame();StageValidationInput.SetHeld(GameAction.Attack,false);
        yield return Wait(.3f);Check(bag.ActiveSlot.IsReloading||bag.StartReload(),"Actual automatic or partial-magazine reload begins "+id);yield return Wait(.08f);
        int sequence=player.AttackAnimationSequence;float before=bag.ActiveSlot.ReloadRemaining,health=player.Health;bool hit=false;
        yield return RecordMotion("priority-reload-"+id,.45f,t=>{
            if(!hit){hit=true;player.TakeDamage(1,Vector2.zero);}
            if(bag.ActiveSlot.IsReloading)Ensure(Animation.PoseName.StartsWith("reload-"),"Hurt concealed reload "+id);
            Ensure(player.AttackAnimationSequence==sequence,"Damage fired or reset reload attack "+id);
        });FlushMotion("priority-reload-"+id);
        Check(player.Health<health&&bag.ActiveSlot.ReloadRemaining<before,"Actual reload clock continued through damage "+id);
        yield return Wait(player.EquippedWeapon.ReloadSeconds);
        Check(!bag.ActiveSlot.IsReloading&&bag.ActiveSlot.Ammo==player.EquippedWeapon.MagazineSize,"Reload completed once at full magazine "+id);
    }
    static IEnumerator DefensePriority(string id,bool dash)
    {
        Fresh(id);expectedDirection=2;yield return Wait(.25f);var action=dash?GameAction.Dash:GameAction.Guard;
        StageValidationInput.SetHeld(action,true);yield return Frame();
        if(dash)StageValidationInput.SetHeld(action,false);
        Check(dash?player.IsDashing:player.IsGuarding||player.IsParrying||player.IsGreatswordCharging,"Actual defensive input entered native state "+id);
        // Include defense and recovery; a .12s window could contain just one
        // rendered frame during a first-use stall and could not support timing.
        bool applied=false;yield return RecordMotion("priority-"+(dash?"dash-":"guard-")+id,.35f,t=>{
            if(!applied){applied=true;player.TakeDamage(1,Vector2.zero);}
            if(player.IsDashing||player.IsGuarding||player.IsParrying||player.IsGreatswordCharging)
                Ensure(!Animation.PoseName.StartsWith("hurt-"),"Hurt overrode defense state "+id);
        });FlushMotion("priority-"+(dash?"dash-":"guard-")+id);StageValidationInput.ReleaseAll();
    }
    static IEnumerator Run()
    {
        MotionResolution();motionQuality.Clear();motionQuality.Add("clip,frames,wallSeconds,simulationSeconds,actualFps,p95IntervalMs,maxIntervalMs,recordingQuality");
        for(int i=0;i<5;i++)yield return Frame();Check(Screen.width==1280&&Screen.height==720,"Native 1280x720 Game View");NativeReviewFloor();
        Fresh();yield return Wait(.25f);player.TakeDamage(1,Vector2.zero);yield return Wait(.3f);
        if(motionCapture)yield return VerifyMotionReadback();
        if(lowRateRetest)
        {
            yield return HurtCase("bastion_shotgun",2,false);
            yield return DefensePriority("training_katana",false);
            checks.Add("Targeted recapture of two previous LOW_RATE clips only; actual timestamps and recording quality remain separate from visual approval.");
            yield break;
        }
        if(directionTransitions)
        {
            foreach(string id in new[]{"worn_dagger","rust_sword","iron_greatsword","hand_crossbow","scrap_pistol","tether_carbine",null})
            {
                Fresh(id);yield return Wait(.25f);
                foreach(int direction in new[]{0,1,2,3,4,5,6,7})
                {
                    // Same live actor keeps native gait/idle clocks between
                    // turns. Each separate .5s clip includes turn, hit, recovery;
                    // actual i-frames then expire outside the recording window.
                    yield return HurtCase(id,direction,true,false,false);yield return Wait(.65f);
                }
            }
            yield break;
        }
        // Use the current shipped pool; the former hunter_bow id is not in it.
        var ids=smoke?new[]{"rust_sword","hand_crossbow","tether_carbine","bastion_shotgun",null}:new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon","tether_carbine","bastion_shotgun",null};
        if(!priorityOnly)foreach(string id in ids)foreach(int direction in smoke?new[]{2,7}:new[]{0,1,2,3,4,5,6,7})foreach(bool moving in new[]{false,true})
            yield return HurtCase(id,direction,moving);
        if(!smoke)
        {
            foreach(string id in ids)if(id!=null)yield return AttackPriority(id);
            foreach(string id in new[]{"tether_carbine","bastion_shotgun","hand_crossbow","scrap_pistol","dragon_cannon"})yield return ReloadPriority(id);
            foreach(string id in new[]{"rust_sword","training_katana","iron_greatsword"})yield return DefensePriority(id,false);
            yield return DefensePriority("rust_sword",true);
        }
        checks.Add("DATA target60FPS, no Time.captureFramerate/fixed capture timestep; no duplicated or interpolated frames. See measured recording-quality.csv. Static/functional PASS is not visual approval.");
    }
    sealed class MotionFrame
    {public int Index;public double Realtime,Simulation;public int UnityFrame;public Color32[] Pixels;public string Body;public int Phase;public string Telemetry;}
    static readonly List<MotionFrame> motionFrames=new List<MotionFrame>();
    static readonly List<string> motionQuality=new List<string>();
    static RenderTexture motionTarget;static int motionPending;static bool motionError,motionFlip;
    static EditorWindow motionView;static object motionSizes;static int motionOldSize,motionPreset=-1,motionOldCustomCount;
    static void MotionResolution()
    {
        if(motionSizes!=null&&motionPreset>=0)return;
        motionView=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));motionView.Focus();
        motionOldSize=(int)motionView.GetType().GetProperty("selectedSizeIndex",All).GetValue(motionView);
        var type=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        if(type==null)throw new Exception("UnityEditor.GameViewSizes type unavailable");
        var singleton=type.GetProperty("instance",All);
        if(singleton==null)throw new Exception("Inherited ScriptableSingleton<GameViewSizes>.instance unavailable with FlattenHierarchy");
        var instance=singleton.GetValue(null);
        if(instance==null)throw new Exception("GameViewSizes singleton resolved to null");
        motionSizes=type.GetMethod("GetGroup",All).Invoke(instance,new[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"),"Standalone")});
        motionOldCustomCount=(int)motionSizes.GetType().GetMethod("GetCustomCount",All).Invoke(motionSizes,null);
        motionPreset=(int)motionSizes.GetType().GetMethod("GetTotalCount",All).Invoke(motionSizes,null);
        var sizeType=Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var preset=Activator.CreateInstance(sizeType,new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),1280,720,"Hero hurt actual clock capture"});
        motionSizes.GetType().GetMethod("AddCustomSize",All).Invoke(motionSizes,new[]{preset});
        motionView.GetType().GetProperty("selectedSizeIndex",All).SetValue(motionView,motionPreset);motionView.Repaint();
    }
    static void RestoreMotion()
    {
        if(motionTarget!=null){AsyncGPUReadback.WaitAllRequests();motionTarget.Release();UnityEngine.Object.Destroy(motionTarget);motionTarget=null;}
        motionFrames.Clear();
        if(motionSizes!=null&&motionPreset>=0)
        {
            motionView.GetType().GetProperty("selectedSizeIndex",All).SetValue(motionView,motionOldSize);
            motionSizes.GetType().GetMethod("RemoveCustomSize",All).Invoke(motionSizes,new object[]{motionPreset});
            Check((int)motionSizes.GetType().GetMethod("GetCustomCount",All).Invoke(motionSizes,null)==motionOldCustomCount,"capture restores Game View resolution and presets");
            motionPreset=-1;motionSizes=null;
        }
    }
    static IEnumerator VerifyMotionReadback()
    {
        Check(SystemInfo.supportsAsyncGPUReadback,"async GPU readback supported");
        paused=true;Time.timeScale=0;yield return Frame();
        var reference=ScreenCapture.CaptureScreenshotAsTexture();var expected=reference.GetPixels32();
        File.WriteAllBytes(Path.Combine(folder,"orientation-reference.png"),reference.EncodeToPNG());UnityEngine.Object.Destroy(reference);
        motionTarget=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);motionTarget.Create();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(motionTarget);Color32[] pixels=null;bool failed=false;
        AsyncGPUReadback.Request(motionTarget,0,TextureFormat.RGBA32,r=>{if(r.hasError)failed=true;else pixels=r.GetData<Color32>().ToArray();});
        double deadline=Time.realtimeSinceStartupAsDouble+5;
        while(pixels==null&&!failed&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(!failed&&pixels!=null,"async orientation reference completed");
        int same=0,flipped=0,width=Screen.width,height=Screen.height;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            var e=expected[y*width+x];var a=pixels[y*width+x];var b=pixels[(height-1-y)*width+x];
            if(a.r!=e.r||a.g!=e.g||a.b!=e.b)same++;
            if(b.r!=e.r||b.g!=e.g||b.b!=e.b)flipped++;
        }
        motionFlip=flipped<same;Check(Math.Min(same,flipped)<width*height*.01f,"async capture agrees with composited screen within one percent");
        File.WriteAllText(Path.Combine(folder,"orientation.txt"),"normalDifferent="+same+" flippedDifferent="+flipped+" verticalFlip="+motionFlip,new UTF8Encoding(false));
        motionTarget.Release();UnityEngine.Object.Destroy(motionTarget);motionTarget=null;paused=false;Time.timeScale=1;
    }
    static IEnumerator RecordMotion(string clip,float seconds,Action<float> sample=null)
    {
        currentClip=clip;motionFrames.Clear();motionPending=0;motionError=false;
        if(motionCapture){motionTarget=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);motionTarget.Create();}
        double start=Time.timeAsDouble;
        while(Time.timeAsDouble-start<seconds)
        {
            // One sample for every real end-of-frame; no fixed capture timestep,
            // no repeated image fill, and no disk or PNG compression in this loop.
            if(Time.timeScale!=1||Time.captureFramerate!=0)throw new Exception("Capture clock altered");
            if(motionFrames.Count>=240)throw new Exception("Bounded raw buffer reached 240 frames; clip aborted rather than silently dropping frames");
            sample?.Invoke((float)(Time.timeAsDouble-start));
            motionView.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return Frame();
            var frame=new MotionFrame{Index=motionFrames.Count,Realtime=Time.realtimeSinceStartupAsDouble,Simulation=Time.timeAsDouble,UnityFrame=Time.frameCount,
                Phase=Animation.FrameIndex,Body=AssetDatabase.GetAssetPath(Body.sprite)+"#"+Body.sprite.name,Telemetry=Observe(clip)};
            motionFrames.Add(frame);if(!motionCapture)continue;ScreenCapture.CaptureScreenshotIntoRenderTexture(motionTarget);motionPending++;
            AsyncGPUReadback.Request(motionTarget,0,TextureFormat.RGBA32,r=>
            {if(r.hasError)motionError=true;else frame.Pixels=r.GetData<Color32>().ToArray();motionPending--;});
        }
        double deadline=Time.realtimeSinceStartupAsDouble+8;
        while(motionPending>0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(!motionError&&motionPending==0,"all real frame readbacks completed "+clip);
        if(motionTarget!=null){motionTarget.Release();UnityEngine.Object.Destroy(motionTarget);motionTarget=null;}
    }
    static void FlushMotion(string clip)
    {
        string path=Path.Combine(folder,clip);Directory.CreateDirectory(path);
        var csv=new StringBuilder("frame,realtime,simulation,unityFrame,key,bodyAsset,pose,direction,hurtProgress,health,handX,handY,behind,sequence,cancelSequence,contacts,ammo,reloadRemaining,x,y,weaponAngle,gameFocused,delta\n");
        var concat=new StringBuilder("ffconcat version 1.0\n");var intervals=new List<double>();
        var texture=new Texture2D(1280,720,TextureFormat.RGBA32,false);Color32[] corrected=motionFlip?new Color32[1280*720]:null;
        try
        {
            for(int i=0;i<motionFrames.Count;i++)
            {
                var f=motionFrames[i];if(motionCapture&&f.Pixels==null)throw new Exception("Missing raw frame "+clip+"/"+i);
                csv.AppendLine(i+","+f.Realtime.ToString("F6",CultureInfo.InvariantCulture)+","+f.Simulation.ToString("F6",CultureInfo.InvariantCulture)+","+f.UnityFrame+","+f.Phase+","+f.Body+","+f.Telemetry);
                if(motionCapture)
                {
                if(motionFlip){for(int y=0;y<720;y++)Array.Copy(f.Pixels,y*1280,corrected,(719-y)*1280,1280);texture.SetPixels32(corrected);}
                else texture.SetPixels32(f.Pixels);
                texture.Apply();string name="frame-"+i.ToString("D4")+".png";File.WriteAllBytes(Path.Combine(path,name),texture.EncodeToPNG());f.Pixels=null;
                }
                string frameName="frame-"+i.ToString("D4")+".png";
                double interval=i+1<motionFrames.Count?motionFrames[i+1].Realtime-f.Realtime:i>0?f.Realtime-motionFrames[i-1].Realtime:1d/60;
                concat.AppendLine("file '"+frameName+"'");concat.AppendLine("option framerate 1000000");concat.AppendLine("duration "+interval.ToString("F6",CultureInfo.InvariantCulture));
                if(i+1<motionFrames.Count)intervals.Add(interval);
            }
            concat.AppendLine("file 'frame-"+(motionFrames.Count-1).ToString("D4")+".png'");concat.AppendLine("option framerate 1000000");
            File.WriteAllText(Path.Combine(path,"timestamps.csv"),csv.ToString(),new UTF8Encoding(false));
            if(motionCapture)File.WriteAllText(Path.Combine(path,"actual-time.ffconcat"),concat.ToString(),new UTF8Encoding(false));
            Ensure(motionFrames.Count>=2,"Capture has fewer than two real frames; no timing claim "+clip);intervals.Sort();double wall=motionFrames[motionFrames.Count-1].Realtime-motionFrames[0].Realtime;
            double simulation=motionFrames[motionFrames.Count-1].Simulation-motionFrames[0].Simulation;
            double fps=(motionFrames.Count-1)/Math.Max(.001,wall),p95=intervals[(int)Math.Min(intervals.Count-1,Math.Ceiling(intervals.Count*.95)-1)]*1000,max=intervals[intervals.Count-1]*1000;
            bool actualClock=Math.Abs(simulation-wall)<=Math.Max(.015,wall*.05);
            string quality=!motionCapture?"TELEMETRY_ONLY_NO_VIDEO":!actualClock?"CLOCK_MISMATCH_NO_ACTUAL_SPEED_PASS":fps>=50&&p95<=30&&max<=100?"SUFFICIENT_FOR_VISUAL_REVIEW":"LOW_RATE_NO_SMOOTHNESS_PASS";
            motionQuality.Add(string.Join(",",clip,motionFrames.Count,wall.ToString("F6",CultureInfo.InvariantCulture),simulation.ToString("F6",CultureInfo.InvariantCulture),fps.ToString("F3",CultureInfo.InvariantCulture),p95.ToString("F3",CultureInfo.InvariantCulture),max.ToString("F3",CultureInfo.InvariantCulture),quality));
            File.WriteAllLines(Path.Combine(folder,"recording-quality.csv"),motionQuality,new UTF8Encoding(false));
            checks.Add("DATA recording "+clip+" frames="+motionFrames.Count+" actualFPS="+fps.ToString("F2",CultureInfo.InvariantCulture)+" p95ms="+p95.ToString("F2",CultureInfo.InvariantCulture)+" maxms="+max.ToString("F2",CultureInfo.InvariantCulture)+" "+quality);
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);motionFrames.Clear();}
    }
    static void Watchdog() { if (running && EditorApplication.timeSinceStartup - began > 1800) Finish("FAIL fixture watchdog"); }
    static void PlayState(PlayModeStateChange state) { if (running && state == PlayModeStateChange.ExitingPlayMode) Finish("INTERRUPTED Play ended"); }
    static void Finish(string result)
    {
        if (!running) return; running = false; EditorApplication.update -= Watchdog; EditorApplication.playModeStateChanged -= PlayState;
        if(true)
        {
            try{if(motionFrames.Count>0){AsyncGPUReadback.WaitAllRequests();FlushMotion("partial-"+currentClip);}RestoreMotion();}
            catch(Exception error){result+="\nMOTION RESTORE FAIL "+error;}
        }
        try
        {
            if (scene.IsValid() && scene.isLoaded) foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
            if (originalScene.IsValid()) SceneManager.SetActiveScene(originalScene);
            foreach (var root in roots) if (root.Key != null) root.Key.SetActive(root.Value);
            foreach (var input in inputs) { var set = (HashSet<GameAction>)Input(input.Key).GetValue(null); set.Clear(); set.UnionWith(input.Value); }
            typeof(StageValidationInput).GetProperty("AimDirection", All).SetValue(null, oldAim);
            Input("enabled").SetValue(null, oldEnabled); Input("initialized").SetValue(null, oldInitialized);
            CombatAudio.Configure(oldAudio); Application.targetFrameRate = oldRate; Application.runInBackground = oldBackground;
            QualitySettings.antiAliasing=oldAA;QualitySettings.anisotropicFiltering=oldAniso;Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom;
            if (autoTick != null) autoTick.GetMethod("SetAutoTick", All).Invoke(null, new object[] { oldTick, oldInterval, false });
            if (oldFocus != null) oldFocus.Focus();
            checks.Add("PASS restored scene roots, input, audio, time, RNG, frame target and AutoTick");
            if (scene.IsValid() && scene.isLoaded)
            { var unload = SceneManager.UnloadSceneAsync(scene); unload.completed += _ => { checks.Add("PASS isolated fixture scene unloaded"); WriteResult(result); }; return; }
        }
        catch (Exception error) { result += "\nRESTORE FAIL " + error; }
        WriteResult(result);
    }
    static void WriteResult(string result)
    {
        File.WriteAllText(Path.Combine(folder, "result.txt"), result + "\n" + string.Join("\n", checks) +
            "\nSupplied isolated weapons/player, actual TakeDamage and GameAction movement/attacks. No normal-campaign, listening, motion-smoothness or performance claim. PNG capture quality and visual review remain separate.\n", new UTF8Encoding(false));
        Debug.Log("[Goal hero hurt] " + result + " " + folder);
    }
}
