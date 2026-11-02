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
public static class CombatBossRegression20261009
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
    static bool running, paused, oldEnabled, oldInitialized, oldBackground, sealOnly, sealMotion, wardenOnly, allBossArt, tideOnly;
    static float oldScale;
    static int oldRate, oldInterval;
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
    static void Fresh(string weapon = "rust_sword", bool protect = true)
    {
        StageValidationInput.ReleaseAll(); StageValidationInput.BeginFrame();
        Remove(actor); actor = null; Remove(player);
        foreach (var hazard in UnityEngine.Object.FindObjectsByType<ArenaHazard>()) Remove(hazard);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<BossRuneProjectile>()) Remove(shot);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<EnemyProjectile>()) Remove(shot);
        foreach (var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>()) Remove(shot);
        bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find(weapon));
        player = PlayerController.Create(Vector2.zero, () => paused, () => false, () => { }, camera, bag, settings);
        player.IsTrainingCombat = true; player.EquipWeapon(bag.EquippedWeapon);
        if (protect) player.SetStageClearSafety(true);
        camera.Target = player.transform;
        StageValidationInput.SetAim(Vector2.right); Physics2D.SyncTransforms();
    }
    public static string Main()=>Start(false);
    public static string MainSealArt()=>Start(true);
    public static string MainSealMotion()=>Start(true,true);
    public static string MainWardenArt()=>Start(false,false,true);
    public static string MainWardenMotion()=>Start(false,true,true);
    public static string MainAllBossArt()=>Start(true,false,true,true);
    public static string MainTideMotion()=>Start(false,true,false,false,true);
    public static string MainSealLowRateRetest()=>Start(true,true,false,false,false,true);
    public static string MainWardenLowRateRetest()=>Start(false,true,true,false,false,true);
    public static string MainTideLowRateRetest()=>Start(false,true,false,false,true,true);
    static bool lowRateRetest;
    static string Start(bool seal,bool motion=false,bool warden=false,bool all=false,bool tide=false,bool lowRate=false)
    {
        if (running || !Application.isPlaying) throw new Exception("Play Mode required; run one fixture at a time.");
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (game == null || game.IsRunActive) throw new Exception("Start at main menu; existing campaign must not be replaced.");
        sealOnly=seal;sealMotion=motion;wardenOnly=warden;allBossArt=all;tideOnly=tide;lowRateRetest=lowRate;
        if(motion&&Time.captureFramerate!=0)throw new Exception("Actual-clock capture requires Time.captureFramerate == 0.");
        folder = Path.Combine("F:/codex/LoglikeCap/Logs/Validation/2026-10-09-"+(allBossArt?"boss-art":tideOnly?"tide-motion":wardenOnly?(sealMotion?"warden-motion":"warden"):sealMotion?"sealcore-motion":sealOnly?"sealcore":"combat"), DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(folder); checks.Clear(); roots.Clear(); inputs.Clear();
        originalScene = SceneManager.GetActiveScene(); oldFocus = EditorWindow.focusedWindow;
        oldScale = Time.timeScale; oldRate = Application.targetFrameRate; oldBackground = Application.runInBackground;
        oldRandom = UnityEngine.Random.state; oldEnabled = (bool)Input("enabled").GetValue(null);
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
            scene = SceneManager.CreateScene("GoalCombat-SuppliedFixture"); SceneManager.SetActiveScene(scene);
            camera = CameraFollow.Create(); camera.FocusRoom(Vector2.zero, new Vector2(100, 100), true);
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            checks.Add("DATA render pipeline=" + (pipeline == null ? "Built-in" : pipeline.GetType().FullName));
            EditorApplication.update += Watchdog; EditorApplication.playModeStateChanged += PlayState;
            camera.StartCoroutine(Guard(allBossArt?RunAllBossArt():tideOnly?RunTideMotion():wardenOnly?(sealMotion?RunWardenMotion():RunWardenArt()):sealMotion?RunSealMotion():sealOnly?RunSealArt():Run()));
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
    static IEnumerator Run()
    {
        yield return Frame(); Fresh(); yield return Frame();
        actor = EnemyController.Create(EnemyType.Tidekeeper, new Vector2(-4, 0), player, () => paused, _ => { }, null);
        Set(actor, "specialTimer", 999f);
        for (int direction = 0; direction < 8; direction++)
        foreach (float travel in new[] { 0f, 3.1f })
        {
            var aim = new Vector2(Mathf.Cos(direction * Mathf.PI / 4), Mathf.Sin(direction * Mathf.PI / 4));
            var origin = new Vector2(-2, -2);
            var hazard = ArenaHazard.CreateChargeRoute(actor, player, origin, aim, travel, 1, .5f, Color.cyan);
            for (int along = 0; along < 9; along++) for (int radial = 0; radial < 32; radial++)
            {
                var normal = new Vector2(Mathf.Cos(radial * Mathf.PI / 16), Mathf.Sin(radial * Mathf.PI / 16));
                var point = origin + aim * travel * along / 8 + normal * (EnemyController.ChargeContactRadius - .001f);
                if (!hazard.Contains(point)) throw new Exception("Charge warning omits an actual contact disk " + direction + "/" + travel);
            }
            var side = new Vector2(-aim.y, aim.x);
            Check(!hazard.Contains(origin + aim * travel * .5f + side * (EnemyController.ChargeContactRadius + .01f)),
                "charge warning includes swept contact and excludes outside width " + direction + "/" + travel);
            Remove(hazard);
        }
        foreach (bool second in new[] { false, true })
        foreach (var action in new[] { CampaignBossAction.TideLanes, CampaignBossAction.TidePools, CampaignBossAction.TideFan, CampaignBossAction.TideSurge })
        { yield return TideAction(action, second); }
        yield return ChargeWall();
        foreach (var weapon in new[] { "returning_edge", "gravity_maul", "tether_carbine", "bastion_shotgun" })
            yield return WeaponSockets(weapon);
    }
    static void NativeReviewFloor()
    {
        if(GameObject.Find("SuppliedNativeBossFloor")!=null||GameObject.Find("SuppliedNativeCoreFloor")!=null)return;
        var floor=new GameObject("SuppliedNativeBossFloor");var ground=floor.AddComponent<SpriteRenderer>();
        ground.sprite=IndividualArt.Sprite("External/ChampiDungeon/Ground");ground.drawMode=SpriteDrawMode.Tiled;
        ground.size=new Vector2(30,18);ground.sortingLayerName="WorldFloor";ground.sortingOrder=0;ground.sharedMaterial=NativeCoreGround.Material;
    }
    static IEnumerator PausedCapture(string name)
    {
        bool before=paused;float scale=Time.timeScale;paused=true;Time.timeScale=0;
        yield return Frame();Capture(name);Time.timeScale=scale;paused=before;
    }
    static void WardenActor(bool second=false)
    {
        Fresh();actor=EnemyController.Create(EnemyType.Boss,new Vector2(-2,0),player,()=>paused,_=>{},null);
        actor.ConfigureArena(new Rect(-9,-5,18,10));Set(actor,"specialTimer",999f);Set(actor,"speed",0f);
        player.Teleport(new Vector2(second?-7:4,0));
        if(second)typeof(EnemyController).GetProperty("Health",All).SetValue(actor,actor.MaxHealth*.4f);
        Physics2D.SyncTransforms();
    }
    static void BeginWardenAction(BossPose pose,bool second=false)
    {
        string method=pose==BossPose.Radial?"BossRadialBurst":pose==BossPose.Volley?"BossAimedVolley":pose==BossPose.Charge?"ChargeAttack":"LeapSlam";
        object[] args=pose==BossPose.Charge||pose==BossPose.Leap?new object[]{second?Vector2.left:Vector2.right,true}:null;
        actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(method,All).Invoke(actor,args));
    }
    static IEnumerator RunWardenArt()
    {
        MotionResolution();for(int i=0;i<5;i++)yield return Frame();
        Check(Screen.width==1280&&Screen.height==720,"warden art review is actual1280x720");NativeReviewFloor();
        var unique=new HashSet<Sprite>();
        foreach(string clip in new[]{"00","01","02","03","04","05","06","07","entrance"})
        foreach(var sprite in IndividualArt.Frames(BossSpriteAnimator.FrameRoot+clip,8))
        {
            Check(sprite.rect.size==new Vector2(112,112)&&sprite.pivot==new Vector2(64,8)&&sprite.pixelsPerUnit==32&&sprite.texture.filterMode==FilterMode.Point,
                "reviewed warden individual geometry "+clip+"/"+sprite.name);unique.Add(sprite);
            var bounds=SpriteVisibleBounds.Local(sprite);
            Check(Mathf.Abs(bounds.min.y)<.001f&&bounds.size.x>1.5f&&bounds.size.x<3&&bounds.size.y>.75f&&bounds.size.y<2.1f,
                "GPU-visible native body stays on reviewed ground pivot "+clip+"/"+sprite.name);
        }
        Check(unique.Count==72,"seventy-two individually imported poses including separate awakening/hurt/collapse");
        foreach(bool second in new[]{false,true})
        foreach(var pose in new[]{BossPose.Radial,BossPose.Volley,BossPose.Charge,BossPose.Leap})
        {
            WardenActor(second);yield return Frame();var animation=actor.GetComponent<BossSpriteAnimator>();
            var view=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            var foot=actor.GetComponent<BoxCollider2D>();var size=foot.size;var offset=foot.offset;
            BeginWardenAction(pose,second);var shown=new HashSet<int>();var phases=new HashSet<int>();
            var prep=new HashSet<int>();var active=new HashSet<int>();var recovery=new HashSet<int>();var shots=new HashSet<EnemyProjectile>();
            float started=Time.time;int previous=-1;bool hit=false;
            while(actor.IsActing&&Time.time-started<6)
            {
                int phase=actor.AttackVisualPhase,index=animation.FrameIndex;shown.Add(index);phases.Add(phase);
                if(phase==0)prep.Add(index);else if(phase==1)active.Add(index);else recovery.Add(index);
                Check(animation.Pose==pose&&view.sprite==IndividualArt.Frames(BossSpriteAnimator.FrameRoot+((int)pose).ToString("D2"),8)[index],"event-driven warden body "+pose+"/"+phase+"/"+index);
                if(foot.size!=size||foot.offset!=offset)throw new Exception("Animation resized movement collider");
                foreach(var shot in UnityEngine.Object.FindObjectsByType<EnemyProjectile>())
                {
                    if(!shots.Add(shot))continue;
                    Check(phase!=0,"no damage projectile before warden release "+pose);
                    Check(shot.Art==(pose==BossPose.Radial?EnemyProjectileArt.BossRadial:EnemyProjectileArt.BossAimed),"authored shot matches action "+pose);
                }
                if(!hit&&phase==0&&Time.time-started>.12f){actor.TakeDamage(1,Vector2.zero);hit=true;Check(animation.Pose==pose,"hurt does not hide attack tell "+pose);}
                if(phase!=previous)
                {
                    var before=view.sprite;float clock=Time.time;yield return PausedCapture(pose+"-"+second+"-phase"+phase);
                    Check(view.sprite==before&&Time.time==clock,"pause preserves rendered warden action "+pose+"/"+phase);previous=phase;
                }
                yield return Frame();
            }
            Check(!actor.IsActing&&phases.SetEquals(new[]{0,1,2})&&shown.Count==8,"all eight poses and phases complete "+pose+"/"+second);
            Check(prep.IsSupersetOf(pose==BossPose.Radial||pose==BossPose.Volley?new[]{0,1,2}:new[]{0,1}),"complete preparation "+pose);
            Check(active.IsSupersetOf(pose==BossPose.Radial?new[]{3,4}:pose==BossPose.Volley?new[]{3,4,5}:new[]{2,3,4}),"complete active poses "+pose);
            Check(recovery.IsSupersetOf(pose==BossPose.Volley?new[]{6,7}:new[]{5,6,7}),"complete recovery "+pose);
            Check(shots.Count==(pose==BossPose.Radial?(second?24:9):pose==BossPose.Volley?(second?12:9):0),"actual authored projectile count "+pose+"/"+second);
            Check(actor.transform.Find("VisualRoot").localPosition==new Vector3(0,-.35f,0),"body returns to original foot anchor "+pose);
            if(pose!=BossPose.Radial)Check(view.flipX==second,"committed screen direction "+pose+"/"+second);
        }
        WardenActor();yield return Frame();var renderer=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var seen=new HashSet<int>();actor.SetIntroLocked(true);float beganAt=Time.time;
        while(Time.time-beganAt<1.2f)
        {actor.SampleEntrance(Time.time-beganAt);seen.Add(Array.IndexOf(IndividualArt.Frames(BossSpriteAnimator.FrameRoot+"entrance",8),renderer.sprite));yield return Frame();}
        Check(seen.IsSupersetOf(new[]{0,1,2,3,4,5,6,7}),"eight separate kneel-to-awake poses render");
        actor.SetIntroLocked(false);yield return Wait(.1f);seen.Clear();actor.TakeDamage(1,Vector2.zero);beganAt=Time.time;
        while(Time.time-beganAt<.4f){seen.Add(Array.IndexOf(IndividualArt.Frames(BossSpriteAnimator.FrameRoot+"06",8),renderer.sprite));yield return Frame();}
        Check(seen.IsSupersetOf(new[]{0,1,2,3,4,5,6,7}),"actual hit renders all eight physical recoil poses");
        yield return PausedCapture("warden-recovered-next-to-approved-hero");
        int deaths=0;Set(actor,"onDied",(Action<EnemyController>)(_=>deaths++));actor.TakeDamage(actor.MaxHealth*100,Vector2.zero);seen.Clear();bool stopped=false;beganAt=Time.time;
        while(actor!=null&&Time.time-beganAt<2)
        {
            int index=Array.IndexOf(IndividualArt.Frames(BossSpriteAnimator.FrameRoot+"07",8),renderer.sprite);seen.Add(index);
            if(!stopped&&index>=3){stopped=true;yield return PausedCapture("warden-dedicated-collapse");Check(deaths==0,"paused collapse does not settle reward");}
            yield return Frame();
        }
        Check(seen.IsSupersetOf(new[]{0,1,2,3,4,5,6,7})&&actor==null&&deaths==1,"complete dedicated collapse and exactly one death callback");
        Fresh();yield return Frame();actor=EnemyController.Create(EnemyType.Tidekeeper,new Vector2(-2,0),player,()=>paused,_=>{},null);
        Set(actor,"specialTimer",999f);actor.SetIntroLocked(true);renderer=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();seen.Clear();beganAt=Time.time;int lastAppearance=14;
        while(Time.time-beganAt<1.2f)
        {
            actor.SampleEntrance(Time.time-beganAt);int index=Array.IndexOf(TidekeeperArt.Frames(3),renderer.sprite);seen.Add(index);
            if(index>=0){Check(index<=lastAppearance,"tide reassembly order never reverses");lastAppearance=index;}yield return Frame();
        }
        Check(seen.IsSupersetOf(new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13}),"all fourteen creator-authored appearance poses render in reverse order");
        actor.SetIntroLocked(false);yield return PausedCapture("tide-authored-appearance-complete");
    }
    static IEnumerator RunAllBossArt()
    {
        yield return RunSealArt();yield return RunWardenArt();
        foreach(bool second in new[]{false,true})
        foreach(var action in new[]{CampaignBossAction.TideLanes,CampaignBossAction.TidePools,CampaignBossAction.TideFan,CampaignBossAction.TideSurge})
            yield return TideAction(action,second);
        Fresh();yield return Frame();int deaths=0;
        actor=EnemyController.Create(EnemyType.Tidekeeper,new Vector2(-2,0),player,()=>paused,_=>deaths++,null);
        Set(actor,"specialTimer",999f);Set(actor,"speed",0f);var renderer=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var seen=new HashSet<int>();actor.TakeDamage(1,Vector2.zero);float start=Time.time;
        while(Time.time-start<.4f){seen.Add(Array.IndexOf(IndividualArt.Frames(TidekeeperArt.Root+"immune",8),renderer.sprite));yield return Frame();}
        Check(seen.IsSupersetOf(new[]{0,1,2,3}),"tide physical fold recoil uses authored guard poses without attack masking");
        actor.TakeDamage(actor.MaxHealth*100,Vector2.zero);seen.Clear();start=Time.time;
        while(actor!=null&&Time.time-start<2)
        {seen.Add(Array.IndexOf(TidekeeperArt.Frames(3),renderer.sprite));yield return Frame();}
        Check(seen.IsSupersetOf(new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13})&&actor==null&&deaths==1,"tide fourteen-pose authored defeat and one reward callback");
    }
    static IEnumerator RunSealArt()
    {
        MotionResolution();for(int i=0;i<5;i++)yield return Frame();
        Check(Screen.width==1280&&Screen.height==720,"art review is actual 1280x720 Game View");
        SealHeartArt.Preload();var unique=new HashSet<Sprite>();
        foreach(string clip in new[]{"idle","move","cast","sweep","charge","hurt","death","entrance"})
        foreach(var sprite in SealHeartArt.Clip(clip))
        {
            Check(sprite.rect.size==new Vector2(80,80)&&sprite.pivot==new Vector2(40,8)&&sprite.pixelsPerUnit==32&&sprite.texture.filterMode==FilterMode.Point,
                "reviewed individual sprite geometry "+clip+"/"+sprite.name);
            unique.Add(sprite);
        }
        Check(unique.Count==60,"sixty separately imported body poses; no action reused as death");
        var floor=new GameObject("SuppliedNativeCoreFloor");
        var ground=floor.AddComponent<SpriteRenderer>();ground.sprite=IndividualArt.Sprite("External/ChampiDungeon/Ground");
        ground.drawMode=SpriteDrawMode.Tiled;ground.size=new Vector2(24,14);ground.sortingLayerName="WorldFloor";ground.sortingOrder=0;
        ground.sharedMaterial=NativeCoreGround.Material;
        foreach(bool second in new[]{false,true})
        foreach(var action in new[]{CampaignBossAction.SealSweep,CampaignBossAction.SealCharge,CampaignBossAction.SealCross,CampaignBossAction.SealVolley})
            yield return SealAction(action,second);

        Fresh();yield return Frame();int deaths=0;
        actor=EnemyController.Create(EnemyType.SealHeart,new Vector2(-2,0),player,()=>paused,_=>deaths++,null);
        Set(actor,"specialTimer",999f);Set(actor,"speed",0f);
        var view=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var observed=new HashSet<int>();actor.SetIntroLocked(true);
        float started=Time.time;
        while(Time.time-started<1.2f)
        {
            actor.SampleEntrance(Time.time-started);yield return Frame();
            observed.Add(Array.IndexOf(SealHeartArt.Clip("entrance"),view.sprite));
        }
        Check(observed.IsSupersetOf(new[]{0,1,2,3,4,5,6,7}),"all eight unique assembly poses render");
        actor.SetIntroLocked(false);yield return Wait(.1f);observed.Clear();
        actor.TakeDamage(1,Vector2.zero);started=Time.time;
        while(Time.time-started<.4f)
        {yield return Frame();observed.Add(Array.IndexOf(SealHeartArt.Clip("hurt"),view.sprite));}
        Check(observed.IsSupersetOf(new[]{0,1,2,3,4,5,6,7}),"actual damage renders all eight dedicated physical recoil poses");
        Capture("core-recovered-next-to-approved-hero");
        actor.TakeDamage(actor.MaxHealth*100,Vector2.zero);observed.Clear();started=Time.time;bool frozen=false;
        while(actor!=null&&Time.time-started<2)
        {
            yield return Frame();if(actor==null)break;
            int index=Array.IndexOf(SealHeartArt.Clip("death"),view.sprite);
            if(observed.Add(index))Capture("core-death-"+index);
            if(index>=0&&index<=6&&view.color.a<=.98f)throw new Exception("Dedicated falling body fades before rubble "+index);
            if(!frozen&&index>=2)
            {
                frozen=true;paused=true;Time.timeScale=0;var before=view.sprite;float health=actor.Health;
                yield return new WaitForSecondsRealtime(.06f);yield return Frame();
                Check(view.sprite==before&&actor.Health==health&&deaths==0,"pause freezes collapse and reward callback");
                Time.timeScale=1;paused=false;
            }
        }
        Check(observed.IsSupersetOf(new[]{0,1,2,3,4,5,6,7}),"all eight dedicated death poses render before removal");
        Check(actor==null&&deaths==1,"one boss death callback and complete body cleanup");
        yield return Frame();
        Check(UnityEngine.Object.FindObjectsByType<SealGroundAttack>().Length==0&&UnityEngine.Object.FindObjectsByType<BossRuneProjectile>().Length==0,
            "no seal ground attacks or hostile runes remain after fixture death");
    }
    static IEnumerator SealAction(CampaignBossAction action,bool second)
    {
        Fresh();yield return Frame();
        actor=EnemyController.Create(EnemyType.SealHeart,new Vector2(-2,0),player,()=>paused,_=>{},null);
        actor.ConfigureArena(new Rect(-9,-5,18,10));Set(actor,"specialTimer",999f);Set(actor,"speed",0f);
        if(second)typeof(EnemyController).GetProperty("Health",All).SetValue(actor,actor.MaxHealth*.4f);
        var direction=second?Vector2.up:Vector2.right;
        actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(action.ToString(),All).Invoke(actor,
            action==CampaignBossAction.SealCross?null:new object[]{direction}));
        var frames=SealHeartArt.Clip(action==CampaignBossAction.SealCharge?"charge":action==CampaignBossAction.SealSweep?"sweep":"cast");
        var view=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var preparation=new HashSet<int>();var release=new HashSet<int>();var recovery=new HashSet<int>();var phases=new HashSet<int>();
        int previous=-1;bool checkedGround=false,hit=false;float started=Time.time;
        while(actor.IsActing&&Time.time-started<8)
        {
            yield return Frame();int phase=actor.AttackVisualPhase,index=Array.IndexOf(frames,view.sprite);phases.Add(phase);
            if(phase==0)preparation.Add(index);else if(phase==1)release.Add(index);else recovery.Add(index);
            if(phase!=previous){Capture(action+"-"+second+"-phase"+phase);previous=phase;}
            if(!hit&&phase==0&&Time.time-started>.25f){actor.TakeDamage(1,Vector2.zero);hit=true;}
            if(Array.IndexOf(SealHeartArt.Clip("hurt"),view.sprite)>=0)throw new Exception("Hurt hides committed seal action "+action+"/"+second);
            if(!checkedGround&&phase==1&&(action==CampaignBossAction.SealSweep||action==CampaignBossAction.SealCross))
            {
                var effects=UnityEngine.Object.FindObjectsByType<SealGroundAttack>();
                if(effects.Length>0)
                {
                    bool any=false;int visiblePixels=0;
                    foreach(var effect in effects)
                    {
                        var hazard=effect.GetComponentInParent<ArenaHazard>();if(hazard==null||!hazard.IsActive)continue;
                        var mesh=effect.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var colors=mesh.colors;
                        Check(effect.GetComponent<MeshRenderer>().sortingLayerName=="WorldFloor","seal ground attack is below actors");
                        for(int i=0;i<colors.Length;i+=4)if(colors[i].a>.01f)
                        {
                            any=true;visiblePixels++;var point=effect.transform.TransformPoint((vertices[i]+vertices[i+2])*.5f);
                            if(!(bool)typeof(ArenaHazard).GetMethod("ContainsActive",All).Invoke(hazard,new object[]{(Vector2)point}))
                                throw new Exception("Visible active seal pixel center differs from damage footprint");
                        }
                    }
                    if(any){checkedGround=true;Check(true,visiblePixels+" visible ground pixels share actual damage footprint "+action+"/"+second);Capture(action+"-"+second+"-ground-active");}
                }
            }
        }
        Check(!actor.IsActing&&phases.SetEquals(new[]{0,1,2}),"seal action returns through all phases "+action+"/"+second);
        Check(preparation.IsSupersetOf(new[]{0,1,2,3}),"four preparation poses render "+action+"/"+second);
        Check(release.IsSupersetOf(action==CampaignBossAction.SealCharge?new[]{4}:new[]{4,5}),"committed release poses render "+action+"/"+second);
        Check(recovery.IsSupersetOf(action==CampaignBossAction.SealCharge?new[]{5,6,7}:new[]{6,7}),"physical recovery poses render "+action+"/"+second);
        Check(hit,"actual damage during action preserved committed poses "+action+"/"+second);
        if(action==CampaignBossAction.SealSweep||action==CampaignBossAction.SealCross)Check(checkedGround,"visible ground energy checked during active damage "+action+"/"+second);
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>())
            if(renderer.name.Contains("SealSpikes")||renderer.name.Contains("TwinCrescent"))throw new Exception("Rejected duplicated crescent/orange spike remains");
    }
    static IEnumerator TideAction(CampaignBossAction action, bool second)
    {
        Fresh(); yield return Frame();
        actor = EnemyController.Create(EnemyType.Tidekeeper, new Vector2(-4, 0), player, () => paused, _ => { }, null);
        actor.ConfigureArena(new Rect(-9, -5, 18, 10));
        Set(actor, "specialTimer", 999f);
        if (second) typeof(EnemyController).GetProperty("Health", All).SetValue(actor, actor.MaxHealth * .4f);
        string method = action == CampaignBossAction.TidePools ? "CallTide" : action.ToString();
        object[] arguments = action == CampaignBossAction.TidePools ? new object[] { true } :
            action == CampaignBossAction.TideLanes ? null : new object[] { Vector2.right };
        actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(method, All).Invoke(actor, arguments));
        var cast = IndividualArt.Frames(TidekeeperArt.Root +
            (action == CampaignBossAction.TideLanes || action == CampaignBossAction.TideFan ? "shield_cast" : "laser_cast"),
            action == CampaignBossAction.TideLanes || action == CampaignBossAction.TideFan ? 10 : 7);
        var renderer = actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var preparation = new HashSet<int>(); var release = new HashSet<int>(); var recovery = new HashSet<int>(); var phases = new HashSet<int>();
        float started = Time.time; int previous = -1; bool pausedOnce = false;
        while (actor.IsActing && Time.time - started < 8)
        {
            yield return Frame(); int phase = actor.AttackVisualPhase;
            phases.Add(phase); int index = Array.IndexOf(cast, renderer.sprite);
            if (phase == 0) preparation.Add(index); if (phase == 1) release.Add(index);
            if (phase == 2) recovery.Add(index);
            if (phase != previous)
            {
                checks.Add("DATA " + action + "/" + second + " phase=" + phase + " at=" + (Time.time - started).ToString("F4"));
                previous = phase;
            }
            if (!pausedOnce && phase == 0 && Time.time - started > .25f)
            {
                pausedOnce = true; float oldTime = Time.time; var before = renderer.sprite;
                paused = true; Time.timeScale = 0;
                yield return new WaitForSecondsRealtime(.06f); yield return Frame();
                Check(Time.time == oldTime && renderer.sprite == before, "pause freezes tide body and action clock " + action + "/" + second);
                Time.timeScale = 1; paused = false;
            }
        }
        Check(!actor.IsActing && phases.SetEquals(new[] { 0, 1, 2 }), "tide action returns through all phases " + action + "/" + second);
        Check(preparation.Contains(0) && preparation.Contains(1) && preparation.Contains(2) && preparation.Contains(3),
            "all four authored preparation poses render before release " + action + "/" + second);
        Check(release.Contains(4) && release.Contains(5) && release.Contains(6),
            "all three authored release poses render on action clock " + action + "/" + second);
        Check(recovery.IsSupersetOf(action==CampaignBossAction.TideLanes||action==CampaignBossAction.TideFan?new[]{7,8,9}:new[]{0,1,2,3,4,5,6}),
            "complete authored recovery renders before return to hover "+action+"/"+second);
        Capture(action + "-recovery-" + second);
    }
    static IEnumerator ChargeWall()
    {
        // Start this contact test without presentation protection. Disabling
        // clear safety intentionally keeps its paid two-second invulnerability;
        // a fixture must not mistake that preserved grace period for a missed hit.
        Fresh(protect: false); yield return Frame();
        actor = EnemyController.Create(EnemyType.Tidekeeper, new Vector2(-.8f, 0), player, () => false, _ => { }, null);
        Set(actor, "acting", true); Set(actor, "collisionAttackActive", true); Set(actor, "specialTimer", 999f);
        var wall = new GameObject("SuppliedChargeContactCover"); wall.layer = GameLayers.Ground;
        wall.transform.position = new Vector2(-.4f, 0); wall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 4);
        Physics2D.SyncTransforms(); float health = player.Health;
        yield return Frame(); yield return Frame();
        Check(player.Health == health, "nearby charging body cannot hit through solid ground");
        wall.SetActive(false); UnityEngine.Object.Destroy(wall); Physics2D.SyncTransforms();
        yield return Frame(); yield return Frame();
        Check(player.Health < health, "the same uncovered charge contact still damages");
    }
    static IEnumerator WeaponSockets(string id)
    {
        // No hostile actors are present here. Stage-clear safety also blocks
        // combat input, so socket/firing cases must use an ordinary player.
        Fresh(id, protect: false); yield return Wait(.35f);
        var weapon = player.EquippedWeapon; var held = player.GetComponent<HeldWeaponView>();
        var animation = player.GetComponent<PlayerAnimationController>();
        var pivot = player.transform.Find("HeldWeaponPivot");
        var renderer = pivot.Find("HeldWeaponSprite").GetComponent<SpriteRenderer>();
        Check(NativeWeaponArt.TryGet(id, out var pose), "new weapon has native grip definition " + id);
        Check(held.RendererSprite.texture == ItemIconLibrary.Get(id, InventoryIconKind.Weapon), "held weapon and icon share reviewed source " + id);
        Check(held.RendererSprite.pixelsPerUnit == 32 && held.RendererSprite.texture.filterMode == FilterMode.Point,
            "new weapon preserves native pixel import " + id);
        bool ranged = weapon.AttackType == WeaponAttackType.Ranged;
        if (ranged) Check(RangedWeaponArt.TryGet(id, out _), "post-failure muzzle mapping exists " + id);
        for (int direction = 0; direction < 8; direction++)
        {
            StageValidationInput.SetAim(new Vector2(Mathf.Sin(direction * Mathf.PI / 4), Mathf.Cos(direction * Mathf.PI / 4)));
            yield return Wait(.25f);
            Check(Vector2.Distance(pivot.localPosition, animation.HandLocalPosition) < .001f && Mathf.Approximately(pivot.localScale.x, 1),
                "native grip stays at body hand " + id + "/" + direction);
            if (ranged)
            {
                int previousSequence = player.AttackAnimationSequence, previousAmmo = bag.ActiveSlot.Ammo;
                StageValidationInput.SetHeld(GameAction.Attack, true);
                float until = Time.time + 1;
                while (player.AttackAnimationSequence == previousSequence && Time.time < until) yield return Frame();
                StageValidationInput.SetHeld(GameAction.Attack, false);
                Check(player.AttackAnimationSequence == previousSequence + 1 && bag.ActiveSlot.Ammo == previousAmmo - 1,
                    "actual input fires exactly one ammo-using attack " + id + "/" + direction);
                var sprite = renderer.sprite;
                var socket = new Vector2(pose.Muzzle.x - sprite.pivot.x, sprite.rect.height - pose.Muzzle.y - sprite.pivot.y) / sprite.pixelsPerUnit;
                if (renderer.flipY) socket.y = -socket.y;
                var renderedMuzzle = (Vector2)renderer.transform.TransformPoint(socket);
                Check(Vector2.Distance(renderedMuzzle, Read<Vector2>(player, "committedShotOrigin")) < .035f,
                    "fired origin matches rendered barrel within one source pixel " + id + "/" + direction);
                int shots = 0;
                foreach (var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())
                    if (Read<int>(shot, "sourceAction") == player.AttackAnimationSequence)
                    {
                        shots++;
                        Check(Vector2.Distance(shot.transform.position, renderedMuzzle) < .035f,
                            "actual projectile begins at rendered barrel " + id + "/" + direction + "/" + shots);
                    }
                Check(shots == (weapon.Family == WeaponFamily.Shotgun ? 3 : 1), "expected actual projectile fan " + id + "/" + direction);
                Capture(id + "-shot-" + direction);
                yield return Wait(.3f);
                Check(bag.StartReload(), "real partial-magazine reload starts " + id + "/" + direction);
                var poses = new HashSet<Sprite>(); float maximum = 0; until = Time.time + weapon.ReloadSeconds + 1;
                while (bag.ActiveSlot.IsReloading && Time.time < until)
                {
                    yield return Frame();
                    if (animation.PoseName.StartsWith("reload-"))
                    {
                        poses.Add(player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite);
                        maximum = Mathf.Max(maximum, animation.ReloadPresentationProgress);
                    }
                    if (Vector2.Distance(pivot.localPosition, animation.HandLocalPosition) > .001f)
                        throw new Exception("Reload grip detached " + id + "/" + direction);
                }
                Check(!bag.ActiveSlot.IsReloading && bag.ActiveSlot.Ammo == weapon.MagazineSize && poses.Count >= 6 && maximum > .9f,
                    "reload completes with authored poses and attached grip " + id + "/" + direction);
            }
            else Capture(id + "-held-" + direction);
        }
    }
    sealed class MotionFrame
    {public int Index;public double Realtime,Simulation;public int UnityFrame;public Color32[] Pixels;public string Body;public int Phase;}
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
        var preset=Activator.CreateInstance(sizeType,new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),1280,720,"Boss actual clock capture"});
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
    static void MotionActor()
    {
        Fresh();actor=EnemyController.Create(EnemyType.SealHeart,new Vector2(-2,0),player,()=>paused,_=>{},null);
        actor.ConfigureArena(new Rect(-9,-5,18,10));Set(actor,"specialTimer",999f);Set(actor,"speed",0f);
    }
    static IEnumerator RunSealMotion()
    {
        MotionResolution();motionQuality.Clear();motionQuality.Add("clip,frames,wallSeconds,simulationSeconds,actualFps,p95IntervalMs,maxIntervalMs,recordingQuality");
        for(int i=0;i<5;i++)yield return Frame();
        Check(Screen.width==1280&&Screen.height==720,"capture is actual 1280x720 Game View");
        var floor=new GameObject("SuppliedNativeCoreFloor");var ground=floor.AddComponent<SpriteRenderer>();
        ground.sprite=IndividualArt.Sprite("External/ChampiDungeon/Ground");ground.drawMode=SpriteDrawMode.Tiled;
        ground.size=new Vector2(24,14);ground.sortingLayerName="WorldFloor";ground.sortingOrder=0;ground.sharedMaterial=NativeCoreGround.Material;
        MotionActor();yield return Wait(.25f);yield return VerifyMotionReadback();
        foreach(var action in lowRateRetest?new[]{CampaignBossAction.SealSweep,CampaignBossAction.SealVolley}:new[]{CampaignBossAction.SealSweep,CampaignBossAction.SealCharge,CampaignBossAction.SealCross,CampaignBossAction.SealVolley})
        {
            MotionActor();yield return Wait(.3f);
            actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(action.ToString(),All).Invoke(actor,action==CampaignBossAction.SealCross?null:new object[]{Vector2.right}));
            yield return RecordMotion(action.ToString(),2.7f);FlushMotion(action.ToString());yield return Frame();
        }
        MotionActor();yield return Wait(.3f);actor.TakeDamage(1,Vector2.zero);
        yield return RecordMotion("hurt",.8f);FlushMotion("hurt");yield return Frame();
        MotionActor();yield return Wait(.3f);actor.TakeDamage(actor.MaxHealth*100,Vector2.zero);
        yield return RecordMotion("death",1.3f);FlushMotion("death");yield return Frame();
        File.WriteAllLines(Path.Combine(folder,"recording-quality.csv"),motionQuality,new UTF8Encoding(false));
        checks.Add("CAPTURED "+(lowRateRetest?"four targeted LOW_RATE recaptures":"six separately timestamped clips")+" at Time.timeScale=1 and Time.captureFramerate=0; compression occurred only after each clip.");
        checks.Add("Recording quality is measured, not an animation-smoothness or performance PASS. Inspect actual timestamps and 1x variable-frame-rate playback.");
    }
    static IEnumerator RunWardenMotion()
    {
        MotionResolution();motionQuality.Clear();motionQuality.Add("clip,frames,wallSeconds,simulationSeconds,actualFps,p95IntervalMs,maxIntervalMs,recordingQuality");
        for(int i=0;i<5;i++)yield return Frame();Check(Screen.width==1280&&Screen.height==720,"warden capture is actual1280x720");NativeReviewFloor();
        WardenActor();yield return Wait(.25f);yield return VerifyMotionReadback();
        if(lowRateRetest)
        {
            WardenActor();yield return Wait(.3f);actor.TakeDamage(1,Vector2.zero);
            yield return RecordMotion("hurt",.8f);FlushMotion("hurt");yield return Frame();
            checks.Add("Targeted Warden hurt LOW_RATE recapture only; no full regression or visual approval claim.");
            yield break;
        }
        foreach(var pose in new[]{BossPose.Radial,BossPose.Volley,BossPose.Charge,BossPose.Leap})
        {
            WardenActor();yield return Wait(.3f);BeginWardenAction(pose);
            yield return RecordMotion("Warden"+pose,2f);FlushMotion("Warden"+pose);yield return Frame();
        }
        WardenActor();yield return Wait(.3f);yield return RecordMotion("idle",1.4f);FlushMotion("idle");yield return Frame();
        WardenActor();yield return Wait(.3f);actor.enabled=false;actor.GetComponent<Rigidbody2D>().linearVelocity=Vector2.right*.65f;
        yield return RecordMotion("walk",1.1f);FlushMotion("walk");yield return Frame();
        WardenActor();yield return Wait(.3f);actor.SetIntroLocked(true);
        yield return RecordMotion("entrance",1.3f,t=>actor.SampleEntrance(t));FlushMotion("entrance");yield return Frame();
        WardenActor();yield return Wait(.3f);actor.TakeDamage(1,Vector2.zero);
        yield return RecordMotion("hurt",.8f);FlushMotion("hurt");yield return Frame();
        WardenActor();yield return Wait(.3f);actor.TakeDamage(actor.MaxHealth*100,Vector2.zero);
        yield return RecordMotion("death",1.3f);FlushMotion("death");yield return Frame();
        checks.Add("CAPTURED nine Warden clips at actual1x; walk is supplied physics velocity; entrance is the presentation owner's sampled clock; no new poses synthesized.");
        checks.Add("Recording quality is measured separately from visual acceptance; actual1x review remains required.");
    }
    static void MotionTideActor()
    {
        Fresh();actor=EnemyController.Create(EnemyType.Tidekeeper,new Vector2(-2,0),player,()=>paused,_=>{},null);
        actor.ConfigureArena(new Rect(-9,-5,18,10));Set(actor,"specialTimer",999f);Set(actor,"speed",0f);
    }
    static IEnumerator RunTideMotion()
    {
        MotionResolution();motionQuality.Clear();motionQuality.Add("clip,frames,wallSeconds,simulationSeconds,actualFps,p95IntervalMs,maxIntervalMs,recordingQuality");
        for(int i=0;i<5;i++)yield return Frame();Check(Screen.width==1280&&Screen.height==720,"tide capture is actual1280x720");NativeReviewFloor();
        MotionTideActor();yield return Wait(.25f);yield return VerifyMotionReadback();
        if(lowRateRetest)
        {
            MotionTideActor();yield return Wait(.3f);actor.SetIntroLocked(true);
            yield return RecordMotion("entrance",1.3f,t=>actor.SampleEntrance(t));FlushMotion("entrance");yield return Frame();
            checks.Add("Targeted Tide entrance LOW_RATE recapture only; no full regression or visual approval claim.");
            yield break;
        }
        foreach(var action in new[]{CampaignBossAction.TideLanes,CampaignBossAction.TidePools,CampaignBossAction.TideFan,CampaignBossAction.TideSurge})
        {
            MotionTideActor();yield return Wait(.3f);
            string method=action==CampaignBossAction.TidePools?"CallTide":action.ToString();
            object[] args=action==CampaignBossAction.TidePools?new object[]{true}:action==CampaignBossAction.TideLanes?null:new object[]{Vector2.right};
            actor.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(method,All).Invoke(actor,args));
            yield return RecordMotion(action.ToString(),3.3f);FlushMotion(action.ToString());yield return Frame();
        }
        MotionTideActor();yield return Wait(.3f);yield return RecordMotion("hover",1f);FlushMotion("hover");yield return Frame();
        MotionTideActor();yield return Wait(.3f);actor.SetIntroLocked(true);
        yield return RecordMotion("entrance",1.3f,t=>actor.SampleEntrance(t));FlushMotion("entrance");yield return Frame();
        MotionTideActor();yield return Wait(.3f);actor.TakeDamage(1,Vector2.zero);
        yield return RecordMotion("hurt",.8f);FlushMotion("hurt");yield return Frame();
        MotionTideActor();yield return Wait(.3f);actor.TakeDamage(actor.MaxHealth*100,Vector2.zero);
        yield return RecordMotion("death",1.3f);FlushMotion("death");yield return Frame();
        checks.Add("CAPTURED eight authored Tide clips at actual1x. Creator uses the same idle/moving hover strip; appearance reverses documented defeat/appearance strip.");
        checks.Add("Actual action clocks and every recovery pose are checked separately in MainAllBossArt. Capture quality never confers visual acceptance.");
    }
    static IEnumerator RecordMotion(string clip,float seconds,Action<float> sample=null)
    {
        motionFrames.Clear();motionPending=0;motionError=false;
        motionTarget=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);motionTarget.Create();
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
                Phase=actor==null?-1:actor.AttackVisualPhase,Body=actor==null?"destroyed":AssetDatabase.GetAssetPath(actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite)};
            motionFrames.Add(frame);ScreenCapture.CaptureScreenshotIntoRenderTexture(motionTarget);motionPending++;
            AsyncGPUReadback.Request(motionTarget,0,TextureFormat.RGBA32,r=>
            {if(r.hasError)motionError=true;else frame.Pixels=r.GetData<Color32>().ToArray();motionPending--;});
        }
        double deadline=Time.realtimeSinceStartupAsDouble+8;
        while(motionPending>0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(!motionError&&motionPending==0,"all real frame readbacks completed "+clip);
        motionTarget.Release();UnityEngine.Object.Destroy(motionTarget);motionTarget=null;
    }
    static void FlushMotion(string clip)
    {
        string path=Path.Combine(folder,clip);Directory.CreateDirectory(path);
        var csv=new StringBuilder("frame,realtime,simulation,unityFrame,phase,bodyAsset\n");
        var concat=new StringBuilder("ffconcat version 1.0\n");var intervals=new List<double>();
        var texture=new Texture2D(1280,720,TextureFormat.RGBA32,false);Color32[] corrected=motionFlip?new Color32[1280*720]:null;
        try
        {
            for(int i=0;i<motionFrames.Count;i++)
            {
                var f=motionFrames[i];if(f.Pixels==null)throw new Exception("Missing raw frame "+clip+"/"+i);
                csv.AppendLine(i+","+f.Realtime.ToString("F6",CultureInfo.InvariantCulture)+","+f.Simulation.ToString("F6",CultureInfo.InvariantCulture)+","+f.UnityFrame+","+f.Phase+","+f.Body);
                if(motionFlip){for(int y=0;y<720;y++)Array.Copy(f.Pixels,y*1280,corrected,(719-y)*1280,1280);texture.SetPixels32(corrected);}
                else texture.SetPixels32(f.Pixels);
                texture.Apply();string name="frame-"+i.ToString("D4")+".png";File.WriteAllBytes(Path.Combine(path,name),texture.EncodeToPNG());f.Pixels=null;
                double interval=i+1<motionFrames.Count?motionFrames[i+1].Realtime-f.Realtime:i>0?f.Realtime-motionFrames[i-1].Realtime:1d/60;
                concat.AppendLine("file '"+name+"'");concat.AppendLine("option framerate 1000000");concat.AppendLine("duration "+interval.ToString("F6",CultureInfo.InvariantCulture));
                if(i+1<motionFrames.Count)intervals.Add(interval);
            }
            concat.AppendLine("file 'frame-"+(motionFrames.Count-1).ToString("D4")+".png'");concat.AppendLine("option framerate 1000000");
            File.WriteAllText(Path.Combine(path,"timestamps.csv"),csv.ToString(),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(path,"actual-time.ffconcat"),concat.ToString(),new UTF8Encoding(false));
            intervals.Sort();double wall=motionFrames[motionFrames.Count-1].Realtime-motionFrames[0].Realtime;
            double simulation=motionFrames[motionFrames.Count-1].Simulation-motionFrames[0].Simulation;
            double fps=(motionFrames.Count-1)/Math.Max(.001,wall),p95=intervals[(int)Math.Min(intervals.Count-1,Math.Ceiling(intervals.Count*.95)-1)]*1000,max=intervals[intervals.Count-1]*1000;
            bool actualClock=Math.Abs(simulation-wall)<=Math.Max(.015,wall*.05);
            string quality=!actualClock?"CLOCK_MISMATCH_NO_ACTUAL_SPEED_PASS":fps>=50&&p95<=30&&max<=100?"SUFFICIENT_FOR_VISUAL_REVIEW":"LOW_RATE_NO_SMOOTHNESS_PASS";
            motionQuality.Add(string.Join(",",clip,motionFrames.Count,wall.ToString("F6",CultureInfo.InvariantCulture),simulation.ToString("F6",CultureInfo.InvariantCulture),fps.ToString("F3",CultureInfo.InvariantCulture),p95.ToString("F3",CultureInfo.InvariantCulture),max.ToString("F3",CultureInfo.InvariantCulture),quality));
            File.WriteAllLines(Path.Combine(folder,"recording-quality.csv"),motionQuality,new UTF8Encoding(false));
            checks.Add("DATA recording "+clip+" frames="+motionFrames.Count+" actualFPS="+fps.ToString("F2",CultureInfo.InvariantCulture)+" p95ms="+p95.ToString("F2",CultureInfo.InvariantCulture)+" maxms="+max.ToString("F2",CultureInfo.InvariantCulture)+" "+quality);
        }
        finally{UnityEngine.Object.DestroyImmediate(texture);motionFrames.Clear();}
    }
    static void Watchdog() { if (running && EditorApplication.timeSinceStartup - began > (sealMotion?600:180)) Finish("FAIL fixture watchdog"); }
    static void PlayState(PlayModeStateChange state) { if (running && state == PlayModeStateChange.ExitingPlayMode) Finish("INTERRUPTED Play ended"); }
    static void Finish(string result)
    {
        if (!running) return; running = false; EditorApplication.update -= Watchdog; EditorApplication.playModeStateChanged -= PlayState;
        if(sealOnly||wardenOnly||tideOnly)
        {
            try{RestoreMotion();}
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
            Time.timeScale = oldScale; UnityEngine.Random.state = oldRandom;
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
            "\nSupplied temporary actors/items, protected player during art checks, forced boss actions, actual GameAction shooting; no campaign, motion-smoothness, listening or performance claim.\n", new UTF8Encoding(false));
        Debug.Log("[Goal combat regression] " + result + " " + folder);
    }
}
