from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/SscaryDefensePresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('yield return new WaitForEndOfFrame(); double time','view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame(); double time')
t=t.replace('behind,motion,contacts,angle','behind,motion,contacts,angle,bodyAsset,originSX,originSY,scaleX,scaleY')
t=t.replace('SscaryDefensePresentation20261001','SscaryRangedPresentation20261001').replace('hero-defense','hero-ranged').replace('Sscary defense presentation','Sscary ranged presentation')
a=t.index('    static string previousPose;');b=t.index('    static void Require(',a)
t=t[:a]+r'''    static int observedEmission,emissions;static Vector2 previousHand;static float maxHandStep,emissionMana;
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();
        var pivot=p.transform.Find("HeldWeaponPivot");var barrel=pivot.Find("HeldWeaponSprite").GetComponent<SpriteRenderer>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        if(p.MotionState!=PlayerMotionState.Dash&&p.EquippedWeapon.AttackType==WeaponAttackType.Ranged)
        {
            if(!a.AuthoredWeaponRotationActive||!a.PoseName.StartsWith(p.IsRangedPresentationActive?"shoot-":"aim-"))throw new Exception("missing native ranged pose "+phase);
            if(Vector2.Distance(pivot.localPosition,a.HandLocalPosition)>.002f)throw new Exception("body/held grip detached "+phase);
            if(body.sprite.rect.size!=new Vector2(48,64)||body.sprite.pivot!=new Vector2(24,22)||body.sprite.pixelsPerUnit!=32)throw new Exception("ranged native body geometry "+phase);
            if(Mathf.Abs(Mathf.DeltaAngle(a.AuthoredWeaponRotation,held.DisplayedActionRotation))>.01f)throw new Exception("barrel/body release clocks disagree "+phase);
            if(barrel.sortingOrder>=body.sortingOrder)throw new Exception("concealed hand barrel crosses body "+phase);
            if(p.IsRangedPresentationActive)
            {
                phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName.Replace("-move",""));
                if(observedEmission!=p.RangedPresentationSequence)
                {
                    if(p.RangedPresentationProgress>.001f)throw new Exception("missed actual first emission frame "+phase+" "+p.RangedPresentationProgress);
                    observedEmission=p.RangedPresentationSequence;emissions++;emissionMana=Mathf.Min(emissionMana,p.Mana);
                    var origin=(Vector2)typeof(PlayerController).GetField("committedShotOrigin",Private).GetValue(p);
                    RangedWeaponArt.Landmarks landmarks;if(!RangedWeaponArt.TryGet(p.EquippedWeapon.Id,out landmarks))throw new Exception("missing muzzle art");
                    var local=(new Vector2(landmarks.Muzzle.x,barrel.sprite.rect.height-landmarks.Muzzle.y)-barrel.sprite.pivot)/barrel.sprite.pixelsPerUnit;
                    if(barrel.flipY)local.y=-local.y;
                    var visible=(Vector2)pivot.TransformPoint(local);
                    if(Vector2.Distance(origin,visible)>.002f)throw new Exception("actual emitted projectile/barrel detach "+phase+" "+Vector2.Distance(origin,visible));
                }
            }
            if(previousHand!=Vector2.zero)maxHandStep=Mathf.Max(maxHandStep,Vector2.Distance(previousHand,a.HandLocalPosition)*32);
            previousHand=a.HandLocalPosition;
        }
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.RangedPresentationSequence,p.IsRangedPresentationActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.RangedPresentationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture)));
    }
'''+t[b:]
t+=r'''    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.11f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static void Fresh(){ResetObservations();observedEmission=game.Player.RangedPresentationSequence;emissions=0;emissionMana=float.PositiveInfinity;previousHand=Vector2.zero;maxHandStep=0;}
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 composited screen");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var id in new[]{"hunter_bow","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"})
        foreach(var direction in new[]{0,1,2,4,5,7})
        foreach(var special in new[]{false,true})
        {
            Equip(id);yield return new WaitForSeconds(.85f);var p=game.Player;var anim=p.GetComponent<PlayerAnimationController>();
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);yield return new WaitForEndOfFrame();Fresh();
            p.Stats.RestoreMana(p.MaxMana);int ammo=p.ActiveWeaponState.Ammo,seq=p.AttackAnimationSequence;float mana=p.Mana;bool started=false,stopped=false;
            var phase=id+"-"+(special?"special":"normal")+"-view-"+direction;
            var motion=Motion(.48f,phase,t=>{if(t>.025f&&!started){started=true;StageValidationInput.SetHeld(special?GameAction.Guard:GameAction.Attack,true);}if(started&&!stopped&&p.AttackAnimationSequence>seq){stopped=true;StageValidationInput.SetHeld(special?GameAction.Guard:GameAction.Attack,false);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(p.AttackAnimationSequence==seq+1,"one actual GameAction initiates action "+phase);
            Require(emissions==(special&&p.EquippedWeapon.Family==WeaponFamily.Pistol?3:1),"every actual emission gets its own body/barrel clock "+phase+" emissions="+emissions);
            Require(phaseSprites.Count>=3,"real native release/recovery pixels "+phase+" images="+phaseSprites.Count);
            Require(p.ActiveWeaponState.Ammo==ammo-(special?0:1),"unchanged owned ammo consumption "+phase);
            if(special)Require(Mathf.Abs(emissionMana-(mana-p.EquippedWeapon.SpecialManaCost))<.01f,"actual special mana spent once "+phase);
            Require(!p.IsRangedPresentationActive&&anim.PoseName.StartsWith("aim-"),"recovery returns to family ready stance "+phase);
            Require(maxHandStep<=5.5f,"bounded actual socket movement "+phase+" pixels="+maxHandStep);Flush();
        }
        foreach(var id in new[]{"hunter_bow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"})
        {
            Equip(id);yield return new WaitForSeconds(.85f);StageValidationInput.SetAim(Vector2.right);Fresh();bool started=false;
            var motion=Motion(.72f,id+"-strafe-backpedal",t=>{if(!started){started=true;StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.MoveRight,true);}if(t>.3f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>.62f)StageValidationInput.ReleaseAll();});
            while(motion.MoveNext())yield return motion.Current;
            Require(traveled>1.5f&&phaseSprites.Count>=5,"independent gait while firing/backpedaling "+id);StageValidationInput.ReleaseAll();Flush();
        }
        Equip("scrap_pistol");yield return new WaitForSeconds(.85f);bool fired=false,paused=false,resumed=false;Sprite pauseSprite=null;Vector2 pauseHand=Vector2.zero;Quaternion pauseAngle=Quaternion.identity;float pauseProgress=0;double pauseStart=0;
        var record=Record(.52f,"pistol-pause",.1f);
        while(record.MoveNext())
        {
            yield return record.Current;StageValidationInput.BeginFrame();
            if(!fired){fired=true;StageValidationInput.SetHeld(GameAction.Attack,true);}
            else if(!paused&&game.Player.IsRangedPresentationActive)
            {
                StageValidationInput.SetHeld(GameAction.Attack,false);paused=true;var anim=game.Player.GetComponent<PlayerAnimationController>();
                pauseSprite=game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;pauseHand=anim.HandLocalPosition;pauseAngle=game.Player.transform.Find("HeldWeaponPivot").localRotation;pauseProgress=game.Player.RangedPresentationProgress;
                pauseStart=Time.realtimeSinceStartupAsDouble;Time.timeScale=0;
            }
            else if(paused&&!resumed&&Time.realtimeSinceStartupAsDouble-pauseStart>.15)
            {
                var anim=game.Player.GetComponent<PlayerAnimationController>();
                Require(pauseSprite==game.Player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite&&pauseHand==anim.HandLocalPosition&&pauseAngle==game.Player.transform.Find("HeldWeaponPivot").localRotation&&pauseProgress==game.Player.RangedPresentationProgress,"pause freezes body/socket/barrel/recoil clock");Time.timeScale=1;resumed=true;
            }
        }
        Require(resumed,"pause scenario ran");Flush();
        Equip("scrap_pistol");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
        Require(game.Player.MotionState==PlayerMotionState.Dash&&!game.Player.IsRangedPresentationActive,"dash cancels burst recovery immediately");yield return new WaitForSeconds(.35f);
        int cancelled=game.Player.RangedPresentationSequence;yield return new WaitForSeconds(.2f);Require(game.Player.RangedPresentationSequence==cancelled,"no delayed burst after dash cancellation");
        Equip("scrap_pistol");yield return new WaitForSeconds(.85f);StageValidationInput.SetHeld(GameAction.Attack,true);yield return null;StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);game.Inventory.AddWeapon(WeaponLootTable.Find("rust_sword"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));yield return new WaitForEndOfFrame();
        Require(!game.Player.IsRangedPresentationActive&&!game.Player.GetComponent<PlayerAnimationController>().PoseName.StartsWith("shoot-"),"swap cancels firearm body/socket state");StageValidationInput.ReleaseAll();
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length==0,"menu clears body and projectiles");
    }
}
'''
(root/'Tools/Validation/SscaryRangedPresentation20261001.cs').write_text(t,encoding='utf-8')
print('Wrote actual GameAction ranged/body/muzzle recorder fixture.')
