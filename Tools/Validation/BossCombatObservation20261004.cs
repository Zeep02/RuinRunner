using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Read-only opt-in combat evidence. Never adjusts actors, input, stats or time.</summary>
    [DefaultExecutionOrder(2100)]
    public sealed class BossCombatObservation : MonoBehaviour
    {
        [Serializable] public sealed class BossRecord
        {
            public string boss, outcome, heldWeapon, sourceScope="weapon is held at observation; projectile may originate before a switch";
            public int seed, stage, upgrade, originalHits, itemHits, attacksStarted, attacksActivated, attacksRecovered;
            public float maxHealth, startingPlayerHealth, endingPlayerHealth, attackPower, damageMultiplier, specialMultiplier, criticalChance, criticalDamage;
            public float startedAt, firstHitAt=-1, endedAt, originalDamage, itemDamage;
            public List<string> inventory=new List<string>();
            public List<string> patterns=new List<string>();
        }
        [Serializable] sealed class Report
        {
            public string scope="Normal pilot observation only; no forced actors, stats, time, items or phases. Death animation excluded. Damage capped at remaining HP for contribution totals.";
            public List<BossRecord> bosses=new List<BossRecord>();
        }
        static BossCombatObservation active;
        GameController game; EnemyController tracked; BossRecord current;
        readonly Report report=new Report(); StreamWriter hits; string folder;
        bool wasActing; int previousPhase=-1; bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetObserver()=>active=null;
        public void Initialize(GameController controller,string directory)
        {
            game=controller;folder=directory;
            hits=new StreamWriter(Path.Combine(folder,"boss-hits.csv"),false,new UTF8Encoding(false));
            hits.AutoFlush=true;hits.WriteLine("gameTime,boss,stage,source,critical,requestedAfterArmor,effectiveDamage,hpBefore,heldWeapon,upgrade,actionSequence,attackPhase");
            active=this;
        }
        static string N(float value)=>value.ToString("0.000",CultureInfo.InvariantCulture);
        public static void RecordDamage(EnemyController enemy,float amount,bool original,bool critical)
        {
            if(active==null||!enemy.IsBoss||enemy.IsDead||active.game==null||active.game.Boss!=enemy)return;
            active.ObserveHit(enemy,amount,original,critical);
        }
        void Bind(EnemyController enemy)
        {
            if(tracked==enemy&&current!=null)return;
            if(current!=null)Complete("replaced-before-death");
            tracked=enemy;finished=false;wasActing=false;previousPhase=-1;
            var p=game.Player;var s=p.Stats;
            current=new BossRecord{boss=enemy.Type.ToString(),seed=game.RunSeed,stage=game.CurrentStageIndex+1,maxHealth=enemy.MaxHealth,
                startingPlayerHealth=p.Health,heldWeapon=p.EquippedWeapon.Id,upgrade=p.WeaponUpgradeLevel,attackPower=p.Damage,
                damageMultiplier=s.WeaponDamageMultiplier,specialMultiplier=s.SpecialDamageMultiplier,criticalChance=s.CriticalChance,criticalDamage=s.CriticalDamage,startedAt=Time.time};
            foreach(var item in game.Inventory.OwnedAccessories)current.inventory.Add(item.Definition.Id+"+"+item.UpgradeLevel);
            report.bosses.Add(current);Save();
        }
        void ObserveHit(EnemyController enemy,float amount,bool original,bool critical)
        {
            Bind(enemy);if(finished)return;
            if(current.firstHitAt<0)current.firstHitAt=Time.time;
            float effective=Mathf.Min(Mathf.Max(0,enemy.Health),amount);
            if(original){current.originalHits++;current.originalDamage+=effective;}else{current.itemHits++;current.itemDamage+=effective;}
            var p=game.Player;
            hits.WriteLine(string.Join(",",N(Time.time),enemy.Type,current.stage,original?"original":"item",critical,N(amount),N(effective),N(enemy.Health),p.EquippedWeapon.Id,p.WeaponUpgradeLevel,p.AttackAnimationSequence,enemy.AttackVisualPhase));
        }
        void LateUpdate()
        {
            if(game==null||game.Player==null)return;
            var boss=game.Boss;
            if(boss==null){if(current!=null&&!finished)Complete("removed-before-death");return;}
            if(boss!=tracked)Bind(boss);
            if(finished)return;
            if(boss.IsDead){Complete("defeated");return;}
            if(boss.IsActing&&!wasActing)
            {
                current.attacksStarted++;
                current.patterns.Add(boss.Type==EnemyType.Boss?boss.GetComponent<BossSpriteAnimator>().Pose.ToString():boss.CurrentBossAction.ToString());
                previousPhase=-1;
            }
            if(boss.IsActing&&boss.AttackVisualPhase!=previousPhase)
            {
                if(boss.AttackVisualPhase==1)current.attacksActivated++;
                if(boss.AttackVisualPhase==2)current.attacksRecovered++;
                previousPhase=boss.AttackVisualPhase;
            }
            wasActing=boss.IsActing;
        }
        void Complete(string outcome)
        {
            if(current==null||finished)return;
            finished=true;current.outcome=outcome;current.endedAt=Time.time;current.endingPlayerHealth=game.Player==null?0:game.Player.Health;Save();
            Debug.Log("[Boss Pressure] "+current.boss+" "+outcome+" fight="+N(current.endedAt-current.startedAt)+"s contact="+N(current.endedAt-current.firstHitAt)+"s original="+N(current.originalDamage)+" item="+N(current.itemDamage)+" patterns="+string.Join("/",current.patterns));
        }
        void Save()=>File.WriteAllText(Path.Combine(folder,"boss-pressure.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
        void OnDestroy()
        {
            if(current!=null&&!finished)Complete("observation-ended");
            if(active==this)active=null;hits?.Dispose();
        }
    }
}
