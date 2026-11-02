using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PlayerController
    {
        public static readonly HashSet<PlayerController> ActiveCombatants=new HashSet<PlayerController>();
        sealed class ActionSession
        {
            public UniqueWeaponAction Kind;
            public Vector2 Point, Aim, Origin;
            public float Expires, Damage;
            public int Sequence, Captured, Links;
            public readonly List<EnemyController> Targets=new List<EnemyController>();
            public readonly List<GameObject> Views=new List<GameObject>();
        }
        readonly Dictionary<long,float> uniqueReady=new Dictionary<long,float>();
        readonly HashSet<int> growthHitActions=new HashSet<int>();
        ActionSession uniqueSession;
        float combatClock, lastDirectHitAt=-10, comboMemoryUntil, returnDashUntil;
        Vector2 returnDashPoint;
        int observedCancelSequence, storedGuardCharges, crossChamberAction=-1;
        float storedGuardDamage, chamberReady;
        float specialInputBuffer,katanaAutoTimer;
        bool katanaAutoCutPending,katanaCounterSucceeded;
        OwnedWeapon lastRangedCopy;
        Func<TalentId,bool> talentQuery;
        Action<CombatGrowthEvent> growthEvent;
        public bool IsTrainingCombat { get; set; }
        public int CombatEpoch=>actionCancelSequence;
        public int UniqueActionsStarted { get; private set; }
        public int UniqueActionsReleased { get; private set; }
        public UniqueWeaponAction ActiveUniqueAction=>uniqueSession==null?UniqueWeaponAction.None:uniqueSession.Kind;
        public void ConfigureCombatGrowth(Func<TalentId,bool> talents,Action<CombatGrowthEvent> notify)
        {talentQuery=talents;growthEvent=notify;}
        bool HasTalent(TalentId id)=>talentQuery!=null&&talentQuery(id);
        long CurrentWeaponIdentity=>ActiveWeaponState?.Item?.Identity??-(long)(EquippedWeapon?.Id.GetHashCode()??1);
        public long CombatWeaponIdentity=>CurrentWeaponIdentity;
        public bool IsCombatEpochValid(int epoch)=>isActiveAndEnabled&&epoch==actionCancelSequence&&!ItemEffectsSuspended&&!dashing;

        void TickExtendedActions()
        {
            combatClock+=Time.deltaTime;
            if(observedCancelSequence!=actionCancelSequence){observedCancelSequence=actionCancelSequence;ClearUniqueSession();ClearPlantedRound();storedGuardCharges=0;}
            if(plantedUntil>0&&combatClock>=plantedUntil)ClearPlantedRound();
            if(katanaAutoCutPending)
            {katanaAutoTimer-=Time.deltaTime;if(katanaAutoTimer<=0){katanaAutoCutPending=false;BeginActionMelee(MeleeAttackMotion.CrossCut,katanaCounterSucceeded?1.6f:.85f);}}
            if(uniqueSession!=null&&combatClock>=uniqueSession.Expires)ClearUniqueSession();
            if(uniqueSession!=null&&uniqueSession.Kind==UniqueWeaponAction.Tether&&uniqueSession.Targets.Count>0)
            {
                var enemy=uniqueSession.Targets[0];
                if(!CombatActionGeometry.Visible(transform.position,enemy)||Vector2.Distance(transform.position,enemy.transform.position)>6)ClearUniqueSession();
            }
        }
        void ClearUniqueSession()
        {if(uniqueSession==null)return;foreach(var view in uniqueSession.Views)if(view!=null)Destroy(view);uniqueSession=null;}
        void UniqueMarker(ActionSession state,string clip,Vector2 point,float size,Transform follow=null)
        {
            var fx=NativeCombatEffect.Create(clip,point,0,size,0,Color.white,spriteRenderer.sortingOrder-3,follow,true);
            if(fx!=null){fx.BelowActor(spriteRenderer,-3);state.Views.Add(fx.gameObject);}
        }
        void Grow(CombatGrowthKind kind,EnemyController target=null,int action=-1,bool counter=false,long weapon=0,bool reloadStartedEmpty=false,bool magazineHit=false)
        {
            growthEvent?.Invoke(new CombatGrowthEvent {Kind=kind,WeaponIdentity=weapon==0?CurrentWeaponIdentity:weapon,
                TargetIdentity=CombatTargetIdentity.For(target),ActionIdentity=action<0?attackAnimationSequence:action,
                IsTraining=IsTrainingCombat,IsRecursive=false,IsBoss=target!=null&&target.IsBoss,IsCounter=counter,ReloadStartedEmpty=reloadStartedEmpty,IsMagazineHit=magazineHit});
        }

        // Presses are buffered uniformly. A paid action can replace only resolved recovery.
        bool CanBeginSpecial()=>attackTimer<=0||IsMeleeSwingActive&&pendingMeleeStep==null&&AttackAnimationProgress>=MeleeMotionTiming.Recovery;
        bool TryUniqueInput()
        {
            if(specialInputBuffer<=0)return false;
            if(storedGuardCharges>0&&CanBeginSpecial())
            {
                storedGuardCharges=0;specialInputBuffer=0;PrepareActionShot(AimDirection);
                if(!shotObstructed)PlayerProjectile.Create(committedShotOrigin,AimDirection,18,storedGuardDamage,false,Color.white,new Vector2(.38f,.18f),1,WeaponFamily.Rifle,true,"stored_guard",this,attackAnimationSequence,0).AsSecondary();
                CombatAudio.Native("magic",AudioChannel.Player,.3f);return true;
            }
            var spec=WeaponActionCatalog.Get(EquippedWeapon);
            if(spec.Kind==UniqueWeaponAction.None)return false;
            if(uniqueSession!=null&&combatClock<uniqueSession.Expires&&uniqueSession.Kind==spec.Kind)
            {if(CanBeginSpecial()){specialInputBuffer=0;if(spec.Kind!=UniqueWeaponAction.FractureLink&&spec.Kind!=UniqueWeaponAction.GravityWell&&spec.Kind!=UniqueWeaponAction.Tether)ReleaseUnique();}return true;}
            if(spec.Kind==UniqueWeaponAction.SunBank)return false; // Native catch/real riposte, banked only on an actual defense.
            if(!CanBeginSpecial()||uniqueReady.TryGetValue(CurrentWeaponIdentity,out var ready)&&combatClock<ready)return true;
            if(!Stats.TrySpendMana(spec.Mana))return true;
            specialInputBuffer=0;CancelMeleeAttack();CancelWeaponAction();
            uniqueReady[CurrentWeaponIdentity]=combatClock+spec.Cooldown;
            weaponActionCooldown=Mathf.Max(weaponActionCooldown,spec.Cooldown);
            ItemEffects.OnManaSpent(spec.Mana,AimDirection);
            var aim=AimDirection.sqrMagnitude>.01f?AimDirection.normalized:Vector2.right;
            var state=new ActionSession{Kind=spec.Kind,Point=transform.position,Aim=aim,Expires=combatClock+3,Damage=Damage*Stats.SpecialDamageMultiplier,Sequence=attackAnimationSequence+1};
            uniqueSession=state;observedCancelSequence=actionCancelSequence;UniqueActionsStarted++;
            switch(spec.Kind)
            {
                case UniqueWeaponAction.ShadowReturn:
                    state.Expires=combatClock+1.4f;UniqueMarker(state,"Wind",state.Point,.8f);
                    BeginActionMelee(MeleeAttackMotion.Thrust,1.15f);StartCoroutine(ActionStep(aim,1.35f,.16f,actionCancelSequence));break;
                case UniqueWeaponAction.BloodRecall:
                    state.Expires=combatClock+2;BeginActionMelee(MeleeAttackMotion.CrossCut,1.2f);break;
                case UniqueWeaponAction.FractureLink:
                    BeginActionMelee(MeleeAttackMotion.Cleave,1.3f);state.Origin=transform.position;state.Point=CombatActionGeometry.Clip(state.Origin,aim,3.1f);
                    for(int i=1;i<=3;i++)UniqueMarker(state,"Ember",Vector2.Lerp(state.Origin,state.Point,i/3f),.65f);break;
                case UniqueWeaponAction.VoidCharge:
                case UniqueWeaponAction.Tether:
                    PrepareActionShot(aim);state.Sequence=attackAnimationSequence;
                    if(shotObstructed)break;
                    var shot=PlayerProjectile.Create(committedShotOrigin,aim,EquippedWeapon.ProjectileSpeed,state.Damage*.6f,false,Color.white,new Vector2(.35f,.18f),1,EquippedWeapon.Family,true,EquippedWeapon.Id,this,attackAnimationSequence,0);
                    shot.OnContact((enemy,point)=>{if(uniqueSession!=state||enemy==null)return;state.Targets.Add(enemy);state.Point=point;UniqueMarker(state,state.Kind==UniqueWeaponAction.VoidCharge?"Void":"Energy",point,.55f,enemy.transform);});
                    break;
                case UniqueWeaponAction.DragonBeacon:
                case UniqueWeaponAction.StarConvergence:
                case UniqueWeaponAction.Bastion:
                    PrepareActionShot(aim);state.Sequence=attackAnimationSequence;
                    state.Point=CombatActionGeometry.Clip(transform.position,aim,spec.Kind==UniqueWeaponAction.Bastion?1.1f:3.7f);
                    UniqueMarker(state,spec.Kind==UniqueWeaponAction.DragonBeacon?"BlueFire":spec.Kind==UniqueWeaponAction.Bastion?"ItemShield":"RifleEnergy",state.Point,spec.Kind==UniqueWeaponAction.Bastion?1.2f:.85f);
                    break;
                case UniqueWeaponAction.ReturningEdge:
                    BeginActionMelee(MeleeAttackMotion.Thrust,.55f);
                    ReturningWeapon.Create(this,EquippedWeapon,aim,state.Damage*.65f,actionCancelSequence);
                    state.Expires=combatClock+.25f;break;
                case UniqueWeaponAction.GravityWell:
                    BeginActionMelee(MeleeAttackMotion.Impact,.9f);state.Point=CombatActionGeometry.Clip(transform.position,aim,2.6f);
                    UniqueMarker(state,"Void",state.Point,1.6f);break;
            }
            CombatAudio.Native(spec.Kind==UniqueWeaponAction.FractureLink||spec.Kind==UniqueWeaponAction.GravityWell?"heavy-impact":"magic",AudioChannel.Player,.32f);
            return true;
        }
        void PrepareActionShot(Vector2 aim)
        {
            attackAnimationSequence++;attackPoseDuration=.23f;attackPoseTimer=.23f;attackTimer=Mathf.Max(attackTimer,.23f);
            PrepareRangedShot(aim);
            if(EquippedWeapon.AttackType==WeaponAttackType.Ranged)
                CombatPresentation.Muzzle(EquippedWeapon.Family,committedShotOrigin,aim,true,EquippedWeapon.Id,spriteRenderer.sortingOrder-1,spriteRenderer);
            else NativeCombatEffect.Create("Energy",committedShotOrigin,0,.65f,.18f,Color.white)?.AboveWeapon(spriteRenderer);
        }
        void BeginActionMelee(MeleeAttackMotion motion,float power)
        {
            activeComboAnimationStep=0;
            var original=MeleeComboCatalog.Get(EquippedWeapon).HeavyStep;
            var step=new MeleeAttackStep(1,.85f,original.Reach,original.HitboxSize,0,7,motion);
            PerformMeleeAttack(step,power*Stats.SpecialDamageMultiplier,motion==MeleeAttackMotion.Cleave||motion==MeleeAttackMotion.Impact);
            weaponActionTimer=attackPoseDuration;
        }
        void ReleaseUnique()
        {
            var state=uniqueSession;if(state==null)return;ClearUniqueSession();UniqueActionsReleased++;
            attackTimer=Mathf.Max(attackTimer,.2f);
            var aim=AimDirection.sqrMagnitude>.01f?AimDirection.normalized:state.Aim;
            var from=(Vector2)transform.position;
            switch(state.Kind)
            {
                case UniqueWeaponAction.ShadowReturn:
                    var returnDirection=state.Point-from;
                    if(returnDirection.magnitude>3.2f)break;
                    AimDirection=returnDirection.normalized;BeginActionMelee(MeleeAttackMotion.Sweep,1.15f);
                    StartCoroutine(ActionStep(returnDirection.normalized,returnDirection.magnitude,.18f,actionCancelSequence));break;
                case UniqueWeaponAction.BloodRecall:
                    BeginActionMelee(MeleeAttackMotion.Sweep,.5f);
                    foreach(var enemy in state.Targets)if(CombatActionGeometry.Visible(from,enemy)&&Vector2.Distance(from,enemy.transform.position)<6)
                    {CombatActionGeometry.Trail("CutStreak",enemy.transform.position,from,.7f,spriteRenderer);SecondaryHit(enemy,state.Damage*.8f,Vector2.zero);}
                    break;
                case UniqueWeaponAction.VoidCharge:
                    if(state.Targets.Count==0||!CombatActionGeometry.Visible(from,state.Targets[0]))break;
                    ActionBlast(state.Targets[0].transform.position,1.5f,state.Damage*1.6f,"Void");break;
                case UniqueWeaponAction.DragonBeacon:
                    if(Physics2D.Linecast(from,state.Point,1<<GameLayers.Ground).collider!=null)break;
                    ActionBlast(state.Point,2,state.Damage*1.75f,"HeavyBlast");StartCoroutine(ActionStep(-aim,.8f,.15f,actionCancelSequence));break;
                case UniqueWeaponAction.SunBank:
                    BeginActionMelee(MeleeAttackMotion.Sweep,.5f);ActionLine(from,CombatActionGeometry.Clip(from,aim,5),.8f,state.Damage*1.3f,"TwinCrescent");break;
                case UniqueWeaponAction.StarConvergence:
                    if(Physics2D.Linecast(from,state.Point,1<<GameLayers.Ground).collider!=null)break;
                    PrepareActionShot(aim);var hitSet=new HashSet<EnemyController>();
                    ActionLine(from,state.Point,.65f,state.Damage,"RifleEnergy",hitSet);
                    ActionLine(state.Point,CombatActionGeometry.Clip(state.Point,aim,5),.65f,state.Damage*1.4f,"ItemThunder",hitSet);break;
                case UniqueWeaponAction.Bastion:
                    PrepareActionShot(aim);int count=3+state.Captured*2;
                    if(shotObstructed)break;
                    for(int i=0;i<count;i++)
                    {var dir=(Vector2)(Quaternion.Euler(0,0,(i-(count-1)*.5f)*7)*aim);PlayerProjectile.Create(committedShotOrigin,dir,15,state.Damage*(1+.3f*state.Captured)/count,false,Color.white,new Vector2(.22f,.16f),1,WeaponFamily.Shotgun,true,EquippedWeapon.Id,this,attackAnimationSequence,0).AsSecondary();}break;
                // Fixed marks are activated by the promised basic contact, not by a free re-press.
                case UniqueWeaponAction.FractureLink:
                case UniqueWeaponAction.GravityWell:uniqueSession=state;break;
                case UniqueWeaponAction.Tether:break;
            }
            CombatAudio.Native(state.Kind==UniqueWeaponAction.DragonBeacon?"heavy-impact":"magic",AudioChannel.Player,.38f);
        }
        IEnumerator ActionStep(Vector2 aim,float distance,float duration,int epoch)
        {
            float elapsed=0,moved=0;distance=GetSafeTravelDistance(aim,distance);
            while(elapsed<duration&&IsCombatEpochValid(epoch))
            {float delta=Mathf.Min(Time.deltaTime,duration-elapsed);float step=Mathf.Min(distance-moved,distance*delta/duration);step=GetSafeTravelDistance(aim,step);body.position+=aim*step;moved+=step;elapsed+=delta;yield return null;}
        }
        public void SecondaryHit(EnemyController target,float amount,Vector2 push)
        {if(!ItemEffectsSuspended&&target!=null&&!target.IsDead)ItemEffects.ApplySecondaryDamage(target,amount,push);}
        void ActionBlast(Vector2 point,float radius,float damage,string clip)
        {
            foreach(var enemy in CombatActionGeometry.Circle(point,radius))SecondaryHit(enemy,damage,((Vector2)enemy.transform.position-point).normalized*5);
            NativeCombatEffect.Create(clip,point,0,radius*2,.35f,Color.white)?.BelowActor(spriteRenderer,-3);
        }
        void ActionLine(Vector2 from,Vector2 to,float width,float damage,string clip,HashSet<EnemyController> seen=null)
        {
            foreach(var enemy in CombatActionGeometry.Segment(from,to,width))if(seen==null||seen.Add(enemy))SecondaryHit(enemy,damage,(to-from).normalized*4);
            CombatActionGeometry.Trail(clip,from,to,Mathf.Max(.55f,width),spriteRenderer);
        }
        public void OnDirectCombatHit(EnemyController enemy,int action,float damage,bool melee,bool finisher=false,bool counter=false,long sourceWeaponIdentity=0,bool magazineHit=false)
        {
            if(enemy==null||ItemEffectsSuspended||ItemEffects.IsDealingProc)return;
            lastDirectHitAt=combatClock;
            Grow(finisher?CombatGrowthKind.FinisherHit:CombatGrowthKind.DirectHit,enemy,action,counter,sourceWeaponIdentity,false,magazineHit);
            ItemEffects.OnOriginalHit(enemy,action,damage);
            ItemEffects.OnCombatContact(enemy,action,damage,melee,finisher);
            if(sourceWeaponIdentity==0||sourceWeaponIdentity==CurrentWeaponIdentity)
                ResolveAwakenedContact(enemy,action,finisher,counter);
            if(finisher&&HasTalent(TalentId.CrossChamber)&&lastRangedCopy!=null&&inventory.FindOwned(lastRangedCopy.Identity)!=null&&lastRangedCopy.Ammo==0&&combatClock>=chamberReady&&crossChamberAction!=action)
            {crossChamberAction=action;chamberReady=combatClock+2;lastRangedCopy.LoadSingleRound();}
            var state=uniqueSession;if(state==null||sourceWeaponIdentity!=0&&sourceWeaponIdentity!=CurrentWeaponIdentity)return;
            if(action==state.Sequence&&state.Kind==UniqueWeaponAction.BloodRecall&&state.Targets.Count<3&&!state.Targets.Contains(enemy))
            {state.Targets.Add(enemy);UniqueMarker(state,"Ember",(Vector2)enemy.transform.position+Vector2.up*.25f,.45f,enemy.transform);}
            if(action==state.Sequence)return;
            if(state.Kind==UniqueWeaponAction.FractureLink&&melee)
            {ClearUniqueSession();ActionLine(state.Origin,state.Point,1,state.Damage*1.1f,"FlameArc");UniqueActionsReleased++;}
            else if(state.Kind==UniqueWeaponAction.GravityWell&&finisher)
            {
                ClearUniqueSession();foreach(var target in CombatActionGeometry.Circle(state.Point,2.4f))
                    SecondaryHit(target,state.Damage*.8f,target.IsBoss?Vector2.zero:(state.Point-(Vector2)target.transform.position).normalized*9);
                NativeCombatEffect.Create("Void",state.Point,0,3.8f,.45f,Color.white)?.BelowActor(spriteRenderer,-3);UniqueActionsReleased++;
            }
            else if(state.Kind==UniqueWeaponAction.Tether&&state.Targets.Contains(enemy)&&!melee&&CombatActionGeometry.Visible(transform.position,enemy))
            {SecondaryHit(enemy,state.Damage*.16f,enemy.IsBoss?Vector2.zero:((Vector2)transform.position-(Vector2)enemy.transform.position).normalized*5);CombatActionGeometry.Trail("Bolt",transform.position,enemy.transform.position,.4f,spriteRenderer);if(++state.Links>=3)ClearUniqueSession();}
        }
        public bool TryCatchHostileProjectile(Vector2 point,Vector2 travel,float incoming)
        {
            if(!isActiveAndEnabled||ItemEffectsSuspended)return false;
            var session=uniqueSession;
            if(session!=null&&session.Kind==UniqueWeaponAction.Bastion&&session.Captured<3&&Vector2.Distance(point,session.Point)<.85f&&Physics2D.Linecast(session.Point,point,1<<GameLayers.Ground).collider==null)
            {session.Captured++;NativeCombatEffect.Create("MetalSpark",point,0,.65f,.2f,Color.white)?.AboveWeapon(spriteRenderer);return true;}
            if(ItemEffects.TryCatchProjectile(point,travel,incoming))return true;
            if(Vector2.Distance(point,transform.position)>.7f||dashing||invincibilityTicks>0)return false;
            bool frontal=travel.sqrMagnitude>.01f&&Vector2.Dot(IsLongswordCatchActive?longswordCatchAim:AimDirection,-travel.normalized)>=.35f;
            if(!HasTalent(TalentId.StoredGuard)||storedGuardCharges!=0||!frontal||!(longswordCatching||IsParrying))return false;
            TakeDamage(incoming,travel);storedGuardCharges=1;storedGuardDamage=Mathf.Min(Damage*1.3f,incoming*2);
            NativeCombatEffect.Create("Energy",(Vector2)transform.position+Vector2.up*.2f,0,.45f,.3f,Color.white)?.BelowActor(spriteRenderer,-3);return true;
        }
        void OnActiveDefense()
        {
            Grow(CombatGrowthKind.Defense);ItemEffects.OnDefenseSuccess(AimDirection);
            if(EquippedWeapon.Id=="sun_oath")
            {ClearUniqueSession();uniqueSession=new ActionSession{Kind=UniqueWeaponAction.SunBank,Point=transform.position,Aim=AimDirection,Damage=Damage*Stats.SpecialDamageMultiplier,Expires=combatClock+4};UniqueMarker(uniqueSession,"RewardGlint",(Vector2)transform.position+Vector2.down*.25f,.5f,transform);observedCancelSequence=actionCancelSequence;}
        }
    }
}
