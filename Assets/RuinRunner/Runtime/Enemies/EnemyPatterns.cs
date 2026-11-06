using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Regular enemy move sets. Every damaging move shows its real shape on the floor before it lands.</summary>
    public sealed partial class EnemyController
    {
        private bool Enraged => Health < MaxHealth * .5f;
        private Vector2 PlayerPoint => player.transform.position;
        private Vector2 ToPlayer
        {
            get
            {
                var delta = PlayerPoint - (Vector2)transform.position;
                return delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;
            }
        }
        private static float AngleOf(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        private void BeginMove(Vector2 direction, Color tint)
        {
            SetFacing(direction.x);
            acting = true;
            AttackVisualPhase = 0;
            SetPlanar(Vector2.zero, true);
            SetPresentationTint(tint);
        }
        private IEnumerator EndMove(float recovery, float cooldown)
        {
            collisionAttackActive = false;
            if (director != null) cooldown *= director.CooldownScale;
            // A move that lands and ends on one frame would skip its contact drawings.
            if (AttackVisualPhase == 1)
            {
                var hold = Mathf.Min(StrikeHoldSeconds - (Time.time - strikeShownAt), recovery * .6f);
                if (hold > 0f)
                {
                    SetPlanar(Vector2.zero, true);
                    yield return new WaitForSeconds(hold);
                    recovery -= hold;
                }
            }
            AttackVisualPhase = 2;
            SetPlanar(Vector2.zero, true);
            yield return new WaitForSeconds(recovery);
            EndAction();
            specialTimer = cooldown;
        }
        private BossNativeEffect Fx(string clip, Vector2 point, float duration, float angle = 0) => BossNativeEffect.CreateOverActors(this, clip, point, duration, angle);
        // Sized to the hit area, so what lands is the shape that was warned.
        private void Strike(string clip, Vector2 point, float diameter, float duration, float angle = 0) => Fx(clip, point, duration, angle).Fit(diameter);
        private void FloorStrike(string clip, Vector2 point, float diameter, float duration) => BossNativeEffect.CreateOnFloor(clip, point, duration).Fit(diameter);
        private void GroundSlam(Vector2 point, float radius)
        {
            FloorStrike(radius < 1.4f ? "ERingSmall" : radius < 2.6f ? "ERingMid" : "ERingStone", point, radius * 2f, .34f);
            // On the floor, so the dust never hides the body that landed or the hero standing by it.
            FloorStrike("EBurstStone", point, radius * 2f, 0f);
        }
        // A straight cut drawn at the length of its real hit rectangle, from where it starts to where it ends.
        private void CutAlong(string clip, Vector2 center, float length, float angle, float duration)
        {
            var along = Rotate(Vector2.right, angle);
            Fx(clip, center - along * length * .5f, duration, angle);
        }
        // Just above the body, unless that is inside the wall it stands against: a shot born in a wall dies on its first step.
        private Vector2 ShotOrigin
        {
            get
            {
                var raised = (Vector2)transform.position + Vector2.up * .15f;
                return Physics2D.OverlapPoint(raised, 1 << GameLayers.Ground) == null ? raised : enemyCollider.bounds.center;
            }
        }
        private void Shoot(Vector2 from, Vector2 direction, float speed, float damage, float size = .22f) =>
            EnemyProjectile.Create(from, direction, speed, damage, normalColor, size, clip: EnemyAttackLook.Shot(Type));
        private void Muzzle(Vector2 direction)
        {
            var clip = EnemyAttackLook.Flash(Type);
            if (clip != null) Fx(clip, ShotOrigin + direction * .35f, .2f, AngleOf(direction));
        }
        private void Cast(float duration)
        {
            var clip = EnemyAttackLook.Cast(Type);
            if (clip != null) Fx(clip, (Vector2)transform.position + Vector2.up * .35f, duration);
        }
        private ArenaHazard Circle(Vector2 point, float radius, float warning, float active, float damageScale = 1) =>
            ArenaHazard.Create(this, player, point, Vector2.one * (radius * 2), 0, true, warning, active, contactDamage * damageScale, DangerTint);
        private ArenaHazard Sector(Vector2 point, float radius, float angle, float arc, float warning, float active) =>
            ArenaHazard.Create(this, player, point, Vector2.one * (radius * 2), angle, true, warning, active, contactDamage, DangerTint, arcDegrees: arc);
        private ArenaHazard Slash(Vector2 center, Vector2 size, float angle, float warning, float active, float damageScale = 1) =>
            ArenaHazard.Create(this, player, center, size, angle, false, warning, active, contactDamage * damageScale, DangerTint);
        // Zero damage: a thin line along the path a shot is really going to fly, up to the wall that stops it.
        private void AimLine(Vector2 from, Vector2 direction, float warning)
        {
            direction = direction.normalized;
            var wall = Physics2D.Raycast(from, direction, MaxAimLine, 1 << GameLayers.Ground);
            var length = wall.collider == null ? MaxAimLine : Mathf.Max(.5f, wall.distance);
            ArenaHazard.Create(this, player, from + direction * length * .5f, new Vector2(length, .16f), AngleOf(direction), false, warning, .02f, 0, DangerTint);
        }

        private IEnumerator GruntLunge(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .9f, .4f));
            var travel = GetSafeTravelDistance(direction, 2.2f);
            ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, .42f, .2f, DangerTint, ContactRadius);
            yield return new WaitForSeconds(.42f);
            collisionAttackActive = true;
            AttackVisualPhase = 1;
            yield return Rush(direction, 2.2f, .2f);
            collisionAttackActive = false;
            Fx("EBurstStone", transform.position, 0f);
            yield return EndMove(.5f, 1.25f);
        }
        private IEnumerator GruntRockToss()
        {
            BeginMove(ToPlayer, Color.white);
            var target = PlayerPoint;
            Circle(target, 1.05f, .85f, .12f);
            yield return new WaitForSeconds(.3f);
            AttackVisualPhase = 1;
            // The rock leaves the hand here and lands on the circle exactly when it hurts.
            EnemyLobbedRock.Throw(this, (Vector2)transform.position + Vector2.up * .5f, target, .55f, 1.5f);
            yield return new WaitForSeconds(.55f);
            GroundSlam(target, 1.05f);
            yield return EndMove(.35f, 1.7f);
        }

        private IEnumerator ShooterHop(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            yield return new WaitForSeconds(.14f);
            AttackVisualPhase = 1;
            yield return Rush(-direction, 2.6f, .24f);
            yield return EndMove(.12f, .55f);
        }
        private IEnumerator ShooterBurst(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .85f, .55f));
            AimLine(ShotOrigin, direction, .5f);
            yield return new WaitForSeconds(.5f);
            AttackVisualPhase = 1;
            var shots = Enraged ? 4 : 3;
            for (var shot = 0; shot < shots; shot++)
            {
                Shoot(ShotOrigin, direction, 8.4f, contactDamage);
                Muzzle(direction);
                PrototypeAudio.Play(PrototypeSound.Shoot, AudioChannel.Monster, .32f);
                yield return new WaitForSeconds(.12f);
            }
            yield return EndMove(.42f, Enraged ? 1f : 1.4f);
        }
        private IEnumerator ShooterFan(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .7f, .4f));
            foreach (var angle in new[] { -32f, -16f, 0f, 16f, 32f }) AimLine(ShotOrigin, Rotate(direction, angle), .62f);
            yield return new WaitForSeconds(.62f);
            AttackVisualPhase = 1;
            foreach (var angle in new[] { -32f, -16f, 0f, 16f, 32f })
                Shoot(ShotOrigin, Rotate(direction, angle), 6.4f, contactDamage);
            Muzzle(direction);
            PrototypeAudio.Play(PrototypeSound.Shoot, AudioChannel.Monster, .5f);
            yield return EndMove(.55f, 1.6f);
        }

        private IEnumerator ChargerRam(Vector2 direction, int rams)
        {
            for (var ram = 0; ram < rams; ram++)
            {
                direction = ToPlayer;
                var windup = ram == 0 ? .68f : .42f;
                BeginMove(direction, new Color(1f, .75f, .25f));
                var travel = GetSafeTravelDistance(direction, 6.4f);
                ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, windup, .36f, DangerTint, ContactRadius);
                Fx("ERushDust", transform.position, windup);
                yield return new WaitForSeconds(windup);
                collisionAttackActive = true;
                AttackVisualPhase = 1;
                yield return Rush(direction, travel, .36f);
                collisionAttackActive = false;
                GroundSlam(transform.position, 1f);
                if (cameraFollow != null) cameraFollow.Shake(.14f);
                if (ram + 1 < rams) yield return new WaitForSeconds(.16f);
            }
            // Spent: the safe window to punish a heavy enemy.
            yield return EndMove(1f, 1.5f);
        }
        private IEnumerator ChargerTusk(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .8f, .35f));
            var angle = AngleOf(direction);
            // A short gore straight ahead with the head down, not a sweep.
            Sector(transform.position, 2.2f, angle, 70f, .5f, .14f);
            yield return new WaitForSeconds(.5f);
            AttackVisualPhase = 1;
            Fx("EChargerTusk", transform.position, 0f, angle);
            yield return Rush(direction, .7f, .1f);
            yield return EndMove(.6f, 1.2f);
        }

        private IEnumerator LeaperHops()
        {
            var hops = Enraged ? 4 : 3;
            for (var hop = 0; hop < hops; hop++)
            {
                var direction = ToPlayer;
                BeginMove(direction, new Color(1f, .92f, .82f));
                var travel = GetSafeTravelDistance(direction, Mathf.Min(2.3f, Vector2.Distance(transform.position, PlayerPoint)));
                var start = body.position;
                var target = start + direction * travel;
                Circle(target, 1.2f, .34f, .1f, .8f);
                yield return new WaitForSeconds(.14f);
                AttackVisualPhase = 1;
                var elapsed = 0f;
                while (elapsed < .2f)
                {
                    elapsed += Time.deltaTime;
                    MoveSafely(Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / .2f)));
                    yield return new WaitForFixedUpdate();
                }
                GroundSlam(target, 1.2f);
                yield return new WaitForSeconds(.1f);
            }
            yield return EndMove(.65f, 1.6f);
        }

        private IEnumerator TideRipple(Vector2 direction)
        {
            BeginMove(direction, new Color(.75f, 1f, 1f));
            Cast(.55f);
            yield return new WaitForSeconds(.55f);
            AttackVisualPhase = 1;
            for (var wave = 0; wave < 3; wave++)
            {
                var aim = ToPlayer;
                foreach (var angle in new[] { -24f, 0f, 24f })
                    Shoot(ShotOrigin, Rotate(aim, angle + wave * 8f), 5.8f, contactDamage);
                Muzzle(aim);
                CombatAudio.Native("hazard-water", AudioChannel.Monster, .18f);
                yield return new WaitForSeconds(.22f);
            }
            yield return EndMove(.6f, 1.5f);
        }

        private IEnumerator SentinelLunge(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .85f, .5f));
            var travel = GetSafeTravelDistance(direction, 4.6f);
            ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, .5f, .24f, DangerTint, ContactRadius);
            yield return new WaitForSeconds(.5f);
            collisionAttackActive = true;
            AttackVisualPhase = 1;
            yield return Rush(direction, travel, .24f);
            collisionAttackActive = false;
            Fx("FxHitSpark", transform.position, .3f, AngleOf(direction));
            yield return EndMove(.5f, 1f);
        }
        private IEnumerator SentinelCross(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .85f, .5f));
            var center = (Vector2)transform.position + direction * 1.5f;
            var angle = AngleOf(direction);
            Slash(center, new Vector2(4.2f, 1f), angle + 45f, .6f, .16f);
            Slash(center, new Vector2(4.2f, 1f), angle - 45f, .78f, .16f);
            yield return new WaitForSeconds(.6f);
            AttackVisualPhase = 1;
            CutAlong("ESentinelCross", center, 4.2f, angle + 45f, .24f);
            yield return new WaitForSeconds(.18f);
            CutAlong("ESentinelCross", center, 4.2f, angle - 45f, .24f);
            yield return EndMove(.85f, 1.2f);
        }

        private IEnumerator SporelingBurst()
        {
            BeginMove(ToPlayer, new Color(1f, .55f, .55f));
            const float Fuse = .75f;
            Circle(transform.position, 1.9f, Fuse, .12f);
            yield return new WaitForSeconds(Fuse);
            AttackVisualPhase = 1;
            BossNativeEffect.CreateOnFloor("ERingSpore", transform.position, .34f).Fit(3.8f);
            BossNativeEffect.CreateOverActors(null, "EBurstSpore", transform.position, .6f).Fit(3.8f);
            if (cameraFollow != null) cameraFollow.Shake(.12f);
            yield return new WaitForSeconds(.14f);
            Health = 0;
            Die();
        }

        private IEnumerator TotemSpiral()
        {
            BeginMove(ToPlayer, new Color(1f, .9f, .55f));
            Cast(.55f);
            yield return new WaitForSeconds(.55f);
            AttackVisualPhase = 1;
            var spin = Random.Range(0f, 90f);
            for (var step = 0; step < (Enraged ? 11 : 8); step++)
            {
                for (var arm = 0; arm < 4; arm++)
                {
                    var angle = (spin + arm * 90f + step * 13f) * Mathf.Deg2Rad;
                    Shoot(ShotOrigin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 4.4f, contactDamage);
                }
                PrototypeAudio.Play(PrototypeSound.Shoot, AudioChannel.Monster, .16f);
                yield return new WaitForSeconds(.17f);
            }
            yield return EndMove(.5f, 1.8f);
        }
        private IEnumerator TotemBeams()
        {
            BeginMove(ToPlayer, new Color(1f, .9f, .55f));
            var angle = AngleOf(ToPlayer);
            foreach (var turn in new[] { 0f, 90f })
                Slash(transform.position, new Vector2(18f, .9f), angle + turn, .95f, .22f);
            yield return new WaitForSeconds(.95f);
            AttackVisualPhase = 1;
            foreach (var turn in new[] { 0f, 90f })
                CutAlong("ETotemBeam", transform.position, 18f, angle + turn, .3f);
            if (cameraFollow != null) cameraFollow.Shake(.1f);
            yield return EndMove(.6f, 1.9f);
        }

        private IEnumerator CrabSweep(Vector2 direction)
        {
            BeginMove(direction, new Color(.8f, 1f, 1f));
            var angle = AngleOf(direction);
            Sector(transform.position, 2.9f, angle, 200f, .55f, .16f);
            yield return new WaitForSeconds(.55f);
            AttackVisualPhase = 1;
            Fx("ECrabSweep", transform.position, .28f, angle);
            Fx("FxWaterSplash", (Vector2)transform.position + direction * 1.6f, .45f);
            yield return EndMove(.75f, 1.35f);
        }
        private IEnumerator CrabBubbles(Vector2 direction)
        {
            BeginMove(direction, new Color(.8f, 1f, 1f));
            Cast(.48f);
            yield return new WaitForSeconds(.48f);
            AttackVisualPhase = 1;
            for (var bubble = -3; bubble <= 3; bubble++)
                Shoot(ShotOrigin, Rotate(direction, bubble * 13f), 3.3f + Mathf.Abs(bubble) * .3f, contactDamage, .3f);
            Muzzle(direction);
            CombatAudio.Native("hazard-water", AudioChannel.Monster, .2f);
            yield return EndMove(.5f, 1.8f);
        }

        private IEnumerator WispBlink()
        {
            BeginMove(ToPlayer, new Color(1f, 1f, 1f, .75f));
            Fx("FxBlinkOut", transform.position, .42f);
            yield return new WaitForSeconds(.42f);
            var side = Rotate(ToPlayer, Random.value < .5f ? 110f : -110f);
            MoveSafely(PlayerPoint + side * 3.6f);
            // The body only arrives on the physics step; everything below is placed from its new position.
            yield return new WaitForFixedUpdate();
            SetPresentationTint(new Color(1f, 1f, 1f, .5f));
            Fx("FxBlinkIn", transform.position, .4f);
            var aim = ToPlayer;
            foreach (var angle in new[] { -18f, 0f, 18f }) AimLine(ShotOrigin, Rotate(aim, angle), .46f);
            yield return new WaitForSeconds(.46f);
            AttackVisualPhase = 1;
            SetPresentationTint(Color.white);
            foreach (var angle in new[] { -18f, 0f, 18f })
                Shoot(ShotOrigin, Rotate(aim, angle), 6.8f, contactDamage);
            Muzzle(aim);
            yield return EndMove(.7f, 1.9f);
        }

        private IEnumerator KnightCombo()
        {
            var windups = new[] { .62f, .36f, .72f };
            for (var hit = 0; hit < 3; hit++)
            {
                var direction = ToPlayer;
                var angle = AngleOf(direction);
                BeginMove(direction, new Color(1f, .85f, .6f));
                var heavy = hit == 2;
                var size = heavy ? new Vector2(5f, 2f) : new Vector2(3.8f, 1.2f);
                var center = (Vector2)transform.position + direction * size.x * .5f;
                Slash(center, size, angle, windups[hit], .16f, heavy ? 1.4f : 1f);
                yield return new WaitForSeconds(windups[hit]);
                AttackVisualPhase = 1;
                CutAlong(heavy ? "EKnightHeavy" : "EKnightCut", center, size.x, angle, heavy ? .3f : .24f);
                if (heavy) BossNativeEffect.CreateOnFloor("EBurstStone", (Vector2)transform.position + direction * size.x, 0f);
                if (heavy) HitFreeze(.1f);
                if (heavy && cameraFollow != null) cameraFollow.Shake(.2f);
                yield return Rush(direction, heavy ? .3f : .7f, .14f);
            }
            // The long recovery is what makes a three-hit combo fair.
            yield return EndMove(1.25f, 1.1f);
        }
        private IEnumerator KnightSpikes(Vector2 direction)
        {
            BeginMove(direction, new Color(1f, .85f, .6f));
            for (var spike = 1; spike <= 5; spike++)
            {
                var point = (Vector2)transform.position + direction * (spike * 1.5f);
                Circle(point, .85f, .55f + spike * .11f, .16f);
                StartCoroutine(DelayedFx("WardenSpike", point, .55f + spike * .11f, 1.05f));
            }
            yield return new WaitForSeconds(.55f);
            AttackVisualPhase = 1;
            yield return new WaitForSeconds(.5f);
            yield return EndMove(.55f, 1.5f);
        }
        private IEnumerator DelayedFx(string clip, Vector2 point, float delay, float duration)
        {
            yield return new WaitForSeconds(delay);
            Fx(clip, point, duration);
        }

        private IEnumerator HexerCurses()
        {
            BeginMove(ToPlayer, new Color(.9f, .75f, 1f));
            AttackVisualPhase = 1;
            for (var curse = 0; curse < (Enraged ? 4 : 3); curse++)
            {
                var point = PlayerPoint;
                Circle(point, 1.25f, .85f, .14f);
                Fx("FxHexSigil", point, .85f);
                StartCoroutine(HexLanding(point, .85f, 2.5f));
                yield return new WaitForSeconds(.38f);
            }
            yield return EndMove(.75f, 2f);
        }
        private IEnumerator HexLanding(Vector2 point, float delay, float diameter)
        {
            yield return new WaitForSeconds(delay);
            FloorStrike("ERingHex", point, diameter, .3f);
            Strike("EBurstHex", point, diameter, .4f);
        }
        private IEnumerator HexerRing()
        {
            BeginMove(ToPlayer, new Color(.9f, .75f, 1f));
            Cast(.5f);
            yield return new WaitForSeconds(.5f);
            AttackVisualPhase = 1;
            for (var bolt = 0; bolt < 10; bolt++)
            {
                var angle = bolt * 36f * Mathf.Deg2Rad;
                Shoot(ShotOrigin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 5f, contactDamage);
            }
            yield return Rush(-ToPlayer, 2.4f, .22f);
            yield return EndMove(.5f, 1.7f);
        }
    }
}
