"""Reuse the verified actual-time native recorder for cleave source/clock review."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/SscaryRangedPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('SscaryRangedPresentation20261001','GreatswordFxPresentation20261001').replace('hero-ranged','greatsword-fx').replace('Sscary ranged presentation','Greatsword FX presentation')
t=t.replace('scaleX,scaleY','scaleX,scaleY,fxClip,fxKey,editorForeground,gameFocused,delta')
t=t.replace('view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame(); double time', 'if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive){var focus=autoTick.Assembly.GetType("Unity.Pipeline.Editor.Commands.FocusEditorCommand").GetMethod("FocusEditor",All);focus.Invoke(null,null);view.Focus();}view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame(); double time')
t=t.replace('var texture = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false);', 'if(f.Pixels==null)continue;var texture = new Texture2D(f.Width, f.Height, TextureFormat.RGBA32, false);')
t=t.replace('catch (Exception error) { try{Flush();}catch{}', 'catch (Exception error) { File.WriteAllText(folder+"/partial-recording.csv",recording.ToString(),new UTF8Encoding(false));File.WriteAllText(folder+"/partial-body-telemetry.csv",telemetry.ToString(),new UTF8Encoding(false));try{Flush();}catch{}')
a=t.index('    static int observedEmission,');b=t.index('    static void Require(',a)
t=t[:a]+r'''    static readonly HashSet<int> fxKeys=new HashSet<int>();static bool sawContactPeak,sawCut;static int observedContacts;
    static void Fresh(){ResetObservations();fxKeys.Clear();sawContactPeak=sawCut=false;observedContacts=game.Player.MeleeContactsResolved;}
    static NativeCombatEffect Cut(){foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.FollowsMeleeAction)return fx;return null;}
    static void Track(string phase)
    {
        var p=game.Player;var a=p.GetComponent<PlayerAnimationController>();var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        var fx=Cut();
        if(fx!=null)
        {
            var v=fx.GetComponent<SpriteRenderer>();bool charged=p.IsChargedCleaveActive;
            if(v.sortingOrder>=body.sortingOrder)throw new Exception("cleave hides source head/hand "+phase);
            if(v.sprite.rect.size!=new Vector2(128,128)||v.sprite.pixelsPerUnit!=32||v.sprite.pivot!=new Vector2(64,64))throw new Exception("source cleave geometry "+phase);
            if(fx.ClipId!="GreatswordCut"||!v.flipY)throw new Exception("wrong source silhouette/orientation "+phase);
            if(Vector2.Distance(fx.transform.position,held.BladePoint(0))>.002f)throw new Exception("moving cut/grip anchor drift "+phase);
            if(v.enabled){sawCut=true;fxKeys.Add(fx.FrameIndex);}
            if(p.MeleeContactsResolved>observedContacts)
            {
                if(fx.FrameIndex!=4)throw new Exception("first actual damage contact misses native bright peak "+phase+" key="+fx.FrameIndex+" p="+p.AttackAnimationProgress);
                sawContactPeak=true;
            }
        }
        observedContacts=p.MeleeContactsResolved;
        if(p.IsMeleeSwingActive){phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);}
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture),fx==null?"":fx.ClipId,fx==null?-1:fx.FrameIndex,UnityEditorInternal.InternalEditorUtility.isApplicationActive,Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture)));
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
        foreach(var direction in new[]{0,1,2,4,5,7})foreach(var mode in new[]{0,1,2})
        {
            Equip("iron_greatsword");yield return new WaitForSeconds(.9f);var p=game.Player;
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            var enemy=Enemy(aim*1.3f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            Fresh();int sequence=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;bool started=false,stopped=false;float hold=mode==2?1.32f:.23f;
            var phase=(mode==0?"normal":mode==1?"charge-partial":"charge-full")+"-view-"+direction;
            var motion=Motion(mode==0?1.5f:hold+.68f,phase,time=>
            {
                if(time>.025f&&!started){started=true;StageValidationInput.SetHeld(mode==0?GameAction.Attack:GameAction.Guard,true);}
                if(started&&!stopped&&(mode==0?p.AttackAnimationSequence>=sequence+2:time>hold&&p.IsGreatswordCharging))
                {stopped=true;StageValidationInput.SetHeld(mode==0?GameAction.Attack:GameAction.Guard,false);}
            });
            while(motion.MoveNext())yield return motion.Current;
            Require(stopped&&p.AttackAnimationSequence==sequence+(mode==0?2:1),"real input reaches only requested combo/release "+phase);
            Require(sawCut&&fxKeys.Count>=5,"distinct native trajectory and breakup "+phase+" observed="+fxKeys.Count);
            Require(sawContactPeak,"source bright pose coincides with actual .42 contact "+phase);
            Require(p.MeleeContactsResolved==contacts+(mode==0?2:1)&&enemy.Health<hp,"original contact count and actual target damage "+phase);
            Require(Cut()==null&&!p.IsMeleeSwingActive,"action and bound trail recover together "+phase);StageValidationInput.ReleaseAll();Flush();
        }
        foreach(var id in new[]{"iron_greatsword","dragon_slayer","blood_reaver"})
        {
            Equip(id);yield return new WaitForSeconds(.9f);StageValidationInput.SetAim(Vector2.right);Fresh();bool started=false;
            var motion=Motion(1.65f,id+"-mobile-combo",t=>{if(!started){started=true;StageValidationInput.SetHeld(GameAction.Attack,true);StageValidationInput.SetHeld(GameAction.MoveRight,true);}if(t>.72f){StageValidationInput.SetHeld(GameAction.MoveRight,false);StageValidationInput.SetHeld(GameAction.MoveLeft,true);}if(t>1.4f)StageValidationInput.ReleaseAll();});
            while(motion.MoveNext())yield return motion.Current;
            Require(sawCut&&traveled>3&&phaseSprites.Count>=8,"independent native gait with moving source grip anchor "+id);StageValidationInput.ReleaseAll();Flush();
        }
        Equip("iron_greatsword");yield return new WaitForSeconds(.9f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.25f);StageValidationInput.SetHeld(GameAction.Guard,false);
        while(!game.Player.IsChargedCleaveActive)yield return null;
        yield return new WaitForEndOfFrame();var cut=Cut();Require(cut!=null,"pause scenario has bound charged source");
        var savedKey=cut.FrameIndex;var savedPosition=cut.transform.position;var savedProgress=game.Player.AttackAnimationProgress;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.16f);yield return new WaitForEndOfFrame();Require(cut.FrameIndex==savedKey&&cut.transform.position==savedPosition&&game.Player.AttackAnimationProgress==savedProgress,"pause freezes source trajectory/body/contact clock");Time.timeScale=1;
        int before=game.Player.MeleeContactsResolved;Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
        Require(Cut()==null&&!game.Player.IsChargedCleaveActive,"real dash cancels prepared cut immediately");yield return new WaitForSeconds(.35f);Require(game.Player.MeleeContactsResolved==before,"cancelled source creates no late damage");
        Equip("iron_greatsword");yield return new WaitForSeconds(.9f);StageValidationInput.SetHeld(GameAction.Guard,true);yield return new WaitForSeconds(.25f);StageValidationInput.SetHeld(GameAction.Guard,false);yield return null;StageValidationInput.BeginFrame();
        game.Inventory.AddWeapon(WeaponLootTable.Find("scrap_pistol"));game.Player.EquipWeapon(game.Inventory.SelectQuickSlot(1));yield return new WaitForEndOfFrame();yield return null;
        Require(Cut()==null&&!game.Player.IsMeleeSwingActive,"swap removes bound cut without altering firearm stance");StageValidationInput.ReleaseAll();
        Equip("iron_greatsword");yield return new WaitForSeconds(.9f);StageValidationInput.SetHeld(GameAction.Guard,true);
        var chargeDeadline=Time.realtimeSinceStartupAsDouble+3;
        while(game.Player.GreatswordChargeProgress<.98f)
        {if(Time.realtimeSinceStartupAsDouble>chargeDeadline)throw new Exception("full-charge input fixture timeout");yield return null;StageValidationInput.BeginFrame();}
        yield return new WaitForEndOfFrame();var preparation=game.Player.GetComponent<WeaponPreparationEffect>();
        var readyField=typeof(WeaponPreparationEffect).GetField("readyFlash",All);var ready=(NativeCombatEffect)readyField.GetValue(preparation);
        Require(ready!=null&&ready.IsBelowActor,"completed-charge glint follows the blade below the native face");
        before=game.Player.MeleeContactsResolved;Pulse(GameAction.Dash);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
        Require(!game.Player.IsGreatswordCharging&&readyField.GetValue(preparation)==null&&Cut()==null,"full-charge dash clears glow and completion glint together");
        yield return new WaitForSeconds(.4f);Require(game.Player.MeleeContactsResolved==before,"cancelled full charge creates no late contact");StageValidationInput.ReleaseAll();
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<NativeCombatEffect>().Length==0&&UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0,"menu removes all source trails and owned player");
    }
}
'''
(root/'Tools/Validation/GreatswordFxPresentation20261001.cs').write_text(t,encoding='utf-8')
print('Generated native-time source trajectory/contact/cancel recorder.')
