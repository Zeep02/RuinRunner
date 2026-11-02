"""Real input finisher audit. Recorder source reused, no synthetic motion frames."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/SweepFxPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('SweepFxPresentation20261001','FinisherFxBaseline20261001').replace('melee-fx-binding/presentation-','finisher-fx/baseline-')
a=t.index('    static readonly HashSet<int> fxKeys');b=t.index('    static void Require(',a)
t=t[:a]+r'''    static SpriteRenderer Finisher(out string clip,out int key)
    {
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())
        {var v=fx.GetComponentInChildren<SpriteRenderer>();if(v!=null){clip="Thrust";key=fx.FrameIndex;return v;}}
        foreach(var fx in UnityEngine.Object.FindObjectsByType<ExternalCombatEffect>())
        {var v=fx.GetComponent<SpriteRenderer>();clip="CrossCut";key=int.Parse(v.sprite.name);return v;}
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.ClipId=="SlamDust")
        {clip="SlamDust";key=fx.FrameIndex;return fx.GetComponent<SpriteRenderer>();}
        clip="";key=-1;return null;
    }
    static void Track(string phase)
    {
        var p=game.Player;var body=p.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var a=p.GetComponent<PlayerAnimationController>();var held=p.GetComponent<HeldWeaponView>();
        traveled+=Vector2.Distance(previousPosition,p.transform.position);previousPosition=p.transform.position;
        string clip;int key;var fx=Finisher(out clip,out key);
        if(fx!=null&&fx.sortingOrder>=body.sortingOrder)throw new Exception("finisher hides native body");
        phaseSprites.Add(body.sprite);phasePoses.Add(a.PoseName);
        var camera=Camera.main;var pixelOrigin=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,42/32f,0)));var across=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-23/32f,42/32f,0)));var down=camera.WorldToScreenPoint(body.transform.TransformPoint(new Vector3(-24/32f,41/32f,0)));
        telemetry.AppendLine(string.Join(",",sample,phase,p.AttackAnimationSequence,p.IsMeleeSwingActive,a.PoseName,a.FacingDirection,a.FrameIndex,body.sprite.name,
            p.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.x.ToString("F4",CultureInfo.InvariantCulture),p.transform.position.y.ToString("F4",CultureInfo.InvariantCulture),
            a.HandLocalPosition.x.ToString("F4",CultureInfo.InvariantCulture),a.HandLocalPosition.y.ToString("F4",CultureInfo.InvariantCulture),a.HandBehindBody,p.ActiveMeleeMotion,p.MeleeContactsResolved,held.DisplayedActionRotation.ToString("F4",CultureInfo.InvariantCulture),AssetDatabase.GetAssetPath(body.sprite),pixelOrigin.x.ToString("F6",CultureInfo.InvariantCulture),pixelOrigin.y.ToString("F6",CultureInfo.InvariantCulture),(across.x-pixelOrigin.x).ToString("F6",CultureInfo.InvariantCulture),(pixelOrigin.y-down.y).ToString("F6",CultureInfo.InvariantCulture),clip,key,UnityEditorInternal.InternalEditorUtility.isApplicationActive,Application.isFocused,Time.deltaTime.ToString("F6",CultureInfo.InvariantCulture)));
    }
''' +t[b:]
t=t.replace('[Bound sweep presentation]','[Finisher baseline]')
t+=r'''    static IEnumerator Ready()
    {
        var deadline=Time.realtimeSinceStartupAsDouble+4;
        while((float)typeof(PlayerController).GetField("attackTimer",Private).GetValue(game.Player)>0)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("readiness timeout");yield return null;StageValidationInput.BeginFrame();}
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 finisher capture");var verify=VerifyReadback();while(verify.MoveNext())yield return verify.Current;
        foreach(var id in new[]{"worn_dagger","rust_sword","training_katana","iron_mace"})foreach(var direction in new[]{0,2,4,6})
        {
            Equip(id);yield return null;var p=game.Player;
            var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
            var ready=Ready();while(ready.MoveNext())yield return ready.Current;
            var profile=MeleeComboCatalog.Get(p.EquippedWeapon);
            for(int preparation=0;preparation<profile.LightSteps.Length-1;preparation++)
            {Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();ready=Ready();while(ready.MoveNext())yield return ready.Current;}
            var enemy=Enemy(aim*1.2f);yield return null;Physics2D.SyncTransforms();float hp=enemy.Health;
            ResetObservations();int sequence=p.AttackAnimationSequence,contacts=p.MeleeContactsResolved;Pulse(GameAction.Attack);
            yield return null;StageValidationInput.BeginFrame();
            var phase=id+"-finisher-view-"+direction;var record=Record(.7f,phase,.14f);
            while(record.MoveNext()){yield return record.Current;StageValidationInput.BeginFrame();}
            Require(p.AttackAnimationSequence==sequence+1&&p.MeleeContactsResolved==contacts+1,"one real finisher input/contact "+phase);
            Require(enemy.Health<hp,"real final-step hurt collider damage "+phase);
            Flush();
        }
        game.ReturnToMainMenu();yield return null;
    }
}
'''
(root/'Tools/Validation/FinisherFxBaseline20261001.cs').write_text(t,encoding='utf-8')
print('Generated sixteen actual finisher input/capture cases; baseline, not normal campaign.')
