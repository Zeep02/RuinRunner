from pathlib import Path
root=Path(__file__).resolve().parents[2]
text=(root/'Tools/Validation/SscaryMeleeFinal20261001.cs').read_text(encoding='utf-8')
text=text[:text.index('    static IEnumerator Run()')]
text=text.replace('SscaryMeleeFinal20261001','SscaryDefensePresentation20261001').replace('2026-10-01-hero-melee/presentation-final-','2026-10-01-hero-defense/presentation-').replace('[Sscary melee presentation]','[Sscary defense presentation]')
a=text.index('    static void Track(');b=text.index('    static void Require(',a)
text=text[:a]+'''    static string previousPose;static Vector2 previousHand;static float previousAngle;static int seamChecks;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.IsBelowActor&&fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("action effect covers the moving actor "+phase);
        if(a.AuthoredWeaponRotationActive)
        {
            if(Vector2.Distance(p.transform.Find("HeldWeaponPivot").localPosition,a.HandLocalPosition)>.002f)throw new Exception("defense blade grip detached "+phase);
            if(a.HandBehindBody&&p.transform.Find("HeldWeaponPivot/HeldWeaponSprite").GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("occluded hand blade crosses face/pack "+phase);
            if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("native body geometry "+phase);
            if(Mathf.Abs(Mathf.DeltaAngle(a.AuthoredWeaponRotation,held.DisplayedActionRotation))>.01f)throw new Exception("body/blade clocks disagree "+phase);
            phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName.Replace("-move",""));
            if(previousPose=="charge"||previousPose=="charge-move")
            {
                if(a.PoseName.StartsWith("charge-cleave"))
                {
                    var jump=Vector2.Distance(previousHand,a.HandLocalPosition)*32;
                    if(jump>3.2f||Mathf.Abs(Mathf.DeltaAngle(previousAngle,held.DisplayedActionRotation))>22)throw new Exception("release snaps from its actual partial/full charge "+phase+" pixels="+jump);
                    seamChecks++;
                }
            }
        }
        previousPose=a.PoseName;previousHand=a.HandLocalPosition;previousAngle=held.DisplayedActionRotation;
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,a.AuthoredWeaponRotationActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture)));
    }
''' +text[b:]
text=text.replace('behind,motion,contacts','behind,motion,contacts,angle')
text+='''    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.10f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static void FreshObservations(){ResetObservations();previousPose=null;seamChecks=0;}
    static void ClearImmunity(){typeof(PlayerController).GetField("invincibilityTicks",Private).SetValue(game.Player,0);}
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 real composited screen");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var direction in new[]{0,1,2,4,5,7})
        {
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));
            Equip("rust_sword");yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(aim);ClearImmunity();FreshObservations();
            StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();float hp=game.Player.Health;bool struck=false;
            var motion=Motion(.52f,"guard-view-"+direction,t=>{if(t>.29f&&!struck){struck=true;game.Player.TakeDamage(10,-aim);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(phasePoses.Contains("guard")&&phaseSprites.Count>=4,"guard raises native arms/sets stance view "+direction+" poses="+phaseSprites.Count);
            Require(struck&&game.Player.Health<hp&&game.Player.Health>hp-10,"real frontal guard reduces damage view "+direction);
            StageValidationInput.SetHeld(GameAction.Guard,false);motion=Motion(.25f,"guard-return-view-"+direction);while(motion.MoveNext())yield return motion.Current;
            Require(!game.Player.IsGuarding&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="idle","guard visibly returns without extending defense view "+direction);Flush();
            foreach(var success in new[]{false,true})
            {
                Equip("training_katana");yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(aim);ClearImmunity();var enemy=Enemy(aim*1.2f);yield return null;Physics2D.SyncTransforms();
                hp=game.Player.Health;float enemyHp=enemy.Health;struck=false;FreshObservations();Pulse(GameAction.Guard);StageValidationInput.SetAim(aim);yield return null;StageValidationInput.BeginFrame();
                motion=Motion(.46f,"parry-"+(success?"success":"miss")+"-view-"+direction,t=>{if(success&&t>.065f&&!struck){Require(game.Player.IsParrying,"successful strike is inside real parry window");struck=true;game.Player.TakeDamage(10,-aim);}});
                while(motion.MoveNext())yield return motion.Current;
                Require(phasePoses.Contains("parry")&&phaseSprites.Count>=4,"parry has separate fast preparation/recovery view "+direction);
                if(success){Require(phasePoses.Contains("parry-success"),"deflection continues from current native hand key");Require(game.Player.Health==hp&&enemy.Health<enemyHp,"parry protects and counters once using actual combat view "+direction);}
                else Require(enemy.Health==enemyHp,"missed parry adds no phantom counter view "+direction);
                Require(!game.Player.IsParrying&&!game.Player.IsParryPresentationActive,"parry window and cosmetic recovery both end view "+direction);Flush();
            }
            foreach(var full in new[]{false,true})
            {
                Equip("iron_greatsword");yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(aim);var enemy=Enemy(aim*1.3f);yield return null;Physics2D.SyncTransforms();float enemyHp=enemy.Health;
                FreshObservations();int contacts=game.Player.MeleeContactsResolved;bool released=false;float hold=full?1.32f:.23f;
                StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();
                motion=Motion(hold+.62f,"charge-"+(full?"full":"partial")+"-view-"+direction,t=>{if(t>hold&&!released){if(full)Require(game.Player.GetComponent<HeldWeaponView>().DisplayedActionRotation>=119,"full charge holds the raised blade before downward release");released=true;StageValidationInput.SetHeld(GameAction.Guard,false);}});
                while(motion.MoveNext())yield return motion.Current;
                Require(phasePoses.Contains("charge")&&phasePoses.Contains("charge-cleave")&&phaseSprites.Count>=8,"native two-hand preparation/release view "+direction+" full="+full+" poses="+phaseSprites.Count);
                Require(seamChecks==1,"release begins at actual visible hand/angle, once view "+direction+" full="+full);
                Require(enemy.Health<enemyHp&&game.Player.MeleeContactsResolved==contacts+1,"one real charged contact at existing damage clock view "+direction+" full="+full);
                Require(!game.Player.IsGreatswordCharging&&!game.Player.IsMeleeSwingActive&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="idle","charged cut recovers to original idle view "+direction);Flush();
            }
        }
        foreach(var id in new[]{"rust_sword","training_katana","iron_greatsword"})
        {
            Equip(id);yield return new WaitForSeconds(.85f);FreshObservations();StageValidationInput.SetHeld(GameAction.MoveRight,true);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();bool released=false;
            var motion=Motion(1.18f,id+"-mobile-defense",t=>{if(t>.45f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>.64f&&!released){released=true;StageValidationInput.SetHeld(GameAction.Guard,false);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(traveled>3,"defense/preparation keeps normal movement "+id+" distance="+traveled);
            Require(phaseSprites.Count>=8,"defense body keeps independent native gait "+id+" poses="+phaseSprites.Count);StageValidationInput.ReleaseAll();Flush();
        }
        Equip("iron_greatsword");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();yield return new WaitForSeconds(.26f);yield return new WaitForEndOfFrame();
        var anim=game.Player.GetComponent<PlayerAnimationController>();var sprite=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var pivot=game.Player.transform.Find("HeldWeaponPivot");
        Require(anim.PoseName=="charge","pause scenario has real authored charge");var savedSprite=sprite.sprite;var savedHand=anim.HandLocalPosition;var savedAngle=pivot.localRotation;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Require(sprite.sprite==savedSprite&&anim.HandLocalPosition==savedHand&&pivot.localRotation==savedAngle,"pause freezes body/hand/blade and preparation together");Time.timeScale=1;
        int before=game.Player.MeleeContactsResolved;Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(!game.Player.IsGreatswordCharging&&anim.PoseName=="dash","dash immediately cancels preparation with native evasion pose");
        var cancel=Motion(.48f,"charge-dash-cancel");while(cancel.MoveNext())yield return cancel.Current;
        Require(game.Player.MeleeContactsResolved==before,"cancelled charge produces no late damage");Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length==0,"dash cancels charge glow completely");Flush();
        Equip("rust_sword");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.24f);
        StageValidationInput.SetHeld(GameAction.Guard,false);Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(game.Player.IsMeleeSwingActive&&anim.PoseName.StartsWith("sweep"),"guard release recovery never blocks immediate primary attack");yield return new WaitForSeconds(.75f);
        StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.20f);game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));yield return new WaitForEndOfFrame();
        Require(game.Player.EquippedWeapon.Family==WeaponFamily.Pistol&&anim.PoseName.StartsWith("aim-pistol"),"weapon swap removes stale guard and native blade posture");StageValidationInput.ReleaseAll();
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0,"menu clears player/preparation/action effects");
    }
}
'''
(root/'Tools/Validation/SscaryDefensePresentation20261001.cs').write_text(text,encoding='utf-8')
# Recover the exact pre-unit version of this small shared effect source for rollback.
path=root/'ArtDirection/Review-20261001/hero-defense/before/Assets__RuinRunner__Runtime__Feedback__NativeCombatEffect.cs'
if not path.exists():
    old=(root/'Assets/RuinRunner/Runtime/Feedback/NativeCombatEffect.cs').read_text(encoding='utf-8')
    old=old.replace('        float loopFrameRate;\n','').replace('        public void SetLoopFrameRate(float framesPerSecond)=>loopFrameRate=Mathf.Max(1,framesPerSecond);\n','').replace('loopFrameRate>0?loopFrameRate:frames.Length<=6?14f:36f','frames.Length<=6?14f:36f')
    path.write_text(old,encoding='utf-8')
print('Created normal-input isolated defense review; no normal campaign claim.')
