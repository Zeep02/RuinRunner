"""Current raised-joint defense regression, original real input/capture fixtures reused."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/GreatswordFxPresentation20261001.cs').read_text(encoding='utf-8').replace('GreatswordFxPresentation20261001','WeaponStandingDefense20261001').replace('greatsword-fx/presentation-','weapon-carry/defense-').replace('new[]{0,1,2,4,5,7}','new[]{0,1,2,3,4,5,6,7}')
t=t.replace('    static void Track(string phase)','    static float nativeHandJump;static Vector2 priorNativeHand;static bool hadNativeHand;\n    static void Track(string phase)')
t=t.replace('        var camera=Camera.main;',r'''        var pivot=p.transform.Find("HeldWeaponPivot");var animation=p.GetComponent<PlayerAnimationController>();
        if(Vector2.Distance(pivot.localPosition,animation.HandLocalPosition)>.001f)throw new Exception("Native charged hand detached "+phase);
        if(hadNativeHand)nativeHandJump=Mathf.Max(nativeHandJump,Vector2.Distance(animation.HandLocalPosition,priorNativeHand)*32);
        priorNativeHand=animation.HandLocalPosition;hadNativeHand=true;
        var camera=Camera.main;''')
# Each independently equipped action is reviewed from its settled family stance.
t=t.replace('Fresh();int sequence=', 'Fresh();nativeHandJump=0;hadNativeHand=false;int sequence=')
t=t.replace('Require(stopped&&p.AttackAnimationSequence', 'Require(nativeHandJump<=6.5f,"actual standing/charge/release/return hand continuity "+phase+" maxNativePixels="+nativeHandJump);\n            Require(stopped&&p.AttackAnimationSequence')
(root/'Tools/Validation/WeaponStandingDefense20261001.cs').write_text(t,encoding='utf-8')
# Check each normal/dagger RMB thrust at raised hands again; retain existing AU full fixtures unchanged.
t=(root/'Tools/Validation/ThrustFxPresentation20261001.cs').read_text(encoding='utf-8').replace('ThrustFxPresentation20261001','WeaponStandingThrust20261001').replace('thrust-fx/presentation-','weapon-carry/thrust-')
(root/'Tools/Validation/WeaponStandingThrust20261001.cs').write_text(t,encoding='utf-8')
# Fresh source snapshot and candidate, old AU remains intact.
for before,after in [('BuildThrustCandidate','BuildWeaponStandingCandidate'),('ReadThrustBuild','ReadWeaponStandingBuild'),('SmokeCandidateThrust','SmokeCandidateWeaponStanding')]:
    p=root/f'Tools/Validation/{before}20261001.ps1'
    s=p.read_text(encoding='utf-8').replace('AU','AV').replace('thrust-fx','weapon-carry').replace('ThrustAV-','WeaponStandingAV-')
    if before=='BuildThrustCandidate':
        a=s.index('$sourcePaths=@(');b=s.index('\n$snapshot=',a)
        s=s[:a]+"$sourcePaths=@('Feedback/MeleeCrossCutEffect.cs','Feedback/MeleeThrustEffect.cs','Feedback/MeleeSweepGeometry.cs','Player/PlayerController.cs','Player/PlayerAnimationController.cs','Player/HeldWeaponView.cs','Player/SscaryArmedFrames.cs','Player/WeaponStandingFrames.cs','Player/MeleeMotionTiming.cs','UI/PrototypeHud.cs','Enemies/EnemyController.cs','Diagnostics/RuntimeSmokeTest.cs')"+s[b:]
    (root/f'Tools/Validation/{after}20261001.ps1').write_text(s,encoding='utf-8')
print('Generated actual eight-direction charged hand/thrust regressions and isolated AV candidate scripts.')

