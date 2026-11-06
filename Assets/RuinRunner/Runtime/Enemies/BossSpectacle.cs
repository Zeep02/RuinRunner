using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Boss phase changes and the Warden's staged move set.</summary>
    public sealed partial class EnemyController
    {
        private bool phaseShifted;
        private int lastWardenMove = -1;

        // Every boss stops at half health and visibly changes gear. It takes no
        // damage during the roar, so a burst of hits cannot skip the moment.
        private bool TryBeginPhaseShift()
        {
            if (phaseShifted || Health >= MaxHealth * .5f) return false;
            phaseShifted = true;
            StartCoroutine(PhaseShift());
            return true;
        }
        private IEnumerator PhaseShift()
        {
            BeginMove(ToPlayer, new Color(1f, .75f, .55f));
            collisionAttackActive = false;
            roarInvulnerable = true;
            if (bossAnimator != null) bossAnimator.Play(BossPose.Radial, 0, 2, .7f);
            CameraFollow.DramaticSlow?.Invoke(.55f, .25f);
            if (cameraFollow != null)
            {
                cameraFollow.FocusPresentation(transform.position);
                holdingCameraFocus = true;
                cameraFollow.Shake(.5f, .16f);
            }
            // The roar clip's rings break at .8 s, which is the moment the blast lands below.
            BossNativeEffect.CreateOverActors(this, "WardenRoar", transform.position, 2.3f);
            yield return new WaitForSeconds(.8f);

            AttackVisualPhase = 1;
            if (bossAnimator != null) bossAnimator.Play(BossPose.Radial, 3, 4, .3f);
            HitFreeze(.14f);
            PrototypeAudio.Play(PrototypeSound.HeavyAttack, AudioChannel.Monster, .6f);
            if (cameraFollow != null) cameraFollow.Shake(.7f, .42f);
            // Knock the player's space open; a shock ring that must be dashed through.
            RingBurst(14, 5.4f, .9f, 0f);
            // Each changes gear in its own element.
            if (Type == EnemyType.Boss) { BossNativeEffect.CreateOnFloor("WardenCrater", transform.position, 2.4f); FloorStrike("ERingStone", transform.position, 7f, .36f); }
            else if (Type == EnemyType.Tidekeeper) { Strike("EGeyserBlast", transform.position, 5f, 0f); FloorStrike("ERingSpore", transform.position, 7f, .36f); BossNativeEffect.Create(this, "EWhirlpool", transform.position, 2f).Fit(7f); }
            else if (Type == EnemyType.SealHeart) { Strike("EManaBurst", transform.position, 7f, 0f); Strike("ESealCircle", transform.position, 7f, 0f); }
            yield return new WaitForSeconds(.3f);
            RingBurst(14, 4.2f, .9f, 12f);
            yield return new WaitForSeconds(.35f);

            ReleaseCameraFocus();
            roarInvulnerable = false;
            if (bossAnimator != null) bossAnimator.Play(BossPose.Radial, 5, 7, .4f);
            yield return EndMove(.4f, .5f);
        }
        private bool holdingCameraFocus;
        private void ReleaseCameraFocus()
        {
            if (holdingCameraFocus && cameraFollow != null) cameraFollow.ReleasePresentation();
            holdingCameraFocus = false;
        }
        // A stage change can remove the boss mid-roar; the camera must not stay parked on it.
        private void OnDisable() => ReleaseCameraFocus();
        private void RingBurst(int count, float speed, float damageScale, float offsetDegrees)
        {
            for (var index = 0; index < count; index++)
            {
                var angle = (offsetDegrees + index * 360f / count) * Mathf.Deg2Rad;
                Shoot(transform.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), speed, contactDamage * damageScale, .27f);
            }
        }
        // Everything stops for a beat on contact: the heavier the blow, the longer the freeze.
        private static void HitFreeze(float seconds) => CameraFollow.DramaticSlow?.Invoke(seconds, .03f);
        private void HeavyImpact(Vector2 point, float strength, Vector2 kick = default)
        {
            if (cameraFollow == null) return;
            cameraFollow.Shake(.22f + strength * .4f, .14f + strength * .3f);
            if (kick != default) cameraFollow.Kick(kick, strength * .5f);
        }

        // ---- Warden: never the same move twice; reads the player's distance.
        private float hugTime;
        // Often it strings the next move straight on; when it does stop, the gap is the opening.
        private float WardenGap(float seconds) => Random.value < (phaseShifted ? .5f : .3f) ? .12f : seconds;
        // Stand next to it too long and it clears the space around its feet.
        private IEnumerator WardenShove()
        {
            BeginMove(ToPlayer, Color.white);
            bossAnimator.Play(BossPose.Radial, 0, 2, .34f);
            Circle(transform.position, 3f, .34f, .12f);
            yield return new WaitForSeconds(.34f);
            AttackVisualPhase = 1;
            bossAnimator.Play(BossPose.Radial, 3, 4, .16f);
            GroundSlam(transform.position, 3f);
            HitFreeze(.05f);
            HeavyImpact(transform.position, .35f);
            yield return new WaitForSeconds(.16f);
            bossAnimator.Play(BossPose.Radial, 5, 7, .4f);
            yield return EndMove(.42f, 1.1f);
        }

        private void WardenChooseMove(float distance, Vector2 direction)
        {
            int move;
            do
            {
                var roll = Random.value;
                if (distance < 3.4f) move = roll < .55f ? 0 : roll < .8f ? 2 : 3;
                else if (distance > 7f) move = roll < .45f ? 1 : roll < .8f ? 3 : 4;
                else move = roll < .3f ? 1 : roll < .55f ? 2 : roll < .8f ? 4 : 3;
                if (phaseShifted && Random.value < .3f) move = 5;
                if (Random.value < .45f) move = Random.Range(6, 10);
            } while (move == lastWardenMove);
            lastWardenMove = move;
            switch (move)
            {
                case 0: StartCoroutine(WardenFistCombo()); break;
                case 1: StartCoroutine(WardenRam(direction)); break;
                case 2: StartCoroutine(WardenCrater(direction)); break;
                case 3: StartCoroutine(WardenRockRain()); break;
                case 4: StartCoroutine(WardenFissure(direction)); break;
                case 5: StartCoroutine(WardenCataclysm()); break;
                case 6: StartCoroutine(WardenTripleLeap()); break;
                case 7: StartCoroutine(WardenRifts()); break;
                case 8: StartCoroutine(WardenCage()); break;
                default: StartCoroutine(WardenRockFan()); break;
            }
        }
        // Three heavier and heavier punches that step toward the player.
        private IEnumerator WardenFistCombo()
        {
            var windups = phaseShifted ? new[] { .46f, .3f, .62f } : new[] { .6f, .4f, .8f };
            for (var hit = 0; hit < 3; hit++)
            {
                var direction = ToPlayer;
                var angle = AngleOf(direction);
                BeginMove(direction, Color.white);
                bossAnimator.Play(BossPose.Volley, 0, 2, windups[hit]);
                var last = hit == 2;
                var radius = last ? 3.4f : 2.6f;
                Sector(transform.position, radius, angle, last ? 360f : 120f, windups[hit], .16f);
                yield return new WaitForSeconds(windups[hit]);
                AttackVisualPhase = 1;
                bossAnimator.Play(BossPose.Volley, 3, 5, .2f);
                var point = (Vector2)transform.position + direction * (last ? 0f : 1.5f);
                if (last) { BossNativeEffect.CreateOnFloor("WardenCrater", point, 2.2f); FloorStrike("ERingStone", point, radius * 2f, .34f); }
                else Fx("EWardenSwipe", transform.position, .28f, angle);
                HitFreeze(last ? .12f : .05f);
                HeavyImpact(point, last ? .7f : .3f, direction);
                PrototypeAudio.Play(PrototypeSound.Hit, AudioChannel.Monster, last ? .55f : .45f);
                yield return Rush(direction, last ? .2f : 1f, .16f);
            }
            bossAnimator.Play(BossPose.Volley, 6, 7, .9f);
            yield return EndMove(.95f, WardenGap(phaseShifted ? .55f : .85f));
        }
        // A full-length ram. Hitting a wall stuns the boss: the big punish window.
        private IEnumerator WardenRam(Vector2 direction)
        {
            var rams = phaseShifted ? 2 : 1;
            var crashed = false;
            for (var ram = 0; ram < rams; ram++)
            {
                direction = ToPlayer;
                var windup = ram == 0 ? .8f : .5f;
                BeginMove(direction, Color.white);
                bossAnimator.Play(BossPose.Charge, 0, 1, windup);
                var travel = GetSafeTravelDistance(direction, 11f);
                crashed = travel < 10.5f;
                ArenaHazard.CreateChargeRoute(this, player, transform.position, direction, travel, windup, .46f, DangerTint, ContactRadius);
                Fx("ERushDust", transform.position, windup).Fit(1.8f);
                if (cameraFollow != null) cameraFollow.Shake(windup, .05f);
                yield return new WaitForSeconds(windup);
                collisionAttackActive = true;
                AttackVisualPhase = 1;
                bossAnimator.Play(BossPose.Charge, 2, 4, .46f);
                yield return CampaignRush(direction, travel, .46f, false);
                collisionAttackActive = false;
                if (crashed) BossNativeEffect.CreateOnFloor("WardenCrater", (Vector2)transform.position + direction * .8f, 2f);
                else GroundSlam(transform.position, 1.6f);
                if (crashed) HitFreeze(.16f);
                HeavyImpact(transform.position, crashed ? .9f : .4f, direction);
            }
            if (crashed)
            {
                RingBurst(8, 4f, .75f, Random.Range(0f, 45f));
                bossAnimator.Play(BossPose.Hit, 0, 7, 1.5f);
                yield return EndMove(1.55f, WardenGap(.6f));
                yield break;
            }
            bossAnimator.Play(BossPose.Charge, 5, 7, .55f);
            yield return EndMove(.6f, WardenGap(.8f));
        }
        // Leap onto the player; the landing throws out rings that must be dodged.
        private IEnumerator WardenCrater(Vector2 direction)
        {
            yield return LeapSlam(ToPlayer, true);
            HeavyImpact(transform.position, .8f);
            HitFreeze(.11f);
            acting = true;
            RingBurst(phaseShifted ? 16 : 11, 5f, .8f, 0f);
            if (phaseShifted)
            {
                yield return new WaitForSeconds(.28f);
                RingBurst(16, 3.8f, .8f, 11f);
            }
            yield return EndMove(.5f, WardenGap(.7f));
        }
        // Slams the floor, then rocks rain on the arena and chase the player.
        private IEnumerator WardenRockRain()
        {
            BeginMove(ToPlayer, Color.white);
            bossAnimator.Play(BossPose.Radial, 0, 2, .65f);
            Cast(.65f);
            yield return new WaitForSeconds(.65f);
            AttackVisualPhase = 1;
            bossAnimator.Play(BossPose.Radial, 3, 4, .3f);
            GroundSlam(transform.position, 1.7f);
            HeavyImpact(transform.position, .5f);
            var total = phaseShifted ? 14 : 9;
            for (var rock = 0; rock < total; rock++)
            {
                var point = rock % 3 == 0 ? PlayerPoint : PlayerPoint + Random.insideUnitCircle * 5.5f;
                if (arenaBounds.HasValue)
                    point = new Vector2(Mathf.Clamp(point.x, arenaBounds.Value.xMin + 1, arenaBounds.Value.xMax - 1), Mathf.Clamp(point.y, arenaBounds.Value.yMin + 1, arenaBounds.Value.yMax - 1));
                Circle(point, 1.15f, .85f, .14f, .85f);
                // The boulder clip lands at exactly .85 s, with the warning.
                Fx("WardenBoulder", point, 2f);
                StartCoroutine(RockImpact(.85f));
                yield return new WaitForSeconds(.16f);
            }
            bossAnimator.Play(BossPose.Radial, 5, 7, .55f);
            yield return EndMove(.55f, WardenGap(.8f));
        }
        private IEnumerator RockImpact(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (cameraFollow != null) cameraFollow.Shake(.1f, .1f);
        }
        // Spikes tear along the floor in three directions.
        private IEnumerator WardenFissure(Vector2 direction)
        {
            BeginMove(direction, Color.white);
            bossAnimator.Play(BossPose.Leap, 0, 1, .7f);
            var angles = phaseShifted ? new[] { -36f, -12f, 12f, 36f } : new[] { -25f, 0f, 25f };
            for (var spike = 1; spike <= 7; spike++)
            {
                var delay = .7f + spike * .09f;
                foreach (var angle in angles)
                {
                    var point = (Vector2)transform.position + Rotate(direction, angle) * (spike * 1.55f);
                    if (arenaBounds.HasValue && !arenaBounds.Value.Contains(point)) continue;
                    Circle(point, .85f, delay, .15f);
                    StartCoroutine(DelayedFx("WardenSpike", point, delay, 1.05f));
                }
            }
            yield return new WaitForSeconds(.7f);
            AttackVisualPhase = 1;
            bossAnimator.Play(BossPose.Leap, 5, 7, .5f);
            HeavyImpact(transform.position, .6f, direction);
            yield return new WaitForSeconds(.5f);
            yield return EndMove(.55f, WardenGap(.8f));
        }
        // Phase two finisher: charges at the centre while the floor closes in; one safe ring.
        private IEnumerator WardenCataclysm()
        {
            BeginMove(ToPlayer, new Color(1f, .75f, .5f));
            bossAnimator.Play(BossPose.Radial, 0, 2, 1.3f);
            if (cameraFollow != null) cameraFollow.Shake(1.3f, .08f);
            BossNativeEffect.CreateOverActors(this, "WardenRoar", transform.position, 3.7f);
            ArenaHazard.Create(this, player, transform.position, Vector2.one * 7f, 0, true, 1.3f, .18f, contactDamage * 1.5f, DangerTint);
            yield return new WaitForSeconds(1.3f);
            AttackVisualPhase = 1;
            bossAnimator.Play(BossPose.Radial, 3, 4, .3f);
            BossNativeEffect.CreateOnFloor("WardenCrater", transform.position, 2.4f);
            FloorStrike("ERingStone", transform.position, 7f, .36f);
            HitFreeze(.2f);
            HeavyImpact(transform.position, 1f);
            yield return new WaitForSeconds(.28f);
            RingBurst(18, 5.6f, .9f, 0f);
            yield return new WaitForSeconds(.3f);
            RingBurst(18, 4.4f, .9f, 10f);
            bossAnimator.Play(BossPose.Hit, 0, 7, 1.2f);
            yield return EndMove(1.25f, WardenGap(.7f));
        }
    }
}
