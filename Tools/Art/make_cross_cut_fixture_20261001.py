"""Actual two-stroke finishing input, source/body/camera and cancellation review."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/ThrustFxPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Ready(')]
t=t.replace('ThrustFxPresentation20261001','CrossCutPresentation20261001').replace('thrust-fx/presentation-','cross-cut/presentation-')
a=t.index('    static readonly HashSet<int> fxKeys');b=t.index('    static void Require(',a)
t=t[:a]+r'''    static readonly HashSet<int>[] fxKeys={new HashSet<int>(),new HashSet<int>()};static readonly bool[] sawPeak={false,false};static bool sawEarly;static int observedContacts,initialContacts;
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
'''+t[b:]
t+=r'''    static IEnumerator Ready()
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
'''
(root/'Tools/Validation/CrossCutPresentation20261001.cs').write_text(t,encoding='utf-8')
print('Generated three katana profiles/eight directions, actual two contacts, native motion/camera and between-hit cancellation.')
