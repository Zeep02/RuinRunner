"""Reuse the proven actual-time native screen recorder for bound melee sweeps."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/GreatswordFxPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('GreatswordFxPresentation20261001','SweepFxPresentation20261001').replace('greatsword-fx','melee-fx-binding').replace('Greatsword FX presentation','Bound sweep presentation')
a=t.index('    static readonly HashSet<int> fxKeys');b=t.index('    static void Require(',a)
t=t[:a]+r'''    static readonly HashSet<int> fxKeys=new HashSet<int>();static bool sawPeak,sawEarly;static int observedContacts;
    static void Fresh(){ResetObservations();fxKeys.Clear();sawPeak=sawEarly=false;observedContacts=game.Player.MeleeContactsResolved;}
    static MeleeSlashEffect Sweep(){foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())if(fx.FollowsMeleeAction)return fx;return null;}
    static void Track(string phase)
    {
        var p=game.Player;var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var a=p.GetComponent<PlayerAnimationController>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var fx=Sweep();
        if(fx!=null)
        {
            var v=fx.GetComponent<SpriteRenderer>();var originals=PixelArtAtlas.GetMeleeSlashFrames(p.EquippedWeapon.Family,false);
            if(v.sortingOrder>=body.sortingOrder)throw new Exception("sweep paints over native body "+phase);
            if(v.sprite!=originals[fx.FrameIndex])throw new Exception("source sweep pixels changed "+phase);
            if(Vector2.Distance(fx.transform.position,held.BladePoint(0))>.002f)throw new Exception("moving sweep/grip drift "+phase);
            if(v.flipX!=((p.ActiveComboAnimationStep&1)==1))throw new Exception("reverse slash silhouette mismatch "+phase);
            if(v.enabled){fxKeys.Add(fx.FrameIndex);if(p.AttackAnimationProgress<MeleeMotionTiming.Contact)sawEarly=true;}
            if(p.MeleeContactsResolved>observedContacts&&p.ActiveMeleeMotion==MeleeAttackMotion.Sweep)
            {if(fx.FrameIndex!=(originals.Length==4?1:2))throw new Exception("source peak misses actual original contact "+phase);sawPeak=true;}
        }
        observedContacts=p.MeleeContactsResolved;
        if(p.IsMeleeSwingActive){phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);}
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture),fx==null?"":p.EquippedWeapon.Family.ToString(),fx==null?-1:fx.FrameIndex,UnityEditorInternal.InternalEditorUtility.isApplicationActive,Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture)));
    }
''' +t[b:]
t+=r'''    static IEnumerator Motion(float seconds,string phase,Action<float> input=null)
    {
        double start=Time.timeAsDouble;var motion=Record(seconds,phase,.11f);
        while(motion.MoveNext()){yield return motion.Current;StageValidationInput.BeginFrame();input?.Invoke((float)(Time.timeAsDouble-start));}
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 composited screen");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var id in new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword"})foreach(var direction in new[]{0,1,2,3,4,5,6,7})
        {
            Equip(id);yield return new WaitForSeconds(.9f);var p=game.Player;var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            var enemy=Enemy(aim*1.05f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            Fresh();int sequence=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;bool started=false,stopped=false;int steps=p.EquippedWeapon.Family==WeaponFamily.Greatsword?1:2;
            var phase=id+"-view-"+direction;var motion=Motion(1.6f,phase,time=>
            {if(time>.025f&&!started){started=true;StageValidationInput.SetHeld(GameAction.Attack,true);}if(started&&!stopped&&p.AttackAnimationSequence>=sequence+steps){stopped=true;StageValidationInput.SetHeld(GameAction.Attack,false);}});
            while(motion.MoveNext())yield return motion.Current;
            Require(stopped&&p.AttackAnimationSequence==sequence+steps,"real input reaches requested sweeps only "+phase);
            Require(sawEarly&&sawPeak&&fxKeys.Count>=4,"source preparation/actual contact/breakup "+phase+" unique="+fxKeys.Count);
            Require(p.MeleeContactsResolved==contacts+steps&&enemy.Health<hp,"unchanged contact count and real target damage "+phase);
            Require(Sweep()==null&&!p.IsMeleeSwingActive,"body and bound sweep finish together "+phase);StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var id in new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword"})
        {
            Equip(id);yield return new WaitForSeconds(.9f);StageValidationInput.SetAim(Vector2.right);Fresh();bool started=false;
            var motion=Motion(1.55f,id+"-mobile",t=>{if(!started){started=true;StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.MoveRight,true);}if(t>.7f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>1.3f)StageValidationInput.ReleaseAll();});
            while(motion.MoveNext())yield return motion.Current;
            Require(sawEarly&&sawPeak&&traveled>3&&phaseSprites.Count>=8,"native independent mobile gait and bound source grip "+id);StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var mode in new[]{0,1})
        {
            Equip("rust_sword");yield return new WaitForSeconds(.9f);Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();
            var deadline=Time.realtimeSinceStartupAsDouble+1;
            while(Sweep()==null){if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("sweep input timeout");yield return null;StageValidationInput.BeginFrame();}
            yield return new WaitForEndOfFrame();var sweep=Sweep();Require(sweep!=null,"cancellation begins with live bound sweep");
            var key=sweep.FrameIndex;var position=sweep.transform.position;var progress=game.Player.AttackAnimationProgress;Time.timeScale=0;
            yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();Require(sweep.FrameIndex==key&&sweep.transform.position==position&&game.Player.AttackAnimationProgress==progress,"pause freezes source/grip/action clock");Time.timeScale=1;
            int before=game.Player.MeleeContactsResolved;
            if(mode==0)Pulse(GameAction.Dash);else{game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));}
            yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
            Require(Sweep()==null,"dash/swap immediately removes prior bound sweep "+mode);yield return new WaitForSeconds(.35f);Require(game.Player.MeleeContactsResolved==before,"cancelled original swing cannot deal late damage "+mode);StageValidationInput.ReleaseAll();
        }
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0,"menu clears all owned source trails");
    }
}
'''
(root/'Tools/Validation/SweepFxPresentation20261001.cs').write_text(t,encoding='utf-8')
print('Generated five-family bound sweep native-time recorder.')

# Ten other native grips and the rarer combo profiles use the same production
# path. Review all eight directions without repeating the base cancellation unit.
catalog=t.replace('SweepFxPresentation20261001','SweepFxCatalog20261001').replace('/presentation-','/catalog-')
base='new[]{"worn_dagger","rust_sword","iron_mace","training_katana","iron_greatsword"}'
catalog=catalog.replace(base,'new[]{"wind_dagger","knight_sword","iron_hammer","quake_mace","moon_blade","dragon_slayer","shadow_fang","blood_moon_katana","blood_reaver","sun_oath"}',1)
start=catalog.index('        foreach(var id in '+base)
end=catalog.index('        game.ReturnToMainMenu();yield return null;Require(',start)
catalog=catalog[:start]+catalog[end:]
(root/'Tools/Validation/SweepFxCatalog20261001.cs').write_text(catalog,encoding='utf-8')
print('Generated ten-weapon/eight-direction catalog supplement.')
