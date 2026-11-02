"""Actual input/action-bound thrust motion, native screen and cancellation recorder."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/SweepFxPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('SweepFxPresentation20261001','ThrustFxPresentation20261001').replace('melee-fx-binding/presentation-','thrust-fx/presentation-').replace('[Bound sweep presentation]','[Bound thrust presentation]')
a=t.index('    static readonly HashSet<int> fxKeys');b=t.index('    static void Require(',a)
t=t[:a]+r'''    static readonly HashSet<int> fxKeys=new HashSet<int>();static bool sawPeak,sawEarly;static int observedContacts;
    static void Fresh(){ResetObservations();fxKeys.Clear();sawPeak=sawEarly=false;observedContacts=game.Player.MeleeContactsResolved;}
    static MeleeThrustEffect Thrust(){foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())if(fx.FollowsThrustAction)return fx;return null;}
    static void Track(string phase)
    {
        var p=game.Player;var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var a=p.GetComponent<PlayerAnimationController>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var fx=Thrust();string clip="";int key=-1;
        if(fx!=null)
        {
            var v=fx.GetComponent<SpriteRenderer>();var family=p.EquippedWeapon.Family;clip=family==WeaponFamily.Dagger?"DaggerStab":"LongswordStab";key=fx.FrameIndex;
            if(!AssetDatabase.GetAssetPath(v.sprite).Contains("External/CombatFX/"+clip+"/"))throw new Exception("Non-native physical thrust cell");
            if(v.sortingOrder>=body.sortingOrder)throw new Exception("Thrust hides native body");
            if(Vector2.Distance(fx.transform.position,held.BladePoint(MeleeThrustEffect.OriginDistance(family)))>.002f)throw new Exception("Thrust detached from displayed blade");
            if(Mathf.Abs(fx.transform.lossyScale.x-fx.transform.lossyScale.y)>.0001f)throw new Exception("Nonuniform thrust stretch");
            if(v.enabled&&p.AttackAnimationProgress>=.16f)fxKeys.Add(key);
            if(v.enabled&&p.AttackAnimationProgress<MeleeMotionTiming.ThrustContact)sawEarly=true;
            if(p.MeleeContactsResolved>observedContacts){if(!v.enabled||key!=(family==WeaponFamily.Dagger?1:2))throw new Exception("Native bright key not on actual damage contact");sawPeak=true;observedContacts=p.MeleeContactsResolved;}
        }
        else observedContacts=p.MeleeContactsResolved;
        phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);sawMelee|=p.IsThrustActive;sawMobileMelee|=p.IsThrustActive&&p.MovementInput.sqrMagnitude>.01f;
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
    static IEnumerator Prepare(int mode)
    {
        var ready=Ready();while(ready.MoveNext())yield return ready.Current;
        for(int i=0;i<mode;i++)
        {Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();ready=Ready();while(ready.MoveNext())yield return ready.Current;}
    }
    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.07f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static IEnumerator Case(string id,int direction,int mode,bool mobile=false)
    {
        Equip(id);yield return null;var p=game.Player;
        var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
        var preparation=Prepare(mode);while(preparation.MoveNext())yield return preparation.Current;
        var enemy=Enemy(aim*(mode<0?1.9f:1.05f));yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
        Fresh();int sequence=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;
        if(mobile)StageValidationInput.SetHeld(GameAction.MoveLeft,true);
        StageValidationInput.SetHeld(mode<0?GameAction.Guard:GameAction.Attack,true);StageValidationInput.SetHeld(mode<0?GameAction.Guard:GameAction.Attack,false);
        yield return null;StageValidationInput.BeginFrame();
        var phase=id+(mobile?"-mobile":"")+"-mode-"+mode+"-view-"+direction;
        var motion=Motion(.6f,phase,time=>{if(time>.3f)StageValidationInput.ReleaseAll();});while(motion.MoveNext())yield return motion.Current;
        Require(p.AttackAnimationSequence==sequence+1&&p.MeleeContactsResolved==contacts+1,"one real thrust input/contact "+phase);
        Require(enemy.Health<hp,"real final-step hurt collider damage "+phase);
        Require(sawEarly&&sawPeak&&fxKeys.Count>=3,"native preparation/contact/breakup "+phase+" keys="+fxKeys.Count);
        Require(sawMelee&&phaseSprites.Count>=8,"native body extension/retraction "+phase);
        if(mobile)Require(sawMobileMelee&&traveled>.5f,"moving body/gait and bound thrust "+phase);
        Require(Thrust()==null&&!p.IsThrustActive,"body and thrust finish together "+phase);StageValidationInput.ReleaseAll();Flush();
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 thrust capture");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        int cases=0;
        foreach(var weapon in WeaponLootTable.All)if(weapon.AttackType==WeaponAttackType.Melee)
        {
            var profile=MeleeComboCatalog.Get(weapon);
            for(int mode=0;mode<profile.LightSteps.Length;mode++)if(profile.LightSteps[mode].Motion==MeleeAttackMotion.Thrust)
            foreach(var direction in new[]{0,1,2,3,4,5,6,7}){var test=Case(weapon.Id,direction,mode);while(test.MoveNext())yield return test.Current;cases++;}
            if(weapon.Family==WeaponFamily.Dagger&&profile.HeavyStep.Motion==MeleeAttackMotion.Thrust)
            foreach(var direction in new[]{0,1,2,3,4,5,6,7}){var test=Case(weapon.Id,direction,-1);while(test.MoveNext())yield return test.Current;cases++;}
        }
        Require(cases>=48,"all active normal/RMB thrust catalog steps in eight directions; cases="+cases);
        foreach(var id in new[]{"worn_dagger","rust_sword"})
        {var mode=MeleeComboCatalog.Get(WeaponLootTable.Find(id)).LightSteps.Length-1;var test=Case(id,2,mode,true);while(test.MoveNext())yield return test.Current;}
        foreach(var id in new[]{"worn_dagger","rust_sword"})
        {
            Equip(id);var ready=Ready();while(ready.MoveNext())yield return ready.Current;
            StageValidationInput.SetAim(Vector2.right);Fresh();StageValidationInput.SetHeld(GameAction.Attack,true);
            var showcase=Motion(3.4f,id+"-combo-1x",time=>{if(time>1.4f&&time<2.0f)StageValidationInput.SetHeld(GameAction.MoveLeft,true);else StageValidationInput.SetHeld(GameAction.MoveLeft,false);if(time>2.7f)StageValidationInput.SetHeld(GameAction.Attack,false);});
            while(showcase.MoveNext())yield return showcase.Current;
            Require(sawEarly&&sawPeak&&fxKeys.Count>=3&&Thrust()==null,"normal-speed complete combo and mobile source continuity "+id);StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var cancellation in new[]{0,1,2})
        {
            Equip("rust_sword");yield return null;var prepare=Prepare(2);while(prepare.MoveNext())yield return prepare.Current;
            Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
            var fx=Thrust();Require(fx!=null&&game.Player.AttackAnimationProgress<MeleeMotionTiming.ThrustContact,"live pre-contact thrust for cancellation");
            int key=fx.FrameIndex,before=game.Player.MeleeContactsResolved;var position=fx.transform.position;float progress=game.Player.AttackAnimationProgress;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();
            Require(fx.FrameIndex==key&&fx.transform.position==position&&game.Player.AttackAnimationProgress==progress,"pause freezes actual source/blade/action clock");Time.timeScale=1;
            if(cancellation==0)Pulse(GameAction.Dash);
            else if(cancellation==1){game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));}
            else{game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>().Length==0,"menu clears owned thrust");break;}
            yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
            Require(Thrust()==null,"dash/swap immediately removes thrust "+cancellation);yield return new WaitForSeconds(.35f);
            Require(game.Player.MeleeContactsResolved==before,"cancelled thrust has no late damage "+cancellation);StageValidationInput.ReleaseAll();
        }
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>().Length==0,"no owned thrust after menu");
    }
}
'''
(root/'Tools/Validation/ThrustFxPresentation20261001.cs').write_text(t,encoding='utf-8')
print('Generated native actual thrust catalog/action recorder and cancellation checks.')
