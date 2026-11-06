using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Fifteen enemies, five per stage. Each exists to make the squad ask something different of the player.</summary>
    public sealed partial class EnemyController
    {
        const float WardReduction = .6f, ShieldReduction = .35f, HealShare = .18f;
        float wardUntil;
        bool Unstoppable => Type == EnemyType.SealGolem;

        void BuildStageMobMoves()
        {
            const float Any = 99f;
            switch (Type)
            {
                // ---- Stage 1
                case EnemyType.BoneShield:
                    Stance(SquadRole.Frontline, 2.1f, .9f);
                    Add("bash", AiTag.Melee, 0, 2.8f, 1.8f, 1, ShieldBash);
                    Add("advance", AiTag.Gapcloser, 2.8f, 7, 4.5f, .9f, ShieldAdvance);
                    break;
                case EnemyType.VineLasher:
                    Stance(SquadRole.Skirmisher, 3.8f, .7f);
                    Add("lash", AiTag.Melee, 0, 5, 3.4f, 1, VineLash);
                    Add("snare", AiTag.Zone, 2, 8, 5, .9f, VineSnare);
                    break;
                case EnemyType.RuinRat:
                    Stance(SquadRole.Flanker, 2.6f);
                    Add("dart", AiTag.Melee, 0, 4.2f, 2.6f, 1, RatDart);
                    Add("double", AiTag.Gapcloser, 2.2f, 7, 4, .8f, RatDoubleDart);
                    break;
                case EnemyType.MossArcher:
                    Stance(SquadRole.Backline, 6.6f, .85f);
                    Add("hop", AiTag.Retreat, 0, 3, 1.5f, 1.7f, ShooterHop);
                    Add("aimed", AiTag.Aimed, 3, 12, 7, 1, ArcherAimedShot);
                    Add("volley", AiTag.Zone, 3, 10, 6, .8f, d => ArcherVolley());
                    break;
                case EnemyType.PowderBeetle:
                    Stance(SquadRole.Skirmisher, 3f);
                    Add("bomb", AiTag.Zone, 0, 4, 2, 1, BeetleBomb);
                    Add("run", AiTag.Gapcloser, 3, 9, 5, 1, BeetleRun);
                    break;
                // ---- Stage 2
                case EnemyType.Drowned:
                    Stance(SquadRole.Frontline, 2f, .85f);
                    Add("swipe", AiTag.Melee, 0, 2.7f, 1.8f, 1, DrownedSwipe);
                    Add("pool", AiTag.Zone, 2.7f, 8, 5, .9f, d => CallTide(false));
                    break;
                case EnemyType.EelSpitter:
                    Stance(SquadRole.Backline, 6f, .8f);
                    Add("spit", AiTag.Aimed, 2.5f, 11, 6, 1, EelSpit);
                    Add("lob", AiTag.Zone, 2.5f, 9, 6, .9f, d => EelLob());
                    Add("dive", AiTag.Retreat, 0, 3, 1.5f, 1.6f, d => EelDive());
                    break;
                case EnemyType.CoralGuard:
                    Stance(SquadRole.Frontline, 2.3f, .85f);
                    Add("smash", AiTag.Melee, 0, 3.2f, 2, 1, CoralSmash);
                    Add("spines", AiTag.Zone, 0, 7, 3.5f, .85f, d => CoralSpines());
                    break;
                case EnemyType.JellyLamp:
                    Stance(SquadRole.Skirmisher, 2.6f, .9f);
                    Add("pulse", AiTag.Zone, 0, 3.4f, 2, 1.1f, d => JellyPulse());
                    Add("bolts", AiTag.Aimed, 3, 9, 5, .9f, d => JellyBolts());
                    break;
                case EnemyType.TideSiren:
                    Stance(SquadRole.Support, 7f, .85f);
                    Add("mend", AiTag.Support, 0, Any, 7, 1.6f, d => SirenMend(), HasWoundedAlly);
                    Add("hymn", AiTag.Support, 0, Any, 7, .9f, d => SirenHymn(), HasAlly);
                    Add("wave", AiTag.Aimed, 0, 10, 6, .8f, SirenWave);
                    break;
                // ---- Stage 3
                case EnemyType.InkShade:
                    Stance(SquadRole.Flanker, 3f);
                    Add("ambush", AiTag.Melee, 2, 10, 5, 1.1f, d => ShadeAmbush());
                    Add("cut", AiTag.Melee, 0, 3, 1.8f, 1, ShadeCut);
                    break;
                case EnemyType.SealGolem:
                    Stance(SquadRole.Frontline, 2.6f, .9f);
                    Add("slam", AiTag.Melee, 0, 3.6f, 2.2f, 1, d => GolemSlam());
                    Add("quake", AiTag.Zone, 3, 9, 5.5f, 1, GolemQuake);
                    break;
                case EnemyType.GildedBolter:
                    Stance(SquadRole.Backline, 7f, .85f);
                    Add("hop", AiTag.Retreat, 0, 3, 1.5f, 1.7f, ShooterHop);
                    Add("snipe", AiTag.Aimed, 3, 13, 8, 1, BolterSnipe);
                    Add("triple", AiTag.Aimed, 3, 10, 6, .9f, BolterTriple);
                    break;
                case EnemyType.ChainWarden:
                    Stance(SquadRole.Skirmisher, 4.4f, .85f);
                    Add("hook", AiTag.Gapcloser, 2.5f, 7.5f, 5, 1.1f, WardenHook);
                    Add("sweep", AiTag.Melee, 0, 3.2f, 2, 1, ChainSweep);
                    break;
                case EnemyType.SealPriest:
                    Stance(SquadRole.Support, 7f, .85f);
                    Add("ward", AiTag.Support, 0, Any, 7, 1.5f, d => PriestWard(), HasAlly);
                    Add("mend", AiTag.Support, 0, Any, 7, 1.3f, d => PriestMend(), HasWoundedAlly);
                    Add("curses", AiTag.Zone, 3, 11, 7, .85f, d => HexerCurses());
                    break;
            }
        }

        bool HasAlly()
        {
            foreach (var other in director.Members)
                if (other != null && other != this && !other.IsDead)
                    return true;
            return false;
        }
        bool HasWoundedAlly() => MostWoundedAlly() != null;
        EnemyController MostWoundedAlly()
        {
            EnemyController worst = null;
            var lowest = .75f;
            foreach (var other in director.Members)
                if (other != null && other != this && !other.IsDead && other.Health / other.MaxHealth < lowest)
                {
                    worst = other;
                    lowest = other.Health / other.MaxHealth;
                }
            return worst;
        }
        public void ReceiveHealing(float share)
        {
            if (dead)
                return;
            Health = Mathf.Min(MaxHealth, Health + MaxHealth * share);
            UpdateHealthBar();
            BossNativeEffect.CreateOverActors(this, "ECastWater", (Vector2)transform.position + Vector2.up * .4f, .5f);
        }
        public void ReceiveWard(float seconds)
        {
            if (dead)
                return;
            wardUntil = Time.time + seconds;
            BossNativeEffect.CreateOverActors(this, "ECastSeal", (Vector2)transform.position + Vector2.up * .4f, .6f);
        }
        // Damage only comes through a shield from the side or behind; a blocked hit rings instead of landing.
        float StageMobDamage(float amount)
        {
            if (Time.time < wardUntil)
                amount *= WardReduction;
            if (Type == EnemyType.BoneShield && !acting && player != null)
            {
                var toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
                if (toPlayer.x * facingDirection > 0f && Mathf.Abs(toPlayer.x) > Mathf.Abs(toPlayer.y) * .6f)
                {
                    amount *= ShieldReduction;
                    Fx("EFlashRune", (Vector2)transform.position + Vector2.right * facingDirection * .4f + Vector2.up * .3f, .18f);
                }
            }
            return amount;
        }

        // ---- Bone Shield: a wall that has to be baited into swinging or walked around.
        IEnumerator ShieldBash(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var angle = AngleOf(direction);
            Sector(transform.position, 2.4f, angle, 80f, .5f, .14f);
            yield return new WaitForSeconds(.5f);
            AttackVisualPhase = 1;
            Fx("EChargerTusk", transform.position, 0f, angle);
            yield return Rush(direction, .9f, .12f);
            yield return EndMove(.55f, 1.3f);
        }
        IEnumerator ShieldAdvance(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var travel = GetSafeTravelDistance(direction, 3.2f);
            ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, .55f, .3f, DangerTint, ContactRadius);
            yield return new WaitForSeconds(.55f);
            collisionAttackActive = true;
            AttackVisualPhase = 1;
            yield return Rush(direction, travel, .3f);
            collisionAttackActive = false;
            yield return EndMove(.7f, 1.5f);
        }

        // ---- Vine Lasher: long straight reach, and roots that close the path around the player.
        IEnumerator VineLash(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var angle = AngleOf(direction);
            var center = (Vector2)transform.position + direction * 2.6f;
            Slash(center, new Vector2(5.2f, .9f), angle, .62f, .16f);
            yield return new WaitForSeconds(.62f);
            AttackVisualPhase = 1;
            CutAlong("ESentinelCut", center, 5.2f, angle, .24f);
            yield return EndMove(.55f, 1.5f);
        }
        IEnumerator VineSnare(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            AttackVisualPhase = 1;
            var side = new Vector2(-direction.y, direction.x);
            for (var i = -1; i <= 1; i++)
            {
                var point = PlayerPoint + side * i * 1.7f;
                Circle(point, .95f, .8f, .16f);
                StartCoroutine(HexLanding(point, .8f, 1.9f));
                yield return new WaitForSeconds(.12f);
            }
            yield return EndMove(.7f, 1.9f);
        }

        // ---- Ruin Rat: weak, quick, comes from behind.
        IEnumerator RatDart(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var travel = GetSafeTravelDistance(direction, 3.4f);
            ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, .3f, .16f, DangerTint, ContactRadius);
            yield return new WaitForSeconds(.3f);
            collisionAttackActive = true;
            AttackVisualPhase = 1;
            yield return Rush(direction, travel, .16f);
            collisionAttackActive = false;
            yield return Rush(-direction, 1.6f, .16f);
            yield return EndMove(.35f, 1.1f);
        }
        IEnumerator RatDoubleDart(Vector2 direction)
        {
            for (var dart = 0; dart < 2; dart++)
            {
                direction = ToPlayer;
                BeginMove(direction, Color.white);
                var travel = GetSafeTravelDistance(direction, 3.6f);
                ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, .34f, .16f, DangerTint, ContactRadius);
                yield return new WaitForSeconds(.34f);
                collisionAttackActive = true;
                AttackVisualPhase = 1;
                yield return Rush(direction, travel, .16f);
                collisionAttackActive = false;
                yield return new WaitForSeconds(.12f);
            }
            yield return EndMove(.6f, 1.5f);
        }

        // ---- Moss Archer: one slow, hard shot; the warning line is the whole fight.
        IEnumerator ArcherAimedShot(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            AimLine(ShotOrigin, direction, .7f);
            yield return new WaitForSeconds(.7f);
            AttackVisualPhase = 1;
            Shoot(ShotOrigin, direction, 12f, contactDamage * 1.5f, .24f);
            Muzzle(direction);
            GameAudio.Play(GameSound.Shoot, AudioChannel.Monster, .4f);
            yield return EndMove(.5f, 1.7f);
        }
        IEnumerator ArcherVolley()
        {
            var target = PlayerPoint;
            BeginMove(ToPlayer, Color.white);
            AttackVisualPhase = 1;
            for (var arrow = 0; arrow < 3; arrow++)
            {
                var point = target + Random.insideUnitCircle * 1.5f;
                Circle(point, .85f, .7f, .14f);
                StartCoroutine(DelayedFx("EShotStoneHit", point, .7f, .24f));
                yield return new WaitForSeconds(.14f);
            }
            yield return EndMove(.7f, 2f);
        }

        // ---- Powder Beetle: leaves the floor unsafe behind it.
        void PlantBomb(Vector2 point)
        {
            Circle(point, 1.45f, 1.1f, .16f, 1.2f);
            Fx("ECastEmber", point + Vector2.up * .2f, 1.1f);
            StartCoroutine(BombBlast(point));
        }
        IEnumerator BombBlast(Vector2 point)
        {
            yield return new WaitForSeconds(1.1f);
            FloorStrike("ERingStone", point, 2.9f, .3f);
            Strike("EBurstSpore", point, 2.9f, .4f);
            if (cameraFollow != null)
                cameraFollow.Shake(.12f);
        }
        IEnumerator BeetleBomb(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            yield return new WaitForSeconds(.26f);
            AttackVisualPhase = 1;
            PlantBomb(transform.position);
            yield return Rush(-direction, 2.8f, .3f);
            yield return EndMove(.3f, 1.8f);
        }
        IEnumerator BeetleRun(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var side = new Vector2(-direction.y, direction.x) * (Random.value < .5f ? 1f : -1f);
            var path = (direction + side * .7f).normalized;
            yield return new WaitForSeconds(.3f);
            AttackVisualPhase = 1;
            PlantBomb(transform.position);
            yield return Rush(path, 2.4f, .24f);
            PlantBomb(transform.position);
            yield return Rush(path, 2.4f, .24f);
            yield return EndMove(.4f, 2.1f);
        }

        // ---- Drowned: slow and durable, makes the floor wet around the fight.
        IEnumerator DrownedSwipe(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var angle = AngleOf(direction);
            Sector(transform.position, 2.5f, angle, 120f, .6f, .16f);
            yield return new WaitForSeconds(.6f);
            AttackVisualPhase = 1;
            Fx("ECrabSweep", transform.position, 0f, angle);
            yield return EndMove(.7f, 1.6f);
        }

        // ---- Eel Spitter: sinks away from anyone who closes in.
        IEnumerator EelSpit(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            foreach (var angle in new[] { -14f, 0f, 14f })
                AimLine(ShotOrigin, Rotate(direction, angle), .55f);
            yield return new WaitForSeconds(.55f);
            AttackVisualPhase = 1;
            foreach (var angle in new[] { -14f, 0f, 14f })
                Shoot(ShotOrigin, Rotate(direction, angle), 8f, contactDamage);
            Muzzle(direction);
            yield return EndMove(.55f, 1.6f);
        }
        IEnumerator EelLob()
        {
            BeginMove(ToPlayer, Color.white);
            var target = PlayerPoint;
            Circle(target, 1.2f, .85f, .14f);
            yield return new WaitForSeconds(.3f);
            AttackVisualPhase = 1;
            EnemyLobbedRock.Throw(this, (Vector2)transform.position + Vector2.up * .5f, target, .55f, 1.8f);
            yield return new WaitForSeconds(.55f);
            FloorStrike("ERingMid", target, 2.4f, .3f);
            yield return EndMove(.4f, 1.8f);
        }
        IEnumerator EelDive()
        {
            BeginMove(ToPlayer, new Color(1f, 1f, 1f, .6f));
            Fx("EFlashWater", transform.position, .3f);
            yield return new WaitForSeconds(.3f);
            var away = -ToPlayer;
            MoveSafely((Vector2)transform.position + Rotate(away, Random.Range(-50f, 50f)) * 5f);
            yield return new WaitForFixedUpdate();
            SetPresentationTint(Color.white);
            Fx("EFlashWater", transform.position, .3f);
            yield return EndMove(.25f, .9f);
        }

        // ---- Coral Guard: a heavy that punishes standing next to it.
        IEnumerator CoralSmash(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var point = (Vector2)transform.position + direction * 1.6f;
            Circle(point, 1.5f, .7f, .16f, 1.2f);
            yield return new WaitForSeconds(.7f);
            AttackVisualPhase = 1;
            GroundSlam(point, 1.5f);
            if (cameraFollow != null)
                cameraFollow.Shake(.14f);
            yield return EndMove(.9f, 1.6f);
        }
        IEnumerator CoralSpines()
        {
            BeginMove(ToPlayer, Color.white);
            Cast(.5f);
            yield return new WaitForSeconds(.5f);
            AttackVisualPhase = 1;
            for (var spine = 0; spine < 8; spine++)
            {
                var angle = spine * 45f * Mathf.Deg2Rad;
                Shoot(ShotOrigin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 6f, contactDamage);
            }
            yield return EndMove(.7f, 2.1f);
        }

        // ---- Jelly Lamp: drifts in and turns the air around itself dangerous.
        IEnumerator JellyPulse()
        {
            BeginMove(ToPlayer, Color.white);
            Circle(transform.position, 2.3f, .8f, .18f);
            Fx("ECastWater", (Vector2)transform.position + Vector2.up * .3f, .8f);
            yield return new WaitForSeconds(.8f);
            AttackVisualPhase = 1;
            FloorStrike("ERingMid", transform.position, 4.6f, .3f);
            yield return EndMove(.5f, 1.8f);
        }
        IEnumerator JellyBolts()
        {
            BeginMove(ToPlayer, Color.white);
            AttackVisualPhase = 1;
            for (var bolt = 0; bolt < 3; bolt++)
            {
                Shoot(ShotOrigin, ToPlayer, 7f, contactDamage);
                yield return new WaitForSeconds(.2f);
            }
            yield return EndMove(.5f, 1.6f);
        }

        // ---- Tide Siren: kill it first, or the fight never ends.
        IEnumerator SirenMend()
        {
            BeginMove(ToPlayer, Color.white);
            Cast(.8f);
            yield return new WaitForSeconds(.8f);
            AttackVisualPhase = 1;
            var patient = MostWoundedAlly();
            if (patient != null)
                patient.ReceiveHealing(HealShare);
            yield return EndMove(.5f, 2.6f);
        }
        IEnumerator SirenHymn()
        {
            BeginMove(ToPlayer, Color.white);
            Cast(.7f);
            yield return new WaitForSeconds(.7f);
            AttackVisualPhase = 1;
            foreach (var other in director.Members)
                if (other != null && other != this && !other.IsDead)
                    other.specialTimer = Mathf.Max(0f, other.specialTimer - .8f);
            yield return EndMove(.5f, 3.2f);
        }
        IEnumerator SirenWave(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            foreach (var angle in new[] { -20f, 0f, 20f })
                AimLine(ShotOrigin, Rotate(direction, angle), .6f);
            yield return new WaitForSeconds(.6f);
            AttackVisualPhase = 1;
            foreach (var angle in new[] { -20f, 0f, 20f })
                Shoot(ShotOrigin, Rotate(direction, angle), 6f, contactDamage);
            yield return EndMove(.6f, 1.9f);
        }

        // ---- Ink Shade: vanishes and comes out behind the weapon.
        IEnumerator ShadeAmbush()
        {
            BeginMove(ToPlayer, new Color(1f, 1f, 1f, .6f));
            Fx("EFlashHex", transform.position, .3f);
            yield return new WaitForSeconds(.32f);
            var aim = player.AimDirection.sqrMagnitude > .01f ? player.AimDirection.normalized : ToPlayer;
            MoveSafely(PlayerPoint - aim * 2.2f);
            yield return new WaitForFixedUpdate();
            SetPresentationTint(Color.white);
            Fx("EFlashHex", transform.position, .3f);
            var direction = ToPlayer;
            var angle = AngleOf(direction);
            var center = (Vector2)transform.position + direction * 1.6f;
            Slash(center, new Vector2(3.2f, 1.2f), angle, .42f, .14f);
            yield return new WaitForSeconds(.42f);
            AttackVisualPhase = 1;
            CutAlong("EKnightCut", center, 3.2f, angle, .22f);
            yield return EndMove(.6f, 1.8f);
        }
        IEnumerator ShadeCut(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var angle = AngleOf(direction);
            var center = (Vector2)transform.position + direction * 1.4f;
            Slash(center, new Vector2(2.8f, 1.2f), angle, .36f, .14f);
            yield return new WaitForSeconds(.36f);
            AttackVisualPhase = 1;
            CutAlong("EKnightCut", center, 2.8f, angle, .2f);
            yield return Rush(-direction, 2f, .18f);
            yield return EndMove(.35f, 1.3f);
        }

        // ---- Seal Golem: doesn't flinch; every swing can be read from across the room.
        IEnumerator GolemSlam()
        {
            BeginMove(ToPlayer, Color.white);
            Circle(transform.position, 2.8f, .95f, .18f, 1.2f);
            yield return new WaitForSeconds(.95f);
            AttackVisualPhase = 1;
            GroundSlam(transform.position, 2.8f);
            if (cameraFollow != null)
                cameraFollow.Shake(.2f);
            yield return EndMove(1f, 1.7f);
        }
        IEnumerator GolemQuake(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            yield return new WaitForSeconds(.35f);
            AttackVisualPhase = 1;
            for (var step = 1; step <= 4; step++)
            {
                var point = (Vector2)transform.position + direction * (1.1f + step * 1.25f);
                if (Physics2D.OverlapPoint(point, 1 << GameLayers.Ground) != null)
                    break;
                Circle(point, .95f, .55f, .14f);
                StartCoroutine(DelayedFx("EBurstStone", point, .55f, .3f));
                yield return new WaitForSeconds(.16f);
            }
            yield return EndMove(.9f, 2f);
        }

        // ---- Gilded Bolter: the same warning as any shot, but it barely gives you time.
        IEnumerator BolterSnipe(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            AimLine(ShotOrigin, direction, .62f);
            yield return new WaitForSeconds(.62f);
            AttackVisualPhase = 1;
            Shoot(ShotOrigin, direction, 14f, contactDamage * 1.5f, .24f);
            Muzzle(direction);
            GameAudio.Play(GameSound.Shoot, AudioChannel.Monster, .45f);
            yield return EndMove(.6f, 1.9f);
        }
        IEnumerator BolterTriple(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            AimLine(ShotOrigin, direction, .5f);
            yield return new WaitForSeconds(.5f);
            AttackVisualPhase = 1;
            for (var bolt = 0; bolt < 3; bolt++)
            {
                Shoot(ShotOrigin, direction, 10f, contactDamage);
                Muzzle(direction);
                yield return new WaitForSeconds(.14f);
            }
            yield return EndMove(.6f, 1.8f);
        }

        // ---- Chain Warden: no safe distance from it, its hook drags you in.
        IEnumerator WardenHook(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var angle = AngleOf(direction);
            var center = (Vector2)transform.position + direction * 3.8f;
            Slash(center, new Vector2(7.6f, .7f), angle, .7f, .16f);
            yield return new WaitForSeconds(.7f);
            AttackVisualPhase = 1;
            CutAlong("ETotemBeam", center, 7.6f, angle, .2f);
            var start = (Vector2)transform.position;
            var along = PlayerPoint - start;
            var reach = Mathf.Clamp(Vector2.Dot(along, direction), 0f, 7.6f);
            var nearest = start + direction * reach;
            if (Vector2.Distance(PlayerPoint, nearest) < .65f && !player.IsDashing)
                player.ApplyPush(-direction * Mathf.Min(16f, reach * 3.4f));
            yield return EndMove(.75f, 2f);
        }
        IEnumerator ChainSweep(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            var angle = AngleOf(direction);
            Sector(transform.position, 3.1f, angle, 150f, .6f, .16f);
            yield return new WaitForSeconds(.6f);
            AttackVisualPhase = 1;
            Fx("EWardenSwipe", transform.position, 0f, angle);
            yield return EndMove(.7f, 1.5f);
        }

        // ---- Seal Priest: its allies are harder to hurt while it stands.
        IEnumerator PriestWard()
        {
            BeginMove(ToPlayer, Color.white);
            Cast(.8f);
            yield return new WaitForSeconds(.8f);
            AttackVisualPhase = 1;
            foreach (var other in director.Members)
                if (other != null && other != this && !other.IsDead)
                    other.ReceiveWard(4f);
            yield return EndMove(.5f, 3.6f);
        }
        IEnumerator PriestMend()
        {
            BeginMove(ToPlayer, Color.white);
            Cast(.8f);
            yield return new WaitForSeconds(.8f);
            AttackVisualPhase = 1;
            var patient = MostWoundedAlly();
            if (patient != null)
                patient.ReceiveHealing(HealShare);
            yield return EndMove(.5f, 2.8f);
        }
    }
}
