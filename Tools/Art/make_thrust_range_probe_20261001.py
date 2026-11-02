"""Adapt the actual physical ruler to native stab forward and side boundaries."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/MeleeRangeProbe20261001.cs').read_text(encoding='utf-8')
t=t.replace('MeleeRangeProbe20261001','ThrustRangeProbe20261001').replace('melee-fx-range/probe-','thrust-fx/probe-')
t=t.replace('foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())', 'foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeThrustEffect>())if(fx.FollowsThrustAction)return fx.GetComponent<SpriteRenderer>();\n        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())')
t=t.replace('mode<0,.42f','mode<0,MeleeMotionTiming.ThrustContact')
old='var inside=Needle(actor+axis*(outer-.12f));var outside=Needle(actor+axis*(outer+.12f));yield return null;Physics2D.SyncTransforms();'
new=r'''var transverse=damageCenter-axis*Vector2.Dot(damageCenter,axis);var side=new Vector2(-axis.y,axis.x);
        var inside=Needle(actor+axis*(outer-.12f)+transverse);var outside=Needle(actor+axis*(outer+.12f)+transverse);
        var sideInside=Needle(actor+damageCenter+side*(damageSize.y*.5f-.065f));var sideOutside=Needle(actor+damageCenter+side*(damageSize.y*.5f+.12f));
        yield return null;Physics2D.SyncTransforms();'''
assert old in t;t=t.replace(old,new)
t=t.replace('int contacts=p.MeleeContactsResolved,sequence=p.AttackAnimationSequence;float before=inside.Health,outsideBefore=outside.Health;', 'int contacts=p.MeleeContactsResolved,sequence=p.AttackAnimationSequence;float before=inside.Health,outsideBefore=outside.Health,sideBefore=sideInside.Health,sideOutsideBefore=sideOutside.Health;')
t=t.replace('var arc=CurrentArc();var visible=', 'Require(sideInside.Health<sideBefore&&sideOutside.Health==sideOutsideBefore,"actual transverse tiny hurt proxy damage boundary "+id+"/"+direction+"/"+mode);\n        var arc=CurrentArc();var visible=')
t=t.replace('string phase=id+', 'var visibleSide=VisibleSpan(arc,side,p.transform.position,false);\n        string phase=id+')
t=t.replace('{"source",AssetDatabase.GetAssetPath(arc.sprite)}', '{"visibleSideMin",visibleSide.x},{"visibleSideMax",visibleSide.y},{"sideInsideDamage",sideBefore-sideInside.Health},{"sideOutsideDamage",sideOutsideBefore-sideOutside.Health},{"source",AssetDatabase.GetAssetPath(arc.sprite)}')
t=t.replace('string phase=id+', 'float sideCenter=Vector2.Dot(damageCenter,side);\n        string phase=id+')
t=t.replace('{"visibleSideMin",visibleSide.x}', '{"actualSideMin",sideCenter-damageSize.y*.5f},{"actualSideMax",sideCenter+damageSize.y*.5f},{"visibleSideMatches",Mathf.Max(Mathf.Abs(sideCenter-damageSize.y*.5f-visibleSide.x),Mathf.Abs(sideCenter+damageSize.y*.5f-visibleSide.y))<=.075f},{"visibleSideMin",visibleSide.x}')
t=t.replace('if(!row.Value<bool>("visibleMatchesOuterRange"))', 'if(!row.Value<bool>("visibleMatchesOuterRange")||!row.Value<bool>("visibleSideMatches"))')
a=t.index('            for(int stepIndex=0;stepIndex<steps.Length;stepIndex++)');b=t.index('        game.ReturnToMainMenu();',a)
t=t[:a]+r'''            for(int stepIndex=0;stepIndex<steps.Length;stepIndex++)if(steps[stepIndex].Motion==MeleeAttackMotion.Thrust)
            foreach(var direction in new[]{0,1,2,3,4,5,6,7})
            {var test=Case(weapon.Id,direction,stepIndex);while(test.MoveNext())yield return test.Current;}
        }
'''+t[b:]
t=t.replace('measurements.Count==304,"every supported normal cut and three greatsword charged cleaves measured in all eight directions"','measurements.Count>=48,"every active normal thrust measured in all eight directions; cases="+measurements.Count')
(root/'Tools/Validation/ThrustRangeProbe20261001.cs').write_text(t,encoding='utf-8')
print('Generated original-contact pixel projection and four actual hurt-boundary probes per normal thrust.')
