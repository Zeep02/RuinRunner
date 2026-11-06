using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Accessories whose passive is a finished pixelfx effect. Strikes land on their target or leave from the weapon, never from the hero's body.</summary>
    public sealed partial class ItemCombatEffects
    {
        sealed class Delayed
        {
            public float At;
            public Action Run;
        }
        // Clips exported by Tools/Art/accessories-fx.mjs.
        static readonly Dictionary<AccessoryType, string[]> PassiveClips = new Dictionary<AccessoryType, string[]>
        {
            {AccessoryType.RetributionThorn,new[]{"WAccRetributionThorn"}},
            {AccessoryType.MirrorShard,new[]{"WAccMirrorShard"}},
            {AccessoryType.CinderSeed,new[]{"WAccCinderSeed"}},
            {AccessoryType.ThunderCharm,new[]{"WAccThunderCharm"}},
            {AccessoryType.PoisonCenser,new[]{"WAccPoisonCenser"}},
            {AccessoryType.ChainSpark,new[]{"WAccChainSpark","WAccChainSparkLine"}},
            {AccessoryType.SoulLantern,new[]{"WAccSoulLantern","WAccSoulLanternBolt","WAccSoulLanternHit"}},
            {AccessoryType.VineSnare,new[]{"WAccVineSnare"}},
            {AccessoryType.ShadowTwinRing,new[]{"WAccShadowTwinRing"}},
            {AccessoryType.QuakeBell,new[]{"WAccQuakeBell"}},
            {AccessoryType.FrostWaveNecklace,new[]{"WAccFrostWaveNecklace"}},
            {AccessoryType.GuardianFeather,new[]{"WAccGuardianFeather"}},
            {AccessoryType.BloodPact,new[]{"WAccBloodPact"}},
            {AccessoryType.SwapFlash,new[]{"WAccSwapFlash"}},
            {AccessoryType.MeteorCompass,new[]{"WAccMeteorCompass"}},
            {AccessoryType.GravityPearl,new[]{"WAccGravityPearl","WAccGravityPearlBurst"}},
            {AccessoryType.TimeHourglass,new[]{"WAccTimeHourglass"}},
            {AccessoryType.HolyTithe,new[]{"WAccHolyTithe"}},
            {AccessoryType.StormCore,new[]{"WAccStormCore"}},
            {AccessoryType.PhoenixAsh,new[]{"WAccPhoenixAsh","WAccPhoenixAshRing"}},
        };
        readonly Dictionary<AccessoryType, int> passiveCounts = new Dictionary<AccessoryType, int>();
        readonly Dictionary<AccessoryType, float> passiveReady = new Dictionary<AccessoryType, float>();
        readonly List<Delayed> passiveQueue = new List<Delayed>();
        readonly HashSet<AccessoryType> passivesLoaded = new HashSet<AccessoryType>();
        readonly Dictionary<EnemyController, int> vineHits = new Dictionary<EnemyController, int>();
        readonly Dictionary<EnemyController, int> vineActions = new Dictionary<EnemyController, int>();
        Vector2 passiveDashFrom;
        public int PassiveProcs { get; private set; }

        bool PassiveReady(AccessoryType type) => Copies(type) > 0 && (!passiveReady.TryGetValue(type, out var at) || clock >= at);
        void PassiveUsed(AccessoryType type)
        {
            if (Tuning(type).Number("effect.cooldownSeconds") > 0)
                passiveReady[type] = clock + Effect(type, "cooldownSeconds");
            PassiveProcs++;
        }
        // Counts one attack. A charge waits out the cooldown instead of being lost.
        bool PassiveCount(AccessoryType type)
        {
            if (Copies(type) == 0)
                return false;
            int need = EffectCount(type, "hitCount");
            passiveCounts.TryGetValue(type, out int hits);
            hits = Mathf.Min(need, hits + 1);
            passiveCounts[type] = hits;
            if (hits < need || !PassiveReady(type))
                return false;
            passiveCounts[type] = 0;
            PassiveUsed(type);
            return true;
        }
        float PassivePower(AccessoryType type) => Snapshot * StackValue(type, "damageFraction", "extraCopyDamageFraction");
        EnemyController[] Near(Vector2 point, float radius, int limit = 16, EnemyController excluded = null) => VisibleTargets(point, radius, limit, Vector2.zero, excluded).ToArray();
        void After(float delay, Action run) => passiveQueue.Add(new Delayed { At = clock + delay, Run = run });
        static Vector2 Feet(EnemyController enemy) => (Vector2)enemy.transform.position + Vector2.down * .25f;
        void HitAll(EnemyController[] enemies, float damage, Vector2 center, float push = 0)
        {
            foreach (var enemy in enemies)
                ProcDamage(enemy, damage, push > 0 ? ((Vector2)enemy.transform.position - center).normalized * push : Vector2.zero);
        }

        void OnPassiveContact(EnemyController enemy)
        {
            Vector2 point = enemy.transform.position;
            if (PassiveCount(AccessoryType.ThunderCharm))
            {
                SigFx.Play("WAccThunderCharm", point);
                ProcDamage(enemy, PassivePower(AccessoryType.ThunderCharm), Vector2.zero);
                CombatAudio.Native("magic", AudioChannel.Player, .2f, 1.2f);
            }
            if (PassiveCount(AccessoryType.ShadowTwinRing))
            {
                float damage = PassivePower(AccessoryType.ShadowTwinRing);
                int others = EffectCount(AccessoryType.ShadowTwinRing, "targetCount") - 1;
                CrossCut(point, false);
                ProcDamage(enemy, damage, Vector2.zero);
                foreach (var other in Near(point, Effect(AccessoryType.ShadowTwinRing, "range"), others, enemy))
                {
                    CrossCut(other.transform.position, true);
                    ProcDamage(other, damage, Vector2.zero);
                }
                CombatAudio.Native("sword", AudioChannel.Player, .2f, .8f);
            }
            if (PassiveCount(AccessoryType.PoisonCenser))
            {
                float damage = PassivePower(AccessoryType.PoisonCenser), radius = Effect(AccessoryType.PoisonCenser, "range"), seconds = Effect(AccessoryType.PoisonCenser, "seconds");
                var pool = Feet(enemy);
                SigFx.Loop("WAccPoisonCenser", pool, 0, true, 12, null, 1, seconds);
                for (float at = .5f; at <= seconds; at += .5f)
                    After(at, () => HitAll(Near(pool, radius), damage, pool));
                CombatAudio.Native("dagger", AudioChannel.Player, .14f, .7f);
            }
            if (PassiveCount(AccessoryType.ChainSpark))
                ChainSparks(enemy);
            if (PassiveCount(AccessoryType.MirrorShard))
            {
                var aim = owner.AimDirection;
                Vector2 from = owner.SkillOrigin;
                var to = CombatActionGeometry.Clip(from, aim, Effect(AccessoryType.MirrorShard, "range"));
                SigFx.Play("WAccMirrorShard", from, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                HitLine(from, to, Effect(AccessoryType.MirrorShard, "width"), PassivePower(AccessoryType.MirrorShard));
                CombatAudio.Native("block", AudioChannel.Player, .14f, 1.4f);
            }
            if (PassiveCount(AccessoryType.QuakeBell))
            {
                float radius = Effect(AccessoryType.QuakeBell, "range");
                var chosen = Near(point, radius);
                SigFx.Play("WAccQuakeBell", Feet(enemy), 0, -1, true);
                HitAll(chosen, PassivePower(AccessoryType.QuakeBell), point);
                foreach (var other in chosen)
                    if (!other.IsDead)
                        EnemyStatus.On(other).Root(Effect(AccessoryType.QuakeBell, "seconds"));
                CombatAudio.Native("heavy-impact", AudioChannel.Player, .24f, .8f);
            }
            if (owner.Health <= owner.MaxHealth * (Copies(AccessoryType.BloodPact) > 0 ? Effect(AccessoryType.BloodPact, "healthThreshold") : 0) && PassiveCount(AccessoryType.BloodPact))
            {
                SigFx.Play("WAccBloodPact", point);
                ProcDamage(enemy, PassivePower(AccessoryType.BloodPact), Vector2.zero);
                owner.Stats.Heal(Effect(AccessoryType.BloodPact, "heal"));
                ResourceGlint();
            }
            if (PassiveCount(AccessoryType.GravityPearl))
            {
                float radius = Effect(AccessoryType.GravityPearl, "range"), seconds = Effect(AccessoryType.GravityPearl, "seconds"), damage = PassivePower(AccessoryType.GravityPearl);
                // The clip's hole hangs a unit above its origin.
                SigFx.Play("WAccGravityPearl", point + Vector2.down, 0, seconds);
                for (float at = 0; at < seconds; at += .1f)
                    After(at, () =>
                    {
                        foreach (var other in Near(point, radius))
                            owner.SigPull(other, point, 6);
                    });
                After(seconds, () =>
                {
                    SigFx.Play("WAccGravityPearlBurst", point);
                    HitAll(Near(point, radius), damage, point);
                    CombatAudio.Native("heavy-impact", AudioChannel.Player, .22f, 1.2f);
                });
                CombatAudio.Native("magic", AudioChannel.Player, .2f, .6f);
            }
        }
        // Counted per target and per attack: a sweep touching three enemies adds one to each, whichever it reached first.
        void OnVineContact(EnemyController enemy, int action)
        {
            if (Copies(AccessoryType.VineSnare) == 0)
                return;
            if (vineActions.TryGetValue(enemy, out int last) && last == action)
                return;
            vineActions[enemy] = action;
            vineHits.TryGetValue(enemy, out int hits);
            hits++;
            if (hits < EffectCount(AccessoryType.VineSnare, "hitCount") || !PassiveReady(AccessoryType.VineSnare) || enemy.IsDead)
            {
                vineHits[enemy] = hits;
                return;
            }
            vineHits[enemy] = 0;
            PassiveUsed(AccessoryType.VineSnare);
            SigFx.Play("WAccVineSnare", Feet(enemy), 0, -1, true);
            EnemyStatus.On(enemy).Root(Effect(AccessoryType.VineSnare, "seconds"));
            ProcDamage(enemy, PassivePower(AccessoryType.VineSnare), Vector2.zero);
            CombatAudio.Native("dagger", AudioChannel.Player, .16f, .6f);
        }
        // The cross closes 1.4 units along +x from the clip's origin, which has to land on the target.
        void CrossCut(Vector2 target, bool mirrored)
        {
            var along = (target - (Vector2)owner.transform.position).normalized;
            SigFx.Play("WAccShadowTwinRing", target - along * 1.4f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg, -1, false, mirrored);
        }
        void ChainSparks(EnemyController first)
        {
            float damage = PassivePower(AccessoryType.ChainSpark);
            var seen = new HashSet<EnemyController> { first };
            var from = first;
            SigFx.Play("WAccChainSpark", first.transform.position);
            ProcDamage(first, damage, Vector2.zero);
            for (int hop = 1; hop < EffectCount(AccessoryType.ChainSpark, "targetCount"); hop++)
            {
                EnemyController next = null;
                foreach (var enemy in Near(from.transform.position, Effect(AccessoryType.ChainSpark, "range")))
                    if (seen.Add(enemy))
                    {
                        next = enemy;
                        break;
                    }
                if (next == null)
                    break;
                damage *= Effect(AccessoryType.ChainSpark, "hopDamageMultiplier");
                SigFx.Line("WAccChainSparkLine", from.transform.position, next.transform.position, .3f);
                SigFx.Play("WAccChainSpark", next.transform.position);
                ProcDamage(next, damage, Vector2.zero);
                from = next;
            }
            CombatAudio.Native("magic", AudioChannel.Player, .18f, 1.5f);
        }
        void HitLine(Vector2 from, Vector2 to, float width, float damage)
        {
            foreach (var enemy in CombatActionGeometry.Segment(from, to, width))
                ProcDamage(enemy, damage, Vector2.zero);
        }

        void OnPassiveKill(EnemyController enemy)
        {
            Vector2 point = enemy.transform.position;
            vineHits.Remove(enemy);
            vineActions.Remove(enemy);
            // A kill made by another passive can't start the next one, or a full room would chain itself away.
            if (!dealingProc && PassiveReady(AccessoryType.CinderSeed))
            {
                PassiveUsed(AccessoryType.CinderSeed);
                SigFx.Play("WAccCinderSeed", point);
                HitAll(Near(point, Effect(AccessoryType.CinderSeed, "range"), 16, enemy), PassivePower(AccessoryType.CinderSeed), point);
                CombatAudio.Native("heavy-impact", AudioChannel.Player, .16f, 1.3f);
            }
            if (!dealingProc && PassiveReady(AccessoryType.SoulLantern))
            {
                var next = Near(point, Effect(AccessoryType.SoulLantern, "range"), 1, enemy);
                if (next.Length > 0)
                {
                    PassiveUsed(AccessoryType.SoulLantern);
                    SigFx.Play("WAccSoulLantern", point);
                    var bolt = SigBolt.Shoot(owner, "WAccSoulLanternBolt", point, (Vector2)next[0].transform.position - point, Effect(AccessoryType.SoulLantern, "projectileSpeed"), PassivePower(AccessoryType.SoulLantern), .3f, 1, Effect(AccessoryType.SoulLantern, "range") * 1.5f);
                    bolt.Impact = "WAccSoulLanternHit";
                    bolt.ItemProc = true;
                    CombatAudio.Native("magic", AudioChannel.Player, .15f, .8f);
                }
            }
            if (Copies(AccessoryType.HolyTithe) > 0)
            {
                passiveCounts.TryGetValue(AccessoryType.HolyTithe, out int kills);
                if (++kills < EffectCount(AccessoryType.HolyTithe, "killCount"))
                {
                    passiveCounts[AccessoryType.HolyTithe] = kills;
                    return;
                }
                passiveCounts[AccessoryType.HolyTithe] = 0;
                PassiveProcs++;
                owner.Stats.Heal(Effect(AccessoryType.HolyTithe, "heal"));
                ResourceGlint();
                var next = Near(owner.transform.position, Effect(AccessoryType.HolyTithe, "range"), 1, enemy);
                if (next.Length > 0)
                {
                    SigFx.Play("WAccHolyTithe", next[0].transform.position);
                    ProcDamage(next[0], PassivePower(AccessoryType.HolyTithe), Vector2.zero);
                    CombatAudio.Native("magic", AudioChannel.Player, .22f, 1.1f);
                }
            }
        }
        void OnPassiveDashEnded()
        {
            if (!PassiveReady(AccessoryType.FrostWaveNecklace))
                return;
            float radius = Effect(AccessoryType.FrostWaveNecklace, "range");
            var chosen = Near(passiveDashFrom, radius);
            if (chosen.Length == 0)
                return;
            PassiveUsed(AccessoryType.FrostWaveNecklace);
            SigFx.Play("WAccFrostWaveNecklace", passiveDashFrom);
            HitAll(chosen, PassivePower(AccessoryType.FrostWaveNecklace), passiveDashFrom);
            foreach (var enemy in chosen)
                if (!enemy.IsDead)
                    EnemyStatus.On(enemy).Slow(Effect(AccessoryType.FrostWaveNecklace, "slowFactor"), Effect(AccessoryType.FrostWaveNecklace, "seconds"));
            CombatAudio.Native("block", AudioChannel.Player, .18f, .8f);
        }
        void OnPassiveSpecial(Vector2 aim)
        {
            aim = aim.sqrMagnitude > .01f ? aim.normalized : Vector2.right;
            if (PassiveReady(AccessoryType.MeteorCompass))
            {
                var chosen = Near(owner.transform.position, Effect(AccessoryType.MeteorCompass, "range"), EffectCount(AccessoryType.MeteorCompass, "targetCount"));
                if (chosen.Length > 0)
                {
                    PassiveUsed(AccessoryType.MeteorCompass);
                    float damage = PassivePower(AccessoryType.MeteorCompass), radius = Effect(AccessoryType.MeteorCompass, "radius"), delay = Effect(AccessoryType.MeteorCompass, "delaySeconds");
                    for (int i = 0; i < chosen.Length; i++)
                    {
                        Vector2 point = chosen[i].transform.position;
                        float start = i * .18f;
                        After(start, () => SigFx.Play("WAccMeteorCompass", point));
                        After(start + delay, () =>
                        {
                            HitAll(Near(point, radius), damage, point);
                            CombatAudio.Native("heavy-impact", AudioChannel.Player, .2f, 1.1f);
                        });
                    }
                }
            }
            if (PassiveReady(AccessoryType.TimeHourglass))
            {
                float radius = Effect(AccessoryType.TimeHourglass, "range");
                Vector2 center = (Vector2)owner.transform.position + aim * radius * .6f;
                var chosen = Near(owner.transform.position, radius + radius * .6f);
                var inside = new List<EnemyController>();
                foreach (var enemy in chosen)
                    if (Vector2.Distance(enemy.transform.position, center) <= radius)
                        inside.Add(enemy);
                if (inside.Count > 0)
                {
                    PassiveUsed(AccessoryType.TimeHourglass);
                    SigFx.Play("WAccTimeHourglass", center);
                    HitAll(inside.ToArray(), PassivePower(AccessoryType.TimeHourglass), center);
                    foreach (var enemy in inside)
                        if (!enemy.IsDead)
                            EnemyStatus.On(enemy).Slow(Effect(AccessoryType.TimeHourglass, "slowFactor"), Effect(AccessoryType.TimeHourglass, "seconds"));
                    CombatAudio.Native("magic", AudioChannel.Player, .2f, .5f);
                }
            }
        }
        void OnPassiveDefense(Vector2 aim)
        {
            if (!PassiveReady(AccessoryType.GuardianFeather))
                return;
            PassiveUsed(AccessoryType.GuardianFeather);
            Vector2 from = owner.SkillOrigin;
            SigFx.Play("WAccGuardianFeather", from, 0, -1, false, false, 1.5f);
            HitAll(VisibleTargets(owner.transform.position, Effect(AccessoryType.GuardianFeather, "range"), 16, aim).ToArray(), PassivePower(AccessoryType.GuardianFeather), owner.transform.position, Effect(AccessoryType.GuardianFeather, "push"));
            owner.Stats.Heal(Effect(AccessoryType.GuardianFeather, "heal"));
            CombatAudio.Native("reward-pickup", AudioChannel.Player, .16f, .8f);
        }
        public void OnHurt()
        {
            if (!Active || !PassiveReady(AccessoryType.RetributionThorn))
                return;
            var chosen = Near(owner.transform.position, Effect(AccessoryType.RetributionThorn, "range"), EffectCount(AccessoryType.RetributionThorn, "targetCount"));
            if (chosen.Length == 0)
                return;
            PassiveUsed(AccessoryType.RetributionThorn);
            float damage = PassivePower(AccessoryType.RetributionThorn);
            foreach (var enemy in chosen)
            {
                SigFx.Play("WAccRetributionThorn", Feet(enemy), 0, -1, true);
                ProcDamage(enemy, damage, Vector2.zero);
            }
            CombatAudio.Native("dagger", AudioChannel.Player, .18f, .7f);
        }
        public void OnWeaponSwapped()
        {
            if (!Active || !PassiveReady(AccessoryType.SwapFlash))
                return;
            var aim = owner.AimDirection;
            float radius = Effect(AccessoryType.SwapFlash, "range");
            // Equipping outside a fight shows nothing and keeps the charge.
            var chosen = VisibleTargets(owner.transform.position, radius, 16, aim).ToArray();
            if (chosen.Length == 0)
                return;
            PassiveUsed(AccessoryType.SwapFlash);
            SigFx.Play("WAccSwapFlash", (Vector2)owner.transform.position + aim * radius * .55f);
            HitAll(chosen, PassivePower(AccessoryType.SwapFlash), owner.transform.position, Effect(AccessoryType.SwapFlash, "push"));
            CombatAudio.Native("magic", AudioChannel.Player, .2f, 1.3f);
        }
        public bool TryCheatDeath()
        {
            if (owner == null || owner.ItemEffectsSuspended || !PassiveReady(AccessoryType.PhoenixAsh))
                return false;
            PassiveUsed(AccessoryType.PhoenixAsh);
            owner.Stats.Heal(owner.MaxHealth * Effect(AccessoryType.PhoenixAsh, "healFraction"));
            var aim = owner.AimDirection;
            float radius = Effect(AccessoryType.PhoenixAsh, "range");
            Vector2 point = owner.transform.position;
            SigFx.Play("WAccPhoenixAshRing", point + RoomLayoutRules.FootOffset, 0, -1, true);
            SigFx.Play("WAccPhoenixAsh", point + aim * radius * .6f);
            HitAll(Near(point, radius), PassivePower(AccessoryType.PhoenixAsh), point, 6);
            CombatAudio.Native("heavy-impact", AudioChannel.Player, .3f, .7f);
            return true;
        }
        void TickPassives()
        {
            for (int i = passiveQueue.Count - 1; i >= 0; i--)
            {
                if (clock < passiveQueue[i].At)
                    continue;
                var run = passiveQueue[i].Run;
                passiveQueue.RemoveAt(i);
                run();
            }
            if (PassiveReady(AccessoryType.StormCore))
            {
                var chosen = Near(owner.transform.position, Effect(AccessoryType.StormCore, "range"), EffectCount(AccessoryType.StormCore, "targetCount"));
                if (chosen.Length > 0)
                {
                    PassiveUsed(AccessoryType.StormCore);
                    float damage = PassivePower(AccessoryType.StormCore);
                    foreach (var enemy in chosen)
                    {
                        SigFx.Play("WAccStormCore", enemy.transform.position);
                        ProcDamage(enemy, damage, Vector2.zero);
                    }
                    CombatAudio.Native("heavy-impact", AudioChannel.Player, .2f, 1.5f);
                }
            }
        }
        void ReconcilePassives()
        {
            foreach (var row in PassiveClips)
            {
                if (Copies(row.Key) == 0)
                {
                    passiveCounts.Remove(row.Key);
                    passivesLoaded.Remove(row.Key);
                    if (row.Key == AccessoryType.VineSnare)
                    {
                        vineHits.Clear();
                        vineActions.Clear();
                    }
                    continue;
                }
                // Load the clips when the item is picked up, not on the frame its first strike lands.
                if (passivesLoaded.Add(row.Key))
                    foreach (var clip in row.Value)
                        NativeCombatEffect.Frames(clip);
            }
        }
    }
}
