"""Generate supplied-item regression of actual family stance -> moving attack -> ready output."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/WeaponCarryAudit20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Run()')]
t=t.replace('WeaponCarryAudit20261001','WeaponStandingMotion20261001').replace('weapon-carry/audit-','weapon-carry/motion-')
start=t.index('    static void Track(string phase)')
stop=t.index('    static void Require(',start)
camera=t[t.index('        var camera=Camera.main;',start):stop]
t=t[:start]+r'''    static float socketStep; static Vector2 previousSocket; static bool hadSocket; static int expectedDirection;
    static bool attacked, gaitDuringAttack;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();var pivot=p.transform.Find("HeldWeaponPivot");
        if(Vector2.Distance(pivot.localPosition,a.HandLocalPosition)>.001f)throw new Exception("visible hand/grip disconnected "+phase);
        if(a.FacingDirection!=expectedDirection)throw new Exception("body turned away from aimed weapon while moving "+phase);
        if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22))throw new Exception("body canvas/pivot changed "+phase);
        if(hadSocket)socketStep=Mathf.Max(socketStep,Vector2.Distance(previousSocket,a.HandLocalPosition)*32);
        previousSocket=a.HandLocalPosition;hadSocket=true;
        attacked|=p.IsMeleeSwingActive||p.IsRangedPresentationActive;gaitDuringAttack|=(p.IsMeleeSwingActive||p.IsRangedPresentationActive)&&p.MovementInput.sqrMagnitude>.01f;
        phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        string clip="";int key=-1;
'''+camera+t[stop:]
t+=r'''    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();Require(Screen.width==1280&&Screen.height==720,"native motion composition");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        for(int warm=0;warm<10;warm++){yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();}
        foreach(var id in new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword","hunter_bow","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"})
        for(int direction=0;direction<8;direction++)
        {
            Equip(id);expectedDirection=direction;
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.45f);yield return new WaitForEndOfFrame();
            var p=game.Player;int sequence=p.AttackAnimationSequence;ResetObservations();socketStep=0;hadSocket=attacked=gaitDuringAttack=false;
            double start=Time.timeAsDouble;bool pressed=false,released=false;var phase=id+"-moving-return-view-"+direction;
            var record=Record(1.35f,phase,.24f);
            while(record.MoveNext())
            {
                yield return record.Current;StageValidationInput.BeginFrame();float time=(float)(Time.timeAsDouble-start);
                if(time>.08f&&!pressed){pressed=true;StageValidationInput.SetHeld(GameAction.MoveLeft,true);StageValidationInput.SetHeld(GameAction.Attack,true);}
                if(p.AttackAnimationSequence>sequence&&!released){released=true;StageValidationInput.SetHeld(GameAction.Attack,false);}
                if(time>.58f)StageValidationInput.SetHeld(GameAction.MoveLeft,false);
            }
            Require(p.AttackAnimationSequence==sequence+1,"one unchanged normal input "+phase);
            Require(attacked&&gaitDuringAttack&&traveled>.5f,"native attack with independent moving gait "+phase);
            Require(phaseSprites.Count>=6,"multiple actual native motion pixels "+phase+" images="+phaseSprites.Count);
            Require(socketStep<=6.5f,"bounded ready/attack/recovery hand continuity "+phase+" maxNativePixels="+socketStep);
            Require(!p.IsMeleeSwingActive&&!p.IsRangedPresentationActive,"returned from actual action "+phase);
            Require(p.GetComponent<PlayerAnimationController>().FrameIndex==8,"family relaxed standing reached "+phase);
            StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var id in new[]{"worn_dagger","rust_sword","iron_greatsword","hunter_bow","scrap_pistol","storm_rifle"})
        {
            Equip(id);expectedDirection=4;StageValidationInput.SetAim(Vector2.down);yield return new WaitForSeconds(.45f);ResetObservations();socketStep=0;hadSocket=false;
            var phase=id+"-standing-loop-1x";var record=Record(1.72f,phase,.7f);while(record.MoveNext()){yield return record.Current;StageValidationInput.BeginFrame();}
            Require(phaseSprites.Count>=4,"original native breathing remains active "+id+" images="+phaseSprites.Count);
            Require(socketStep<=2.5f,"breathing has no grip drift "+id+" maxNativePixels="+socketStep);Flush();
        }
        game.ReturnToMainMenu();yield return null;
    }
}
'''
(root/'Tools/Validation/WeaponStandingMotion20261001.cs').write_text(t,encoding='utf-8')
r=(root/'Tools/Validation/SscaryRangedPresentation20261001.cs').read_text(encoding='utf-8').replace('SscaryRangedPresentation20261001','WeaponStandingRanged20261001').replace('hero-ranged/presentation-','weapon-carry/ranged-').replace('new[]{0,1,2,4,5,7}','new[]{0,1,2,3,4,5,6,7}')
(root/'Tools/Validation/WeaponStandingRanged20261001.cs').write_text(r,encoding='utf-8')
print('Generated 88 normal moving attacks, six natural idle loops, and all 96 ranged normal/special cases.')

