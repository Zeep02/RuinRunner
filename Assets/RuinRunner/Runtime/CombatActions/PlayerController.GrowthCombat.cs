using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PlayerController
    {
        Vector2 plantedRound;
        float plantedUntil,plantedDamage;
        int plantedAction=-1;
        long plantedWeaponIdentity;
        bool crossAwakeReady;
        readonly HashSet<long> volleyReadyCopies=new HashSet<long>();
        int awakenedAction=-1;
        NativeCombatEffect plantedView;
        WeaponFamily previousWeaponFamily;
        EquipmentMutation CurrentMutation=>ActiveWeaponState?.Item?.Mutation??EquipmentMutation.None;
        bool IsAwake(EquipmentOath oath)=>ActiveWeaponState?.Item!=null&&ActiveWeaponState.Item.Awakened&&ActiveWeaponState.Item.Oath==oath;
        MeleeAttackStep ApplyFinisherMutation(MeleeAttackStep step,bool finisher)
        {
            if(!finisher)return step;
            if(CurrentMutation==EquipmentMutation.LongFinisher)
                return new MeleeAttackStep(step.DamageMultiplier,step.DelayMultiplier,step.Reach+.3f,new Vector2(step.HitboxSize.x*1.28f,step.HitboxSize.y*.62f),step.AngleOffset,step.Knockback,step.Motion);
            if(CurrentMutation==EquipmentMutation.WideFinisher)
                return new MeleeAttackStep(step.DamageMultiplier,step.DelayMultiplier,step.Reach*.8f,new Vector2(step.HitboxSize.x*.8f,step.HitboxSize.y*1.3f),step.AngleOffset,step.Knockback,step.Motion);
            return step;
        }
        void ConfigureMutatedProjectile(PlayerProjectile shot)
        {
            bool volleyReady=volleyReadyCopies.Remove(CurrentWeaponIdentity);
            shot.WithMutation(CurrentMutation==EquipmentMutation.Ricochet,CurrentMutation==EquipmentMutation.AnchorRound||volleyReady);
        }
        public void PlantAwakenedRound(Vector2 point,float damage,int sourceAction,long sourceWeaponIdentity)
        {
            // One mark per fired action. Late pellets cannot replace a newer or
            // already consumed mark, and an unequipped copy cannot plant one.
            if(sourceWeaponIdentity!=CurrentWeaponIdentity||sourceAction<=plantedAction)return;
            ClearPlantedRound();plantedRound=point;plantedDamage=damage;plantedUntil=combatClock+2;
            plantedAction=sourceAction;plantedWeaponIdentity=sourceWeaponIdentity;
            plantedView=NativeCombatEffect.Create("RewardRing",point,0,.65f,0,Color.white,spriteRenderer.sortingOrder-4,null,true);
        }
        void ClearPlantedRound(){if(plantedView!=null)Destroy(plantedView.gameObject);plantedView=null;plantedUntil=0;}
        void ResolveAwakenedContact(EnemyController enemy,int action,bool finisher,bool counter)
        {
            if(plantedUntil>combatClock&&plantedWeaponIdentity==CurrentWeaponIdentity&&action>plantedAction&&CombatActionGeometry.Visible(plantedRound,enemy))
            {var point=plantedRound;var damage=plantedDamage;ClearPlantedRound();ActionBlast(point,1.25f,damage,"CannonBlast");}
            if(awakenedAction==action)return;
            if(counter&&IsAwake(EquipmentOath.CounterDiscipline))
            {awakenedAction=action;var from=(Vector2)transform.position;ActionLine(from,CombatActionGeometry.Clip(from,AimDirection,4),.55f,Damage*.35f,"PiercingHit");}
            else if(finisher&&IsAwake(EquipmentOath.FinisherHunt))
            {awakenedAction=action;var from=(Vector2)transform.position;var tip=CombatActionGeometry.Clip(from,AimDirection,2.9f);ActionLine(tip,from,.9f,Damage*.32f,"TwinCrescent");}
        }
        public void OnSpecialCommitted()
        {
            if(!crossAwakeReady||!IsAwake(EquipmentOath.CrossWeaponChain))return;
            crossAwakeReady=false;var from=(Vector2)transform.position;
            var clip=previousWeaponFamily>=WeaponFamily.Crossbow?"RifleEnergy":"TwinCrescent";
            ActionLine(from,CombatActionGeometry.Clip(from,AimDirection,3.2f),.75f,Damage*.38f,clip);
        }
        void OnCombatReload(OwnedWeapon item,bool wasEmpty)
        {
            if(item==null)return;
            Grow(CombatGrowthKind.ReloadCompleted,null,item.ReloadSequence,false,item.Identity,wasEmpty);
            if(wasEmpty&&item.Awakened&&item.Oath==EquipmentOath.SteadyVolley)volleyReadyCopies.Add(item.Identity);
        }
        IEnumerator AimedRifleRelease(Vector2 aim,float damage,bool critical,Color color,int epoch)
        {
            NativeCombatEffect.Create("Charge",committedShotOrigin,0,.75f,.12f,Color.white)?.BelowActor(spriteRenderer,-3);
            float elapsed=0;
            while(elapsed<.1f&&IsCombatEpochValid(epoch)){elapsed+=Time.deltaTime;yield return null;}
            if(!IsCombatEpochValid(epoch))yield break;
            PrepareRangedShot(aim,.2f);
            SpawnProjectile(aim,damage*1.2f,critical,color,new Vector2(.7f,.22f),6,EquippedWeapon.ProjectileSpeed*1.25f,true);
            CombatPresentation.Muzzle(EquippedWeapon.Family,committedShotOrigin,aim,true,EquippedWeapon.Id,spriteRenderer.sortingOrder-1,spriteRenderer);
        }
    }
}
