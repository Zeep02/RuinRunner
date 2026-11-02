from pathlib import Path
root=Path(__file__).resolve().parents[2]
base=(root/'Tools/Validation/SscaryThrustFinal20261001.cs').read_text(encoding='utf-8')
text=base[:base.index('    static IEnumerator Run()')]
text=text.replace('SscaryThrustFinal20261001','SscaryMeleePresentation20261001').replace('2026-10-01-hero-thrust','2026-10-01-hero-melee').replace('sawThrust,sawMobileThrust','sawMelee,sawMobileMelee')
start=text.index('    static void Track(');end=text.index('    static void Require(',start)
text=text[:start]+'''    static readonly HashSet<string> phasePoses=new HashSet<string>();
    static float traveled;static Vector2 previousPosition;
    static void ResetObservations(){sawMelee=sawMobileMelee=false;phaseSprites.Clear();phasePoses.Clear();traveled=0;previousPosition=game.Player.transform.position;}
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())if(fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("large arc hides the body "+phase);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<ExternalCombatEffect>())if(fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("finisher hides the body "+phase);
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.IsBelowActor&&fx.GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("ground impact paints above its moving actor "+phase);
        if(p.IsMeleeSwingActive)
        {
            string expected=p.IsChargedCleaveActive?"charge-cleave":p.IsThrustActive?"thrust":p.ActiveMeleeMotion==MeleeAttackMotion.Cleave||p.ActiveMeleeMotion==MeleeAttackMotion.Impact?"cleave":p.EquippedWeapon.Family==WeaponFamily.Dagger?(p.ActiveComboAnimationStep&1)==1?"dagger-backcut":"dagger-cut":(p.ActiveComboAnimationStep&1)==1?"reverse-sweep":"sweep";
            if(!a.PoseName.StartsWith(expected))throw new Exception("active melee uses missing/wrong body clip "+phase+" expected="+expected+" observed="+a.PoseName);
            if(Vector2.Distance(p.transform.Find("HeldWeaponPivot").localPosition,a.HandLocalPosition)>.002f)throw new Exception("blade grip detached from exact keyed hand "+phase);
            if(a.HandBehindBody&&p.transform.Find("HeldWeaponPivot/HeldWeaponSprite").GetComponent<SpriteRenderer>().sortingOrder>=body.sortingOrder)throw new Exception("occluded hand blade paints across face/pack "+phase);
            if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("native body geometry "+phase);
            sawMelee=true;sawMobileMelee|=a.PoseName.EndsWith("-move");phaseSprites.Add(body.sprite);phasePoses.Add(expected);
        }
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved));
    }
''' +text[end:]
text=text.replace('frame,phase,sequence,thrust,pose,direction,key,sprite,progress,x,y,handX,handY','frame,phase,sequence,melee,pose,direction,key,sprite,progress,x,y,handX,handY,behind,motion,contacts')
text=text.replace('[Sscary thrust presentation]','[Sscary melee presentation]')
text+='''    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 composited screen, fixed camera policy");
        var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var direction in new[]{0,1,2,4,5,7})
        foreach(var heavy in new[]{false,true})
        {
            Equip(heavy?"iron_greatsword":"rust_sword");game.Player.Stats.RestoreMana(game.Player.MaxMana);
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            yield return new WaitForSeconds(.85f);var enemy=Enemy(aim*1.3f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            ResetObservations();
            if(heavy){StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.12f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);}
            else Pulse(GameAction.Attack);
            yield return null;StageValidationInput.BeginFrame();
            var motion=Record(heavy?.60f:.42f,(heavy?"cleave":"sweep")+"-view-"+direction,.14f);
            while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();}
            Require(sawMelee&&phaseSprites.Count>=5,"multiple true body poses in "+(heavy?"cleave":"sweep")+" view "+direction+" observed="+phaseSprites.Count);
            Require(enemy.Health<hp,"real contact damages target in "+(heavy?"cleave":"sweep")+" view "+direction);Flush();
        }
        foreach(var id in new[]{"rust_sword","worn_dagger","training_katana","iron_greatsword","iron_mace"})
        foreach(var moving in new[]{false,true})
        {
            Equip(id);game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
            var enemy=Enemy(new Vector2(1.35f,moving?1.8f:0));yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            ResetObservations();StageValidationInput.BeginFrame();StageValidationInput.SetAim(Vector2.right);
            if(moving)StageValidationInput.SetHeld(GameAction.MoveRight,true);StageValidationInput.SetHeld(GameAction.Attack,true);
            yield return null;StageValidationInput.BeginFrame();var before=(Vector2)game.Player.transform.position;
            var phase=id+(moving?"-moving":"-standing");var motion=Record(1.85f,phase,.13f);double moveStart=Time.realtimeSinceStartupAsDouble;
            while(motion.MoveNext())
            {
                yield return motion.Current;StageValidationInput.BeginFrame();
                // Return along the same clear lane so every combo remains in the actual viewport.
                if(moving&&Time.realtimeSinceStartupAsDouble-moveStart>.8){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}
            }
            StageValidationInput.ReleaseAll();Require(sawMelee&&phaseSprites.Count>=8,"actual multiple keyed combo poses "+phase+" observed="+phaseSprites.Count);
            Require(phasePoses.Contains(id=="worn_dagger"?"dagger-cut":"sweep")&&phasePoses.Contains(id=="worn_dagger"?"dagger-backcut":"reverse-sweep")||id=="iron_greatsword"&&phasePoses.Contains("cleave"),"forward/reverse or two-hand finisher body clips "+phase);
            if(id=="rust_sword"||id=="worn_dagger")Require(phasePoses.Contains("thrust"),"combo still connects to previously authored thrust "+phase);
            if(id=="iron_greatsword"||id=="iron_mace")Require(phasePoses.Contains("cleave"),"real downward finisher "+phase);
            if(moving){Require(sawMobileMelee,"attack uses independent gait "+id);Require(traveled>3f,"continuous attack movement along real return path "+id+" distance="+traveled);}
            else Require(enemy.Health<hp,"normal combo damage remains functional "+id);Flush();
        }
        Equip("rust_sword");yield return new WaitForSeconds(.9f);var untouched=Enemy(Vector2.right*1.3f);yield return null;Physics2D.SyncTransforms();float originalHp=untouched.Health;
        Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(game.Player.IsMeleeSwingActive,"cancel scenario begins a real slash before contact");int contacts=game.Player.MeleeContactsResolved;
        Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(!game.Player.IsMeleeSwingActive&&game.Player.GetComponent<PlayerAnimationController>().PoseName=="dash","dash cancels body and committed blade together");
        var dashRecord=Record(.45f,"slash-dash-return",.07f);while(dashRecord.MoveNext()){yield return dashRecord.Current;StageValidationInput.BeginFrame();}Flush();
        Require(untouched.Health==originalHp&&game.Player.MeleeContactsResolved==contacts,"cancelled pre-contact slash never deals delayed damage");
        Equip("iron_greatsword");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.12f);StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
        yield return new WaitForSeconds(.09f);yield return new WaitForEndOfFrame();Require(game.Player.IsMeleeSwingActive,"pause scenario starts actual cleave");
        var anim=game.Player.GetComponent<PlayerAnimationController>();var renderer=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var frozen=renderer.sprite;var hand=anim.HandLocalPosition;int key=anim.FrameIndex;
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{true});Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
        Require(anim.FrameIndex==key&&anim.HandLocalPosition==hand&&renderer.sprite==frozen,"pause freezes actual keyed body/hand/weapon");
        typeof(GameController).GetMethod("SetManualPause",Private).Invoke(game,new object[]{false});Time.timeScale=1;yield return new WaitForSeconds(.6f);
        Require(!game.Player.IsMeleeSwingActive&&anim.PoseName=="idle"&&!anim.HandBehindBody,"recovery returns to breathing original idle/socket/occlusion");
        Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();game.Inventory.AddWeapon(WeaponLootTable.Find("rust_sword"));game.Player.EquipWeapon(WeaponLootTable.Find("rust_sword"));yield return new WaitForEndOfFrame();
        Require(!game.Player.IsMeleeSwingActive&&!anim.PoseName.StartsWith("cleave"),"weapon change drops old combat pose immediately");
        ClearEffects();yield return new WaitForSeconds(.8f);Require(WeaponSecondaryParticles.ActiveCount==0&&NativeCombatEffect.ActiveCount==0,"native attack aftermath cleans up");
        StageValidationInput.ReleaseAll();game.ReturnToMainMenu();game.enabled=true;
    }
}
'''
(root/'Tools/Validation/SscaryMeleePresentation20261001.cs').write_text(text,encoding='utf-8')
begin=text.index('        foreach(var direction in new[]{0,1,2,4,5,7})');end=text.index('        foreach(var id in new[]{',begin)
final=text[:begin]+text[end:]
final=final.replace('SscaryMeleePresentation20261001','SscaryMeleeFinal20261001').replace('/presentation-"','/presentation-final-"')
(root/'Tools/Validation/SscaryMeleeFinal20261001.cs').write_text(final,encoding='utf-8')
dagger=text[:begin]+text[begin:end].replace('foreach(var heavy in new[]{false,true})','foreach(var heavy in new[]{false})').replace('"iron_greatsword":"rust_sword"','"iron_greatsword":"worn_dagger"')+text[end:]
dagger=dagger.replace('SscaryMeleePresentation20261001','SscaryShortBladeFinal20261001').replace('/presentation-"','/presentation-short-blade-"')
(root/'Tools/Validation/SscaryShortBladeFinal20261001.cs').write_text(dagger,encoding='utf-8')
mace=final.replace('SscaryMeleeFinal20261001','SscaryMaceFinal20261001').replace('/presentation-final-"','/presentation-mace-final-"').replace('new[]{"rust_sword","worn_dagger","training_katana","iron_greatsword","iron_mace"}','new[]{"iron_greatsword","iron_mace"}')
marker='        Equip("rust_sword");yield return new WaitForSeconds(.9f);'
insertion='''        foreach(var direction in new[]{0,2})foreach(var moving in new[]{false,true})
        {
            Equip("iron_mace");game.Player.Stats.RestoreMana(game.Player.MaxMana);yield return new WaitForSeconds(.9f);
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            var enemy=Enemy(aim*1.3f+(moving?new Vector2(1.2f,1.2f):Vector2.zero));yield return null;Physics2D.SyncTransforms();float hp=enemy.Health,mp=game.Player.Mana;int slamContacts=game.Player.MeleeContactsResolved;
            ResetObservations();StageValidationInput.BeginFrame();if(moving)StageValidationInput.SetHeld(GameAction.MoveLeft,true);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
            var phase="mace-slam-"+direction+(moving?"-moving":"-standing");var motion=Record(.8f,phase,.22f);
            while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();}StageValidationInput.ReleaseAll();
            Require(sawMelee&&phasePoses.Contains("cleave"),"RMB slam uses true two-hand downward body "+phase);
            Require(game.Player.MeleeContactsResolved==slamContacts+1,"RMB slam resolves exactly one committed contact "+phase);
            Require(game.Player.Mana<mp,"RMB slam retains actual mana cost "+phase);
            if(!moving)Require(enemy.Health<hp,"RMB ground impact still damages real target "+phase);Flush();
        }
'''
mace=mace.replace(marker,insertion+marker)
(root/'Tools/Validation/SscaryMaceFinal20261001.cs').write_text(mace,encoding='utf-8')
print('Generated actual GameAction pose/contact/gait/cancel/pause/occlusion recorder.')
