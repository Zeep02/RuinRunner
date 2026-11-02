"""Actual input/physics contact ruler; paused stills are not motion/campaign evidence."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/SweepFxPresentation20261001.cs').read_text(encoding='utf-8')
t=t[:t.index('    static IEnumerator Motion(')]
t=t.replace('SweepFxPresentation20261001','MeleeRangeProbe20261001').replace('melee-fx-binding/presentation-','melee-fx-range/probe-')
t=t.replace('using RuinRunner;','using RuinRunner;\nusing Newtonsoft.Json.Linq;')
a=t.index('    static readonly HashSet<int> fxKeys');b=t.index('    static void Require(',a)
t=t[:a]+'    static readonly JArray measurements=new JArray();\n    static void Track(string phase){}\n'+t[b:]
t=t.replace('checks.Clear(); customSizes.Clear();','measurements.Clear(); checks.Clear(); customSizes.Clear();')
old='File.WriteAllText(folder + "/result.txt", "PASS staged supplied-weapon/target motion review; not normal campaign, Windows build or 60fps performance evidence\\n" + string.Join("\\n", checks));'
new='File.WriteAllText(folder + "/result.txt", (AllMatched()?"PASS ":"FAIL visual/actual outer reach mismatch; ")+"actual input/physics contact ruler completed. Supplied 0.04-unit hurt proxies and paused contact stills; not normal NPC/campaign/motion/performance.\\n"+string.Join("\\n",checks));'
assert old in t;t=t.replace(old,new)
t=t.replace('Debug.Log("[Bound sweep presentation] PASS " + folder);','Debug.Log("[Melee range probe] " + (AllMatched()?"PASS ":"FAIL visible/actual range mismatch ") + folder);')
t+=r'''    static bool AllMatched(){foreach(JObject row in measurements)if(!row.Value<bool>("visibleMatchesOuterRange"))return false;return true;}
    static IEnumerator Advance(float seconds)
    {
        var stop=Time.timeAsDouble+seconds;
        while(Time.timeAsDouble<stop){yield return null;StageValidationInput.BeginFrame();}
    }
    static IEnumerator Ready()
    {
        var deadline=Time.realtimeSinceStartupAsDouble+5;
        while((float)typeof(PlayerController).GetField("attackTimer",Private).GetValue(game.Player)>0)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("attack readiness deadline");yield return null;StageValidationInput.BeginFrame();}
    }
    static EnemyController Needle(Vector2 point)
    {
        var enemy=Enemy(point-(Vector2)game.Player.transform.position);
        var collider=(BoxCollider2D)enemy.DamageCollider;
        collider.offset=Vector2.zero;collider.size=Vector2.one*.04f;
        return enemy;
    }
    static SpriteRenderer CurrentArc()
    {
        foreach(var fx in UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>())if(fx.FollowsMeleeAction)return fx.GetComponent<SpriteRenderer>();
        foreach(var fx in UnityEngine.Object.FindObjectsByType<NativeCombatEffect>())if(fx.FollowsMeleeAction)return fx.GetComponent<SpriteRenderer>();
        throw new Exception("The real active contact arc is missing");
    }
    static Vector2 VisibleSpan(SpriteRenderer view,Vector2 axis,Vector2 actor,bool opaque)
    {
        var sprite=view.sprite;var path=AssetDatabase.GetAssetPath(sprite);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(path));
        var pixels=source.GetPixels32();float min=float.PositiveInfinity,max=float.NegativeInfinity;
        for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
        {
            var alpha=pixels[y*source.width+x].a;if(opaque?alpha!=255:alpha==0)continue;
            // Project each original pixel quad, not padded Sprite.bounds, onto
            // the committed attack axis. No fitted camera offset or art edits.
            foreach(var corner in new[]{new Vector2(x,y),new Vector2(x+1,y),new Vector2(x,y+1),new Vector2(x+1,y+1)})
            {
                var local=(corner-sprite.pivot)/sprite.pixelsPerUnit;
                if(view.flipX)local.x=-local.x;if(view.flipY)local.y=-local.y;
                float d=Vector2.Dot((Vector2)view.transform.TransformPoint(local)-actor,axis);min=Mathf.Min(min,d);max=Mathf.Max(max,d);
            }
        }
        UnityEngine.Object.DestroyImmediate(source);return new Vector2(min,max);
    }
    static IEnumerator Case(string id,int direction,int mode)
    {
        Equip(id);var p=game.Player;var ready=Ready();while(ready.MoveNext())yield return ready.Current;
        var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.SetAim(aim);
        var profile=MeleeComboCatalog.Get(p.EquippedWeapon);var step=mode<0?profile.HeavyStep:profile.LightSteps[mode];
        var angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg+step.AngleOffset;
        var axis=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
        var actor=(Vector2)p.transform.position;float previousOuter=step.Reach+step.HitboxSize.x*.5f;
        var hand=p.GetComponent<PlayerAnimationController>().MeleeHandPosition(aim,step.Motion,mode,mode<0,.42f);
        Require(MeleeSweepGeometry.TryEnvelope(step,p.EquippedWeapon.Family,hand,axis,out var damageCenter,out var damageSize),"active bound-cut descriptor "+id);
        float outer=Vector2.Dot(damageCenter,axis)+damageSize.x*.5f;
        var inside=Needle(actor+axis*(outer-.12f));var outside=Needle(actor+axis*(outer+.12f));yield return null;Physics2D.SyncTransforms();
        Require(Physics2D.Linecast(actor,inside.DamageCollider.bounds.center,1<<GameLayers.Ground).collider==null,"real range ray is not occluded "+id+"/"+direction+"/"+mode);
        for(int preparation=0;preparation<mode;preparation++)
        {
            Pulse(GameAction.Attack);yield return null;StageValidationInput.BeginFrame();
            ready=Ready();while(ready.MoveNext())yield return ready.Current;
        }
        int contacts=p.MeleeContactsResolved,sequence=p.AttackAnimationSequence;float before=inside.Health,outsideBefore=outside.Health;
        if(mode<0)
        {
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);
            var charge=Advance(.75f);while(charge.MoveNext())yield return charge.Current;
            Require(p.IsGreatswordCharging,"native full charge "+id);StageValidationInput.SetHeld(GameAction.Guard,false);
        }
        else Pulse(GameAction.Attack);
        var deadline=Time.realtimeSinceStartupAsDouble+3;
        while(p.MeleeContactsResolved==contacts)
        {if(Time.realtimeSinceStartupAsDouble>deadline)throw new Exception("real contact input deadline "+id);yield return null;StageValidationInput.BeginFrame();}
        yield return new WaitForEndOfFrame();Time.timeScale=0;
        Require(p.MeleeContactsResolved==contacts+1&&p.AttackAnimationSequence==sequence+1,"one real contact "+id+"/"+direction+"/"+mode);
        Require(inside.Health<before&&outside.Health==outsideBefore,"actual inner/outer tiny hurt proxy damage boundary "+id+"/"+direction+"/"+mode);
        var arc=CurrentArc();var visible=VisibleSpan(arc,axis,p.transform.position,false);var opaque=VisibleSpan(arc,axis,p.transform.position,true);
        string phase=id+"-view-"+direction+"-mode-"+mode;Capture(phase+"-contact");
        var record=new JObject{{"phase",phase},{"weapon",id},{"direction",direction},{"mode",mode},{"motion",step.Motion.ToString()},
            {"reach",step.Reach},{"previousOuterRay",previousOuter},{"boxWidth",damageSize.x},{"boxHeight",damageSize.y},{"actualOuterRay",outer},
            {"visibleForwardMin",visible.x},{"visibleForwardMax",visible.y},{"opaqueForwardMax",opaque.y},{"shortfall",outer-visible.y},
            {"visibleMatchesOuterRange",Mathf.Abs(outer-visible.y)<=.15f},{"insideDamage",before-inside.Health},{"outsideDamage",outsideBefore-outside.Health},
            {"source",AssetDatabase.GetAssetPath(arc.sprite)},{"sourcePivot",new JArray(arc.sprite.pivot.x,arc.sprite.pivot.y)},
            {"sourcePPU",arc.sprite.pixelsPerUnit},{"scale",new JArray(arc.transform.lossyScale.x,arc.transform.lossyScale.y)},
            {"actor",new JArray(p.transform.position.x,p.transform.position.y)},{"grip",new JArray(p.GetComponent<HeldWeaponView>().BladePoint(0).x,p.GetComponent<HeldWeaponView>().BladePoint(0).y)},
            {"fxCenter",new JArray(arc.transform.position.x,arc.transform.position.y)},{"progress",p.AttackAnimationProgress},
            {"scope","Supplied 0.04-unit damage probes, real input/physics and paused original contact still; not normal NPC/campaign/motion/performance."}};
        measurements.Add(record);File.WriteAllText(folder+"/range-measurements.json",measurements.ToString(),new UTF8Encoding(false));
        Time.timeScale=1;StageValidationInput.ReleaseAll();var settle=Advance(.5f);while(settle.MoveNext())yield return settle.Current;
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil").SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        center=game.DungeonMap.GetRoomCenter(3);game.CameraFollow.FocusRoom(center,new Vector2(20,14),true);
        Resolution(1280,720);yield return null;yield return new WaitForEndOfFrame();
        Require(Screen.width==1280&&Screen.height==720,"native 1280x720 geometry stills");
        foreach(var weapon in WeaponLootTable.All)if(weapon.AttackType==WeaponAttackType.Melee)
        {
            var steps=MeleeComboCatalog.Get(weapon).LightSteps;
            for(int stepIndex=0;stepIndex<steps.Length;stepIndex++)if(steps[stepIndex].Motion==MeleeAttackMotion.Sweep||
                steps[stepIndex].Motion==MeleeAttackMotion.Cleave&&weapon.Family==WeaponFamily.Greatsword)
            foreach(var direction in new[]{0,1,2,3,4,5,6,7})
            {var test=Case(weapon.Id,direction,stepIndex);while(test.MoveNext())yield return test.Current;}
        }
        foreach(var id in new[]{"iron_greatsword","dragon_slayer","blood_reaver"})foreach(var direction in new[]{0,1,2,3,4,5,6,7})
        {var test=Case(id,direction,-1);while(test.MoveNext())yield return test.Current;}
        game.ReturnToMainMenu();yield return null;Require(measurements.Count==304,"every supported normal cut and three greatsword charged cleaves measured in all eight directions");
    }
}
'''
(root/'Tools/Validation/MeleeRangeProbe20261001.cs').write_text(t,encoding='utf-8')
print('Generated actual-physics contact ruler; 304 paused native geometry snapshots.')
