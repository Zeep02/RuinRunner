"""Actual guard and death cancellation from a live, pre-contact native stab."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/ThrustFxPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('ThrustFxPresentation20261001','ThrustCancel20261001').replace('thrust-fx/presentation-','thrust-fx/cancel-')
t+=r'''    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        foreach(var death in new[]{false,true})
        {
            Equip("rust_sword");yield return null;var ready=Prepare(2);while(ready.MoveNext())yield return ready.Current;
            var p=game.Player;int contacts=p.MeleeContactsResolved;Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();
            Require(Thrust()!=null&&p.AttackAnimationProgress<MeleeMotionTiming.ThrustContact,"live native thrust before "+(death?"death":"guard"));
            Capture(death?"death-before":"guard-before");
            if(death)p.TakeDamage(99999,-p.AimDirection);else StageValidationInput.SetHeld(GameAction.Guard,true);
            yield return null;StageValidationInput.BeginFrame();yield return new WaitForEndOfFrame();yield return null;
            if(death)
            {
                Require(Thrust()==null&&p.IsDead,"actual death removes bound source immediately");yield return new WaitForEndOfFrame();Capture("death-after");
                yield return new WaitForSecondsRealtime(.35f);Require(p.MeleeContactsResolved==contacts,"no cancelled stab contact after death");
            }
            else
            {
                Require(Thrust()!=null&&!p.IsGuarding,"early guard input preserves the already committed stab until existing recovery timing");
                yield return new WaitForSeconds(.4f);
                Require(Thrust()==null&&p.IsGuarding&&p.MeleeContactsResolved==contacts+1,"existing one-contact stab recovers into held guard with no residual source");yield return new WaitForEndOfFrame();Capture("guard-after");
            }
            StageValidationInput.ReleaseAll();
        }
        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>().Length==0,"final native thrust cleanup");
    }
}
'''
(root/'Tools/Validation/ThrustCancel20261001.cs').write_text(t,encoding='utf-8')
print('Generated real guard and death cancellation, transient profile; no normal campaign claim.')
