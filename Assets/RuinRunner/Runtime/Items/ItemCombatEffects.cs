using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Run-owned, bounded item actions. Only original player contacts enter OnOriginalHit.</summary>
    public sealed partial class ItemCombatEffects : MonoBehaviour
    {
        sealed class Mark
        {
            public EnemyController Enemy;
            public int Contacts, Ticks;
            public float Damage, NextTick, TickInterval;
            public NativeCombatEffect View;
        }
        sealed class Shard
        {
            public Transform Root;
            public Vector2 Point;
            public float Age, Heal;
        }
        sealed class Wake
        {
            public Vector2 Point;
            public float Age, Damage;
            public NativeCombatEffect View;
        }
        readonly Dictionary<EnemyController, Mark> marks = new Dictionary<EnemyController, Mark>();
        readonly Dictionary<EnemyController, HashSet<int>> contacts = new Dictionary<EnemyController, HashSet<int>>();
        readonly List<EnemyController> expired = new List<EnemyController>();
        readonly List<EnemyController> markKeys = new List<EnemyController>();
        readonly List<EnemyController> contactKeys = new List<EnemyController>();
        readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        readonly List<Shard> shards = new List<Shard>();
        readonly List<Wake> wakes = new List<Wake>();
        readonly HashSet<EnemyController> wakeHits = new HashSet<EnemyController>();
        readonly HashSet<EnemyController> orbitHits = new HashSet<EnemyController>();
        readonly List<EnemyController> targets = new List<EnemyController>();
        readonly NativeCombatEffect[] orbits = new NativeCombatEffect[2];
        readonly Dictionary<AccessoryType, int> ownedEffectStrength = new Dictionary<AccessoryType, int>();
        readonly HashSet<int> manaHitActions = new HashSet<int>();
        readonly Dictionary<AccessoryType,AccessoryTuning> runTuning=new Dictionary<AccessoryType,AccessoryTuning>();
        SetBonusDefinition[] setDefinitions;
        int manaThreadHits;
        float manaThreadReady, bellReady, rushUntil, rushReady, focusCharge, focusReady, powderReady;
        float lastLightReady, lastLightUntil, lastLightShield;
        NativeCombatEffect focusView;
        bool hadBloodPair;
        PlayerController owner;
        InventorySystem inventory;
        float clock, shardReady, echoReady, orbitReady, chainReady, shieldReady, bashReady, wakeReady;
        float echoAt = -1, echoDamage, orbitUntil, orbitDamage, shieldUntil, shield, wakeDamage;
        Vector2 echoOrigin, echoAim, previousDashPoint;
        int fangContacts, manaCasts;
        bool dealingProc, dashWake, wakeRefunded;
        static readonly Color Blood = new Color(1f, .28f, .35f);
        static readonly Color Arcane = new Color(.73f, .72f, 1f);
        public int BleedTicksResolved { get; private set; }
        public int EchoContactsResolved { get; private set; }
        public int ChainContactsResolved { get; private set; }
        public int OrbitContactsResolved { get; private set; }
        public int WakeContactsResolved { get; private set; }
        public int BashContactsResolved { get; private set; }
        public int ShardsCollected { get; private set; }
        public int ActiveShards => shards.Count;
        public float Shield => shield+lastLightShield;
        public int ManaThreadProcs { get; private set; }
        public int GuardBellProcs { get; private set; }
        public int RushProcs { get; private set; }
        public int FocusProcs { get; private set; }
        public int PowderProcs { get; private set; }
        public int LastLightProcs { get; private set; }
        public bool IsDealingProc => dealingProc;

        public void Initialize(PlayerController player, InventorySystem bag)
        {
            if(inventory!=null)inventory.ReloadCompleted-=OnReloadCompleted;
            owner = player; inventory = bag;
            runTuning.Clear(); foreach(var definition in AccessoryLootTable.All)runTuning.Add(definition.Type,definition.Tuning);
            setDefinitions=ItemSetCatalog.Capture();
            if(inventory!=null)inventory.ReloadCompleted+=OnReloadCompleted;
            ReconcileOwnedEffects();
        }
        int Copies(AccessoryType type)
        {
            int count = 0;
            if (inventory != null) foreach (var item in inventory.Accessories) if (item.Type == type) count++;
            return Mathf.Min(Tuning(type).EffectCount("maxCopies"), count);
        }
        AccessoryTuning Tuning(AccessoryType type)
        {
            if(inventory!=null)for(int i=0;i<inventory.OwnedAccessories.Count;i++)
                if(inventory.OwnedAccessories[i].Definition.Type==type)return inventory.OwnedAccessories[i].Definition.Tuning;
            return runTuning.TryGetValue(type,out var tuning)?tuning:AccessoryTuning.Legacy(AccessoryLootTable.Find(type).Id);
        }
        AccessoryTuning SetTuning(ItemSetType type,int pieces)
        {
            if(setDefinitions!=null)foreach(var definition in setDefinitions)
                if(definition.SetType==type&&definition.RequiredPieces==pieces)return definition.Tuning;
            return AccessoryTuning.Legacy(ItemSetCatalog.TuningId(type,pieces));
        }
        float Effect(AccessoryType type,string key)=>Tuning(type).Effect(key);
        int EffectCount(AccessoryType type,string key)=>Tuning(type).EffectCount(key);
        float StackValue(AccessoryType type,string key,string perCopy)=>Effect(type,key)+Effect(type,perCopy)*(Copies(type)-1);
        AccessoryTuning BleedTuning=>Copies(AccessoryType.BerserkerEmblem)>0?Tuning(AccessoryType.BerserkerEmblem):SetTuning(ItemSetType.BloodMoon,2);
        bool Set(ItemSetType type, int count) => inventory != null && inventory.GetSetPieceCount(type) >= count;
        int Upgrade(AccessoryType type) => inventory == null ? 0 : inventory.GetAccessoryUpgradeLevel(type);
        float ProcStrength(AccessoryType type) => Upgrade(type) >= 3 ? Effect(type,"legacyStrengthMultiplier") : 1f;
        bool Active => owner != null && !owner.ItemEffectsSuspended && Time.timeScale > 0;
        float Snapshot => owner.Damage * owner.Stats.SpecialDamageMultiplier;
        int Order(Vector2 point) => 1000 - Mathf.RoundToInt(point.y * 100) + 3;
        void DestroyView(NativeCombatEffect view) { if (view != null) Destroy(view.gameObject); }

        public void OnOriginalHit(EnemyController enemy, int action, float sourceDamage)
        {
            if (!Active || enemy == null || dealingProc) return;
            int emblem = Copies(AccessoryType.BerserkerEmblem);
            int fang = Copies(AccessoryType.VampireFang);
            bool bloodSet = Set(ItemSetType.BloodMoon, 2);
            if (emblem == 0 && fang == 0 && !bloodSet && Copies(AccessoryType.ManaThread)==0 && Copies(AccessoryType.RushSpur)==0 && Copies(AccessoryType.SteadyLocket)==0) return;
            if (!contacts.TryGetValue(enemy, out var seen)) contacts[enemy] = seen = new HashSet<int>();
            // A shotgun fan, a burst and an explosive direct/blast contact count once per action/target.
            if (!seen.Add(action)) return;
            seen.RemoveWhere(value => value < owner.AttackAnimationSequence - 32);
            if(Copies(AccessoryType.ManaThread)>0&&manaHitActions.Add(action))
            {
                manaHitActions.RemoveWhere(value=>value<owner.AttackAnimationSequence-32);
                int need=EffectCount(AccessoryType.ManaThread,Upgrade(AccessoryType.ManaThread)>=2?"legacyHitCount":"hitCount");
                manaThreadHits=Mathf.Min(need,manaThreadHits+1);
                if(manaThreadHits>=need&&clock>=manaThreadReady)
                {
                    manaThreadHits=0;manaThreadReady=clock+Effect(AccessoryType.ManaThread,"cooldownSeconds");ManaThreadProcs++;
                    owner.Stats.RestoreMana(StackValue(AccessoryType.ManaThread,"mana","extraCopyMana")*ProcStrength(AccessoryType.ManaThread));
                    ResourceGlint();
                }
            }
            if(Copies(AccessoryType.RushSpur)>0&&clock<rushUntil&&clock>=rushReady)
            {
                rushUntil=0;rushReady=clock+Effect(AccessoryType.RushSpur,Upgrade(AccessoryType.RushSpur)>=2?"legacyCooldownSeconds":"cooldownSeconds");RushProcs++;
                Vector2 push=((Vector2)enemy.transform.position-(Vector2)owner.transform.position).normalized;
                if(!enemy.IsDead&&Physics2D.Linecast(owner.transform.position,enemy.DamageCollider.ClosestPoint(owner.transform.position),1<<GameLayers.Ground).collider==null)
                {
                    ProcDamage(enemy,sourceDamage*StackValue(AccessoryType.RushSpur,"damageFraction","extraCopyDamageFraction")*ProcStrength(AccessoryType.RushSpur),push*Effect(AccessoryType.RushSpur,"push"));
                    NativeCombatEffect.Create("PiercingHit",enemy.transform.position,Mathf.Atan2(push.y,push.x)*Mathf.Rad2Deg,.6f,.24f,Color.white,Order(owner.transform.position)-5);
                    CombatAudio.Native("dagger",AudioChannel.Player,.2f);
                }
            }
            float preparation=Effect(AccessoryType.SteadyLocket,Upgrade(AccessoryType.SteadyLocket)>=2?"legacyPreparationSeconds":"preparationSeconds");
            if(Copies(AccessoryType.SteadyLocket)>0&&focusCharge>=preparation&&clock>=focusReady)
            {
                focusCharge=0;focusReady=clock+Effect(AccessoryType.SteadyLocket,"cooldownSeconds");FocusProcs++;DestroyView(focusView);focusView=null;
                if(!enemy.IsDead&&Physics2D.Linecast(owner.transform.position,enemy.DamageCollider.ClosestPoint(owner.transform.position),1<<GameLayers.Ground).collider==null)
                {
                    ProcDamage(enemy,sourceDamage*StackValue(AccessoryType.SteadyLocket,"damageFraction","extraCopyDamageFraction")*ProcStrength(AccessoryType.SteadyLocket),Vector2.zero);
                    NativeCombatEffect.Create("MetalSpark",enemy.transform.position,0,.6f,.22f,Color.white,Order(owner.transform.position)-5);
                    CombatAudio.Native("heavy-impact",AudioChannel.Player,.16f,1.2f);
                }
            }
            if(enemy.IsDead)return;
            if (emblem > 0 || bloodSet)
            {
                if (!marks.TryGetValue(enemy, out var mark) && marks.Count < BleedTuning.EffectCount("maxMarks"))
                    marks[enemy] = mark = new Mark { Enemy = enemy };
                if (mark != null && ++mark.Contacts >= (Upgrade(AccessoryType.BerserkerEmblem) >= 2 ? EffectCount(AccessoryType.BerserkerEmblem,"legacyHitCount") : BleedTuning.EffectCount("hitCount")))
                {
                    mark.Contacts = 0;
                    MarkEnemy(enemy, sourceDamage * (emblem > 0 ? StackValue(AccessoryType.BerserkerEmblem,"damageFraction","extraCopyDamageFraction") * ProcStrength(AccessoryType.BerserkerEmblem) : SetTuning(ItemSetType.BloodMoon,2).Effect("damageFraction")));
                }
            }
            if (fang > 0 && ++fangContacts >= EffectCount(AccessoryType.VampireFang,Upgrade(AccessoryType.VampireFang)>=2?"legacyHitCount":"hitCount"))
            {
                fangContacts = 0;
                SpawnShard(enemy.transform.position); // Boss fights have a contact-based fallback, without adds.
            }
        }

        void MarkEnemy(EnemyController enemy, float damage)
        {
            if (enemy == null || enemy.IsDead) return;
            if (!marks.TryGetValue(enemy, out var mark))
            {
                if (marks.Count >= BleedTuning.EffectCount("maxMarks")) return;
                marks[enemy] = mark = new Mark { Enemy = enemy };
            }
            bool fresh = mark.Ticks == 0;
            mark.Damage = Mathf.Max(mark.Damage, damage); mark.Ticks = BleedTuning.EffectCount("tickCount"); mark.TickInterval=BleedTuning.Effect("tickInterval");
            if (fresh) mark.NextTick = clock + mark.TickInterval; // Refreshing never postpones an already scheduled tick.
            if (mark.View == null)
                mark.View = NativeCombatEffect.Create("RewardGlint", (Vector2)enemy.transform.position + Vector2.up * .45f,
                    0, .33f, 0, Blood, Order(enemy.transform.position), enemy.transform, true);
            if (fresh) CombatAudio.Native("dagger", AudioChannel.Player, .14f, .8f);
        }

        public void OnEnemyDefeated(EnemyController enemy)
        {
            if (!Active || enemy == null) return;
            SpawnShard(enemy.transform.position);
            if (!dealingProc && Set(ItemSetType.BloodMoon, 3) && marks.TryGetValue(enemy, out var mark) && mark.Ticks > 0)
            {
                // Only a player kill spreads. Bleed/chain/orbit/wake kills cannot recursively spread.
                var spread = VisibleTargets(enemy.transform.position, SetTuning(ItemSetType.BloodMoon,3).Effect("spreadRange"), SetTuning(ItemSetType.BloodMoon,3).EffectCount("spreadCount"), Vector2.zero, enemy).ToArray();
                foreach (var next in spread) MarkEnemy(next, mark.Damage * SetTuning(ItemSetType.BloodMoon,3).Effect("spreadDamageFraction"));
                if (spread.Length > 0) NativeCombatEffect.Create("RewardRing", enemy.transform.position, 0, 1.3f, .3f, Blood, Order(enemy.transform.position));
            }
            if (marks.TryGetValue(enemy, out var old)) DestroyView(old.View);
            marks.Remove(enemy); contacts.Remove(enemy);
        }

        void SpawnShard(Vector2 point)
        {
            int fang = Copies(AccessoryType.VampireFang);
            if (fang == 0 || clock < shardReady || shards.Count >= EffectCount(AccessoryType.VampireFang,"maxShards")) return;
            shardReady = clock + Effect(AccessoryType.VampireFang,"cooldownSeconds");
            // Pick a visible, accessible position between the victim and player; never scatter across a wall.
            Vector2 from = owner.transform.position;
            if (Physics2D.Linecast(from, point, 1 << GameLayers.Ground).collider != null) point = from;
            point = Vector2.Lerp(point, from, .2f);
            if (Physics2D.OverlapCircle(point, .22f, 1 << GameLayers.Ground) != null) point = from;
            var root = new GameObject("RecoveryShard").transform; root.position = point;
            var icon = root.gameObject.AddComponent<SpriteRenderer>();
            icon.sprite = IndividualArt.Sprite("Equipment/Accessory/ruby_heart");
            // The ruby is now an authored 16x18 sprite; retain its native pixel size.
            icon.sortingOrder = Order(point); root.localScale = Vector3.one;
            var glow = NativeCombatEffect.Create("RewardGlint", point, 0, .50f, 0, Blood, Order(point) - 1, root, true);
            shards.Add(new Shard { Root = root, Point = point, Heal = StackValue(AccessoryType.VampireFang,"heal","extraCopyHeal") * ProcStrength(AccessoryType.VampireFang) });
        }

        public void OnManaSpent(float amount, Vector2 aim)
        {
            if (!Active || amount <= 0f) return;
            owner.OnSpecialCommitted();
            OnActiveSpecial(amount,aim);
            Vector2 origin = owner.transform.position;
            aim = aim.sqrMagnitude > .01f ? aim.normalized : Vector2.right;
            if (Copies(AccessoryType.ArcaneLens) > 0 && clock >= echoReady)
            {
                echoReady = clock + Effect(AccessoryType.ArcaneLens,"cooldownSeconds"); echoAt = -1; lensArmedUntil=clock+Effect(AccessoryType.ArcaneLens,"windowSeconds"); echoOrigin = origin; echoAim = aim;
                echoDamage = Snapshot * StackValue(AccessoryType.ArcaneLens,"damageFraction","extraCopyDamageFraction") * ProcStrength(AccessoryType.ArcaneLens);
                NativeCombatEffect.Create("Arcane", origin + aim * .5f, 0, .8f, .22f, Arcane, Order(origin));
            }
            if (Copies(AccessoryType.SageSigil) > 0 && clock >= orbitReady)
            {
                orbitReady = clock + Effect(AccessoryType.SageSigil,"cooldownSeconds"); orbitUntil = clock + Effect(AccessoryType.SageSigil,"durationSeconds");
                orbitDamage = Snapshot * StackValue(AccessoryType.SageSigil,"damageFraction","extraCopyDamageFraction") * ProcStrength(AccessoryType.SageSigil); orbitHits.Clear();
                for (int i = 0; i < EffectCount(AccessoryType.SageSigil,"orbitCount"); i++)
                {
                    DestroyView(orbits[i]);
                    orbits[i] = NativeCombatEffect.Create("Energy", origin, 0, .32f, 0, Color.white, Order(origin), null, true);
                }
                CombatAudio.Native("magic", AudioChannel.Player, .20f, 1.12f);
            }
            if (Copies(AccessoryType.SoulCapacitor) > 0 && clock >= chainReady)
            {
                chainReady = clock + Effect(AccessoryType.SoulCapacitor,"cooldownSeconds");
                Chain(origin, aim, Snapshot * StackValue(AccessoryType.SoulCapacitor,"damageFraction","extraCopyDamageFraction") * ProcStrength(AccessoryType.SoulCapacitor));
            }
            if (Set(ItemSetType.Arcanist, 2) && ++manaCasts >= SetTuning(ItemSetType.Arcanist,2).EffectCount("castCount"))
            {
                manaCasts = 0;
                // The third paid action returns a small amount; refund never emits a mana-spend event.
                owner.Stats.RestoreMana(Set(ItemSetType.Arcanist, 3) ? Mathf.Min(SetTuning(ItemSetType.Arcanist,3).Effect("manaLimit"), amount * SetTuning(ItemSetType.Arcanist,3).Effect("manaRefundFraction")) : SetTuning(ItemSetType.Arcanist,2).Effect("mana"));
                NativeCombatEffect.Create("RewardSpark", origin, 0, .75f, .36f, Arcane, Order(origin));
            }
        }

        void Chain(Vector2 origin, Vector2 aim, float damage)
        {
            var hit = new HashSet<EnemyController>();
            for (int hop = 0; hop < EffectCount(AccessoryType.SoulCapacitor,Upgrade(AccessoryType.SoulCapacitor)>=2?"legacyTargetCount":"targetCount"); hop++)
            {
                var candidates = VisibleTargets(origin, Effect(AccessoryType.SoulCapacitor,hop==0?"initialRange":"hopRange"), 8, hop == 0 ? aim : Vector2.zero);
                EnemyController next = null;
                foreach (var e in candidates) if (!hit.Contains(e)) { next = e; break; }
                if (next == null) break;
                hit.Add(next); var end = (Vector2)next.DamageCollider.ClosestPoint(origin);
                var delta = end - origin;
                // Short native bolts along an actual, wall-tested hop. No stretched source canvas.
                int segments = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / .45f), 1, 9);
                int lightningOrder = Mathf.Min(Order(end), Order(owner.transform.position) - 4);
                for (int i = 0; i < segments; i++)
                    NativeCombatEffect.Create("ItemThunder", Vector2.Lerp(origin, end, (i + .5f) / segments),
                        Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90, .6f, .24f, Color.white, lightningOrder);
                ProcDamage(next, damage, delta.normalized * Effect(AccessoryType.SoulCapacitor,"push")); ChainContactsResolved++;
                origin = end; damage *= Effect(AccessoryType.SoulCapacitor,"hopDamageMultiplier");
            }
            if (hit.Count > 0) CombatAudio.Native("magic", AudioChannel.Player, .25f, .9f);
        }

        public void OnDashStarted(Vector2 start)
        {
            if(cadenceStep==1)cadenceStep=2;
            rushUntil=0;focusCharge=0;DestroyView(focusView);focusView=null;
            dashWake = Active && Set(ItemSetType.Storm, 2) && clock >= wakeReady;
            if (!dashWake) return;
            wakeReady = clock + SetTuning(ItemSetType.Storm,2).Effect("cooldownSeconds"); previousDashPoint = start; wakeDamage = Snapshot * SetTuning(ItemSetType.Storm,2).Effect("damageFraction");
            wakeHits.Clear(); wakeRefunded = false;
            AddWake(start);
        }
        public void OnDashMoved(Vector2 point)
        {
            if (!dashWake || !Active) return;
            if (Vector2.Distance(previousDashPoint, point) < SetTuning(ItemSetType.Storm,2).Effect("markSpacing")) return;
            if (Physics2D.Linecast(previousDashPoint, point, 1 << GameLayers.Ground).collider != null) return;
            AddWake(point); previousDashPoint = point;
        }
        public void OnDashEnded()
        {
            dashWake = false;
            if(Active&&Copies(AccessoryType.RushSpur)>0&&clock>=rushReady)rushUntil=clock+Effect(AccessoryType.RushSpur,"windowSeconds");
        }
        void AddWake(Vector2 point)
        {
            if (wakes.Count >= SetTuning(ItemSetType.Storm,2).EffectCount("maxWakes")) return;
            wakes.Add(new Wake { Point = point, Damage = wakeDamage, View = NativeCombatEffect.Create("Wind", point, 0,
                .85f, SetTuning(ItemSetType.Storm,2).Effect("durationSeconds"), new Color(.7f, 1f, .9f, .65f), Order(point) - 3) });
        }

        public void OnDefenseSuccess(Vector2 aim)
        {
            if (!Active) return;
            if(Copies(AccessoryType.GuardBell)>0&&clock>=bellReady)
            {
                bellReady=clock+Effect(AccessoryType.GuardBell,Upgrade(AccessoryType.GuardBell)>=2?"legacyCooldownSeconds":"cooldownSeconds");GuardBellProcs++;
                owner.Stats.RestoreMana(StackValue(AccessoryType.GuardBell,"mana","extraCopyMana")*ProcStrength(AccessoryType.GuardBell));ResourceGlint();
            }
            if (!Set(ItemSetType.Guardian, 2)) return;
            Vector2 point = owner.transform.position;
            if (clock >= shieldReady)
            {
                shieldReady = clock + SetTuning(ItemSetType.Guardian,2).Effect("cooldownSeconds"); shieldUntil = clock + SetTuning(ItemSetType.Guardian,2).Effect("durationSeconds"); shield = SetTuning(ItemSetType.Guardian,2).Effect("shield");
                NativeCombatEffect.Create("ItemShield", point, 0, 1.05f, .5f, Color.white, Order(point))?.BelowActor(owner.BodyRenderer,-3);
            }
            if (Set(ItemSetType.Guardian, 3) && clock >= bashReady)
            {
                var tuning=SetTuning(ItemSetType.Guardian,3);
                bashReady = clock + tuning.Effect("cooldownSeconds");
                var chosen = VisibleTargets(point, tuning.Effect("range"), tuning.EffectCount("targetCount"), aim).ToArray();
                foreach (var enemy in chosen) { ProcDamage(enemy, Snapshot * tuning.Effect("damageFraction") + owner.Stats.Armor * tuning.Effect("armorDamageFraction"), aim * tuning.Effect("push")); BashContactsResolved++; }
                NativeCombatEffect.Create("ImpactRing", point + aim * .65f, 0, 1.3f, .30f, new Color(1f, .85f, .56f), Order(point));
                CombatAudio.Native("heavy-impact", AudioChannel.Player, .24f);
            }
        }
        public float AbsorbDamage(float raw)
        {
            if(Active&&raw>0&&Copies(AccessoryType.LastLightPendant)>0)
            {
                float threshold=Effect(AccessoryType.LastLightPendant,Upgrade(AccessoryType.LastLightPendant)>=2?"legacyHealthThreshold":"healthThreshold");
                if(clock>=lastLightReady&&owner.Stats.Health<=owner.Stats.MaxHealth*threshold)
                {
                    lastLightReady=clock+Effect(AccessoryType.LastLightPendant,"cooldownSeconds");lastLightUntil=clock+Effect(AccessoryType.LastLightPendant,"durationSeconds");LastLightProcs++;
                    lastLightShield=StackValue(AccessoryType.LastLightPendant,"shield","extraCopyShield")*ProcStrength(AccessoryType.LastLightPendant);
                    NativeCombatEffect.Create("ItemShield",owner.transform.position,0,1.05f,.45f,Color.white,Order(owner.transform.position)-5)?.BelowActor(owner.BodyRenderer,-3);
                    CombatAudio.Native("magic",AudioChannel.Player,.2f,.85f);
                }
                if(clock<lastLightUntil){float lastLightUsed=Mathf.Min(Mathf.Max(0,raw),lastLightShield);lastLightShield-=lastLightUsed;raw-=lastLightUsed;}
            }
            if (!Active || shield <= 0 || clock >= shieldUntil || !Set(ItemSetType.Guardian, 2)) return raw;
            float used = Mathf.Min(Mathf.Max(0f, raw), shield); shield -= used;
            NativeCombatEffect.Create("ItemShield", owner.transform.position, 0, .95f, .25f, Color.white, Order(owner.transform.position))?.BelowActor(owner.BodyRenderer,-3);
            return raw - used;
        }

        List<EnemyController> VisibleTargets(Vector2 origin, float radius, int limit, Vector2 aim, EnemyController excluded = null)
        {
            targets.Clear();
            var filter = new ContactFilter2D(); filter.SetLayerMask(1 << GameLayers.Enemy); filter.useTriggers = true;
            overlaps.Clear(); Physics2D.OverlapCircle(origin, radius, filter, overlaps);
            foreach (var collider in overlaps)
            {
                var enemy = EnemyController.DamageTarget(collider);
                if (enemy == null || enemy == excluded || enemy.IsDead || targets.Contains(enemy)) continue;
                var point = collider.ClosestPoint(origin); var delta = point - origin;
                if (aim.sqrMagnitude > .01f && delta.sqrMagnitude > .01f && Vector2.Dot(aim.normalized, delta.normalized) < .35f) continue;
                if (Physics2D.Linecast(origin, point, 1 << GameLayers.Ground).collider != null) continue;
                targets.Add(enemy);
            }
            targets.Sort((a, b) => ((Vector2)a.transform.position - origin).sqrMagnitude.CompareTo(((Vector2)b.transform.position - origin).sqrMagnitude));
            if (targets.Count > limit) targets.RemoveRange(limit, targets.Count - limit);
            return targets;
        }
        void ProcDamage(EnemyController target, float damage, Vector2 push)
        {
            if (target == null || target.IsDead || damage <= 0) return;
            bool previous = dealingProc; dealingProc = true;
            try { target.TakeItemDamage(damage, push); }
            finally { dealingProc = previous; }
        }
        public void ApplySecondaryDamage(EnemyController target,float damage,Vector2 push)=>ProcDamage(target,damage,push);

        void Update()
        {
            if (owner == null || owner.IsDead) { CancelTransient(); return; }
            if (!Active) return;
            clock += Time.deltaTime;
            TickExtendedItems();
            if(clock>=lastLightUntil||Copies(AccessoryType.LastLightPendant)==0)lastLightShield=0;
            TickFocus();
            if (!Set(ItemSetType.Guardian, 2) || clock >= shieldUntil) shield = 0;
            TickMarks(); TickShards(); TickWakes(); TickOrbits();
            if (echoAt >= 0 && clock >= echoAt)
            {
                echoAt = -1;
                if (Copies(AccessoryType.ArcaneLens) > 0 && Physics2D.Linecast(echoOrigin, owner.transform.position, 1 << GameLayers.Ground).collider == null)
                {
                    var chosen = VisibleTargets(echoOrigin, Effect(AccessoryType.ArcaneLens,"range"), Set(ItemSetType.Arcanist, 3) ? SetTuning(ItemSetType.Arcanist,3).EffectCount("lensTargetCount") : EffectCount(AccessoryType.ArcaneLens,"targetCount"), echoAim).ToArray();
                    foreach (var e in chosen)
                    {
                        Vector2 point = e.DamageCollider.ClosestPoint(echoOrigin);
                        NativeCombatEffect.Create("Arcane", point, 0, .85f, .32f, Arcane, Order(point));
                        ProcDamage(e, echoDamage, echoAim); EchoContactsResolved++;
                    }
                    if (chosen.Length > 0) CombatAudio.Native("magic", AudioChannel.Player, .17f, 1.3f);
                }
            }
        }
        void TickMarks()
        {
            expired.Clear();
            bool enabled = Copies(AccessoryType.BerserkerEmblem) > 0 || Set(ItemSetType.BloodMoon, 2);
            // Copy keys: a kill callback may remove a mark while resolving its last tick.
            markKeys.Clear(); markKeys.AddRange(marks.Keys);
            foreach (var enemy in markKeys)
            {
                var mark = marks[enemy];
                if (!enabled || enemy == null || enemy.IsDead) { DestroyView(mark.View); expired.Add(enemy); continue; }
                if (mark.Ticks <= 0 || clock < mark.NextTick) continue;
                mark.NextTick += mark.TickInterval; mark.Ticks--;
                ProcDamage(enemy, mark.Damage, Vector2.zero); BleedTicksResolved++;
                if (mark.Ticks == 0) { DestroyView(mark.View); mark.View = null; mark.Damage = 0; }
            }
            foreach (var enemy in expired) { marks.Remove(enemy); contacts.Remove(enemy); }
            // Bound the contact ledger even for enemies that never received a mark.
            contactKeys.Clear(); contactKeys.AddRange(contacts.Keys);
            foreach (var enemy in contactKeys) if (enemy == null || enemy.IsDead) contacts.Remove(enemy);
        }
        void TickShards()
        {
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var shard = shards[i]; shard.Age += Time.deltaTime;
                if (shard.Root == null || shard.Age > Effect(AccessoryType.VampireFang,"durationSeconds") || Copies(AccessoryType.VampireFang) == 0)
                { if (shard.Root != null) Destroy(shard.Root.gameObject); shards.RemoveAt(i); continue; }
                shard.Root.position = shard.Point + Vector2.up * (.12f + Mathf.Sin(shard.Age * 3) * .03f);
                Vector2 foot = (Vector2)owner.transform.position + RoomLayoutRules.FootOffset;
                if (shard.Age < Effect(AccessoryType.VampireFang,"pickupDelaySeconds") || Vector2.Distance(foot, shard.Point) > Effect(AccessoryType.VampireFang,"pickupRadius") || Physics2D.Linecast(foot, shard.Point, 1 << GameLayers.Ground).collider != null) continue;
                owner.Stats.Heal(shard.Heal); ShardsCollected++;
                NativeCombatEffect.Create("RewardSpark", owner.transform.position, 0, .8f, .35f, Blood, Order(owner.transform.position));
                CombatAudio.Native("reward-pickup", AudioChannel.Player, .25f);
                Destroy(shard.Root.gameObject); shards.RemoveAt(i);
            }
        }
        void TickWakes()
        {
            for (int i = wakes.Count - 1; i >= 0; i--)
            {
                var wake = wakes[i]; wake.Age += Time.deltaTime;
                var tuning=SetTuning(ItemSetType.Storm,2);
                if (wake.Age >= tuning.Effect("durationSeconds") || !Set(ItemSetType.Storm, 2)) { DestroyView(wake.View); wakes.RemoveAt(i); continue; }
                if (wakeHits.Count >= tuning.EffectCount("targetCount")) continue;
                var chosen = VisibleTargets(wake.Point, tuning.Effect("hitRadius"), 8, Vector2.zero).ToArray();
                foreach (var e in chosen)
                {
                    if (wakeHits.Count >= tuning.EffectCount("targetCount") || !wakeHits.Add(e)) continue;
                    ProcDamage(e, wake.Damage, Vector2.zero); WakeContactsResolved++;
                    if (Set(ItemSetType.Storm, 3) && !wakeRefunded) { owner.Stats.RestoreMana(SetTuning(ItemSetType.Storm,3).Effect("mana")); wakeRefunded = true; }
                }
            }
        }
        void TickOrbits()
        {
            Vector2 origin = owner.transform.position;
            for (int i = 0; i < orbits.Length; i++)
            {
                if (orbits[i] == null) continue;
                if (clock >= orbitUntil || Copies(AccessoryType.SageSigil) == 0) { DestroyView(orbits[i]); orbits[i] = null; continue; }
                float angle = clock * Effect(AccessoryType.SageSigil,"angularSpeed") + i * Mathf.PI;
                Vector2 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Effect(AccessoryType.SageSigil,"orbitRadius");
                var wall = Physics2D.Linecast(origin, point, 1 << GameLayers.Ground);
                if (wall.collider != null) point = wall.point + wall.normal * .08f;
                Vector2 previous = orbits[i].transform.position; orbits[i].transform.position = point;
                int limit = EffectCount(AccessoryType.SageSigil,Upgrade(AccessoryType.SageSigil)>=2?"legacyTargetCount":"targetCount");
                if (orbitHits.Count >= limit) continue;
                var chosen = VisibleTargets(origin, Effect(AccessoryType.SageSigil,"detectionRadius"), 8, Vector2.zero).ToArray();
                foreach (var e in chosen)
                {
                    if (orbitHits.Contains(e) || orbitHits.Count >= limit) continue;
                    var contact = e.DamageCollider.ClosestPoint(point);
                    Vector2 span = point - previous;
                    float t = span.sqrMagnitude < .001f ? 1f : Mathf.Clamp01(Vector2.Dot(contact - previous, span) / span.sqrMagnitude);
                    if (Vector2.Distance(contact, previous + span * t) > Effect(AccessoryType.SageSigil,"hitRadius") || Physics2D.Linecast(previous, contact, 1 << GameLayers.Ground).collider != null) continue;
                    orbitHits.Add(e); ProcDamage(e, orbitDamage, Vector2.zero); OrbitContactsResolved++;
                    NativeCombatEffect.Create("ImpactRing", contact, 0, .5f, .2f, Arcane, Order(contact));
                }
            }
        }

        public void CancelTransient()
        {
            ClearExtendedItems();
            DestroyView(focusView);focusView=null;focusCharge=0;rushUntil=0;lastLightShield=0;
            foreach (var mark in marks.Values) DestroyView(mark.View);
            foreach (var shard in shards) if (shard.Root != null) Destroy(shard.Root.gameObject);
            foreach (var wake in wakes) DestroyView(wake.View);
            for (int i = 0; i < orbits.Length; i++) { DestroyView(orbits[i]); orbits[i] = null; }
            marks.Clear(); contacts.Clear(); shards.Clear(); wakes.Clear(); wakeHits.Clear(); orbitHits.Clear();
            echoAt = -1; shield = 0; dashWake = false;
            // Owner/run lifetime cooldowns deliberately survive teleport, inventory movement and stage cleanup.
        }
        public void ReconcileOwnedEffects()
        {
            ReconcileExtendedItems();
            if(Copies(AccessoryType.ManaThread)==0){manaThreadHits=0;manaHitActions.Clear();}
            if(Copies(AccessoryType.RushSpur)==0)rushUntil=0;
            if(EffectStrengthReduced(AccessoryType.SteadyLocket)||Copies(AccessoryType.SteadyLocket)==0){focusCharge=0;DestroyView(focusView);focusView=null;}
            if(EffectStrengthReduced(AccessoryType.LastLightPendant)||Copies(AccessoryType.LastLightPendant)==0)lastLightShield=0;
            bool emblemReduced = EffectStrengthReduced(AccessoryType.BerserkerEmblem);
            bool fangReduced = EffectStrengthReduced(AccessoryType.VampireFang);
            bool lensReduced = EffectStrengthReduced(AccessoryType.ArcaneLens);
            bool sageReduced = EffectStrengthReduced(AccessoryType.SageSigil);
            bool bloodPair = Set(ItemSetType.BloodMoon, 2);
            if (emblemReduced || hadBloodPair && !bloodPair || Copies(AccessoryType.BerserkerEmblem) == 0 && !bloodPair)
            { foreach (var mark in marks.Values) DestroyView(mark.View); marks.Clear(); }
            hadBloodPair = bloodPair;
            if (fangReduced || Copies(AccessoryType.VampireFang) == 0)
            { foreach (var shard in shards) if (shard.Root != null) Destroy(shard.Root.gameObject); shards.Clear(); }
            if (lensReduced || Copies(AccessoryType.ArcaneLens) == 0) echoAt = -1;
            if (sageReduced || Copies(AccessoryType.SageSigil) == 0)
                for (int i = 0; i < orbits.Length; i++) { DestroyView(orbits[i]); orbits[i] = null; }
            if (!Set(ItemSetType.Storm, 2))
            { foreach (var wake in wakes) DestroyView(wake.View); wakes.Clear(); dashWake = false; }
            if (!Set(ItemSetType.Guardian, 2)) shield = 0;
            // Shared owner cooldowns and paid/contact counters remain intact.
        }
        bool EffectStrengthReduced(AccessoryType type)
        {
            int strength = Copies(type) * 4 + Upgrade(type);
            ownedEffectStrength.TryGetValue(type, out int previous); ownedEffectStrength[type] = strength;
            return strength < previous;
        }
        void ResourceGlint()
        {
            NativeCombatEffect.Create("RewardSpark",(Vector2)owner.transform.position+RoomLayoutRules.FootOffset,0,.65f,.28f,Arcane,Order(owner.transform.position)-5);
            CombatAudio.Native("reward-pickup",AudioChannel.Player,.16f,1.15f);
        }
        void TickFocus()
        {
            if(Copies(AccessoryType.SteadyLocket)==0)return;
            if(owner.MovementInput.sqrMagnitude>.01f||owner.MotionState==PlayerMotionState.Dash)
            {focusCharge=0;DestroyView(focusView);focusView=null;return;}
            float preparation=Effect(AccessoryType.SteadyLocket,Upgrade(AccessoryType.SteadyLocket)>=2?"legacyPreparationSeconds":"preparationSeconds");
            if(clock<focusReady)return;
            focusCharge=Mathf.Min(preparation,focusCharge+Time.deltaTime);
            if(focusCharge>=preparation&&focusView==null)
                focusView=NativeCombatEffect.Create("RifleEnergy",(Vector2)owner.transform.position+RoomLayoutRules.FootOffset,0,.32f,0,Color.white,Order(owner.transform.position)-5,owner.transform,true);
        }
        void OnReloadCompleted(OwnedWeapon item,bool wasEmpty)
        {
            if(!Active||!wasEmpty||Copies(AccessoryType.PowderSatchel)==0||clock<powderReady||owner.ActiveWeaponState?.Item!=item)return;
            powderReady=clock+Effect(AccessoryType.PowderSatchel,Upgrade(AccessoryType.PowderSatchel)>=2?"legacyCooldownSeconds":"cooldownSeconds");PowderProcs++;
            Vector2 origin=owner.transform.position;
            var chosen=VisibleTargets(origin,Effect(AccessoryType.PowderSatchel,"range"),EffectCount(AccessoryType.PowderSatchel,"targetCount"),Vector2.zero).ToArray();
            foreach(var enemy in chosen)ProcDamage(enemy,Snapshot*StackValue(AccessoryType.PowderSatchel,"damageFraction","extraCopyDamageFraction")*ProcStrength(AccessoryType.PowderSatchel),((Vector2)enemy.transform.position-origin).normalized*Effect(AccessoryType.PowderSatchel,"push"));
            NativeCombatEffect.Create("PowderFlash",origin+RoomLayoutRules.FootOffset,0,.8f,.28f,Color.white,Order(origin)-5);
            NativeCombatEffect.Create("GroundDust",origin+RoomLayoutRules.FootOffset,0,1.1f,.38f,Color.white,Order(origin)-6);
            CombatAudio.Native("heavy-impact",AudioChannel.Player,.23f,.9f);
        }
        void OnDestroy()
        {
            if(inventory!=null)inventory.ReloadCompleted-=OnReloadCompleted;
            CancelTransient();
        }
    }
}
