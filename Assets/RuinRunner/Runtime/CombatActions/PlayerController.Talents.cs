using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The talents. Each one changes how a run is played and asks for something back.</summary>
    public sealed partial class PlayerController
    {
        const float GlassDamage = 1.6f, GlassHealth = .5f, LastStandDamage = 1.2f, LastStandHeal = .5f, MiserPerGold = .002f, RevenantHealth = .6f;
        const float NearMissDamage = 1.5f, GambleLow = .4f, GambleHigh = 2.2f, ChantStep = .2f, ChantWindow = 2f;
        const int ChantMaximum = 5;
        float chantUntil, overdriveUntil, manaLockUntil, nearMissUntil, gambleRoll = 1;
        int chantStacks, gambleSequence = -1, skillPresses;
        bool revenantSpent, dashDodged;
        readonly HashSet<EnemyController> dashCut = new HashSet<EnemyController>();
        internal Func<int> GoldQuery;
        float fullCupUntil, bloodUntil, duelFactor = 1;
        // Prey mark (id corpse_burst) and counter step (id ghost_mine): ids kept so saved unlocks stay valid.
        const float PreyRange = 7, PreySeconds = 5, PreyBonus = 1.5f, UnmarkedPenalty = .85f, CounterWindow = 1.2f, CounterBonus = 1.4f, CounterRecharge = 1.25f;
        EnemyController prey;
        SigFx preyMark, counterGlint;
        float preyUntil, counterUntil;
        int bloodStacks, duelFrame = -1;
        PlayerProgression progressionCache;
        int MaxDashes => MaxStoredDashes + (HasTalent(TalentId.DoubleStep) ? 1 : 0);
        float DashRecharge => DashRechargeSeconds * (HasTalent(TalentId.PhantomStep) ? 1.3f : 1) * (HasTalent(TalentId.GhostMine) ? CounterRecharge : 1);

        // Everything the permanent tree grants, read from what this profile has bought.
        public void ConfigureMastery(string[] unlocked)
        {
            float Ranks(string id) => TalentCatalog.MasteryRank(unlocked, System.Array.Find(TalentCatalog.Mastery, track => track.Id == id));
            Stats.MasteryHealth = 8 * Ranks("vigor");
            Stats.MasteryMana = 8 * Ranks("focus");
            Stats.MasteryDamage = .04f * Ranks("edge");
            Stats.MasterySkill = .05f * Ranks("sorcery");
            Stats.MasteryCritical = .02f * Ranks("precision");
            Stats.MasteryCriticalDamage = .08f * Ranks("ferocity");
            Stats.MasterySpeed = .12f * Ranks("swift");
            var progression = GetComponent<PlayerProgression>();
            if (progression != null)
                progression.ExperienceScale = 1 + .08f * Ranks("wisdom");
        }
        // Rules that depend on who is being hit, applied once to every source of damage.
        internal float TalentDamageAgainst(EnemyController enemy)
        {
            float result = 1;
            if (enemy.MaxHealth <= 0)
                return result;
            if (HasTalent(TalentId.ExecutionerEdge) && enemy.Health / enemy.MaxHealth <= .3f)
                result *= 1.6f;
            if (HasTalent(TalentId.FirstBlood) && enemy.Health >= enemy.MaxHealth)
                result *= 2;
            // Bosses fight alone, so the mark neither helps nor costs against them.
            if (HasTalent(TalentId.CorpseBurst) && !enemy.IsBoss)
                result *= enemy == prey && combatClock < preyUntil ? PreyBonus : UnmarkedPenalty;
            return result;
        }
        public bool ManaOverdriven => combatClock < overdriveUntil;

        float TalentDamage
        {
            get
            {
                float result = 1;
                if (HasTalent(TalentId.GlassBlade))
                    result *= GlassDamage;
                if (HasTalent(TalentId.LastStand) && Stats.MaxHealth > 0)
                    result *= 1 + LastStandDamage * (1 - Stats.Health / Stats.MaxHealth);
                if (HasTalent(TalentId.Miser) && GoldQuery != null)
                    result *= 1 + GoldQuery() * MiserPerGold;
                if (combatClock < nearMissUntil)
                    result *= NearMissDamage;
                if (combatClock < bloodUntil)
                    result *= 1 + .08f * bloodStacks;
                if (HasTalent(TalentId.Momentum) && movementInput.sqrMagnitude > .01f)
                    result *= 1.25f;
                if (HasTalent(TalentId.CursedPower))
                    result *= .75f;
                if (HasTalent(TalentId.LateBloom))
                {
                    if (progressionCache == null)
                        progressionCache = GetComponent<PlayerProgression>();
                    result *= .8f + .06f * ((progressionCache != null ? progressionCache.Level : 1) - 1);
                }
                if (HasTalent(TalentId.Duelist))
                {
                    if (duelFrame != Time.frameCount)
                    {
                        duelFrame = Time.frameCount;
                        int near = CombatActionGeometry.Circle(transform.position, 7, 3).Count;
                        duelFactor = near == 1 ? 1.7f : near >= 3 ? .8f : 1;
                    }
                    result *= duelFactor;
                }
                if (HasTalent(TalentId.Gambler))
                {
                    // One roll per swing, shot or skill, so a skill's several hits agree with each other.
                    int sequence = attackAnimationSequence + skillPresses;
                    if (sequence != gambleSequence)
                    {
                        gambleSequence = sequence;
                        gambleRoll = UnityEngine.Random.Range(GambleLow, GambleHigh);
                    }
                    result *= gambleRoll;
                }
                return result;
            }
        }
        float ChantMultiplier => HasTalent(TalentId.ChainChant) && combatClock < chantUntil ? 1 + chantStacks * ChantStep : 1;
        float SkillTalentMultiplier => ChantMultiplier * (combatClock < fullCupUntil ? 1.8f : 1);

        void ApplyTalentStats()
        {
            if (Stats == null)
                return;
            Stats.TalentHealthScale = (HasTalent(TalentId.GlassBlade) ? GlassHealth : 1) * (revenantSpent ? RevenantHealth : 1);
            Stats.TalentHealScale = HasTalent(TalentId.LastStand) ? LastStandHeal : 1;
            Stats.TalentManaScale = HasTalent(TalentId.ManaHeart) ? 1.75f : 1;
            Stats.TalentRegenScale = HasTalent(TalentId.ManaHeart) ? .5f : 1;
            dashCharges = Mathf.Min(dashCharges, MaxDashes);
            if (HasTalent(TalentId.DoubleStep) && dashCharges == MaxStoredDashes)
                dashCharges = MaxDashes;
            Stats.Recalculate();
        }
        // Every paid skill goes through here. Mana is its only price.
        bool PayForSkill(float cost)
        {
            if (HasTalent(TalentId.FullCup) && Stats.Mana >= Stats.MaxMana - .01f)
                fullCupUntil = combatClock + 1.5f;
            if (ManaOverdriven)
            {
                skillPresses++;
                return true;
            }
            cost *= ChantMultiplier;
            if (!Stats.TrySpendMana(cost))
            {
                if (!HasTalent(TalentId.BloodPact))
                    return false;
                float shortfall = cost - Stats.Mana;
                if (Stats.Health <= shortfall)
                    return false;
                Stats.TrySpendMana(Stats.Mana);
                Stats.PayHealth(shortfall);
                SigFx.Play("TalentPact", Ahead(.9f));
            }
            skillPresses++;
            if (HasTalent(TalentId.ChainChant))
            {
                chantStacks = combatClock < chantUntil ? Mathf.Min(ChantMaximum, chantStacks + 1) : 0;
                chantUntil = combatClock + ChantWindow;
            }
            if (HasTalent(TalentId.VoidRush) && Stats.Mana < cost && combatClock >= manaLockUntil)
            {
                overdriveUntil = combatClock + 4;
                manaLockUntil = combatClock + 10;
                SigFx.Play("TalentOverdrive", Feet, 0, -1, true);
            }
            return true;
        }
        void TalentOnKill(EnemyController enemy)
        {
            if (HasTalent(TalentId.HunterBreath))
                Stats.RestoreMana(10);
            if (HasTalent(TalentId.Bloodthirst))
            {
                bloodStacks = combatClock < bloodUntil ? Mathf.Min(5, bloodStacks + 1) : 1;
                bloodUntil = combatClock + 5;
            }
            if (HasTalent(TalentId.CorpseBurst) && enemy != null)
                MarkNextPrey(enemy);
        }
        // A kill hands the mark to the closest survivor: the hunt goes one enemy at a time instead of clearing a crowd.
        void MarkNextPrey(EnemyController fallen)
        {
            EnemyController next = null;
            float best = float.MaxValue;
            foreach (var candidate in CombatActionGeometry.Circle(fallen.transform.position, PreyRange, 16))
            {
                if (candidate == null || candidate == fallen || candidate.IsDead || candidate.IsBoss)
                    continue;
                float distance = ((Vector2)candidate.transform.position - (Vector2)fallen.transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    next = candidate;
                }
            }
            if (preyMark != null)
                preyMark.Stop();
            preyMark = null;
            prey = next;
            if (next == null)
                return;
            preyUntil = combatClock + PreySeconds;
            preyMark = SigFx.Loop("TalentMark", (Vector2)next.transform.position + Vector2.up * .2f, 0, false, 12, next.transform, 1, PreySeconds);
        }
        // The first basic attack after a dash lands as a sure critical; consumed by that swing or shot.
        bool CounterStrike()
        {
            if (!HasTalent(TalentId.GhostMine) || combatClock >= counterUntil)
                return false;
            counterUntil = 0;
            if (counterGlint != null)
                counterGlint.Stop();
            counterGlint = null;
            return true;
        }
        void TalentOnDashStart(Vector2 start)
        {
            dashDodged = false;
            dashCut.Clear();
            if (!HasTalent(TalentId.GhostMine))
                return;
            counterUntil = combatClock + DashDuration + CounterWindow;
            if (counterGlint != null)
                counterGlint.Stop();
            counterGlint = SigFx.Loop("TalentReady", SkillOrigin, 0, false, 12, transform, 1, DashDuration + CounterWindow);
        }
        void TalentOnDashStep(Vector2 point)
        {
            if (!HasTalent(TalentId.RazorWind))
                return;
            foreach (var enemy in CombatActionGeometry.Circle(point, .85f))
                if (dashCut.Add(enemy))
                {
                    SigHit(enemy, Damage * .9f, DashDirection * 2, "TalentCut");
                    Stats.RestoreMana(4);
                }
        }
        void TalentOnDodge()
        {
            if (!HasTalent(TalentId.CloseCall) || dashDodged)
                return;
            dashDodged = true;
            nearMissUntil = combatClock + 3;
            Stats.RestoreMana(15);
            CameraFollow.DramaticSlow?.Invoke(.45f, .3f);
            SigFx.Play("TalentNearMiss", Ahead(.9f));
        }
        bool TalentRevive()
        {
            if (!HasTalent(TalentId.Revenant) || revenantSpent)
                return false;
            revenantSpent = true;
            ApplyTalentStats();
            Stats.Heal(Stats.MaxHealth * 2);
            invincibilityTicks = GameTick.FromSeconds(3);
            StartCoroutine(InkRise.Revive(this));
            SigArea(transform.position, 2.8f, Damage * 2, null, 8, .2f);
            return true;
        }
    }
}
