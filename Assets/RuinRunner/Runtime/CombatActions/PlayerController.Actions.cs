using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PlayerController
    {
        public static readonly HashSet<PlayerController> ActiveCombatants = new HashSet<PlayerController>();
        float combatClock;
        int observedCancelSequence;
        float specialInputBuffer, katanaAutoTimer;
        bool katanaAutoCutPending, katanaCounterSucceeded;
        Func<TalentId, bool> talentQuery;
        Action<CombatGrowthEvent> growthEvent;
        public bool IsTrainingCombat { get; set; }
        public int CombatEpoch => actionCancelSequence;
        public int UniqueActionsStarted { get; private set; }
        public void ConfigureCombatGrowth(Func<TalentId, bool> talents, Action<CombatGrowthEvent> notify)
        {
            talentQuery = talents;
            growthEvent = notify;
            ApplyTalentStats();
            Stats?.Heal(Stats.MaxHealth * 2);
        }
        bool HasTalent(TalentId id) => talentQuery != null && talentQuery(id);
        long CurrentWeaponIdentity => ActiveWeaponState?.Item?.Identity ?? -(long)(EquippedWeapon?.Id.GetHashCode() ?? 1);
        public long CombatWeaponIdentity => CurrentWeaponIdentity;
        public bool IsCombatEpochValid(int epoch) => isActiveAndEnabled && epoch == actionCancelSequence && !ItemEffectsSuspended && !dashing;

        void TickExtendedActions()
        {
            combatClock += Time.deltaTime;
            SigTick();
            if (observedCancelSequence != actionCancelSequence)
            {
                observedCancelSequence = actionCancelSequence;
                ClearPlantedRound();
            }
            if (plantedUntil > 0 && combatClock >= plantedUntil)
                ClearPlantedRound();
            if (katanaAutoCutPending)
            {
                katanaAutoTimer -= Time.deltaTime;
                if (katanaAutoTimer <= 0)
                {
                    katanaAutoCutPending = false;
                    BeginActionMelee(MeleeAttackMotion.CrossCut, katanaCounterSucceeded ? 1.6f : .85f);
                }
            }
        }
        void Grow(CombatGrowthKind kind, EnemyController target = null, int action = -1, bool counter = false, long weapon = 0, bool reloadStartedEmpty = false, bool magazineHit = false)
        {
            growthEvent?.Invoke(new CombatGrowthEvent
            {
                Kind = kind,
                WeaponIdentity = weapon == 0 ? CurrentWeaponIdentity : weapon,
                TargetIdentity = CombatTargetIdentity.For(target),
                ActionIdentity = action < 0 ? attackAnimationSequence : action,
                IsTraining = IsTrainingCombat,
                IsRecursive = false,
                IsBoss = target != null && target.IsBoss,
                IsCounter = counter,
                ReloadStartedEmpty = reloadStartedEmpty,
                IsMagazineHit = magazineHit
            });
        }

        // Presses are buffered uniformly. A paid action can replace only resolved recovery.
        bool CanBeginSpecial() => attackTimer <= 0 || IsMeleeSwingActive && pendingMeleeStep == null && AttackAnimationProgress >= MeleeMotionTiming.Recovery;
        bool TryUniqueInput()
        {
            if (specialInputBuffer <= 0)
                return false;
            return sig != null && SigPress();
        }
        void BeginActionMelee(MeleeAttackMotion motion, float power, int comboStep = 0)
        {
            activeComboAnimationStep = comboStep;
            var original = MeleeComboCatalog.Get(EquippedWeapon).HeavyStep;
            var step = new MeleeAttackStep(1, .85f, original.Reach, original.HitboxSize, 0, 7, motion);
            PerformMeleeAttack(step, power * Stats.SpecialDamageMultiplier, motion == MeleeAttackMotion.Cleave || motion == MeleeAttackMotion.Impact);
            weaponActionTimer = attackPoseDuration;
        }
        IEnumerator ActionStep(Vector2 aim, float distance, float duration, int epoch)
        {
            float elapsed = 0, moved = 0;
            distance = GetSafeTravelDistance(aim, distance);
            while (elapsed < duration && IsCombatEpochValid(epoch))
            {
                float delta = Mathf.Min(Time.deltaTime, duration - elapsed);
                float step = Mathf.Min(distance - moved, distance * delta / duration);
                step = GetSafeTravelDistance(aim, step);
                body.position += aim * step;
                moved += step;
                elapsed += delta;
                yield return null;
            }
        }
        internal void ActionShake(float amount)
        {
            if (cameraFollow != null)
                cameraFollow.Shake(amount);
        }
        public void SecondaryHit(EnemyController target, float amount, Vector2 push)
        {
            if (!ItemEffectsSuspended && target != null && !target.IsDead)
                ItemEffects.ApplySecondaryDamage(target, amount, push);
        }
        void ActionBlast(Vector2 point, float radius, float damage, string clip)
        {
            foreach (var enemy in CombatActionGeometry.Circle(point, radius))
                SecondaryHit(enemy, damage, ((Vector2)enemy.transform.position - point).normalized * 5);
            NativeCombatEffect.Create(clip, point, 0, radius * 2, .35f, Color.white)?.BelowActor(spriteRenderer, -3);
        }
        void ActionLine(Vector2 from, Vector2 to, float width, float damage, string clip, HashSet<EnemyController> seen = null)
        {
            foreach (var enemy in CombatActionGeometry.Segment(from, to, width))
                if (seen == null || seen.Add(enemy))
                    SecondaryHit(enemy, damage, (to - from).normalized * 4);
            CombatActionGeometry.Trail(clip, from, to, Mathf.Max(.55f, width), spriteRenderer);
        }
        public void OnDirectCombatHit(EnemyController enemy, int action, float damage, bool melee, bool finisher = false, bool counter = false, long sourceWeaponIdentity = 0, bool magazineHit = false)
        {
            if (enemy == null || ItemEffectsSuspended || ItemEffects.IsDealingProc)
                return;
            Grow(finisher ? CombatGrowthKind.FinisherHit : CombatGrowthKind.DirectHit, enemy, action, counter, sourceWeaponIdentity, false, magazineHit);
            ItemEffects.OnOriginalHit(enemy, action, damage);
            ItemEffects.OnCombatContact(enemy, action, damage, melee, finisher);
            if (sourceWeaponIdentity == 0 || sourceWeaponIdentity == CurrentWeaponIdentity)
                ResolveAwakenedContact(enemy, action, finisher, counter);
        }
        public bool TryCatchHostileProjectile(Vector2 point, Vector2 travel, float incoming)
        {
            if (!isActiveAndEnabled || ItemEffectsSuspended)
                return false;
            if (SigCatchProjectile(point))
                return true;
            if (ItemEffects.TryCatchProjectile(point, travel, incoming))
                return true;
            if (dashing && Vector2.Distance(point, transform.position) <= .7f)
                TalentOnDodge();
            return false;
        }
        void OnActiveDefense()
        {
            Grow(CombatGrowthKind.Defense);
            ItemEffects.OnDefenseSuccess(AimDirection);
        }
    }
}
