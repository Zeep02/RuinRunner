using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The bosses' big set pieces. Each one takes over the room rather than swinging at one spot, always warns the real
    /// shape on the floor first, and ends in a window to hit back.</summary>
    public sealed partial class EnemyController
    {
        // ---- Shared
        private const float SmiteHitSeconds = .54f;
        private Rect BossArena => arenaBounds ?? new Rect(PlayerPoint - Vector2.one * 9f, Vector2.one * 18f);
        private bool OnFloor(Vector2 point) => BossArena.Contains(point) && (navigationTerrain == null || navigationTerrain.IsOpen(point)) && Physics2D.OverlapPoint(point, 1 << GameLayers.Ground) == null;
        private void Fired(Vector2 from, Vector2 direction, float speed, float size, string clip) => EnemyProjectile.Create(from, direction, speed, contactDamage, normalColor, size, clip: clip);
        private static Vector2 Heading(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
        private void PreloadGrandMoves()
        {
            string[] clips;
            if (Type == EnemyType.Boss)
                clips = new[] { "EQuakeRift", "EShotRockBig", "EShotRockBigHit" };
            else if (Type == EnemyType.Tidekeeper)
                clips = new[] { "EWhirlpool", "EWaveCrash", "EGeyserBlast", "EShotWave", "EShotWaveHit" };
            else if (Type == EnemyType.SealHeart)
                clips = new[] { "ESealBeamLoop", "ESmite", "ESealCircle", "EManaBurst", "EShotOrbit", "EShotOrbitHit" };
            else
                return;
            foreach (var clip in clips)
                NativeCombatEffect.Frames(clip);
        }
        // Gone from view and from harm: the body travels under the floor or through the seal.
        private void SetBodyAway(bool away)
        {
            roarInvulnerable = away;
            spriteRenderer.enabled = !away;
            var shadow = transform.Find("NativeGroundShadow");
            if (shadow != null)
                shadow.gameObject.SetActive(!away);
        }
        private IEnumerator ArriveAt(Vector2 root)
        {
            body.position = ClampToArena(root);
            LinearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
        }
        private bool CanStandAt(Vector2 root) => OnFloor(root) && (navigationTerrain == null || StageNavigation.IsClear(navigationTerrain, root));

        // ---- Warden: the room itself is its weapon.
        // Leaps straight from one landing into the next; the last one throws out the big ring.
        private IEnumerator WardenTripleLeap()
        {
            var leaps = phaseShifted ? 4 : 3;
            for (var leap = 0; leap < leaps; leap++)
            {
                yield return LeapSlam(ToPlayer, true);
                acting = true;
                var last = leap == leaps - 1;
                HeavyImpact(transform.position, last ? .85f : .45f);
                if (last)
                    HitFreeze(.1f);
                RingBurst(last ? 14 : 7, 4.6f, .7f, Random.Range(0f, 30f));
            }
            bossAnimator.Play(BossPose.Radial, 5, 7, .7f);
            yield return EndMove(.7f, WardenGap(.8f));
        }
        // The floor tears open in straight lines out from under its fists.
        private IEnumerator WardenRifts()
        {
            var aim = ToPlayer;
            BeginMove(aim, Color.white);
            bossAnimator.Play(BossPose.Leap, 0, 1, .75f);
            const float Length = 8.6f, Warn = .75f;
            var turns = phaseShifted ? new[] { -60f, -20f, 20f, 60f } : new[] { -30f, 0f, 30f };
            var from = (Vector2)transform.position;
            var reaches = new float[turns.Length];
            for (var i = 0; i < turns.Length; i++)
            {
                var along = Rotate(aim, turns[i]);
                var wall = Physics2D.Raycast(from, along, Length, 1 << GameLayers.Ground);
                reaches[i] = wall.collider == null ? Length : Mathf.Max(1f, wall.distance);
                Slash(from + along * reaches[i] * .5f, new Vector2(reaches[i], 1.15f), AngleOf(along), Warn, .55f);
            }
            yield return new WaitForSeconds(Warn - .15f);
            for (var i = 0; i < turns.Length; i++)
            {
                var along = Rotate(aim, turns[i]);
                BossNativeEffect.CreateOnFloor("EQuakeRift", from + along * reaches[i] * .5f, 0f, AngleOf(along)).Fit(reaches[i]);
            }
            yield return new WaitForSeconds(.15f);
            AttackVisualPhase = 1;
            bossAnimator.Play(BossPose.Leap, 2, 4, .3f);
            HitFreeze(.07f);
            HeavyImpact(from, .75f, aim);
            GameAudio.Play(GameSound.HeavyAttack, AudioChannel.Monster, .55f);
            yield return new WaitForSeconds(.55f);
            bossAnimator.Play(BossPose.Leap, 5, 7, .55f);
            yield return EndMove(.6f, WardenGap(.7f));
        }
        // Spikes close in on the hero ring by ring. One lane stays open, and it turns as the rings tighten.
        private IEnumerator WardenCage()
        {
            BeginMove(ToPlayer, Color.white);
            bossAnimator.Play(BossPose.Radial, 0, 2, .6f);
            Cast(.6f);
            var center = player.FeetPoint;
            var lane = Random.Range(0f, 360f);
            var radii = new[] { 5.2f, 3.7f, 2.2f };
            var counts = new[] { 14, 10, 7 };
            for (var ring = 0; ring < radii.Length; ring++)
            {
                var delay = .6f + ring * .42f;
                for (var spike = 0; spike < counts[ring]; spike++)
                {
                    var angle = spike * 360f / counts[ring] + ring * 11f;
                    if (Mathf.Abs(Mathf.DeltaAngle(angle, lane + ring * 32f)) < 34f)
                        continue;
                    var point = center + Heading(angle) * radii[ring];
                    if (!OnFloor(point))
                        continue;
                    Circle(point, .82f, delay, .16f);
                    StartCoroutine(DelayedFx("WardenSpike", point, delay, 1.05f));
                }
            }
            yield return new WaitForSeconds(.6f);
            AttackVisualPhase = 1;
            bossAnimator.Play(BossPose.Radial, 3, 4, .3f);
            HeavyImpact(transform.position, .55f);
            yield return new WaitForSeconds(1.15f);
            bossAnimator.Play(BossPose.Radial, 5, 7, .5f);
            yield return EndMove(.5f, WardenGap(.75f));
        }
        // Hurls fans of boulders, the lanes between one fan's rocks closing in the next.
        private IEnumerator WardenRockFan()
        {
            var waves = phaseShifted ? 3 : 2;
            for (var wave = 0; wave < waves; wave++)
            {
                var aim = ToPlayer;
                BeginMove(aim, Color.white);
                bossAnimator.Play(BossPose.Volley, 0, 2, .5f);
                var turns = wave % 2 == 0 ? new[] { -34f, -17f, 0f, 17f, 34f } : new[] { -26f, -9f, 9f, 26f };
                foreach (var turn in turns)
                    AimLine(ShotOrigin, Rotate(aim, turn), .5f);
                yield return new WaitForSeconds(.5f);
                AttackVisualPhase = 1;
                bossAnimator.Play(BossPose.Volley, 3, 5, .2f);
                foreach (var turn in turns)
                    Fired(ShotOrigin, Rotate(aim, turn), 6.6f, .62f, "EShotRockBig");
                FloorStrike("EBurstStone", (Vector2)transform.position + aim * 1.2f, 2.4f, 0f);
                HitFreeze(.04f);
                HeavyImpact(transform.position, .35f, aim);
                GameAudio.Play(GameSound.Hit, AudioChannel.Monster, .45f);
                yield return new WaitForSeconds(.22f);
            }
            bossAnimator.Play(BossPose.Volley, 6, 7, .55f);
            yield return EndMove(.6f, WardenGap(.8f));
        }

        // ---- Tidekeeper: the water does the fighting and the guardian rides it.
        // A whirlpool opens under it, drags the hero in and throws arms of water out.
        private IEnumerator TideMaelstrom()
        {
            IsTideLaneCast = false;
            const float Warn = .8f, Channel = 3.2f, Pull = 1.5f, KnockbackDecay = 22f;
            BeginCampaignAction(ToPlayer, CampaignBossAction.TideMaelstrom, Warn);
            var center = (Vector2)transform.position;
            BossNativeEffect.Create(this, "EWhirlpool", center, Warn + Channel).Fit(6.4f);
            Circle(center, 1.45f, Warn, Channel);
            CombatAudio.Native("hazard-water", AudioChannel.Monster, .35f, .8f);
            yield return new WaitForSeconds(Warn);
            CampaignRelease(Channel);
            var arms = CampaignSecondPhase ? 4 : 3;
            var spin = Random.Range(0f, 360f);
            float elapsed = 0f, nextVolley = 0f;
            while (elapsed < Channel)
            {
                elapsed += Time.deltaTime;
                var toCenter = center - PlayerPoint;
                // A push decays at a fixed rate, so topping it up by that rate holds this steady drift.
                if (!AnimationPaused && toCenter.sqrMagnitude > .5f && toCenter.sqrMagnitude < 90f)
                    player.ApplyPush(toCenter.normalized * (Pull * KnockbackDecay * Time.deltaTime));
                if (elapsed >= nextVolley)
                {
                    nextVolley += .17f;
                    for (var arm = 0; arm < arms; arm++)
                        Shoot(center, Heading(spin + arm * 360f / arms), 4.6f, contactDamage, .42f);
                    spin += 19f;
                    CombatAudio.Native("hazard-water", AudioChannel.Monster, .1f, 1.2f);
                }
                yield return null;
            }
            yield return Recover(1.15f);
        }
        // Walls of water cross the whole room from behind the guardian. Each has one gap to slip through.
        private IEnumerator TideWaveWall()
        {
            IsTideLaneCast = true;
            const float Warn = .9f, GapHalf = 1.5f, Speed = 4.4f;
            var toHero = ToPlayer;
            BeginCampaignAction(toHero, CampaignBossAction.TideWaveWall, Warn);
            var sideways = Mathf.Abs(toHero.x) >= Mathf.Abs(toHero.y);
            var travel = sideways ? new Vector2(Mathf.Sign(toHero.x), 0f) : new Vector2(0f, Mathf.Sign(toHero.y));
            var across = sideways ? Vector2.up : Vector2.right;
            var arena = BossArena;
            var low = sideways ? arena.yMin : arena.xMin;
            var high = sideways ? arena.yMax : arena.xMax;
            var rear = sideways ? (travel.x > 0f ? arena.xMin : arena.xMax) : (travel.y > 0f ? arena.yMin : arena.yMax);
            var walls = CampaignSecondPhase ? 2 : 1;
            for (var wall = 0; wall < walls; wall++)
            {
                var hero = Vector2.Dot(PlayerPoint, across);
                var gap = Mathf.Clamp(hero + Random.Range(-2.6f, 2.6f), low + GapHalf + .5f, high - GapHalf - .5f);
                // The first floor behind the guardian along each row is where that part of the wave rises.
                Vector2 RowStart(float row)
                {
                    for (var step = .6f; step < Mathf.Max(arena.width, arena.height); step += .5f)
                    {
                        var candidate = across * row + (sideways ? Vector2.right : Vector2.up) * (rear + (sideways ? travel.x : travel.y) * step);
                        if (OnFloor(candidate))
                            return candidate;
                    }
                    return new Vector2(float.NaN, float.NaN);
                }
                foreach (var edge in new[] { gap - GapHalf, gap + GapHalf })
                {
                    var from = RowStart(edge);
                    if (!float.IsNaN(from.x))
                        AimLine(from, travel, Warn);
                }
                yield return new WaitForSeconds(wall == 0 ? Warn : Warn * .6f);
                CampaignRelease(.6f);
                var crest = 0;
                for (var row = low + .5f; row < high; row += .95f)
                {
                    var start = RowStart(row);
                    if (float.IsNaN(start.x))
                        continue;
                    if (Mathf.Abs(row - gap) >= GapHalf)
                        Fired(start, travel, Speed, .85f, "EShotWave");
                    if (sideways && crest++ % 4 == 0)
                    {
                        var rise = BossNativeEffect.CreateOverActors(this, "EWaveCrash", start, 0f).Fit(5f);
                        if (travel.x < 0f)
                            rise.transform.localScale = Vector3.Scale(rise.transform.localScale, new Vector3(-1f, 1f, 1f));
                    }
                    else if (!sideways && crest++ % 3 == 0)
                        BossNativeEffect.Create(this, "TideSplash", start, .55f);
                }
                CombatAudio.Native("hazard-water", AudioChannel.Monster, .45f, .85f);
                HeavyImpact(transform.position, .4f);
                yield return new WaitForSeconds(.7f);
            }
            yield return Recover(1f);
        }
        // Sinks out of sight, and a whirlpool opening under the hero is the only warning before it bursts out in a geyser.
        private IEnumerator TideDive()
        {
            IsTideLaneCast = false;
            const float Warn = .75f;
            BeginCampaignAction(ToPlayer, CampaignBossAction.TideDive, .35f);
            var dives = CampaignSecondPhase ? 4 : 3;
            for (var dive = 0; dive < dives; dive++)
            {
                BossNativeEffect.Create(this, "TideSplash", transform.position, .4f);
                yield return new WaitForSeconds(.2f);
                SetBodyAway(true);
                var target = player.FeetPoint;
                if (!CanStandAt(target))
                    target = PlayerPoint;
                if (!CanStandAt(target))
                    target = transform.position;
                BossNativeEffect.Create(this, "EWhirlpool", target, Warn).Fit(3.4f);
                Circle(target, 1.65f, Warn, .16f);
                CombatAudio.Native("hazard-water", AudioChannel.Monster, .25f, 1.1f);
                yield return new WaitForSeconds(Warn);
                yield return ArriveAt(target);
                SetBodyAway(false);
                CampaignRelease(.32f);
                Strike("EGeyserBlast", target, 3.6f, 0f);
                var count = CampaignSecondPhase ? 12 : 8;
                var turn = Random.Range(0f, 45f);
                for (var drop = 0; drop < count; drop++)
                    Shoot(target, Heading(turn + drop * 360f / count), 4.8f, contactDamage, .42f);
                HitFreeze(.05f);
                HeavyImpact(target, .5f);
                CombatAudio.Native("hazard-water", AudioChannel.Monster, .45f, .9f);
                yield return new WaitForSeconds(dive < dives - 1 ? .42f : .1f);
            }
            yield return Recover(1.2f);
        }
        // Geysers run after the hero in a line, so standing still or running straight both lose.
        private IEnumerator TideGeyserChase()
        {
            IsTideLaneCast = false;
            BeginCampaignAction(ToPlayer, CampaignBossAction.TideGeysers, .5f);
            Fx("ECastWater", transform.position, .5f);
            yield return new WaitForSeconds(.5f);
            var count = CampaignSecondPhase ? 12 : 8;
            CampaignRelease(count * .24f);
            for (var geyser = 0; geyser < count; geyser++)
            {
                var point = player.FeetPoint + (geyser % 3 == 2 ? Random.insideUnitCircle * 1.8f : Vector2.zero);
                if (OnFloor(point))
                {
                    Circle(point, 1.15f, .62f, .16f);
                    StartCoroutine(GeyserAt(point, .62f));
                }
                yield return new WaitForSeconds(.24f);
            }
            yield return new WaitForSeconds(.6f);
            yield return Recover(1f);
        }
        private IEnumerator GeyserAt(Vector2 point, float delay)
        {
            yield return new WaitForSeconds(delay);
            Strike("EGeyserBlast", point, 2.5f, 0f);
            CombatAudio.Native("hazard-water", AudioChannel.Monster, .2f, 1.2f);
            if (cameraFollow != null)
                cameraFollow.Shake(.1f, .08f);
        }

        // ---- Seal Heart: it judges from everywhere at once.
        // Beams swing around the room from its core. They stop at walls, so cover breaks them.
        private IEnumerator SealBeams()
        {
            IsSealCrossCast = true;
            const float Warn = 1f, Sweep = 3.6f;
            BeginCampaignAction(ToPlayer, CampaignBossAction.SealBeams, Warn);
            var second = CampaignSecondPhase;
            var arms = second ? 4 : 2;
            var turn = (bossPatternIndex % 2 == 0 ? 1f : -1f) * (second ? 62f : 50f);
            // Never starts on the hero, and turns away from where the warning first pointed.
            var start = AngleOf(ToPlayer) + 180f / arms;
            var origin = (Vector2)transform.position;
            for (var arm = 0; arm < arms; arm++)
                BossSweepBeam.Create(this, player, origin, start + arm * 360f / arms, turn, Warn, Sweep, 1.05f, contactDamage);
            Fx("ECastSeal", origin, Warn);
            yield return new WaitForSeconds(Warn);
            CampaignRelease(Sweep);
            HeavyImpact(origin, .5f);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .45f, .7f);
            var elapsed = 0f;
            var nextBloom = 1f;
            while (elapsed < Sweep)
            {
                elapsed += Time.deltaTime;
                if (second && elapsed >= nextBloom)
                {
                    nextBloom += 1.1f;
                    for (var shot = 0; shot < 8; shot++)
                        Fired(ShotOrigin, Heading(elapsed * 40f + shot * 45f), 3.2f, .42f, "EShotOrbit");
                }
                yield return null;
            }
            yield return Recover(1.1f);
        }
        // Vanishes and reappears beside the hero, cutting right through them each time, then bursts.
        private IEnumerator SealBlinkCuts()
        {
            IsSealCrossCast = false;
            BeginCampaignAction(ToPlayer, CampaignBossAction.SealBlink, .3f);
            var second = CampaignSecondPhase;
            var cuts = second ? 4 : 3;
            for (var cut = 0; cut < cuts; cut++)
            {
                Fx("FxBlinkOut", transform.position, .3f);
                yield return new WaitForSeconds(.2f);
                SetBodyAway(true);
                yield return new WaitForSeconds(.14f);
                var arrival = (Vector2)transform.position;
                var side = Random.Range(0f, 360f);
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var candidate = PlayerPoint + Heading(side + attempt * 45f) * 3.3f;
                    if (CanStandAt(candidate))
                    {
                        arrival = candidate;
                        break;
                    }
                }
                yield return ArriveAt(arrival);
                SetBodyAway(false);
                Fx("FxBlinkIn", arrival, .3f);
                var aim = ToPlayer;
                CampaignActionDirection = aim;
                var reach = GetSafeTravelDistance(aim, 5f);
                var windup = cut == 0 ? .55f : .42f;
                Slash(arrival + aim * reach * .5f, new Vector2(Mathf.Max(1f, reach), 1.3f), AngleOf(aim), windup, .16f);
                yield return new WaitForSeconds(windup);
                CampaignRelease(.2f);
                CutAlong("EKnightHeavy", arrival + aim * reach * .5f, reach, AngleOf(aim), .3f);
                HitFreeze(.05f);
                HeavyImpact(arrival, .4f, aim);
                GameAudio.Play(GameSound.Hit, AudioChannel.Monster, .45f);
                yield return CampaignRush(aim, reach, .14f, false);
            }
            Strike("EManaBurst", transform.position, 5f, 0f);
            for (var shot = 0; shot < 12; shot++)
                Fired(ShotOrigin, Heading(shot * 30f), 4.6f, .42f, "EShotOrbit");
            HeavyImpact(transform.position, .6f);
            yield return Recover(1.2f);
        }
        // Bolts of light hammer down on the hero and all around them.
        private IEnumerator SealJudgment()
        {
            IsSealCrossCast = false;
            BeginCampaignAction(ToPlayer, CampaignBossAction.SealJudgment, .6f);
            Fx("ECastSeal", transform.position, .6f);
            yield return new WaitForSeconds(.6f);
            var count = CampaignSecondPhase ? 11 : 7;
            CampaignRelease(count * .3f);
            for (var bolt = 0; bolt < count; bolt++)
            {
                var point = player.FeetPoint;
                if (bolt % 2 == 1)
                    point += Random.insideUnitCircle.normalized * Random.Range(1.6f, 3.2f);
                if (OnFloor(point))
                {
                    Circle(point, 1.3f, SmiteHitSeconds, .2f);
                    Strike("ESmite", point, 3.6f, 0f);
                    StartCoroutine(SmiteShake(SmiteHitSeconds));
                }
                yield return new WaitForSeconds(.3f);
            }
            yield return new WaitForSeconds(SmiteHitSeconds);
            yield return Recover(1.05f);
        }
        private IEnumerator SmiteShake(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (cameraFollow != null)
                cameraFollow.Shake(.12f, .1f);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .2f, 1.3f);
        }
        // Draws a great circle, stands exposed while it fills, then a column of light and rings of seal shots.
        private IEnumerator SealBloom()
        {
            IsSealCrossCast = false;
            const float Warn = 1.7f;
            BeginCampaignAction(ToPlayer, CampaignBossAction.SealBloom, Warn);
            var center = (Vector2)transform.position;
            Strike("ESealCircle", center, 6f, 0f);
            Circle(center, 2.3f, Warn, .3f, 1.4f);
            yield return new WaitForSeconds(Warn);
            var rings = CampaignSecondPhase ? 6 : 4;
            CampaignRelease(rings * .34f);
            HitFreeze(.1f);
            HeavyImpact(center, .8f);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .5f, .8f);
            for (var ring = 0; ring < rings; ring++)
            {
                for (var shot = 0; shot < 14; shot++)
                    Fired(ShotOrigin, Heading(shot * 360f / 14f + ring * (ring % 2 == 0 ? 13f : -13f)), 3.4f + ring * .25f, .42f, "EShotOrbit");
                yield return new WaitForSeconds(.34f);
            }
            yield return Recover(1.2f);
        }
    }
}
