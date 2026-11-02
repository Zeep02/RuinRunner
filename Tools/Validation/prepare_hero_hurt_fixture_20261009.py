"""Reuse the already audited isolated-fixture/capture harness for hurt-only checks."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
base=(root/'Tools/Validation/CombatBossRegression20261009.cs').read_text(encoding='utf-8-sig')
head=base[:base.index('    public static string Main()=>Start(false);')]
head=head.replace('CombatBossRegression20261009','GoalHeroHurtMotion20261009')
head=head.replace('oldBackground, sealOnly, sealMotion, wardenOnly, allBossArt, tideOnly;', 'oldBackground, motionCapture, smoke, directionTransitions, priorityOnly;')
head=head.replace('bag = new InventorySystem(); bag.AddWeapon(WeaponLootTable.Find(weapon));','bag = new InventorySystem(); if(weapon!=null)bag.AddWeapon(WeaponLootTable.Find(weapon));')
head=head.replace('bool protect = true','bool protect = false')
head=head.replace('static int oldRate, oldInterval;', 'static int oldRate, oldInterval, oldAA;static AnisotropicFiltering oldAniso;')
start=base[base.index('    static string Start('):base.index('    static IEnumerator Run()')]
start=start.replace('static string Start(bool seal,bool motion=false,bool warden=false,bool all=false,bool tide=false)','static string Start(bool motion,bool quick=false,bool transitions=false,bool priorities=false)')
start=start.replace('sealOnly=seal;sealMotion=motion;wardenOnly=warden;allBossArt=all;tideOnly=tide;', 'motionCapture=motion;smoke=quick;directionTransitions=transitions;priorityOnly=priorities;')
a=start.index('        folder = Path.Combine(');b=start.index('\n        Directory.CreateDirectory',a)
start=start[:a]+'''        folder = Path.Combine("F:/codex/LoglikeCap/Logs/Validation/2026-10-09-goal/hero-hurt", (priorities?"priority-":transitions?"transitions-":smoke?"smoke-":motion?"motion-":"functional-")+DateTime.Now.ToString("HHmmss"));'''+start[b:]
start=start.replace('GoalCombat-SuppliedFixture','GoalHeroHurt-SuppliedFixture')
start=start.replace('oldRandom = UnityEngine.Random.state;', 'oldAA=QualitySettings.antiAliasing;oldAniso=QualitySettings.anisotropicFiltering;oldRandom = UnityEngine.Random.state;')
start=start.replace('camera.StartCoroutine(Guard(allBossArt?RunAllBossArt():tideOnly?RunTideMotion():wardenOnly?(sealMotion?RunWardenMotion():RunWardenArt()):sealMotion?RunSealMotion():sealOnly?RunSealArt():Run()));','camera.StartCoroutine(Guard(Run()));')
floor=base[base.index('    static void NativeReviewFloor()'):base.index('    static IEnumerator PausedCapture')]
motion=base[base.index('    sealed class MotionFrame'):base.index('    static void MotionActor()')]
motion=motion.replace('public int Phase;}','public int Phase;public string Telemetry;}')
record=base[base.index('    static IEnumerator RecordMotion('):base.index('    static void Watchdog()')]
record=record.replace('motionFrames.Clear();motionPending=0;motionError=false;', 'currentClip=clip;motionFrames.Clear();motionPending=0;motionError=false;')
record=record.replace('motionTarget=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);motionTarget.Create();','if(motionCapture){motionTarget=new RenderTexture(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);motionTarget.Create();}')
record=record.replace('Phase=actor==null?-1:actor.AttackVisualPhase,Body=actor==null?"destroyed":AssetDatabase.GetAssetPath(actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite)', 'Phase=Animation.FrameIndex,Body=AssetDatabase.GetAssetPath(Body.sprite)+"#"+Body.sprite.name,Telemetry=Observe(clip)')
record=record.replace('motionFrames.Add(frame);ScreenCapture.CaptureScreenshotIntoRenderTexture(motionTarget);motionPending++;','motionFrames.Add(frame);if(!motionCapture)continue;ScreenCapture.CaptureScreenshotIntoRenderTexture(motionTarget);motionPending++;')
record=record.replace('motionTarget.Release();UnityEngine.Object.Destroy(motionTarget);motionTarget=null;', 'if(motionTarget!=null){motionTarget.Release();UnityEngine.Object.Destroy(motionTarget);motionTarget=null;}')
record=record.replace('string path=Path.Combine(folder,clip);Directory.CreateDirectory(path);','string path=Path.Combine(folder,clip);Directory.CreateDirectory(path);')
record=record.replace('unityFrame,phase,bodyAsset\\n','unityFrame,key,bodyAsset,pose,direction,hurtProgress,health,handX,handY,behind,sequence,cancelSequence,contacts,ammo,reloadRemaining,x,y,weaponAngle,gameFocused,delta\\n')
record=record.replace('if(f.Pixels==null)throw new Exception', 'if(motionCapture&&f.Pixels==null)throw new Exception')
record=record.replace('+f.Phase+","+f.Body);','+f.Phase+","+f.Body+","+f.Telemetry);')
record=record.replace('                if(motionFlip){for(int y=0;y<720;', '                if(motionCapture)\n                {\n                if(motionFlip){for(int y=0;y<720;')
record=record.replace('f.Pixels=null;\n                double interval', 'f.Pixels=null;\n                }\n                string frameName="frame-"+i.ToString("D4")+".png";\n                double interval')
record=record.replace('"file \'"+name+"\'"','"file \'"+frameName+"\'"')
record=record.replace('File.WriteAllText(Path.Combine(path,"actual-time.ffconcat"),concat.ToString(),new UTF8Encoding(false));','if(motionCapture)File.WriteAllText(Path.Combine(path,"actual-time.ffconcat"),concat.ToString(),new UTF8Encoding(false));')
record=record.replace('quality=fps>=50&&p95<=30&&max<=100?', 'quality=!motionCapture?"TELEMETRY_ONLY_NO_VIDEO":fps>=50&&p95<=30&&max<=100?')
record=record.replace('intervals.Sort();double wall=', 'Ensure(motionFrames.Count>=2,"Capture has fewer than two real frames; no timing claim "+clip);intervals.Sort();double wall=')
finish=base[base.index('    static void Watchdog()'):]
finish=finish.replace('(sealMotion?600:180)','1800')
finish=finish.replace('if(sealOnly||wardenOnly||tideOnly)','if(true)')
finish=finish.replace('try{RestoreMotion();}', 'try{if(motionFrames.Count>0){AsyncGPUReadback.WaitAllRequests();FlushMotion("partial-"+currentClip);}RestoreMotion();}')
finish=finish.replace('Time.timeScale = oldScale;', 'QualitySettings.antiAliasing=oldAA;QualitySettings.anisotropicFiltering=oldAniso;Time.timeScale = oldScale;')
finish=finish.replace('Supplied temporary actors/items, protected player during art checks, forced boss actions, actual GameAction shooting; no campaign, motion-smoothness, listening or performance claim.', 'Supplied isolated weapons/player, actual TakeDamage and GameAction movement/attacks. No normal-campaign, listening, motion-smoothness or performance claim. PNG capture quality and visual review remain separate.')
finish=finish.replace('[Goal combat regression]','[Goal hero hurt]')
custom=r'''
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
        bool applied=false;yield return RecordMotion("priority-"+(dash?"dash-":"guard-")+id,.12f,t=>{
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
        if(directionTransitions)
        {
            foreach(string id in new[]{"worn_dagger","rust_sword","iron_greatsword","hunter_bow","scrap_pistol","tether_carbine",null})
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
        var ids=smoke?new[]{"rust_sword","hunter_bow","tether_carbine","bastion_shotgun",null}:new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword","hunter_bow","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon","tether_carbine","bastion_shotgun",null};
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
'''
result=head+'    public static string Main()=>Start(false);\n    public static string MainMotion()=>Start(true);\n    public static string MainSmoke()=>Start(true,true);\n    public static string MainDirectionTransitions()=>Start(true,false,true);\n    public static string MainPriorityMotion()=>Start(true,false,false,true);\n'+start+floor+custom+motion+record+finish
result=result.replace('Boss actual clock capture','Hero hurt actual clock capture')
target=root/'Tools/Validation/GoalHeroHurtMotion20261009.cs';target.write_text(result,encoding='utf-8')
print(str(target))
