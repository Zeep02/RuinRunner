using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class ItemCombatEffects
    {
        readonly List<Vector2> routeMarks=new List<Vector2>();
        readonly List<NativeCombatEffect> routeViews=new List<NativeCombatEffect>();
        readonly HashSet<int> extendedActions=new HashSet<int>();
        Vector2 recallFrom,recallTo,previousWalk;
        float recallUntil,recallDamage,recallReady,cadenceUntil,cadenceReady,cageUntil,cageReady,routeReady,focusPrismReady,aegisReady,lensArmedUntil;
        int cadenceStep,cagedBullets,focusContacts;
        EnemyController focusedEnemy;
        float cagedDamage;
        NativeCombatEffect recallView,cageView,focusPrismView;
        public int ExtendedItemProcs {get;private set;}
        public int CapturedItemBullets=>cagedBullets;
        public void OnCombatContact(EnemyController enemy,int action,float damage,bool melee,bool finisher)
        {
            if(!Active||dealingProc||enemy==null)return;
            if(Copies(AccessoryType.ArcaneLens)>0&&clock<lensArmedUntil)
            {lensArmedUntil=0;echoAim=((Vector2)enemy.transform.position-echoOrigin).normalized;echoAt=clock+Effect(AccessoryType.ArcaneLens,"releaseDelaySeconds");}
            bool first=extendedActions.Add(action);extendedActions.RemoveWhere(a=>a<owner.AttackAnimationSequence-32);
            if(first&&Copies(AccessoryType.CadenceClasp)>0&&clock>=cadenceReady){cadenceStep=1;cadenceUntil=clock+Effect(AccessoryType.CadenceClasp,"windowSeconds");}
            if(first&&Copies(AccessoryType.RecallSpool)>0&&finisher&&clock>=recallReady)
            {
                recallFrom=owner.transform.position;recallTo=CombatActionGeometry.Clip(recallFrom,owner.AimDirection,Effect(AccessoryType.RecallSpool,"range"));recallUntil=clock+Effect(AccessoryType.RecallSpool,"windowSeconds");recallDamage=damage*Effect(AccessoryType.RecallSpool,"damageFraction");
                DestroyView(recallView);recallView=NativeCombatEffect.Create("CutStreak",recallTo,0,.7f,0,Color.white,Order(recallTo),null,true);
            }
            if(first&&cagedBullets>0&&Copies(AccessoryType.PrismCage)>0)
            {
                int count=cagedBullets;float releaseDamage=cagedDamage;cagedBullets=0;cagedDamage=0;DestroyView(cageView);cageView=null;
                for(int i=0;i<count;i++)
                {
                    var aim=(Vector2)(Quaternion.Euler(0,0,(i-(count-1)*.5f)*Effect(AccessoryType.PrismCage,"fanAngle"))*owner.AimDirection);
                    var muzzle=(Vector2)owner.transform.position+aim*Effect(AccessoryType.PrismCage,"spawnOffset");
                    if(Physics2D.Linecast(owner.transform.position,muzzle,1<<GameLayers.Ground).collider==null)
                        PlayerProjectile.Create(muzzle,aim,Effect(AccessoryType.PrismCage,"projectileSpeed"),releaseDamage/count,false,Color.white,new Vector2(Effect(AccessoryType.PrismCage,"projectileWidth"),Effect(AccessoryType.PrismCage,"projectileHeight")),EffectCount(AccessoryType.PrismCage,"pierce"),WeaponFamily.Rifle,true,"prism_cage",owner,action,0).AsSecondary();
                }
                ExtendedItemProcs++;
            }
            if(first&&routeMarks.Count>=EffectCount(AccessoryType.TrailSpindle,"requiredMarkCount")&&Copies(AccessoryType.TrailSpindle)>0&&clock>=routeReady)
            {
                var seen=new HashSet<EnemyController>();routeReady=clock+Effect(AccessoryType.TrailSpindle,"cooldownSeconds");
                for(int i=1;i<routeMarks.Count;i++)
                {CombatActionGeometry.Trail("ItemThunder",routeMarks[i-1],routeMarks[i],.5f,owner.BodyRenderer);foreach(var target in CombatActionGeometry.Segment(routeMarks[i-1],routeMarks[i],Effect(AccessoryType.TrailSpindle,"width")))if(seen.Add(target))ProcDamage(target,damage*Effect(AccessoryType.TrailSpindle,"damageFraction"),Vector2.zero);}
                ClearRoute();ExtendedItemProcs++;
            }
            if(first&&Copies(AccessoryType.FocusPrism)>0&&clock>=focusPrismReady)
            {
                if(focusedEnemy==enemy)focusContacts=Mathf.Min(EffectCount(AccessoryType.FocusPrism,"hitCount"),focusContacts+1);
                else
                {
                    if(focusContacts>=EffectCount(AccessoryType.FocusPrism,"hitCount")&&CombatActionGeometry.Visible(enemy.transform.position,focusedEnemy)&&Vector2.Distance(enemy.transform.position,focusedEnemy.transform.position)<Effect(AccessoryType.FocusPrism,"range"))
                    {CombatActionGeometry.Trail("Bolt",focusedEnemy.transform.position,enemy.transform.position,.6f,owner.BodyRenderer);ProcDamage(focusedEnemy,damage*Effect(AccessoryType.FocusPrism,"damageFraction"),Vector2.zero);focusPrismReady=clock+Effect(AccessoryType.FocusPrism,"cooldownSeconds");ExtendedItemProcs++;}
                    DestroyView(focusPrismView);focusPrismView=null;focusedEnemy=enemy;focusContacts=1;
                }
                if(focusContacts==EffectCount(AccessoryType.FocusPrism,"hitCount")&&focusPrismView==null)
                    focusPrismView=NativeCombatEffect.Create("RewardGlint",(Vector2)enemy.transform.position+Vector2.down*.3f,0,.45f,0,Color.white,Order(enemy.transform.position)-3,enemy.transform,true);
            }
        }
        void OnActiveSpecial(float amount,Vector2 aim)
        {
            if(Copies(AccessoryType.RecallSpool)>0&&clock<recallUntil&&clock>=recallReady)
            {
                recallUntil=0;recallReady=clock+Effect(AccessoryType.RecallSpool,"cooldownSeconds");DestroyView(recallView);recallView=null;
                CombatActionGeometry.Trail("TwinCrescent",recallTo,recallFrom,.8f,owner.BodyRenderer);
                foreach(var target in CombatActionGeometry.Segment(recallTo,recallFrom,Effect(AccessoryType.RecallSpool,"width")))ProcDamage(target,recallDamage,Vector2.zero);
                CombatAudio.Native("sword",AudioChannel.Player,.25f);ExtendedItemProcs++;
            }
            if(Copies(AccessoryType.CadenceClasp)>0&&cadenceStep==2&&clock<cadenceUntil&&clock>=cadenceReady)
            {
                cadenceStep=0;cadenceReady=clock+Effect(AccessoryType.CadenceClasp,"cooldownSeconds");
                // A completed attack -> dash -> special sequence opens a directional safe lane.
                var from=(Vector2)owner.transform.position;var to=CombatActionGeometry.Clip(from,aim,Effect(AccessoryType.CadenceClasp,"range"));
                CombatActionGeometry.Trail("Wind",from,to,.9f,owner.BodyRenderer);
                foreach(var target in CombatActionGeometry.Segment(from,to,Effect(AccessoryType.CadenceClasp,"width")))ProcDamage(target,Snapshot*Effect(AccessoryType.CadenceClasp,"damageFraction"),aim*Effect(AccessoryType.CadenceClasp,"push"));
                owner.Stats.RestoreMana(Mathf.Min(Effect(AccessoryType.CadenceClasp,"manaLimit"),amount*Effect(AccessoryType.CadenceClasp,"manaRefundFraction")));ExtendedItemProcs++;
            }
            if(Copies(AccessoryType.PrismCage)>0&&clock>=cageReady)
            {cageUntil=clock+Effect(AccessoryType.PrismCage,"windowSeconds");cageReady=clock+Effect(AccessoryType.PrismCage,"cooldownSeconds");DestroyView(cageView);cageView=NativeCombatEffect.Create("ItemShield",owner.transform.position,0,1.2f,Effect(AccessoryType.PrismCage,"windowSeconds"),Color.white,Order(owner.transform.position)-4,owner.transform);}
        }
        public bool TryCatchProjectile(Vector2 point,Vector2 direction,float damage)
        {
            if(!Active||Copies(AccessoryType.PrismCage)==0||clock>=cageUntil||cagedBullets>=EffectCount(AccessoryType.PrismCage,"maxProjectiles")||Vector2.Distance(point,owner.transform.position)>Effect(AccessoryType.PrismCage,"catchRadius")||Vector2.Dot(owner.AimDirection,-direction)<Effect(AccessoryType.PrismCage,"facingDot"))return false;
            cagedBullets++;cagedDamage=Mathf.Min(Snapshot*Effect(AccessoryType.PrismCage,"damageCapFraction"),cagedDamage+damage*Effect(AccessoryType.PrismCage,"returnedDamageMultiplier"));
            if(cagedBullets==1){DestroyView(cageView);cageView=NativeCombatEffect.Create("Energy",(Vector2)owner.transform.position+Vector2.down*.3f,0,.35f,0,Color.white,Order(owner.transform.position)-3,owner.transform,true);}
            NativeCombatEffect.Create("Energy",point,0,.42f,.2f,Color.white)?.AboveWeapon(owner.BodyRenderer);CombatAudio.Native("block",AudioChannel.Player,.18f);return true;
        }
        public float AbsorbManaAegis(float damage)
        {
            if(!Active||damage<=0||Copies(AccessoryType.ManaAegis)==0||clock<aegisReady||owner.Mana<Effect(AccessoryType.ManaAegis,"manaCost"))return damage;
            if(!owner.Stats.TrySpendMana(Effect(AccessoryType.ManaAegis,"manaCost")))return damage;
            aegisReady=clock+Effect(AccessoryType.ManaAegis,"cooldownSeconds");ExtendedItemProcs++;
            NativeCombatEffect.Create("ItemShield",owner.transform.position,0,1.1f,.3f,Color.white)?.BelowActor(owner.BodyRenderer,-3);
            // Defensive conversion is not an active special and emits no mana-spend proc event.
            return Mathf.Max(0,damage-Effect(AccessoryType.ManaAegis,"absorbDamage"));
        }
        void TickExtendedItems()
        {
            if(focusedEnemy==null||focusedEnemy.IsDead){DestroyView(focusPrismView);focusPrismView=null;focusedEnemy=null;focusContacts=0;}
            if(clock>=recallUntil){DestroyView(recallView);recallView=null;}
            if(clock>=cadenceUntil)cadenceStep=0;
            if(Copies(AccessoryType.TrailSpindle)>0&&!owner.IsDashing&&owner.MovementInput.sqrMagnitude>.1f&&clock>=routeReady)
            {
                Vector2 point=owner.transform.position;
                if(routeMarks.Count==0||Vector2.Distance(previousWalk,point)>=Effect(AccessoryType.TrailSpindle,"markSpacing"))
                {
                    if(routeMarks.Count==EffectCount(AccessoryType.TrailSpindle,"markCount")){routeMarks.RemoveAt(0);DestroyView(routeViews[0]);routeViews.RemoveAt(0);}
                    routeMarks.Add(point);previousWalk=point;routeViews.Add(NativeCombatEffect.Create("Energy",point+Vector2.down*.25f,0,.3f,0,Color.white,Order(point)-4,null,true));
                }
            }
        }
        void ClearRoute(){foreach(var view in routeViews)DestroyView(view);routeViews.Clear();routeMarks.Clear();}
        void ClearExtendedItems()
        {ClearRoute();DestroyView(recallView);recallView=null;DestroyView(cageView);cageView=null;DestroyView(focusPrismView);focusPrismView=null;recallUntil=0;cadenceStep=0;cagedBullets=0;cagedDamage=0;focusContacts=0;focusedEnemy=null;lensArmedUntil=0;}
        void ReconcileExtendedItems()
        {
            if(Copies(AccessoryType.RecallSpool)==0){recallUntil=0;DestroyView(recallView);recallView=null;}
            if(Copies(AccessoryType.CadenceClasp)==0)cadenceStep=0;
            if(Copies(AccessoryType.PrismCage)==0){cageUntil=0;cagedBullets=0;cagedDamage=0;DestroyView(cageView);cageView=null;}
            if(Copies(AccessoryType.TrailSpindle)==0)ClearRoute();
            if(Copies(AccessoryType.FocusPrism)==0){DestroyView(focusPrismView);focusPrismView=null;focusedEnemy=null;focusContacts=0;}
        }
    }
}
