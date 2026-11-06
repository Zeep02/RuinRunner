using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The 70 signature skills. Each case is one weapon from Tools/Art/weapons70.json.</summary>
    public sealed partial class PlayerController
    {
        float sigLockUntil;bool sigHurt;
        internal bool SigMoveLocked=>sig!=null&&combatClock<sigLockUntil;

        bool SigPassThrough()
        {
            switch(sig.Id)
            {
                case "merc_slab":case "ironback_blade":return true;
            }
            return false;
        }
        // A second press is the second half of a skill already paid for, so it never waits for a swing to end.
        bool SigAllowsDuringAttack()
        {
            switch(sig.Id)
            {
                case "instant_king":case "siege_engineer":return true;
                case "falling_blossom":return sig.Stage>0;
                case "shadow_suture":return sig.Stage==2;
                case "hour_minute":case "dragon_spine":case "bannerless":case "star_fold":case "mirror_eye":case "small_eclipse":return sig.Stage==1;
                case "world_pendulum":return sig.Stage==1&&sig.Count>0;
                case "lightning_carbine":return sig.Stage==1&&sig.Marks.Count>0;
            }
            return false;
        }
        bool SigCanCast()
        {
            switch(sig.Id)
            {
                case "thousand_returns":return sig.Points.Count>0;
                case "scrap_revolver":return ActiveWeaponState!=null&&ActiveWeaponState.Ammo>0;
                case "last_bell":return SigNearestToAim(9)!=null;
            }
            return true;
        }
        EnemyController SigNearestToAim(float range)
        {
            EnemyController best=null;float bestScore=float.MaxValue;
            foreach(var enemy in CombatActionGeometry.Circle(transform.position,range,12))
            {
                var to=(Vector2)enemy.transform.position-(Vector2)transform.position;
                var off=Mathf.Abs(Vector2.SignedAngle(SigAim,to));
                if(off<best_angle&&off+to.magnitude*3<bestScore){bestScore=off+to.magnitude*3;best=enemy;}
            }
            return best;
        }
        const float best_angle=70;

        // ---- first press
        void SigCast()
        {
            var aim=SigAim;var from=(Vector2)transform.position;var angle=SigAngle;
            var A=C("A");var B=C("B");var CC=C("C");var hit=C("Hit");
            switch(sig.Id)
            {
                // ---- daggers
                case "alley_awl":
                    // Straight through whatever is in the way, and out the far side with a cut hanging behind.
                    sig.Until=combatClock+.4f;SigSwing(MeleeAttackMotion.Thrust,1.2f);SigDash(aim,3f,.13f,null,()=>{var to=(Vector2)transform.position;SigFx.Play(B,(from+to)*.5f,angle,-1);SigSeg(from,to,1f,D(1.1f),CC,A,3);ActionShake(.1f);});break;
                case "saw_fang":
                    // Three rakes in a row in front of the hero, each one a real hit that opens a wound, then a hop back.
                    sig.Until=combatClock+.35f;SigSwing(MeleeAttackMotion.Sweep,1f);
                    {
                        var at=Ahead(1.5f);var dir=angle;var pop=CC;var wound=A;
                        for(int i=0;i<3;i++){int index=i;SigAfter(.05f+i*.09f,()=>{SigFx.Play(B,at,dir+(index-1)*35,-1,false,index==1);foreach(var enemy in SigArea(at,1.3f,D(.45f),hit,0,.05f))EnemyStatus.On(enemy).AddDot(this,"bleed",D(.13f),3,wound);if(index==2)SigFx.Play(pop,at,0,-1);});}
                    }
                    SigAfter(.28f,()=>SigDash(-aim,1.4f,.16f));break;
                case "acrobat_knife":
                {
                    SigSwing(MeleeAttackMotion.Thrust,.4f);
                    for(int i=-1;i<=1;i++)
                    {
                        var knife=SigBolt.Shoot(this,A,SkillOrigin,Quaternion.Euler(0,0,i*22)*aim,13,D(.6f),.36f,1,4.8f);knife.ReturnsToOwner=true;knife.Impact=hit;
                        knife.OnCaught=()=>{if(sig==null||sig.Id!="acrobat_knife")return;sig.Until=combatClock+.45f;SigFx.Play(B,SkillOrigin,0,-1);};
                    }
                    break;
                }
                case "frost_fang":
                    sig.Stage=1;sig.Until=combatClock+2.2f;SigSwing(MeleeAttackMotion.Sweep,.9f);break;
                case "viper_tongue":
                {
                    sig.Stage=1;SigSwing(MeleeAttackMotion.Thrust,1f);var at=Ahead(1.9f);
                    SigFx.Play(C("Cast"),at,0,-1,true);SigFx.Play(B,at,0,-1,true);
                    SigAfter(.15f,()=>{foreach(var enemy in SigArea(at,1.5f,D(.4f),hit,0,.08f))EnemyStatus.On(enemy).AddDot(this,"poison",D(.14f),3,C("C"));});
                    break;
                }
                case "ember_claw":
                {
                    SigSwing(MeleeAttackMotion.Sweep,.9f);
                    var dropped=new List<Vector2>();var embers=new List<SigFx>();var struck=new Dictionary<EnemyController,int>();float next=.15f,moved=0;var last=(Vector2)transform.position;
                    float pop=D(.4f);
                    SigDash(aim,3f,.2f,p=>{moved+=Vector2.Distance(last,p);last=p;if(moved>=next){next+=.6f;var point=p+Vector2.down*.2f;dropped.Add(point);embers.Add(SigFx.Loop(A,point,0,true,15));}},()=>{
                        for(int i=0;i<dropped.Count;i++)
                        {
                            int index=i;
                            SigAfter(.12f+.075f*i,()=>{
                                embers[index].Stop();SigFx.Play(B,dropped[index],0,.7f);
                                foreach(var enemy in CombatActionGeometry.Circle(dropped[index],.85f))
                                {struck.TryGetValue(enemy,out var count);if(count>=2)continue;struck[enemy]=count+1;SigHit(enemy,pop,((Vector2)enemy.transform.position-dropped[index]).normalized*3,hit);}
                                ActionShake(.05f);CombatAudio.Native("heavy-impact",AudioChannel.Player,.16f);
                            });
                        }
                    });
                    break;
                }
                case "shadow_suture":
                {
                    // The tendrils are the hit: every enemy they catch gets a thread, and the cut comes on the next press.
                    SigSwing(MeleeAttackMotion.Sweep,.2f);var center=Ahead(SutureReach);sig.Stage=0;sig.Marks.Clear();sig.ClearViews();
                    SigFx.Play(C("Cast"),center,0,-1,true);
                    SigAfter(.2f,()=>{
                        if(sig==null||sig.Id!="shadow_suture")return;
                        foreach(var enemy in SigArea(center,SutureRadius,D(.5f),hit,0,.06f))
                        {sig.Marks.Add(enemy);sig.Views.Add(SigFx.Line(A,SkillOrigin,enemy.transform.position,-1,false,true));}
                        if(sig.Marks.Count>0){sig.Stage=2;sig.Until2=combatClock+5;}
                    });
                    break;
                }
                case "blood_bank":
                {
                    // What was saved up decides how big it gets; an empty bank still bites.
                    var saved=sig.A;var at=Ahead(1.9f);var big=Mathf.Lerp(.7f,1.25f,saved/10);var radius=1.1f+.14f*saved;
                    SigSwing(MeleeAttackMotion.Thrust,1+.22f*saved);Stats.Heal(saved*.5f);
                    SigFx.Play(C("Cast"),at,0,-1,true,false,big);
                    SigAfter(.14f,()=>{SigFx.Play(B,at,0,-1,true,false,big);SigArea(at,radius,D(.7f+.16f*saved),hit,4,.12f+saved*.012f);});
                    sig.A=0;if(sig.View!=null){sig.View.Stop();sig.View=null;}
                    break;
                }
                case "hour_minute":
                    sig.Stage=1;sig.Until=combatClock+5;sig.Point=from;sig.Points.Clear();sig.Points.Add(from);sig.Timer=combatClock+.12f;sig.ClearViews();break;
                case "night_thread":
                {
                    // Marked enemies first, then whoever else is near, up to four: stars fall on each and the thread between them cuts.
                    var marks=new List<EnemyController>(sig.Marks);sig.Marks.Clear();foreach(var view in sig.Views)if(view!=null)view.Stop();sig.Views.Clear();
                    foreach(var enemy in CombatActionGeometry.Circle(Ahead(2.5f),4.5f,8))if(marks.Count<4&&!marks.Contains(enemy))marks.Add(enemy);
                    SigSwing(MeleeAttackMotion.Sweep,.4f);var fall=C("Cast");
                    if(marks.Count==0)SigFx.Play(fall,Ahead(2.5f),0,-1,true);
                    for(int i=0;i<marks.Count;i++)if(marks[i]!=null)SigFx.Play(fall,marks[i].transform.position,0,-1,true);
                    for(int i=0;i+1<marks.Count;i++)if(marks[i]!=null&&marks[i+1]!=null)SigFx.Line(B,marks[i].transform.position,marks[i+1].transform.position,.3f);
                    SigAfter(.34f,()=>{
                        for(int i=0;i<marks.Count;i++)
                        {
                            if(marks[i]==null)continue;
                            if(i+1<marks.Count&&marks[i+1]!=null)SigSeg(marks[i].transform.position,marks[i+1].transform.position,.55f,D(1.2f),CC,hit);
                            SigHit(marks[i],D(.9f),Vector2.zero,hit);
                        }
                        ActionShake(.14f);
                    });
                    break;
                }
                // ---- greatswords
                case "lumber_blade":
                    sig.Until=combatClock+.5f;SigSwing(MeleeAttackMotion.Sweep,1.25f);break;
                case "warden_door":
                    sig.Stage=1;sig.Flag=false;sig.Until=combatClock+.55f;sigLockUntil=sig.Until;IsGuarding=true;weaponActionTimer=.55f;break;
                case "cinder_cleaver":
                {
                    SigSwing(MeleeAttackMotion.Cleave,1.1f);var far=Ahead(4);SigCastAt((from+far)*.5f,.7f);
                    SigAfter(.13f,()=>{
                        SigFx.Line(A,from,far,1.1f,true);
                        for(int i=0;i<5;i++)
                        {
                            var at=Vector2.Lerp(from,far,(i+.5f)/5);int index=i;
                            SigAfter(index*.06f,()=>{SigFx.Play(B,at,0,.7f);foreach(var enemy in SigArea(at,.8f,D(.3f),null,1,0))EnemyStatus.On(enemy).AddDot(this,"burn",D(.08f),2);});
                        }
                    });
                    break;
                }
                case "glacier_tomb":
                {
                    // Each pillar is bigger than the last, so the ridge climbs away from the hero and hits harder the further it goes.
                    SigSwing(MeleeAttackMotion.Cleave,1f);var clips=new[]{A,B,CC};
                    for(int i=0;i<3;i++)
                    {
                        var at=CombatActionGeometry.Clip(from,aim,1.6f+i*1.6f);int index=i;
                        SigAfter(.14f+.13f*i,()=>{SigFx.Play(clips[index],at,0,-1,true);foreach(var enemy in SigArea(at,1.1f+.4f*index,D(.55f+.2f*index),hit,2,.1f+index*.05f))EnemyStatus.On(enemy).Slow(.55f,2);});
                    }
                    break;
                }
                case "thunder_slug":
                {
                    SigSwing(MeleeAttackMotion.Cleave,1.05f);sig.Point=Ahead(1.6f);SigCastAt(sig.Point);sig.Target=null;sig.Stage=1;sig.Until=combatClock+.45f;
                    SigAfter(.15f,()=>{
                        if(sig==null)return;
                        var target=sig.Target;var fixedPoint=sig.Point;
                        var nail=SigFx.Loop(A,target!=null?(Vector2)target.transform.position:fixedPoint,0,false,12,target!=null?target.transform:null,1,.8f);
                        SigAfter(.7f,()=>{var p=target!=null?(Vector2)target.transform.position:fixedPoint;SigFx.Play(B,p,0,.42f);SigFx.Play(CC,p,0,.45f,true);SigArea(p,1.5f,D(1.4f),hit,3,.12f);});
                    });
                    break;
                }
                case "gravity_stele":
                {
                    var center=Ahead(2.3f);sigLockUntil=combatClock+.2f;
                    SigCastAt(center,1,true);
                    for(int i=0;i<7;i++)SigAfter(i*.1f,()=>{foreach(var enemy in CombatActionGeometry.Circle(center,3.2f))SigPull(enemy,center,6);});
                    SigAfter(.72f,()=>{SigSwing(MeleeAttackMotion.Cleave,.6f);SigFx.Play(CC,center,0,.3f,true);SigFx.Play(B,center,0,.6f);SigArea(center,1.9f,D(1.6f),hit,5,.16f);});
                    break;
                }
                case "executioner_scale":
                    sig.Stage=1;sig.Until=combatClock+.55f;sigLockUntil=sig.Until+.1f;
                    sig.View=SigFx.Loop(A,(Vector2)transform.position+Vector2.up*.9f,0,false,12,transform,1,.6f);break;
                case "dawn_gate":
                {
                    var gate=Ahead(1.6f);sig.Point=gate;SigCastAt(gate);SigFx.Play(A,gate,0,.62f);
                    var dir=aim;
                    SigAfter(.28f,()=>{SigSwing(MeleeAttackMotion.Cleave,.6f);SigSeg(gate,gate+dir*5.6f,1.15f,D(2f),B,hit,4);ActionShake(.15f);if(sig!=null)sig.Until=combatClock+3;});
                    break;
                }
                case "continent_scar":
                {
                    SigSwing(MeleeAttackMotion.Cleave,.8f);var a=from+aim*.6f;var b=CombatActionGeometry.Clip(from,aim,6.6f);var mid=(a+b)*.5f;SigCastAt(mid,.75f);
                    SigFx.Line(A,a,b,1.25f,true);
                    for(int i=0;i<5;i++)
                    {
                        var p1=Vector2.Lerp(a,mid,i/5f);var p2=Vector2.Lerp(b,mid,i/5f);
                        SigAfter(.5f+i*.1f,()=>{SigFx.Play(B,p1,0,.5f);SigFx.Play(B,p2,0,.5f);SigArea(p1,.85f,D(.35f),hit,2,0);SigArea(p2,.85f,D(.35f),hit,2,.05f);});
                    }
                    SigAfter(1.05f,()=>{SigFx.Play(CC,mid,0,.7f);SigArea(mid,2.2f,D(2f),hit,6,.2f);});
                    break;
                }
                // ---- katanas
                case "trainee_blade":
                    sig.Stage=1;sig.Until=combatClock+.16f;sigLockUntil=sig.Until;weaponActionTimer=.16f;attackTimer=Mathf.Max(attackTimer,.3f);break;
                case "wanderer_blade":
                {
                    BeginActionMelee(MeleeAttackMotion.Sweep,.4f);
                    SigDash(aim,2.5f,.16f,null,()=>{var to=(Vector2)transform.position;SigSeg(from,to,1f,D(1.35f),A,hit);SigFx.Play(B,to,angle,.45f);});
                    break;
                }
                case "moon_white":
                {
                    SigSwing(MeleeAttackMotion.Sweep,.3f);
                    for(int i=-1;i<=1;i+=2){var dir=(Vector2)(Quaternion.Euler(0,0,i*13)*aim);var wave=SigBolt.Shoot(this,A,from+aim*.4f,dir,12.5f,D(1.05f),.42f,4,7);wave.Impact=B;}
                    break;
                }
                case "red_lotus":
                {
                    SigSwing(MeleeAttackMotion.Sweep,.8f);var far=Ahead(3.5f);SigFx.Line(A,from,far,.55f,true);
                    SigAfter(.45f,()=>{for(int i=0;i<5;i++){var at=Vector2.Lerp(from,far,(i+.5f)/5);int index=i;SigAfter(index*.05f,()=>{SigFx.Play(B,at,0,-1);SigArea(at,.72f,D(.5f),hit,1,.03f);});}SigAfter(.28f,()=>{SigFx.Play(C("E"),far,0,-1);SigArea(far,1.95f,D(1.1f),hit,3,.14f);});});
                    break;
                }
                case "wind_cutter":
                    sig.Stage=1;sig.Until=combatClock+3.2f;sig.View=SigFx.Loop(A,SkillOrigin,0,false,12,transform);break;
                case "falling_blossom":
                    sig.Stage=1;sig.Flag=true;sig.Timer=combatClock;sigReady=combatClock+.1f;SigSwing(MeleeAttackMotion.Sweep,.7f,0);SigFx.Play(A,Ahead(1.3f),angle);break;
                case "mu_eum":
                {
                    SigSwing(MeleeAttackMotion.Sweep,.2f);sig.Marks.Clear();sig.Stage=1;sig.Until=combatClock+.6f;sigHurt=false;
                    foreach(var enemy in CombatActionGeometry.Segment(from,Ahead(2.9f),2.3f)){sig.Marks.Add(enemy);sig.Views.Add(SigFx.Loop(A,enemy.transform.position,angle,false,12,enemy.transform));}
                    break;
                }
                case "instant_king":
                    sig.Count=2;sig.Until=combatClock+InstantKingWindow;sig.Points.Clear();sig.ClearViews();SigInstantCut(0);break;
                case "thousand_returns":
                {
                    // A ghost of the hero stands in each spot and swings for real: a spinning cut, then a cross on top of it.
                    var points=new List<Vector2>(sig.Points);var angles=new List<float>(sig.Angles);var marks=new List<SigFx>(sig.Views);var weapon=EquippedWeapon;var cross=C("Cross");
                    sig.Points.Clear();sig.Angles.Clear();sig.Views.Clear();
                    for(int i=0;i<points.Count;i++)
                    {
                        int index=i;
                        SigAfter(.2f*i,()=>{
                            if(marks[index]!=null)marks[index].Stop();
                            var dir=new Vector2(Mathf.Cos(angles[index]*Mathf.Deg2Rad),Mathf.Sin(angles[index]*Mathf.Deg2Rad));var feet=points[index];
                            SigPhantom.Swing(feet,dir,2,weapon,new Color(.7f,.9f,1f,.9f));
                            SigFx.Play(B,feet,angles[index],.56f,false,dir.x<0);SigArea(feet,PhantomReach,D(.7f),hit,3,.08f);
                            SigAfter(.17f,()=>{SigFx.Play(cross,feet+dir*.5f,angles[index],.56f);SigArea(feet,PhantomReach,D(.5f),null,2,.05f);});
                        });
                    }
                    break;
                }
                // ---- spears
                case "sentry_spear":
                    sig.Until=combatClock+.35f;SigSwing(MeleeAttackMotion.Thrust,1.35f);SigFx.Play(B,Feet-aim*1.1f,0,.5f,true,false,.55f);
                    {var tip=SkillOrigin;var dir=aim;SigAfter(.08f,()=>{var end=CombatActionGeometry.Clip(tip,dir,4);SigFx.Reach(CC,tip,end,4);foreach(var enemy in SigSeg(tip,end,.7f,D(.7f),null,hit,6))enemy.Stagger(.7f);ActionShake(.08f);});}
                    break;
                case "hunt_harpoon":
                {
                    SigSwing(MeleeAttackMotion.Thrust,.2f);
                    var rope=SigFx.Line(B,from,from+aim*.1f,-1,false,true).Native(4.4f);
                    var harpoon=SigBolt.Shoot(this,A,from+aim*.5f,aim,16,D(1f),.3f,1,6.5f);harpoon.Impact=CC;
                    harpoon.OnMove=b=>{if(rope!=null)rope.SetLine(transform.position,b.transform.position);};
                    harpoon.OnHit=(enemy,point)=>{if(rope!=null)rope.Stop();SigPull(enemy,transform.position,16);SigFx.Play(CC,point,0,-1);ActionShake(.1f);};
                    harpoon.OnEnd=(point,wall)=>{if(rope!=null)rope.Stop();};
                    break;
                }
                case "side_blade_pike":
                    SigSwing(MeleeAttackMotion.Sweep,.05f);SigFx.Play(A,transform.position,angle,.56f,true);SigFx.Play(B,Feet,0,.5f,true);SigArea(transform.position,2.5f,D(1.2f),hit,8,.12f);break;
                case "thunder_needle":
                    sig.Stage=1;sig.Until=combatClock+7;sig.Point=Ahead(2.6f);if(sig.View!=null)sig.View.Stop();sig.View=SigFx.Loop(A,sig.Point,0,false);SigSwing(MeleeAttackMotion.Thrust,.2f);
                    {var at=sig.Point;SigFx.Play(CC,at,0,-1,true);SigAfter(.08f,()=>{SigFx.Play(C("D"),at,0,-1);SigArea(at,2f,D(.8f),hit,3,.12f);});}
                    break;
                case "tide_fang":
                {
                    // The current goes out in three jets as wide as the hit, whirls at the far end, then comes back and drags everything home.
                    SigSwing(MeleeAttackMotion.Thrust,.8f);var far=Ahead(4.2f);var tip=SkillOrigin;var side=new Vector2(-aim.y,aim.x)*.2f;
                    foreach(var off in new[]{-1,0,1})SigFx.Play(A,tip+side*off,angle+off*8,-1,false,off<0);
                    SigAfter(.1f,()=>{SigSeg(from,far,1.25f,D(.75f),null,hit,8);SigFx.Play(CC,far,0,-1);ActionShake(.08f);});
                    SigAfter(.28f,()=>SigFx.Play(C("D"),far,0,-1,true));
                    SigAfter(.62f,()=>{
                        var home=(Vector2)transform.position;var back=Mathf.Atan2(home.y-far.y,home.x-far.x)*Mathf.Rad2Deg;var cross=new Vector2(-(home-far).normalized.y,(home-far).normalized.x)*.2f;
                        foreach(var off in new[]{-1,0,1})SigFx.Play(B,far+cross*off,back+off*8,-1,false,off<0);
                        foreach(var enemy in CombatActionGeometry.Segment(far,home,1.25f)){SigHit(enemy,D(.45f),Vector2.zero,hit);SigPull(enemy,home,8);}
                        SigFx.Play(CC,home+(far-home).normalized*.9f,0,-1);ActionShake(.1f);
                    });
                    break;
                }
                case "thorn_spear":
                {
                    SigSwing(MeleeAttackMotion.Thrust,.9f);var at=Ahead(2.5f);SigFx.Line(A,from,at,.4f,true);
                    SigAfter(.18f,()=>{SigFx.Play(CC,at,0,-1,true);for(int i=0;i<5;i++){var ring=at+new Vector2(Mathf.Cos(i*1.2566f),Mathf.Sin(i*1.2566f))*1f;int index=i;SigAfter(.04f*index,()=>SigFx.Play(CC,ring,0,-1,true));}foreach(var enemy in SigArea(at,1.55f,D(.5f),hit,0,.08f)){EnemyStatus.On(enemy).Root(1.6f);SigFx.Loop(B,enemy.transform.position,0,false,12,enemy.transform,1,1.6f);}});
                    break;
                }
                case "chain_horizon":
                {
                    SigSwing(MeleeAttackMotion.Thrust,.3f);StartCoroutine(SigChainFan(aim,hit));
                    break;
                }
                case "bannerless":
                {
                    // Driven in where the cursor is: the ring is the area, and the next press sends the charge out of it.
                    var at=AimPoint(BannerReach);sig.Stage=1;sig.Until=combatClock+BannerSeconds;sig.Point=at;sig.Timer=combatClock+.5f;sig.ClearViews();
                    SigSwing(MeleeAttackMotion.Thrust,.3f);
                    SigFx.Play(C("Cast"),at,0,-1,true);ActionShake(.12f);
                    sig.View=SigFx.Loop(A,at,0,true);sig.View2=SigFx.Loop(CC,at,0,false,8);
                    SigArea(at,BannerRadius,D(.8f),hit,4,0);
                    break;
                }
                case "dragon_spine":
                {
                    SigSwing(MeleeAttackMotion.Thrust,.9f);sig.ClearViews();sig.Points.Clear();sig.Stage=1;sig.Until=combatClock+5;sig.Dir=aim;
                    var end=Ahead(4.7f);SigSeg(from,end,.7f,D(.6f),B,hit,2);
                    var dropAt=new List<Vector2>();for(int i=0;i<5;i++)dropAt.Add(Vector2.Lerp(from,end,(i+1)/5f));
                    foreach(var p in dropAt)sig.Points.Add(p);
                    for(int i=0;i<dropAt.Count;i++){var p=dropAt[i];SigAfter(i*.06f,()=>{if(sig!=null&&sig.Id=="dragon_spine"&&sig.Stage==1)sig.Views.Add(SigFx.Loop(A,p,angle-90,true));});}
                    break;
                }
                case "polaris":
                    // The thrust itself throws the lance; the star stays for further thrusts through it.
                    sig.Stage=1;sig.Until=combatClock+8;sig.Point=Ahead(2.1f);sig.ClearViews();sig.View=SigFx.Loop(A,sig.Point,0,false);SigSwing(MeleeAttackMotion.Thrust,.4f);
                    {var tip=SkillOrigin;var dir=aim;SigAfter(.08f,()=>SigLightSpear(tip,dir,D(1.2f)));}
                    break;
                // ---- staves
                case "novice_branch":
                    sig.Holding=true;sig.A=0;sigLockUntil=0;sig.View=SigFx.Loop(C("Cast"),SkillOrigin,0,false,30).Tracking(()=>SkillOrigin);break;
                case "charcoal_staff":
                {
                    SigShotPose(aim);SigFx.Play(C("Muzzle"),committedShotOrigin,angle);
                    for(int i=0;i<9;i++){var dir=(Vector2)(Quaternion.Euler(0,0,UnityEngine.Random.Range(-32f,32f))*aim);var ember=SigBolt.Shoot(this,A,committedShotOrigin,dir,UnityEngine.Random.Range(6.5f,10.5f),D(.26f),.2f,1,UnityEngine.Random.Range(2.1f,3.5f));ember.Impact=B;ember.OnHit=(enemy,point)=>EnemyStatus.On(enemy).AddDot(this,"burn",D(.09f),2.5f);}
                    break;
                }
                case "pebble_staff":
                {
                    SigShotPose(aim);var at=AimPoint(4.6f);SigFx.Play(A,at,0,.6f);SigCastAt(at,.5f);
                    SigAfter(.08f,()=>{SigFx.Play(B,at,0,.5f);SigArea(at,1.15f,D(1.5f),hit,5,.1f);});
                    break;
                }
                case "frost_branch":
                {
                    SigShotPose(aim);SigFx.Play(C("Muzzle"),committedShotOrigin,angle);
                    var ice=SigBolt.Shoot(this,A,committedShotOrigin,aim,12,D(1f),.26f,1,9);ice.Impact=hit;
                    ice.OnHit=(enemy,point)=>EnemyStatus.On(enemy).Slow(.55f,2.5f);
                    ice.OnEnd=(point,wall)=>{SigFx.Play(CC,point,0,.5f);var dir=ice.Direction;foreach(var turn in new[]{80f,-80f,110f,-110f}){var shard=SigBolt.Shoot(this,B,point,Quaternion.Euler(0,0,turn)*dir,10,D(.4f),.2f,1,2.6f);shard.Impact=hit;shard.OnHit=(e,p)=>EnemyStatus.On(e).Slow(.6f,1.6f);}};
                    break;
                }
                case "storm_cloud_staff":
                {
                    SigShotPose(aim);var start=Ahead(1.2f);var cloud=SigFx.Loop(A,start,0,false,12,null,1,4.1f);
                    for(int i=1;i<=12;i++)
                    {
                        var at=CombatActionGeometry.Clip(start,aim,i*.38f);
                        SigAfter(i*.33f,()=>{
                            if(cloud!=null)cloud.transform.position=at+Vector2.up*1.6f;
                            var under=CombatActionGeometry.Circle(at,1.7f,1);
                            var p=under.Count>0?(Vector2)under[0].transform.position:at+UnityEngine.Random.insideUnitCircle*1.2f;
                            SigFx.Play(B,p,0,-1,true);if(under.Count>0)SigHit(under[0],D(.42f),Vector2.zero,hit);ActionShake(.05f);
                        });
                    }
                    break;
                }
                case "swamp_lantern":
                {
                    SigShotPose(aim);var at=AimPoint(4.6f);SigCastAt(at,.5f);
                    var glob=SigBolt.Shoot(this,A,committedShotOrigin,at-committedShotOrigin,8.5f,D(.4f),.2f,1,Vector2.Distance(committedShotOrigin,at));glob.Impact=hit;
                    glob.OnEnd=(point,wall)=>{SigFx.Play(CC,point,0,.5f);SigFx.Loop(B,point,0,true,12,null,1,4);for(int i=1;i<=8;i++)SigAfter(i*.5f,()=>{foreach(var enemy in CombatActionGeometry.Circle(point,1.55f))EnemyStatus.On(enemy).AddDot(this,"poison",D(.14f),1.2f);});};
                    break;
                }
                case "refractor_staff":
                    sig.Stage=1;sig.Until=combatClock+7;sig.Point=Ahead(2f);sig.ClearViews();sig.View=SigFx.Loop(A,sig.Point,0,false);SigShotPose(aim);
                    {
                        var prism=sig.Point;var rays=new[]{C("E"),C("G"),C("F")};SigFx.Play(B,prism,0,-1);ActionShake(.1f);
                        for(int i=-1;i<=1;i++){var end=CombatActionGeometry.Clip(prism,Quaternion.Euler(0,0,i*22)*aim,LightSpearUnits);SigFx.Reach(rays[i+1],prism,end,LightSpearUnits);SigSeg(prism,end,.6f,D(.8f),null,hit,2);}
                    }
                    break;
                case "undertaker_candle":
                    SigShotPose(aim);SigCastAt(AimPoint(4.4f));Runner.StartCoroutine(SigSouls(AimPoint(4.4f),A,B,hit));break;
                case "star_fold":
                {
                    // Steps through a door to the cursor and leaves the door open behind.
                    var to=AimPoint(StarFoldReach);sig.Stage=1;sig.Until=combatClock+StarFoldSeconds;sig.Point=from;sig.ClearViews();SigShotPose(aim);
                    SigFx.Play(C("D"),from,0,-1,true);sig.View=SigFx.Loop(A,from,0,true,12);
                    invincibilityTicks=Mathf.Max(invincibilityTicks,GameTick.FromSeconds(.25f));GetComponent<DashAfterimageEffect>()?.Stamp();body.position=to;transform.position=to;
                    SigFx.Play(C("E"),to,0,-1,true);SigFx.Play(C("Cast"),to,0,-1,true);SigArea(to,StarFoldRadius,D(1.2f),hit,5,.14f);
                    break;
                }
                case "season_end":
                {
                    SigShotPose(aim);var at=AimPoint(4.2f);int season=sig.Count%4;sig.Count++;var clips=new[]{A,B,CC,C("D")};
                    SigFx.Play(clips[season],at,0,-1,true);
                    if(season==0)for(int i=0;i<5;i++){var ring=at+new Vector2(Mathf.Cos(i*1.2566f),Mathf.Sin(i*1.2566f))*1.15f;SigAfter(.05f*i,()=>SigFx.Play(A,ring,0,-1,true));}
                    if(season==1)SigFx.Play(C("E"),at,0,-1);
                    if(season==2)SigFx.Play(C("G"),at,0,-1,true);
                    if(season==2)for(int i=0;i<3;i++){var ring=at+new Vector2(Mathf.Cos(i*2.0944f+.5f),Mathf.Sin(i*2.0944f+.5f))*1.1f;SigFx.Play(CC,ring,0,-1,true);}
                    if(season==3)for(int i=0;i<6;i++){var ring=at+new Vector2(Mathf.Cos(i*1.0472f),Mathf.Sin(i*1.0472f))*1.3f;SigAfter(.08f+.03f*i,()=>SigFx.Play(C("F"),ring,0,-1,true));}
                    ActionShake(.1f);
                    foreach(var enemy in SigArea(at,1.9f,D(1f),hit,season==2?9:1))
                    {
                        if(season==0)EnemyStatus.On(enemy).Root(1.5f);
                        else if(season==1)EnemyStatus.On(enemy).AddDot(this,"burn",D(.14f),3);
                        else if(season==3)EnemyStatus.On(enemy).Slow(.45f,3);
                    }
                    break;
                }
                // ---- orbs
                case "whetstone_orb":
                    sig.Stage=1;sig.Until=combatClock+3.2f;orb.Radius=2.6f;orb.Spin=520;orb.ContactUntil=Time.time+3.2f;orb.ContactDamage=D(.45f);orb.ContactHit=hit;
                    SigFx.Play(A,orb.Position,0,-1);sig.Timer=combatClock;break;
                case "lantern_orb":
                    sig.Stage=1;sig.Until=combatClock+6.5f;orb.Pin(AimPoint(3.2f));SigCastAt(orb.PinPoint);sig.ClearViews();sig.View=SigFx.Loop(A,orb.PinPoint,0,true);break;
                case "iron_core":
                {
                    SigShotPose(aim);var origin=orb.Position;SigFx.Play(CC,origin,0,-1);ActionShake(.08f);
                    for(int i=0;i<10;i++){var dir=(Vector2)(Quaternion.Euler(0,0,-55+i*110f/9)*aim);var shard=SigBolt.Shoot(this,A,origin,dir,12,D(.36f),.3f,1,3.6f);shard.ReturnsToOwner=true;shard.Impact=hit;
                        shard.OnCaught=()=>SigFx.Line(B,orb.Position,orb.Position+dir*.8f,.14f);}
                    break;
                }
                case "frost_satellite":
                    sig.Stage=1;sig.Until=combatClock+3.6f;orb.Radius=1.8f;orb.Spin=260;sig.Points.Clear();sig.Timer=combatClock;break;
                case "ember_heart":
                {
                    var at=orb.Position;float heat=sig.A;sig.A=0;if(sig.View!=null){sig.View.Stop();sig.View=null;}
                    SigFx.Play(B,at,0,-1,true,false,Mathf.Lerp(.7f,1.4f,heat/10));SigFx.Play(CC,at,0,-1,true,false,Mathf.Lerp(.6f,1.2f,heat/10));SigArea(at,1.3f+.14f*heat,D(.6f+.42f*heat),hit,4+heat*.3f,.12f+heat*.014f);break;
                }
                case "storm_marble":
                {
                    SigShotPose(aim);sig.Marks.Clear();SigFx.Play(C("E"),orb.Position,0,-1);
                    var marble=SigBolt.Shoot(this,A,orb.Position,aim,11,D(.7f),.5f,1,5.5f);marble.ReturnsToOwner=true;marble.ReturnClip=CC;marble.Impact=hit;
                    marble.OnHit=(enemy,point)=>{
                        if(sig==null)return;
                        if(!marble.Returning){sig.Marks.Add(enemy);sig.Views.Add(SigFx.Loop(C("D"),enemy.transform.position,0,false,15,enemy.transform,1,3));}
                        else if(sig.Marks.Contains(enemy)){SigFx.Play(B,enemy.transform.position,0,-1,true);SigFx.Play(C("E"),(Vector2)enemy.transform.position+Vector2.up*.3f,0,-1);SigArea(point,1.6f,D(1.3f),hit,4,.16f);}
                    };
                    break;
                }
                case "twin_star":
                    sig.Stage=1;sig.Until=combatClock+3;orb.ContactUntil=Time.time+3;orb.ContactDamage=D(.35f);orb.ContactHit=hit;orb.Radius=1.3f;
                    orbTwo.ContactUntil=Time.time+3;orbTwo.ContactDamage=D(.35f);orbTwo.ContactHit=hit;orbTwo.Radius=2.2f;
                    sig.Timer=combatClock;sig.ClearViews();sig.View=SigFx.Line(C("D"),orb.Position,orbTwo.Position,-1,false,true);SigFx.Play(C("Cast"),orb.Position,0,-1);SigFx.Play(C("Cast"),orbTwo.Position,0,-1);break;
                case "mirror_eye":
                    sig.Stage=1;sig.Until=combatClock+MirrorSeconds;sig.ClearViews();SigFx.Play(C("Cast"),orb.Position,0,-1);
                    for(int i=0;i<3;i++)sig.Views.Add(SigFx.Loop(A,SigMirrorAt(i),0,false));
                    SigMirrorVolley(D(.8f));
                    break;
                case "small_eclipse":
                    sig.Stage=1;sig.Until=combatClock+8;orb.Pin(orb.Position);sig.Point=orb.PinPoint;sig.A=0;sig.Count=0;sig.B=Mathf.Atan2(transform.position.y-sig.Point.y,transform.position.x-sig.Point.x)*Mathf.Rad2Deg;sig.ClearViews();
                    sig.View=SigFx.Loop(C("D"),sig.Point,0,false,30);sig.View.SetScale(EclipseScale(0));
                    // The ring's rim is where the release will hit; it widens as the hero circles.
                    sig.View2=SigFx.Loop(C("E"),sig.Point,0,true);sig.View2.SetScale(EclipseRadius(0)/EclipseRingUnits);
                    SigFx.Play(C("Cast"),sig.Point,0,-1);orb.GetComponent<SpriteRenderer>().enabled=false;break;
                case "world_pendulum":
                    // Everything inside the ring gets caught.
                    sig.Stage=1;sig.Until=combatClock+4;sig.Count=0;sig.ClearViews();sig.View=SigFx.Loop(CC,Feet,0,true,12,transform,1,4);break;
                // ---- guns
                case "scrap_revolver":
                {
                    int shots=0;while(inventory.TryConsumeAmmo()&&shots<6)shots++;
                    var clip=C("Shot");var flash=C("Muzzle");
                    for(int i=0;i<shots;i++)
                    {
                        int index=i;
                        SigAfter(i*.07f,()=>{PrepareRangedShot(SigAim,.09f);var spread=index*4.5f*(index%2==0?1:-1);var dir=(Vector2)(Quaternion.Euler(0,0,spread)*SigAim);SigFx.Play(flash,committedShotOrigin,SigAngle);if(!shotObstructed){var round=SigBolt.Shoot(this,clip,committedShotOrigin,dir,18,D(.62f),.18f,1,11);round.Impact=hit;}ActionShake(.04f);if(index==shots-1)SigFx.Play(A,committedShotOrigin,0,.5f);});
                    }
                    attackTimer=Mathf.Max(attackTimer,shots*.07f);
                    break;
                }
                case "gate_shotgun":
                {
                    SigShotPose(aim);SigFx.Play(A,committedShotOrigin,angle);if(shotObstructed)break;
                    for(int i=0;i<10;i++){var dir=(Vector2)(Quaternion.Euler(0,0,-30+i*60f/9)*aim);var pellet=SigBolt.Shoot(this,C("Shot"),committedShotOrigin,dir,13,D(.32f),.2f,1,4.4f);pellet.Impact=hit;pellet.OnHit=(enemy,point)=>{SigPull(enemy,(Vector2)enemy.transform.position+dir*3,11);SigFx.Play(B,(Vector2)enemy.transform.position+Vector2.down*.25f,0,.5f,true);};}
                    SigDash(-aim,.55f,.1f,null,null,false);ActionShake(.14f);break;
                }
                case "scout_carbine":
                    // The sight sits on the cursor and the sight line runs back to the muzzle for as long as the shot is held.
                    sig.Holding=true;sig.A=0;sig.View=SigFx.Loop(A,AimPoint(ScoutRange),0,false,12).Tracking(()=>AimPoint(ScoutRange));
                    sig.View2=SigFx.Line(CC,SkillOrigin,AimPoint(ScoutRange),-1,false,true);break;
                case "furnace_sprayer":
                {
                    SigShotPose(aim);var clip=A;var smoke=B;
                    var torch=SigFx.Loop(C("Cast"),SkillOrigin,angle,false,30,null,1,1.3f).Tracking(()=>SkillOrigin,()=>SigAngle);
                    for(int i=0;i<15;i++)
                    {
                        int index=i;
                        SigAfter(i*.08f,()=>{if(EquippedWeapon==null||EquippedWeapon.Id!="furnace_sprayer")return;PrepareRangedShot(SigAim,.09f);var dir=(Vector2)(Quaternion.Euler(0,0,UnityEngine.Random.Range(-17f,17f))*SigAim);if(!shotObstructed){var tongue=SigBolt.Shoot(this,clip,committedShotOrigin,dir,10,D(.2f),.5f,3,3.6f);tongue.Impact=hit;tongue.OnHit=(e,p)=>EnemyStatus.On(e).AddDot(this,"burn",D(.08f),2);}if(index%5==0)SigFx.Play(smoke,committedShotOrigin+Vector2.up*.15f,0,.55f);});
                    }
                    attackTimer=Mathf.Max(attackTimer,1.2f);
                    break;
                }
                case "frost_bolt_rifle":
                    sig.Count=3;sig.Stacks.Clear();SigShotPose(aim);SigFx.Play(A,committedShotOrigin,angle,.5f);
                    {var end=CombatActionGeometry.Clip(committedShotOrigin,aim,LightSpearUnits);SigFx.Reach(C("Cast"),committedShotOrigin,end,LightSpearUnits);foreach(var enemy in SigSeg(committedShotOrigin,end,.7f,D(1f),null,hit,2))EnemyStatus.On(enemy).Slow(.5f,2.5f);ActionShake(.1f);}
                    break;
                case "ricochet_pistol":
                {
                    SigShotPose(aim);SigFx.Play(C("Muzzle"),committedShotOrigin,angle);if(shotObstructed)break;
                    var round=SigBolt.Shoot(this,A,committedShotOrigin,aim,17,D(1.05f),.3f,2,26);round.Bounces=2;round.Wall=C("E");round.Impact=B;
                    round.OnHit=(enemy,point)=>{if(round.BouncesDone>0)SigHit(enemy,D(.8f*round.BouncesDone),Vector2.zero,B);};
                    break;
                }
                case "lightning_carbine":
                {
                    sig.Marks.Clear();sig.ClearViews();sig.Stage=1;sig.Until=combatClock+5;
                    for(int i=0;i<3;i++)
                    {
                        int index=i;
                        SigAfter(i*.12f,()=>{PrepareRangedShot(SigAim,.1f);SigFx.Play(C("Muzzle"),committedShotOrigin,SigAngle);if(shotObstructed)return;var shot=SigBolt.Shoot(this,A,committedShotOrigin,Quaternion.Euler(0,0,(index-1)*5)*SigAim,20,D(.5f),.2f,1,11);shot.Impact=hit;shot.OnHit=(enemy,point)=>{if(sig==null||sig.Id!="lightning_carbine"||sig.Marks.Contains(enemy)||sig.Marks.Count>=3)return;sig.Marks.Add(enemy);sig.Views.Add(SigFx.Loop(B,enemy.transform.position,0,false,12,enemy.transform));};});
                    }
                    break;
                }
                case "siege_engineer":
                    // Braced: a slam, dust and plates on the floor that stay as long as the gun is planted.
                    sig.Stage=1;sig.A=0;sigLockUntil=float.MaxValue;sigReady=combatClock+.1f;sig.ClearViews();
                    SigFx.Play(C("Cast"),Feet,0,-1,true);SigFx.Play(C("D"),Feet,0,.5f,true);sig.View2=SigFx.Loop(CC,Feet,0,true,12);
                    sig.Views.Add(SigFx.Line(C("E"),SkillOrigin,AimPoint(ScoutRange),-1,false,true));
                    ActionShake(.12f);PrototypeAudio.Play(PrototypeSound.Block,AudioChannel.Player,.5f);break;
                case "last_bell":
                    sig.Target=SigNearestToAim(9);sig.Stage=1;sig.Until=combatClock+1.2f;sig.Until2=combatClock+6;sig.ClearViews();
                    sig.View=SigFx.Loop(A,sig.Target.transform.position,0,false,12/1.2f*1f,sig.Target.transform);break;
                case "meteor_waybill":
                {
                    var points=new List<Vector2>(sig.Points);sig.Points.Clear();foreach(var view in sig.Views)if(view!=null)view.Stop();sig.Views.Clear();SigShotPose(aim);
                    points.Add(AimPoint(6.5f));var rock=C("Cast");
                    for(int i=0;i<points.Count;i++)
                    {
                        var at=points[i];
                        SigAfter(.2f*i,()=>{SigFx.Play(rock,at,0,-1);SigAfter(MeteorFall,()=>{SigFx.Play(CC,at,0,-1,true);SigArea(at,1.7f,D(1.4f),hit,4,.18f);});});
                    }
                    break;
                }
            }
        }
        const float SutureReach=2.3f,SutureRadius=1.7f,PhantomReach=2.25f,InstantKingWindow=1.8f,BannerReach=4.6f,BannerRadius=2.6f,BannerSeconds=7,StarFoldReach=7,StarFoldSeconds=6,StarFoldRadius=1.8f,
            MirrorSeconds=6,MeteorFall=.62f,ScoutRange=9,LightSpearUnits=8,EclipseRingUnits=2.6f;
        // One draw of the blade: a step to the cursor side and a line left hanging in the air. The last one sets them all off.
        void SigInstantCut(int step)
        {
            var from=(Vector2)transform.position;var dir=SigAim;var end=Ahead(3.7f);var line=C("B");var hit=C("Hit");
            SigSwing(MeleeAttackMotion.Sweep,.2f,step);
            SigDash(dir,Vector2.Distance(from,end),.07f,null,()=>{
                if(sig==null||sig.Id!="instant_king")return;
                var to=(Vector2)transform.position;SigSeg(from,to,.75f,D(.7f),line,hit,1);sig.Points.Add(from);sig.Points.Add(to);
                sig.Views.Add(SigFx.Line(C("E"),from,to,-1,false,true));ActionShake(.08f);
            });
        }
        void SigInstantShatter()
        {
            var points=new List<Vector2>(sig.Points);sig.Points.Clear();sig.Count=0;var views=new List<SigFx>(sig.Views);sig.Views.Clear();var flash=C("B");var shatter=C("C");var hit=C("Hit");
            CameraFollow.MeleeHitStop?.Invoke(.08f,.05f);
            SigAfter(.1f,()=>{
                foreach(var view in views)if(view!=null)view.Stop();
                for(int i=0;i+1<points.Count;i+=2)
                {
                    var a=points[i];var b=points[i+1];SigSeg(a,b,1.1f,D(1.5f),flash,hit,4);
                    for(int k=0;k<3;k++)SigFx.Play(shatter,Vector2.Lerp(a,b,(k+.5f)/3),0,-1);
                }
                ActionShake(.2f);
            });
        }
        void SigLightSpear(Vector2 from,Vector2 dir,float damage)
        {
            var end=CombatActionGeometry.Clip(from,dir,LightSpearUnits);
            SigFx.Reach(C("C"),from,end,LightSpearUnits);SigSeg(from,end,.75f,damage,null,C("Hit"),2);ActionShake(.08f);
        }
        // Eight spears go out across the fan one after another and stick where they land; the chains go taut, then all of them reel in at once.
        IEnumerator SigChainFan(Vector2 aim,string hit)
        {
            const int Count=8;const float Reach=4.4f,Flight=.06f,Gap=.045f,Hold=.45f,Reel=.16f;
            var chainClip=C("A");var headClip=C("D");var bite=C("B");var yank=C("E");var seen=new HashSet<EnemyController>();
            var chains=new SigFx[Count];var heads=new SigFx[Count];var ends=new Vector2[Count];var dirs=new Vector2[Count];
            float clock=0;
            bool Gone()=>sig==null||sig.Id!="chain_horizon"||ItemEffectsSuspended;
            yield return new WaitForSeconds(.1f);
            for(int i=0;i<Count;i++){dirs[i]=(Vector2)(Quaternion.Euler(0,0,55-i*110f/(Count-1))*aim);ends[i]=CombatActionGeometry.Clip(transform.position,dirs[i],Reach);}
            var flown=new bool[Count];
            while(clock<Gap*Count+Flight&&!Gone())
            {
                for(int i=0;i<Count;i++)
                {
                    var start=Gap*i;if(clock<start)continue;
                    if(chains[i]==null&&!flown[i]){chains[i]=SigFx.Line(chainClip,transform.position,transform.position,-1,false,true).Native(Reach);heads[i]=SigFx.Loop(headClip,transform.position,Mathf.Atan2(dirs[i].y,dirs[i].x)*Mathf.Rad2Deg,false,15);}
                    var k=Mathf.Clamp01((clock-start)/Flight);var from=(Vector2)transform.position;var at=Vector2.Lerp(from,ends[i],k);
                    if(chains[i]!=null)chains[i].SetLine(from,at);if(heads[i]!=null)heads[i].transform.position=at;
                    if(k>=1&&!flown[i])
                    {
                        flown[i]=true;SigFx.Play(bite,ends[i],Mathf.Atan2(dirs[i].y,dirs[i].x)*Mathf.Rad2Deg,-1);ActionShake(.05f);
                        foreach(var enemy in CombatActionGeometry.Segment(from,ends[i],.75f))if(seen.Add(enemy)){SigHit(enemy,D(.9f),Vector2.zero,hit);EnemyStatus.On(enemy).Root(.6f);}
                    }
                }
                clock+=Time.deltaTime;yield return null;
            }
            for(float wait=0;wait<Hold&&!Gone();wait+=Time.deltaTime)
            {
                for(int i=0;i<Count;i++)if(chains[i]!=null)chains[i].SetLine(transform.position,ends[i]);
                yield return null;
            }
            // Reel in: whatever stands on a chain is dragged along with it.
            var dragged=new HashSet<EnemyController>();
            for(float t=0;t<Reel&&!Gone();t+=Time.deltaTime)
            {
                var from=(Vector2)transform.position;var k=t/Reel;k=k*k;
                for(int i=0;i<Count;i++)
                {
                    var at=Vector2.Lerp(ends[i],from,k);if(chains[i]!=null)chains[i].SetLine(from,at);if(heads[i]!=null)heads[i].transform.position=at;
                    foreach(var enemy in CombatActionGeometry.Segment(from,at,.75f))if(dragged.Add(enemy)){SigHit(enemy,D(.5f),Vector2.zero,hit);SigPull(enemy,from,12);}
                }
                yield return null;
            }
            for(int i=0;i<Count;i++){if(chains[i]!=null)chains[i].Stop();if(heads[i]!=null)heads[i].Stop();}
            if(!Gone()){SigFx.Play(yank,SkillOrigin,0,-1);ActionShake(.14f);}
        }
        // Runs each thread out one at a time: a step beside the enemy and a cut across it.
        IEnumerator SigSutureCuts(List<EnemyController> targets)
        {
            var line=C("B");var hit=C("Hit");
            foreach(var target in targets)
            {
                if(target==null||target.IsDead||sig==null||sig.Id!="shadow_suture")continue;
                var c=(Vector2)target.transform.position;var from=(Vector2)transform.position;var to=(c-from).normalized;var side=new Vector2(-to.y,to.x);
                var at=c-to*1f;SigSwing(MeleeAttackMotion.Sweep,.2f);
                SigDash((at-from).normalized,(at-from).magnitude,.09f,null,()=>{if(target==null)return;var p=(Vector2)target.transform.position;SigSeg(p+side*1.6f,p-side*1.6f,.8f,D(1.5f),line,hit,3);ActionShake(.1f);});
                yield return new WaitForSeconds(.2f);
            }
        }
        // Back along the way it came, with a cut on every stretch of the road. The clock stands where the hero was when the press came.
        IEnumerator SigRewind(List<Vector2> path,Vector2 last)
        {
            const float Seconds=.32f;var line=C("B");var hit=C("Hit");
            SigFx.Play(C("Cast"),last,0,-1,true);
            invincibilityTicks=Mathf.Max(invincibilityTicks,GameTick.FromSeconds(Seconds));
            float total=0;for(int i=1;i<path.Count;i++)total+=Vector2.Distance(path[i-1],path[i]);
            int leg=path.Count-1;float walked=0;
            for(float t=0;t<Seconds&&leg>0&&!dead;t+=Time.deltaTime)
            {
                var want=total*t/Seconds;
                while(leg>0&&walked+Vector2.Distance(path[leg],path[leg-1])<want)
                {walked+=Vector2.Distance(path[leg],path[leg-1]);var a=path[leg];var b=path[leg-1];SigSeg(a,b,.85f,D(.7f),line,hit,1);leg--;}
                if(leg>0){var span=Vector2.Distance(path[leg],path[leg-1]);body.position=Vector2.Lerp(path[leg],path[leg-1],span>0?(want-walked)/span:1);}
                GetComponent<DashAfterimageEffect>()?.Stamp();
                yield return null;
            }
            for(;leg>0;leg--)SigSeg(path[leg],path[leg-1],.85f,D(.7f),line,hit,1);
            if(path.Count>0)body.position=path[0];
        }
        // Mirrors circle the hero a third of a turn apart.
        Vector2 SigMirrorAt(int index)
        {var a=(combatClock*150+index*120)*Mathf.Deg2Rad;return (Vector2)transform.position+Vector2.up*.15f+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*1.45f;}
        void SigMirrorVolley(float damage)
        {
            var target=AimPoint(30);var ray=C("B");var hit=C("Hit");
            for(int i=0;i<sig.Views.Count;i++)
            {
                if(sig.Views[i]==null)continue;
                var from=(Vector2)sig.Views[i].transform.position;SigFx.Play(C("F"),from,0,-1);
                var end=CombatActionGeometry.Clip(from,target-from,LightSpearUnits);SigFx.Reach(ray,from,end,LightSpearUnits);SigSeg(from,end,.5f,damage,null,hit,1);
            }
        }
        void SigMirrorBreak(bool throwShards)
        {
            var hit=C("Hit");var shard=C("D");
            for(int i=0;i<sig.Views.Count;i++)
            {
                if(sig.Views[i]==null)continue;
                var from=(Vector2)sig.Views[i].transform.position;SigFx.Play(C("C"),from,0,-1);
                if(throwShards)for(int k=0;k<3;k++)
                {var a=UnityEngine.Random.Range(0,360f)*Mathf.Deg2Rad;var piece=SigBolt.Shoot(this,shard,from,new Vector2(Mathf.Cos(a),Mathf.Sin(a)),11,D(.5f),.26f,1,8);piece.Impact=hit;piece.HomingTurn=540;piece.HomingRange=7;}
            }
            sig.Stage=0;sig.ClearViews();
        }
        // How far the eclipse reaches for how much of the hero's circling has gone into it, and how large it stands.
        float EclipseRadius(float turns)=>1.8f+turns;
        float EclipseScale(float turns)=>.55f+turns*.4f;
        IEnumerator SigSouls(Vector2 target,string flameClip,string burstClip,string hit)
        {
            var start=SkillOrigin;
            var flames=new SigFx[3];var bends=new[]{1.8f,-1.8f,0f};var ends=new Vector2[3];
            var side=new Vector2(-SigAim.y,SigAim.x);
            for(int i=0;i<3;i++){flames[i]=SigFx.Loop(flameClip,start,0,false);ends[i]=target+side*(i-1)*.45f;}
            for(float t=0;t<.55f;t+=Time.deltaTime)
            {
                var k=t/.55f;
                for(int i=0;i<3;i++)if(flames[i]!=null){var control=(start+ends[i])*.5f+side*bends[i];flames[i].transform.position=(1-k)*(1-k)*start+2*(1-k)*k*control+k*k*ends[i];}
                yield return null;
            }
            yield return new WaitForSeconds(.28f);
            for(int i=0;i<3;i++)
            {
                if(flames[i]!=null)flames[i].Stop();
                if(!ItemEffectsSuspended){SigFx.Play(burstClip,ends[i],0,.65f);SigArea(ends[i],1.15f,D(.85f),hit,3,.08f);}
                yield return new WaitForSeconds(.24f);
            }
        }

        // ---- second press. True when the press was spent here.
        bool SigRecast()
        {
            var aim=SigAim;var from=(Vector2)transform.position;var hit=C("Hit");
            switch(sig.Id)
            {
                case "frost_fang":
                    if(sig.Stage==1&&combatClock<sig.Until&&sig.Target!=null){sig.Stage=2;sig.Until=combatClock+.4f;SigSwing(MeleeAttackMotion.Thrust,1f);return true;}break;
                case "shadow_suture":
                    if(sig.Stage==2&&sig.Marks.Count>0)
                    {
                        var targets=new List<EnemyController>(sig.Marks);sig.Marks.Clear();sig.ClearViews();sig.Stage=0;
                        targets.Sort((a,b)=>((Vector2)a.transform.position-from).sqrMagnitude.CompareTo(((Vector2)b.transform.position-from).sqrMagnitude));
                        StartCoroutine(SigSutureCuts(targets));
                        return true;
                    }break;
                case "hour_minute":
                    if(sig.Stage==1)
                    {
                        var path=new List<Vector2>(sig.Points);path.Add(from);sig.Stage=0;sig.ClearViews();
                        StartCoroutine(SigRewind(path,from));
                        return true;
                    }break;
                case "falling_blossom":
                    if(sig.Stage>=1&&sig.Stage<3&&combatClock<sig.Timer+1.1f)
                    {
                        var gap=combatClock-sig.Timer;sig.Flag&=gap>=.18f&&gap<=.75f;sig.Timer=combatClock;sig.Stage++;
                        // Three different blows of the combo, not the first one three times.
                        SigSwing(MeleeAttackMotion.Sweep,.8f,sig.Stage-1);SigFx.Play(C(sig.Stage==2?"C":"D"),Ahead(1.3f),SigAngle);
                        if(sig.Stage==3)
                        {
                            var perfect=sig.Flag;var at=Ahead(1.3f);var flower=C("B");var opening=C("E");sig.Stage=0;
                            if(perfect)
                            {
                                CameraFollow.MeleeHitStop?.Invoke(.07f,.05f);
                                SigAfter(.16f,()=>{SigFx.Play(flower,at,0,-1);SigArea(at,2.25f,D(2.1f),hit,5,.2f);SigAfter(.12f,()=>SigFx.Play(opening,at,0,-1));SigAfter(.24f,()=>{SigFx.Play(opening,at+new Vector2(.9f,.5f),0,-1);SigFx.Play(opening,at+new Vector2(-.9f,-.5f),0,-1);});});
                            }
                        }
                        return true;
                    }break;
                case "instant_king":
                    if(sig.Count>0)
                    {
                        sig.Count--;sig.Until=combatClock+InstantKingWindow;bool last=sig.Count==0;
                        SigInstantCut(2-sig.Count);
                        if(last)SigAfter(.12f,()=>{if(sig!=null&&sig.Id=="instant_king")SigInstantShatter();});
                        return true;
                    }break;
                case "dragon_spine":
                    if(sig.Stage==1)
                    {
                        sig.Stage=0;var nodes=new List<Vector2>(sig.Points);sig.ClearViews();var spike=C("C");var side=new Vector2(-sig.Dir.y,sig.Dir.x);
                        for(int i=0;i<nodes.Count;i++)
                        {
                            var p=nodes[i];
                            // A pair of ribs at every bone of the spine, each pointing away from it.
                            SigAfter(i*.06f,()=>{var outward=Mathf.Atan2(side.y,side.x)*Mathf.Rad2Deg-90;SigFx.Play(spike,p+side*.25f,outward,.7f,true);SigFx.Play(spike,p-side*.25f,outward+180,.7f,true,true);SigFx.Play(C("D"),p,0,.5f);SigSeg(p-side*1.5f,p+side*1.5f,.6f,D(.75f),null,hit,2);ActionShake(.04f);});
                        }
                        return true;
                    }break;
                case "bannerless":
                    if(sig.Stage==1)
                    {
                        // Shades of the spear charge out of the ring, shoulder to shoulder, towards the cursor.
                        var at=sig.Point;var dir=(AimPoint(30)-at).sqrMagnitude>.04f?(AimPoint(30)-at).normalized:aim;var side=new Vector2(-dir.y,dir.x);var spear=C("B");var burst=C("Cast");sig.Stage=0;sig.ClearViews();
                        SigSwing(MeleeAttackMotion.Thrust,.3f);SigFx.Play(burst,at,0,-1,true);ActionShake(.14f);
                        for(int i=-2;i<=2;i++)
                        {
                            var start=at+side*i*.7f-dir*(Mathf.Abs(i)*.5f);float delay=Mathf.Abs(i)*.06f;
                            SigAfter(delay,()=>{var end=CombatActionGeometry.Clip(start,dir,LightSpearUnits);SigFx.Reach(spear,start,end,LightSpearUnits);SigSeg(start,end,.7f,D(.8f),null,hit,3);ActionShake(.06f);});
                        }
                        return true;
                    }break;
                case "star_fold":
                    if(sig.Stage==1)
                    {
                        // Back through the door: the fold closes behind the hero and everything between the two doors is cut by it.
                        var door=sig.Point;sig.Stage=0;sig.ClearViews();SigShotPose(aim);
                        SigFx.Play(C("D"),from,0,-1,true);SigFx.Reach(C("F"),from,door,8);SigSeg(from,door,1.2f,D(1.7f),null,hit,3);
                        invincibilityTicks=Mathf.Max(invincibilityTicks,GameTick.FromSeconds(.25f));body.position=door;transform.position=door;
                        SigFx.Play(C("E"),door,0,-1,true);SigFx.Play(C("Cast"),door,0,-1,true);SigArea(door,StarFoldRadius,D(.8f),hit,4,.14f);
                        return true;
                    }break;
                case "mirror_eye":
                    if(sig.Stage==1){SigMirrorBreak(true);ActionShake(.1f);return true;}break;
                case "wind_cutter":
                    if(sig.Stage==1){SigWindRelease();return true;}break;
                case "small_eclipse":
                    if(sig.Stage==1)
                    {
                        // The corona folds inward for a beat, then the whole ring goes off at once.
                        var turns=sig.A/360f;var at=sig.Point;var radius=EclipseRadius(turns);sig.Stage=0;sig.ClearViews();orb.Release();orb.GetComponent<SpriteRenderer>().enabled=true;
                        var bigger=Mathf.Clamp(radius/2.6f,.8f,1.5f);
                        SigFx.Play(C("C"),at,0,.45f,true,false,bigger);foreach(var enemy in CombatActionGeometry.Circle(at,radius))SigPull(enemy,at,8);
                        CameraFollow.MeleeHitStop?.Invoke(.08f,.05f);
                        SigAfter(.28f,()=>{SigFx.Play(C("B"),at,0,-1,true,false,bigger);SigFx.Play(C("F"),at,0,-1,true,false,bigger);SigArea(at,radius,D(.8f+turns*1.7f),hit,-6,.24f);});
                        return true;
                    }break;
                case "world_pendulum":
                    if(sig.Stage==1&&sig.Count>0)
                    {
                        int count=sig.Count;var clip=C("B");sig.Stage=0;sig.Count=0;var views=new List<SigFx>(sig.Views);sig.Views.Clear();if(sig.View!=null){sig.View.Stop();sig.View=null;}
                        for(int i=0;i<count;i++)
                        {
                            int index=i;
                            SigAfter(i*.09f,()=>{if(index<views.Count&&views[index]!=null){var start=views[index].transform.position;views[index].Stop();var shot=SigBolt.Shoot(this,clip,start,SigAim,15,D(.85f),.26f,1,14);shot.Impact=hit;shot.HomingTurn=420;shot.HomingRange=9;}});
                        }
                        return true;
                    }break;
                case "lightning_carbine":
                    if(sig.Stage==1&&sig.Marks.Count>0)
                    {
                        var marks=new List<EnemyController>(sig.Marks);sig.Marks.Clear();sig.Stage=0;sig.ClearViews();var arc=C("C");var pop=C("D");
                        for(int i=0;i<marks.Count;i++)
                        {
                            int index=i;var a=marks[i];var b=i+1<marks.Count?marks[i+1]:null;
                            SigAfter(i*.1f,()=>{if(a==null)return;SigFx.Play(C("E"),a.transform.position,0,-1,true);SigFx.Play(pop,a.transform.position,0,-1);SigHit(a,D(1f),Vector2.zero);ActionShake(.08f);if(b!=null)SigSeg(a.transform.position,b.transform.position,.5f,D(.8f),arc,null,0);});
                        }
                        return true;
                    }break;
                case "siege_engineer":
                    if(sig.Stage==1){sig.Stage=0;sigLockUntil=0;sig.ClearViews();SigFx.Play(C("D"),Feet,0,.5f,true);return true;}break;
                case "last_bell":
                    if(sig.Stage==1&&combatClock>=sig.Until&&sig.Target!=null)
                    {
                        SigShotPose(aim);var target=sig.Target;var shock=C("B");sig.Stage=0;sig.ClearViews();SigFx.Play(C("Muzzle"),committedShotOrigin,SigAngle);
                        var dir=((Vector2)target.transform.position-committedShotOrigin).normalized;
                        var shot=SigBolt.Shoot(this,C("Shot"),committedShotOrigin,dir,32,D(1.7f),.2f,1,14);shot.Impact=hit;
                        shot.OnHit=(enemy,point)=>{if(enemy!=target)return;SigAfter(.2f,()=>{var end=CombatActionGeometry.Clip(point,dir,LightSpearUnits);SigFx.Reach(shock,point,end,LightSpearUnits);SigSeg(point,end,1f,D(1.9f),null,hit,5);ActionShake(.2f);CameraFollow.MeleeHitStop?.Invoke(.07f,.05f);});};
                        return true;
                    }
                    if(sig.Stage==1)return true;
                    break;
            }
            return false;
        }

        void SigWindRelease()
        {
            sig.Stage=0;if(sig.View!=null){sig.View.Stop();sig.View=null;}
            var tip=SkillOrigin;foreach(var turn in new[]{-18f,0f,18f}){var wave=SigBolt.Shoot(this,C("B"),tip,Quaternion.Euler(0,0,turn)*SigAim,15,D(turn==0?1.5f:.9f),.6f,9,7f);wave.Impact=C("Hit");}
            ActionShake(.1f);BeginActionMelee(MeleeAttackMotion.Sweep,.5f);
        }

        // ---- things that run every frame
        void SigTick()
        {
            if(sig==null)return;
            if(sig.Holding)
            {
                sig.A+=Time.deltaTime;
                if(settings.GetKeyUp(GameAction.Guard)||sig.A>=1.4f){sig.Holding=false;SigReleaseHold();}
            }
            sig.Marks.RemoveAll(enemy=>enemy==null||enemy.IsDead);
            switch(sig.Id)
            {
                case "frost_fang":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=0;sig.Target=null;if(sig.View!=null){sig.View.Stop();sig.View=null;}}break;
                case "viper_tongue":
                    if(sig.View!=null&&(sig.Target==null||sig.Target.IsDead)){sig.View.Stop();sig.View=null;sig.Target=null;}break;
                case "shadow_suture":
                    if(sig.Stage==2)
                    {
                        if(sig.Marks.Count==0||combatClock>=sig.Until2){sig.Stage=0;sig.Marks.Clear();sig.ClearViews();break;}
                        while(sig.Views.Count>sig.Marks.Count){var extra=sig.Views.Count-1;if(sig.Views[extra]!=null)sig.Views[extra].Stop();sig.Views.RemoveAt(extra);}
                        for(int i=0;i<sig.Views.Count;i++)if(sig.Views[i]!=null)sig.Views[i].SetLine(SkillOrigin,sig.Marks[i].transform.position);
                    }break;
                case "hour_minute":
                    if(sig.Stage==1)
                    {
                        if(combatClock>=sig.Until){sig.Stage=0;sig.ClearViews();}
                        else if(combatClock>=sig.Timer)
                        {
                            sig.Timer=combatClock+.12f;var last=sig.Points[sig.Points.Count-1];var now=(Vector2)transform.position;
                            // The road home stays on the floor as a thin line, so the press has something to show for itself.
                            if(Vector2.Distance(last,now)>.2f){sig.Views.Add(SigFx.Line(C("C"),last,now,-1,true,true));sig.Points.Add(now);}
                        }
                    }break;
                case "night_thread":
                    for(int i=sig.Views.Count-1;i>=0;i--)if(i>=sig.Marks.Count){if(sig.Views[i]!=null)sig.Views[i].Stop();sig.Views.RemoveAt(i);}break;
                case "warden_door":
                    if(sig.Stage==1&&(combatClock>=sig.Until||sig.Flag))
                    {
                        sig.Stage=0;IsGuarding=false;weaponActionTimer=0;sigLockUntil=0;var aim=SigAim;var front=Ahead(1.4f);
                        SigSeg(transform.position,(Vector2)transform.position+aim*1.9f,1.7f,D(.7f),null,C("Hit"),9);
                        SigSwing(MeleeAttackMotion.Cleave,sig.Flag?1.8f:1.05f);SigAfter(.12f,()=>SigFx.Play(C("C"),front,0,.5f,true));
                    }break;
                case "executioner_scale":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=2;sig.Until2=combatClock+.5f;SigSwing(MeleeAttackMotion.Cleave,1f);}
                    if(sig.Stage==2&&combatClock>=sig.Until2)sig.Stage=0;
                    break;
                case "trainee_blade":
                    if(sig.Stage==1&&combatClock>=sig.Until)
                    {
                        sig.Stage=0;var end=Ahead(3.4f);BeginActionMelee(MeleeAttackMotion.Sweep,.1f);SigSeg(transform.position,end,.65f,D(1.7f),C("A"),C("Hit"),4);
                        SigFx.Play(C("B"),end,0,.5f);ActionShake(.08f);
                    }break;
                case "wind_cutter":
                    if(sig.Stage==1&&combatClock>=sig.Until)SigWindRelease();break;
                case "mu_eum":
                    if(sig.Stage==1&&combatClock>=sig.Until)
                    {
                        sig.Stage=0;var clip=C("C");var hit=C("Hit");var turn=C("D");
                        var marks=new List<EnemyController>(sig.Marks);foreach(var view in sig.Views)if(view!=null)view.Stop();sig.Views.Clear();sig.Marks.Clear();
                        if(!sigHurt)foreach(var enemy in marks)if(enemy!=null){var p=(Vector2)enemy.transform.position;SigFx.Line(clip,p+new Vector2(-1.2f,.8f),p+new Vector2(1.2f,-.8f),.34f);SigFx.Line(clip,p+new Vector2(-1.2f,-.8f),p+new Vector2(1.2f,.8f),.34f);SigFx.Play(turn,p,0,-1);SigHit(enemy,D(1.9f),Vector2.zero,hit);}
                        if(marks.Count>0)ActionShake(.12f);
                    }break;
                case "instant_king":
                    if(sig.Count>0&&combatClock>=sig.Until)SigInstantShatter();break;
                case "scout_carbine":
                    if(sig.Holding&&sig.View2!=null)sig.View2.SetLine(SkillOrigin,AimPoint(ScoutRange));break;
                case "novice_branch":
                    if(sig.Holding&&sig.View!=null)sig.View.SetScale(.65f+Mathf.Min(1,sig.A/.75f)*.6f);break;
                case "mirror_eye":
                    if(sig.Stage==1)
                    {
                        if(combatClock>=sig.Until){SigMirrorBreak(false);break;}
                        for(int i=0;i<sig.Views.Count;i++)if(sig.Views[i]!=null)sig.Views[i].transform.position=SigMirrorAt(i);
                    }break;
                case "bannerless":
                    if(sig.Stage==1)
                    {
                        if(combatClock>=sig.Until){sig.Stage=0;sig.ClearViews();break;}
                        if(combatClock>=sig.Timer)
                        {
                            sig.Timer=combatClock+.5f;
                            foreach(var enemy in CombatActionGeometry.Circle(sig.Point,BannerRadius,12)){EnemyStatus.On(enemy).Slow(.55f,.7f);SigHit(enemy,D(.25f),Vector2.zero,C("Hit"));}
                        }
                    }break;
                case "thunder_needle":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=0;if(sig.View!=null){sig.View.Stop();sig.View=null;}}break;
                case "polaris":case "refractor_staff":case "star_fold":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=0;sig.ClearViews();}break;
                case "dragon_spine":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=0;sig.ClearViews();}break;
                case "whetstone_orb":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=0;orb.Radius=1.05f;orb.Spin=150;break;}
                    // The orbit reads as one spinning blade whose edge is where the orb cuts.
                    if(sig.Stage==1&&combatClock>=sig.Timer){sig.Timer=combatClock+.38f;SigFx.Loop(C("D"),transform.position,combatClock*260,true,60,transform,1,.5f);ActionShake(.03f);}
                    break;
                case "lantern_orb":
                    if(sig.Stage==1&&combatClock>=sig.Until){sig.Stage=0;orb.Release();sig.ClearViews();}break;
                case "frost_satellite":
                    if(sig.Stage==1)
                    {
                        if(combatClock>=sig.Until){sig.Stage=0;orb.Radius=1.05f;orb.Spin=150;break;}
                        if(combatClock>=sig.Timer)
                        {
                            sig.Timer=combatClock+.16f;var p=orb.Position;SigFx.Play(C("A"),p,0,-1,true);
                            foreach(var enemy in CombatActionGeometry.Circle(p,.8f)){EnemyStatus.On(enemy).Slow(.5f,1.4f);if(!sig.Stacks.TryGetValue(enemy,out var count)||count%5==0)SigHit(enemy,D(.2f),Vector2.zero);sig.Stacks[enemy]=count+1;}
                        }
                    }break;
                case "twin_star":
                    if(sig.Stage==1&&combatClock>=sig.Until)
                    {
                        // They crash together where the beam last ran.
                        sig.Stage=0;orb.Radius=1.05f;orbTwo.Radius=1.5f;var crash=(orb.Position+orbTwo.Position)*.5f;sig.ClearViews();
                        SigFx.Play(C("C"),crash,0,-1,true);SigArea(crash,1.8f,D(1.2f),C("Hit"),4,.16f);break;
                    }
                    if(sig.Stage==1)
                    {
                        if(sig.View!=null)sig.View.SetLine(orb.Position,orbTwo.Position);
                        if(combatClock>=sig.Timer){sig.Timer=combatClock+.25f;SigSeg(orb.Position,orbTwo.Position,.5f,D(.18f),null,C("Hit"),0);}
                    }
                    break;
                case "small_eclipse":
                    if(sig.Stage==1)
                    {
                        if(combatClock>=sig.Until){sig.Stage=0;sig.ClearViews();orb.Release();orb.GetComponent<SpriteRenderer>().enabled=true;break;}
                        var now=Mathf.Atan2(transform.position.y-sig.Point.y,transform.position.x-sig.Point.x)*Mathf.Rad2Deg;
                        sig.A=Mathf.Min(720,sig.A+Mathf.Abs(Mathf.DeltaAngle(sig.B,now)));sig.B=now;
                        var charged=sig.A/360f;sig.View.SetScale(EclipseScale(charged));sig.View2.SetScale(EclipseRadius(charged)/EclipseRingUnits);
                    }break;
                case "world_pendulum":
                    if(sig.Stage==1&&combatClock>=sig.Until&&sig.Count==0){sig.Stage=0;if(sig.View!=null){sig.View.Stop();sig.View=null;}}
                    for(int i=0;i<sig.Views.Count;i++)if(sig.Views[i]!=null){var a=(combatClock*200+i*360f/Mathf.Max(1,sig.Views.Count))*Mathf.Deg2Rad;sig.Views[i].transform.position=(Vector2)transform.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*1.3f;}
                    break;
                case "siege_engineer":
                    // While the gun is planted a sight line runs from the muzzle along the aim.
                    if(sig.Stage==1&&sig.Views.Count>0&&sig.Views[0]!=null)sig.Views[0].SetLine(SkillOrigin,AimPoint(ScoutRange));
                    if(sig.Flag&&combatClock>=sig.Until2){sig.Flag=false;sig.A=0;if(sig.View!=null){sig.View.Stop();sig.View=null;}}
                    if(!sig.Flag&&attackTimer<=-.2f)sig.A=Mathf.Max(0,sig.A-Time.deltaTime*.45f);
                    break;
                case "last_bell":
                    if(sig.Stage==1&&(sig.Target==null||sig.Target.IsDead||combatClock>=sig.Until2)){sig.Stage=0;sig.Target=null;sig.ClearViews();}break;
            }
        }
        Vector2 HeldTip=>SkillOrigin;
        void SigReleaseHold()
        {
            var aim=SigAim;
            if(sig.Id=="scout_carbine"&&sig.View!=null){var shotAt=AimPoint(ScoutRange)-SkillOrigin;if(shotAt.sqrMagnitude>.04f)aim=shotAt.normalized;}
            sig.ClearViews();
            SigShotPose(aim);var hit=C("Hit");
            switch(sig.Id)
            {
                case "novice_branch":
                {
                    SigFx.Play(C("Muzzle"),committedShotOrigin,SigAngle);if(shotObstructed)break;
                    bool full=sig.A>=.75f;var bolt=SigBolt.Shoot(this,full?C("B"):C("Shot"),committedShotOrigin,aim,full?15:12,D(.8f+Mathf.Min(1,sig.A)*1.3f),full?.32f:.22f,full?2:1,12);bolt.Impact=hit;break;
                }
                case "scout_carbine":
                {
                    SigFx.Play(C("Muzzle"),committedShotOrigin,SigAngle);if(shotObstructed)break;
                    bool steady=sig.A>=.8f;var bolt=SigBolt.Shoot(this,C("Shot"),committedShotOrigin,aim,steady?30:22,D(.9f+Mathf.Min(1,sig.A)*1.2f),.2f,steady?2:1,14);bolt.Impact=steady?C("B"):hit;break;
                }
            }
        }

        // ---- reactions to the player's own attacks
        float SigMeleeMult()
        {
            if(sig==null)return 1;
            if(sig.Id=="acrobat_knife"&&combatClock<sig.Until){sig.Until=0;return 1.6f;}
            return 1;
        }
        float SigDelayMult()
        {
            if(sig==null)return 1;
            if(sig.Id=="acrobat_knife"&&combatClock<sig.Until)return .5f;
            if(sig.Id=="wind_cutter"&&sig.Stage==1)return .55f;
            return 1;
        }
        void SigOnContact(Vector2 center,float angle,MeleeAttackStep step,bool heavy)
        {
            if(sig==null)return;
            var aim=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));var from=(Vector2)transform.position;var hit=C("Hit");
            switch(sig.Id)
            {
                case "merc_slab":
                    if(heavy){SigFx.Play(C("A"),center,0,.5f);ActionShake(.1f);}break;
                case "lumber_blade":
                    if(combatClock<sig.Until)SigFx.Play(C("A"),center,angle,.5f);break;
                case "dawn_gate":
                    if(combatClock<sig.Until){sig.Until=0;var dir=(center-sig.Point).normalized;SigSeg(sig.Point,sig.Point+dir*4.4f,.8f,D(.9f),C("C"),hit,3);}break;
                case "thousand_returns":
                    sig.Points.Add(center);sig.Angles.Add(angle);sig.Views.Add(SigFx.Loop(C("A"),center,angle,false,12,null,1,6));
                    if(sig.Points.Count>3){sig.Points.RemoveAt(0);sig.Angles.RemoveAt(0);if(sig.Views[0]!=null)sig.Views[0].Stop();sig.Views.RemoveAt(0);}
                    break;
                case "thunder_needle":
                    if(sig.Stage==1&&step.Motion==MeleeAttackMotion.Thrust&&Vector2.Distance(from+aim*2.2f,sig.Point)<1.3f)
                    {
                        sig.Stage=0;var at=sig.Point;if(sig.View!=null){sig.View.Stop();sig.View=null;}
                        SigFx.Line(C("B"),from+aim*1.6f,at,.3f);SigFx.Play(C("C"),at,0,-1,true);SigFx.Play(C("D"),at,0,-1);SigArea(at,2.5f,D(1.35f),hit,3,.16f);
                    }break;
                case "polaris":
                    if(sig.Stage==1&&step.Motion==MeleeAttackMotion.Thrust&&Vector2.Distance(from+aim*2.1f,sig.Point)<1.5f)
                    {SigFx.Play(C("B"),sig.Point,0,.3f);SigLightSpear(sig.Point,aim,D(.85f));}
                    break;
            }
        }
        void SigOnMeleeHit(EnemyController enemy,float damage,bool heavy,MeleeAttackMotion motion)
        {
            if(sig==null||enemy==null)return;
            var hit=C("Hit");
            switch(sig.Id)
            {
                case "alley_awl":
                    if(combatClock<sig.Until)
                    {
                        sig.Until=0;bool behind=enemy.FacingDirection*(transform.position.x-enemy.transform.position.x)<0;
                        if(behind)SigHit(enemy,D(1f),Vector2.zero,C("A"));
                    }break;
                case "saw_fang":
                    if(combatClock<sig.Until)EnemyStatus.On(enemy).AddDot(this,"bleed",D(.13f),3,C("A"));break;
                case "frost_fang":
                    if(sig.Stage==1&&sig.Target==null)
                    {sig.Target=enemy;EnemyStatus.On(enemy).Slow(.5f,2.2f);if(sig.View!=null)sig.View.Stop();sig.View=SigFx.Loop(C("A"),(Vector2)enemy.transform.position+Vector2.up*.2f,0,false,12,enemy.transform);}
                    else if(sig.Stage==2&&enemy==sig.Target)
                    {sig.Stage=0;sig.Target=null;if(sig.View!=null){sig.View.Stop();sig.View=null;}SigFx.Play(C("B"),(Vector2)enemy.transform.position+Vector2.up*.15f,0,.55f);SigHit(enemy,D(1.2f),Vector2.zero);ActionShake(.1f);}
                    break;
                case "viper_tongue":
                    if(sig.Stage==1)
                    {sig.Stage=0;sig.Target=enemy;sig.Count=0;if(sig.View!=null)sig.View.Stop();sig.View=SigFx.Loop(C("A"),(Vector2)enemy.transform.position+Vector2.up*.55f,0,false,12,enemy.transform);}
                    else if(sig.Target==enemy)
                    {
                        sig.Count++;sig.View?.SetScale(1+sig.Count*.45f);
                        if(sig.Count>=3)
                        {
                            var at=(Vector2)enemy.transform.position;sig.Target=null;if(sig.View!=null){sig.View.Stop();sig.View=null;}
                            SigFx.Play(C("B"),at,0,.7f);foreach(var near in SigArea(at,1.95f,D(1f),hit,3,.12f))EnemyStatus.On(near).AddDot(this,"poison",D(.16f),3,C("C"));
                        }
                    }
                    break;
                case "blood_bank":
                {
                    sig.A=Mathf.Min(10,sig.A+1);var clip=C("A"+Mathf.Clamp(Mathf.CeilToInt(sig.A/2),1,5));
                    if(sig.View==null)sig.View=SigFx.Loop(clip,(Vector2)transform.position+new Vector2(.5f,.1f),0,false,12,transform);else sig.View.SetClip(clip);
                    break;
                }
                case "night_thread":
                    if(!sig.Marks.Contains(enemy)&&sig.Marks.Count<4){sig.Marks.Add(enemy);sig.Views.Add(SigFx.Loop(C("A"),(Vector2)enemy.transform.position+Vector2.up*.5f,0,false,12,enemy.transform));}break;
                case "lumber_blade":
                    if(combatClock<sig.Until){var side=new Vector2(-SigAim.y,SigAim.x);SigPull(enemy,(Vector2)enemy.transform.position+side,10);}break;
                case "merc_slab":
                    if(heavy)enemy.Stagger(.6f);break;
                case "thunder_slug":
                    if(sig.Stage==1&&sig.Target==null&&combatClock<sig.Until)sig.Target=enemy;break;
                case "executioner_scale":
                    if(sig.Stage==2)
                    {
                        var ratio=1-enemy.Health/Mathf.Max(1,enemy.MaxHealth);SigFx.Line(C("B"),(Vector2)enemy.transform.position+new Vector2(-1,.8f),(Vector2)enemy.transform.position+new Vector2(1,-.8f),.34f);
                        SigHit(enemy,D(2.3f*ratio+.3f),Vector2.zero);
                        if(enemy.IsDead){sigReady=Mathf.Min(sigReady,combatClock+.6f);SigFx.Play(C("C"),enemy.transform.position,0,.6f);}
                    }break;
                case "sentry_spear":
                    if(combatClock<sig.Until&&Vector2.Distance(transform.position,enemy.transform.position)>1.35f){enemy.Stagger(.7f);SigFx.Play(C("A"),(Vector2)enemy.transform.position+Vector2.up*.2f);}break;
            }
        }
        // Every family's ranged LMB passes here so a weapon can change its bolt.
        void SigDressShot(SigBolt bolt)
        {
            if(CurrentMutation==EquipmentMutation.Ricochet)bolt.Bounces=Mathf.Max(bolt.Bounces,1);
            bolt.Impact=C("Hit");bolt.OnHit=(enemy,point)=>SigShotLanded(enemy,point,false);
            switch(sig.Id)
            {
                case "frost_bolt_rifle":
                    if(sig.Count>0)
                    {
                        sig.Count--;bolt.SetClip(C("B"));
                        bolt.OnHit=(enemy,point)=>{
                            if(sig==null||sig.Id!="frost_bolt_rifle")return;
                            sig.Stacks.TryGetValue(enemy,out var count);count++;sig.Stacks[enemy]=count;
                            EnemyStatus.On(enemy).Slow(.75f-.15f*count,2.5f);
                            if(count>=3){sig.Stacks.Remove(enemy);SigFx.Play(C("C"),(Vector2)enemy.transform.position+Vector2.up*.15f,0,.55f);SigHit(enemy,D(1.5f),Vector2.zero);ActionShake(.1f);}
                            else SigFx.Loop(C("D"+count),(Vector2)enemy.transform.position+Vector2.up*.5f,0,false,12,enemy.transform,1,1.2f);
                        };
                    }
                    break;
                case "refractor_staff":
                    if(sig.Stage==1)
                    {
                        var prism=sig.Point;
                        bolt.OnMove=b=>{
                            if(b.Teleported||Vector2.Distance(b.transform.position,prism)>.55f)return;
                            b.Teleported=true;SigFx.Play(C("B"),prism,0,.2f);var dir=b.Direction;var clips=new[]{C("Shot"),C("C"),C("D")};
                            for(int i=-1;i<=1;i++){var split=SigBolt.Shoot(this,clips[i+1],prism,Quaternion.Euler(0,0,i*20)*dir,b.Speed,b.Damage*.8f,.2f,1,8,true,false,attackAnimationSequence);split.Impact=C("Hit");}
                            b.Damage=0;b.MoveTo(prism,dir);b.Finish();
                        };
                    }
                    break;
            }
            if(!shotPlantsRound)return;
            var landed=bolt.OnHit;var ended=bolt.OnEnd;var plantAction=attackAnimationSequence;var plantWeapon=CurrentWeaponIdentity;var plantDamage=Damage*.6f;
            bolt.OnHit=(enemy,point)=>{landed?.Invoke(enemy,point);PlantAwakenedRound(point,plantDamage,plantAction,plantWeapon);};
            bolt.OnEnd=(point,wall)=>{ended?.Invoke(point,wall);PlantAwakenedRound(point,plantDamage,plantAction,plantWeapon);};
        }
        void SigShotLanded(EnemyController enemy,Vector2 point,bool orbStrike)
        {
            if(sig==null||enemy==null)return;
            switch(sig.Id)
            {
                case "ember_heart":
                {
                    sig.A=Mathf.Min(10,sig.A+1);var clip=C("A"+Mathf.Clamp(Mathf.CeilToInt(sig.A/2),1,5));
                    if(sig.View==null)sig.View=SigFx.Loop(clip,orb.Position,0,false,12,orb.transform);else sig.View.SetClip(clip);
                    break;
                }
            }
        }
        void SigOrbSetup()
        {
            orb.ContactHit=C("Hit");
            if(sig.Id=="twin_star")
            {
                orbTwo=SigOrb.Create(this,NativeCombatEffect.Frames(C("Shot"))[0],180);orbTwo.UsePixelFxMaterial();orbTwo.Radius=1.5f;orbTwo.Spin=-190;orbTwo.Impact=C("Hit");orbTwo.ContactHit=C("Hit");
                orb.OnStrikeHit=(enemy,point)=>SigTwinMark(enemy,0);orbTwo.OnStrikeHit=(enemy,point)=>SigTwinMark(enemy,1);
            }
        }
        void SigTwinMark(EnemyController enemy,int side)
        {
            if(sig==null||enemy==null)return;
            sig.Stacks.TryGetValue(enemy,out var bits);bits|=1<<side;sig.Stacks[enemy]=bits;
            if(bits==3)
            {sig.Stacks.Remove(enemy);SigFx.Play(C("C"),(Vector2)enemy.transform.position+Vector2.up*.15f,0,.42f);SigHit(enemy,D(1f),Vector2.zero);}
            else SigFx.Loop(C(side==0?"A":"B"),(Vector2)enemy.transform.position+Vector2.up*.55f,0,false,12,enemy.transform,1,1.4f);
        }
        void SigOrbStrikeStarted(Vector2 direction,float damage,bool critical)
        {
            switch(sig.Id)
            {
                case "twin_star":
                    orbTwo.Damage=damage*.8f;orbTwo.Action=attackAnimationSequence;orbTwo.Strike(direction,2.9f);break;
                case "mirror_eye":
                    if(sig.Stage==1)SigMirrorVolley(damage*.6f);
                    break;
            }
        }
        void SigLob(Vector2 direction,float damage)
        {
            var target=AimPoint(6.5f);var impact=C("Hit");var mark=sig;var origin=committedShotOrigin;
            var shell=SigBolt.Shoot(this,C("Shot"),origin,target-origin,11,damage*.4f,.2f,1,Vector2.Distance(origin,target));shell.Impact=null;
            shell.OnEnd=(point,wall)=>{
                SigFx.Play(impact,point,0,.6f);SigArea(point,1.25f,damage,null,3,.1f);
                if(sig==null||sig!=mark)return;
                sig.Points.Add(point);
                if(sig.Points.Count>3){sig.Points.RemoveAt(0);if(sig.Views.Count>0){if(sig.Views[0]!=null)sig.Views[0].Stop();sig.Views.RemoveAt(0);}}
                foreach(var view in sig.Views)if(view!=null)view.Stop();sig.Views.Clear();
                for(int i=0;i<sig.Points.Count;i++)sig.Views.Add(SigFx.Loop(C("A"+(i+1)),sig.Points[i],0,true));
            };
        }
        bool SigTryBlock(Vector2 direction)
        {
            sigHurt=true;
            if(sig==null||sig.Id!="warden_door"||sig.Stage!=1)return false;
            if(direction.sqrMagnitude<.001f||Vector2.Dot(SigAim,-direction.normalized)<.25f)return false;
            sig.Flag=true;invincibilityTicks=Mathf.Max(invincibilityTicks,GameTick.FromSeconds(.2f));
            SigFx.Play(C("A"),SkillOrigin,SigAngle);OnActiveDefense();
            return true;
        }
        void SigParried()
        {
            if(sig==null)return;
            SigFx.Play(C("A"),SkillOrigin,SigAngle,.42f);
        }
        bool SigCatchProjectile(Vector2 point)
        {
            if(sig==null||sig.Id!="world_pendulum"||sig.Stage!=1||combatClock>=sig.Until||sig.Count>=5)return false;
            if(Vector2.Distance(point,transform.position)>2.3f)return false;
            sig.Count++;sig.Views.Add(SigFx.Loop(C("A"),point,0,false));SigFx.Play(C("D"),point,0,.3f);
            return true;
        }
        // Gun-side rules a shot must respect. False holds the shot.
        bool SigShotAllowed()
        {
            if(sig==null)return true;
            if(sig.Holding)return false;
            if(sig.Id=="siege_engineer"&&sig.Flag)return false;
            return true;
        }
        float SigShotDelayMult()
        {
            if(sig==null)return 1;
            if(sig.Id=="siege_engineer"&&sig.Stage==1)return .34f;
            return 1;
        }
        void SigShotFired(Vector2 direction)
        {
            if(sig==null||sig.Id!="siege_engineer"||sig.Stage!=1)return;
            sig.A+=.055f;
            SigFx.Play(C("A"),(Vector2)transform.position+new Vector2(-direction.x*.2f,.15f),UnityEngine.Random.Range(0,360f),.3f);
            if(sig.View==null&&sig.A>.6f)sig.View=SigFx.Loop(C("B"),committedShotOrigin,0,false,12,transform);
            if(sig.A>=1){sig.Flag=true;sig.Until2=combatClock+1.6f;}
        }
    }
}
