using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Shared plumbing of the 70 signature weapons: input, per-weapon state and the damage/effect helpers their skills are written in.</summary>
    public sealed partial class PlayerController
    {
        sealed class SigState
        {
            public string Id;
            public int Stage,Count;
            public float A,B,Until,Until2,Timer;
            public bool Flag,Holding;
            public Vector2 Point,Point2,Dir;
            public EnemyController Target;
            public SigFx View,View2;
            public readonly List<EnemyController> Marks=new List<EnemyController>();
            public readonly List<Vector2> Points=new List<Vector2>();
            public readonly List<float> Angles=new List<float>();
            public readonly List<SigFx> Views=new List<SigFx>();
            public readonly Dictionary<EnemyController,int> Stacks=new Dictionary<EnemyController,int>();
            public void ClearViews()
            {
                if(View!=null)View.Stop();if(View2!=null)View2.Stop();
                foreach(var view in Views)if(view!=null)view.Stop();
                Views.Clear();View=null;View2=null;
            }
        }
        SigState sig;SigOrb orb,orbTwo;SigRunner sigRunner;float sigReady;
        SigRunner Runner{get{if(sigRunner==null)sigRunner=new GameObject("SigRunner").AddComponent<SigRunner>();return sigRunner;}}
        string C(string role)=>SignatureMeleeFx.Clip(sig.Id,role);
        float D(float multiplier)=>Damage*Stats.SpecialDamageMultiplier*SkillTalentMultiplier*multiplier;
        Vector2 SigAim=>AimDirection.sqrMagnitude>.01f?AimDirection.normalized:Vector2.right*facing;
        float SigAngle=>Mathf.Atan2(SigAim.y,SigAim.x)*Mathf.Rad2Deg;
        Vector2 Feet=>(Vector2)transform.position+Vector2.down*.2f;
        Vector2 Ahead(float distance)=>CombatActionGeometry.Clip(transform.position,SigAim,distance);
        // Where a skill leaves the hero: the orb, the muzzle or the blade's reach along the aim. Never the body itself.
        internal Vector2 SkillOrigin
        {
            get
            {
                if(orb!=null)return orb.Position;
                if(EquippedWeapon==null)return Ahead(.9f);
                if(EquippedWeapon.AttackType==WeaponAttackType.Ranged)
                {
                    var muzzle=HeldWeaponView.ShotMuzzlePosition(EquippedWeapon,transform,SigAim)-(Vector2)transform.position;
                    return CombatActionGeometry.Clip(transform.position,muzzle,muzzle.magnitude);
                }
                var length=NativeWeaponArt.TryGet(EquippedWeapon.Id,out var pose)?Vector2.Distance(pose.Grip,pose.Muzzle)/32f:1f;
                return Ahead(Mathf.Clamp(.35f+length*.8f,.8f,2.2f));
            }
        }
        Vector2 AimPoint(float reach)
        {
            if(!StageValidationInput.Enabled&&Camera.main!=null)
            {
                var mouse=(Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition)-(Vector2)transform.position;
                if(mouse.sqrMagnitude>.01f)return CombatActionGeometry.Clip(transform.position,mouse,Mathf.Min(reach,mouse.magnitude));
            }
            return Ahead(reach);
        }
        void SigAfter(float delay,Action action)
        {
            Runner.After(delay,()=>{if(this!=null&&!ItemEffectsSuspended&&!dead)action();});
        }

        internal void SigOnEquip(WeaponDefinition weapon)
        {
            if(sig!=null)sig.ClearViews();
            sigLockUntil=0;
            if(orb!=null){Destroy(orb.gameObject);orb=null;}
            if(orbTwo!=null){Destroy(orbTwo.gameObject);orbTwo=null;}
            sig=weapon!=null&&SignatureWeaponCatalog.Has(weapon.Id)?new SigState{Id=weapon.Id}:null;
            if(sig==null)return;
            if(weapon.Family==WeaponFamily.Orb)
            {
                orb=SigOrb.Create(this,PixelArtAtlas.GetHeldWeaponSprite(weapon));
                orb.OnStrikeHit=(enemy,point)=>{SigOnShotHit(enemy,point,true);};orb.Impact=C("Hit");
                SigOrbSetup();
            }
        }
        // Both go through here so a skill can swap between the player's hit rules and its own effects.
        internal void SigBoltHit(EnemyController enemy,float damage,Vector2 push,bool direct,bool critical,int action,string impact,Vector2 point)
        {
            if(ItemEffectsSuspended||enemy==null||enemy.IsDead)return;
            if(direct)OnDirectCombatHit(enemy,action,damage,false,sourceWeaponIdentity:CurrentWeaponIdentity,magazineHit:true);
            enemy.MuteNextHitFx();
            if(direct)enemy.TakeDamage(damage,push,critical);else SecondaryHit(enemy,damage,push);
            if(impact!=null)SigFx.Play(impact,point,Mathf.Atan2(push.y,push.x)*Mathf.Rad2Deg);
        }
        void SigHit(EnemyController enemy,float damage,Vector2 push,string impact=null)
        {
            if(enemy==null||enemy.IsDead)return;
            enemy.MuteNextHitFx();SecondaryHit(enemy,damage,push);
            if(impact!=null)SigFx.Play(impact,(Vector2)enemy.transform.position+Vector2.up*.15f);
        }
        List<EnemyController> SigArea(Vector2 point,float radius,float damage,string impact=null,float push=3,float shake=.07f)
        {
            var hits=CombatActionGeometry.Circle(point,radius);
            EnvironmentProp.HitCircle(point,radius);
            foreach(var enemy in hits)SigHit(enemy,damage,((Vector2)enemy.transform.position-point).normalized*push,impact);
            if(shake>0)ActionShake(shake);
            return hits;
        }
        List<EnemyController> SigSeg(Vector2 from,Vector2 to,float width,float damage,string lineClip=null,string impact=null,float push=3)
        {
            to=CombatActionGeometry.Clip(from,to-from,Vector2.Distance(from,to));
            var hits=CombatActionGeometry.Segment(from,to,width);
            EnvironmentProp.HitSegment(from,to,width);
            foreach(var enemy in hits)SigHit(enemy,damage,(to-from).normalized*push,impact);
            if(lineClip!=null)SigFx.Line(lineClip,from,to);
            return hits;
        }
        internal void SigPull(EnemyController enemy,Vector2 toward,float strength)
        {
            if(enemy==null||enemy.IsDead||enemy.IsBoss)return;
            var body=enemy.GetComponent<Rigidbody2D>();
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity=(toward-(Vector2)enemy.transform.position).normalized*strength;
#else
            body.velocity=(toward-(Vector2)enemy.transform.position).normalized*strength;
#endif
            enemy.Stagger(.3f);
        }
        void SigDash(Vector2 direction,float distance,float duration,Action<Vector2> onStep=null,Action done=null,bool ghosts=true)
        {StartCoroutine(SigDashRoutine(direction,distance,duration,onStep,done,ghosts,actionCancelSequence));}
        IEnumerator SigDashRoutine(Vector2 direction,float distance,float duration,Action<Vector2> onStep,Action done,bool ghosts,int epoch)
        {
            invincibilityTicks=Mathf.Max(invincibilityTicks,GameTick.FromSeconds(duration));
            distance=GetSafeTravelDistance(direction,distance);
            float elapsed=0,moved=0;int ghost=-1;
            while(elapsed<duration&&IsCombatEpochValid(epoch))
            {
                float delta=Mathf.Min(Time.deltaTime,duration-elapsed),step=GetSafeTravelDistance(direction,Mathf.Min(distance-moved,distance*delta/duration));
                body.position+=direction*step;moved+=step;elapsed+=delta;
                onStep?.Invoke(body.position);
                if(ghosts&&Mathf.FloorToInt(elapsed/.04f)!=ghost){ghost=Mathf.FloorToInt(elapsed/.04f);GetComponent<DashAfterimageEffect>()?.Stamp();}
                yield return null;
            }
            done?.Invoke();
        }
        void SigSwing(MeleeAttackMotion motion,float power,int comboStep=0)
        {CancelMeleeAttack();BeginActionMelee(motion,power,comboStep);}
        void SigShotPose(Vector2 aim)
        {
            attackAnimationSequence++;attackPoseDuration=.2f;attackPoseTimer=.2f;attackTimer=Mathf.Max(attackTimer,.2f);
            PrepareRangedShot(aim);
        }
        // Everything a family shoots from its LMB. Returns the bolt so a weapon can dress it.
        void SigBasicShot(Vector2 direction,float damage,bool critical)
        {
            shotPlantsRound=volleyReadyCopies.Remove(CurrentWeaponIdentity)||CurrentMutation==EquipmentMutation.AnchorRound;
            var w=EquippedWeapon;var action=attackAnimationSequence;var impact=C("Hit");
            if(w.Family==WeaponFamily.Orb)
            {
                if(sig.Id=="lantern_orb"&&orb.Pinned)
                {
                    // Fired from the lantern at what the hero is pointing at, not parallel to the hero's own aim.
                    var toCursor=AimPoint(30)-orb.Position;if(toCursor.sqrMagnitude>.04f)direction=toCursor.normalized;
                    SigFx.Play(C("B"),orb.Position,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                    var light=SigBolt.Shoot(this,C("Shot"),orb.Position,direction,w.ProjectileSpeed,damage,.34f,1,11,true,critical,action);light.HomingTurn=150;light.HomingRange=3;
                    SigDressShot(light);return;
                }
                orb.Damage=damage;orb.Action=action;orb.Critical=critical;orb.Impact=impact;orb.Strike(direction,sig.Id=="whetstone_orb"&&combatClock<sig.Until?3.3f:2.5f);
                SigOrbStrikeStarted(direction,damage,critical);
                return;
            }
            SigFx.Play(C("Muzzle"),committedShotOrigin,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            if(shotObstructed)return;
            if(w.Family==WeaponFamily.Shotgun)
            {
                int pellets=sig.Id=="furnace_sprayer"?3:5;
                for(int i=0;i<pellets;i++)
                {var dir=(Vector2)(Quaternion.Euler(0,0,(i-(pellets-1)*.5f)*8)*direction);SigDressShot(SigBolt.Shoot(this,C("Shot"),committedShotOrigin,dir,w.ProjectileSpeed,damage/pellets*1.3f,.2f,1,5.5f,true,critical,action));}
                return;
            }
            if(w.Family==WeaponFamily.Cannon){SigLob(direction,damage);return;}
            SigDressShot(SigBolt.Shoot(this,C("Shot"),committedShotOrigin,direction,w.ProjectileSpeed,damage,.2f,w.ProjectilePierce,12,true,critical,action));
        }
        void SigOnShotHit(EnemyController enemy,Vector2 point,bool orbStrike){SigShotLanded(enemy,point,orbStrike);}
        internal bool SigBlocks(Vector2 direction)=>SigTryBlock(direction);
        internal void SigOnParry(){SigParried();}
        // Press handling. Returns false when the family's own RMB should run instead.
        bool SigPress()
        {
            if(SigPassThrough())
            {
                // The native guard or charge takes it from here; the skill still has to look like one.
                if(CanBeginSpecial()&&weaponActionCooldown<=0&&Mana>=WeaponActionCatalog.Get(EquippedWeapon).Mana)SigFlourish();
                return false;
            }
            // A press during a swing waits in the buffer instead of being thrown away.
            if(!CanBeginSpecial()&&!SigAllowsDuringAttack())return true;
            specialInputBuffer=0;
            if(SigRecast()){SigFlourish();return true;}
            if(combatClock<sigReady||SigStillRunning())return true;
            var spec=WeaponActionCatalog.Get(EquippedWeapon);
            if(!SigCanCast()||!PayForSkill(spec.Mana))return true;
            sigReady=combatClock+spec.Cooldown;
            ItemEffects.OnManaSpent(spec.Mana,SigAim);UniqueActionsStarted++;
            sigCastPlaced=false;
            SigCast();
            SigFlourish();
            CombatAudio.Native("magic",AudioChannel.Player,.3f);
            return true;
        }
        float nextSigFlourish,sigCastScale=1;Vector2 sigCastAt;bool sigCastPlaced,sigCastCentred;
        void SigCastAt(Vector2 point,float scale=1,bool centred=false){sigCastAt=point;sigCastScale=scale;sigCastCentred=centred;sigCastPlaced=true;}
        // Sustained stances replace their state when cast again, so a second cast has to wait for the first to finish.
        bool SigStillRunning()
        {
            if(sig.Stage==0||combatClock>=sig.Until)return false;
            switch(sig.Id)
            {
                case "wind_cutter":case "whetstone_orb":case "frost_satellite":case "twin_star":case "world_pendulum":
                case "mu_eum":case "thunder_slug":case "warden_door":case "executioner_scale":case "trainee_blade":return true;
            }
            return false;
        }
        // The weapon's own big moment. What the skill actually does is drawn by its cases; this is what makes pressing it feel like something.
        void SigFlourish()
        {
            if(combatClock<nextSigFlourish||sig==null||!SignatureCastTable.Styles.TryGetValue(sig.Id,out var style)||style==SigCastStyle.None)return;
            nextSigFlourish=combatClock+.35f;
            var aim=SigAim;var angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;var clip=SignatureMeleeFx.Clip(sig.Id,"Cast");
            if(style==SigCastStyle.Aim){SigFx.Play(clip,SkillOrigin,angle,-1,false,aim.x<0);return;}
            // A skill with a real centre draws its picture there, sized to what it actually hits.
            if(sigCastPlaced)
            {
                sigCastPlaced=false;var at=sigCastAt;
                // Clips stand on their pivot. A picture that surrounds its point has to come down by half its height.
                if(sigCastCentred){var picture=NativeCombatEffect.Frames(clip)[0];at+=Vector2.down*(picture.rect.height*.5f-picture.pivot.y)/picture.pixelsPerUnit*sigCastScale;}
                SigFx.Play(clip,at,0,-1,false,false,sigCastScale);return;
            }
            if(orb!=null){SigFx.Play(clip,orb.Position);return;}
            // Far enough ahead that the whole picture clears the hero, as far as the walls allow.
            var frame=NativeCombatEffect.Frames(clip)[0];
            var clear=Mathf.Clamp(frame.rect.width/frame.pixelsPerUnit*.5f+.5f,1.8f,3.5f);
            SigFx.Play(clip,CombatActionGeometry.Clip(FeetPoint,aim,clear));
        }
    }
}
