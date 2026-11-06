using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class EnemyController
    {
        public bool IsTideLaneCast { get; private set; }
        public bool IsSealCrossCast { get; private set; }
        public Vector2 CampaignActionDirection { get; private set; } = Vector2.down;
        public CampaignBossAction CurrentBossAction { get; private set; }
        public float CampaignPhaseDuration { get; private set; } = 1;
        readonly CampaignBossBrain campaignBrain = new CampaignBossBrain();
        bool CampaignSecondPhase => Health < MaxHealth * .5f;

        private void UpdateTidekeeper(Vector2 delta, Vector2 direction)
        {
            if (acting)
                return;
            if (TryBeginPhaseShift())
                return;
            SetPlanar(SteerAround(delta.magnitude > 6 ? direction : delta.magnitude < 2.5f ? -direction : Vector2.zero) * speed * .7f);
            if (specialTimer > 0)
                return;
            bossPatternIndex++;
            switch (campaignBrain.Select(delta.magnitude, CampaignSecondPhase, true))
            {
                case 0:
                    StartCoroutine(TideLanes());
                    break;
                case 1:
                    StartCoroutine(CallTide(true));
                    break;
                case 2:
                    StartCoroutine(TideFan(direction));
                    break;
                case 3:
                    StartCoroutine(TideSurge(direction));
                    break;
                case 4:
                    StartCoroutine(TideMaelstrom());
                    break;
                case 5:
                    StartCoroutine(TideWaveWall());
                    break;
                case 6:
                    StartCoroutine(TideDive());
                    break;
                default:
                    StartCoroutine(TideGeyserChase());
                    break;
            }
        }
        private void UpdateSealHeart(Vector2 delta, Vector2 direction)
        {
            if (acting)
                return;
            if (TryBeginPhaseShift())
                return;
            var tangent = new Vector2(-direction.y, direction.x) * (bossPatternIndex % 2 == 0 ? 1 : -1);
            SetPlanar(SteerAround(delta.magnitude > 5.5f ? direction : delta.magnitude < 2 ? -direction : tangent * .25f) * speed * .65f);
            if (specialTimer > 0)
                return;
            bossPatternIndex++;
            switch (campaignBrain.Select(delta.magnitude, CampaignSecondPhase, false))
            {
                case 0:
                    StartCoroutine(SealSweep(direction));
                    break;
                case 1:
                    StartCoroutine(SealCharge(direction));
                    break;
                case 2:
                    StartCoroutine(SealCross());
                    break;
                case 3:
                    StartCoroutine(SealVolley(direction));
                    break;
                case 4:
                    StartCoroutine(SealBeams());
                    break;
                case 5:
                    StartCoroutine(SealBlinkCuts());
                    break;
                case 6:
                    StartCoroutine(SealJudgment());
                    break;
                default:
                    StartCoroutine(SealBloom());
                    break;
            }
        }
        private void BeginCampaignAction(Vector2 direction, CampaignBossAction action = CampaignBossAction.None, float warning = .8f)
        {
            acting = true;
            AttackVisualPhase = 0;
            CurrentBossAction = action;
            CampaignPhaseDuration = warning;
            CampaignActionDirection = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
            SetPlanar(Vector2.zero, true);
            SetFacing(direction.x);
            GameAudio.Play(GameSound.HeavyAttack, AudioChannel.Monster, .25f);
        }
        private void CampaignRelease(float duration)
        {
            AttackVisualPhase = 1;
            CampaignPhaseDuration = duration;
        }
        private IEnumerator Recover(float seconds)
        {
            // A boss should be hit in its openings, not stood around waiting for them.
            seconds *= CampaignSecondPhase ? .55f : .75f;
            AttackVisualPhase = 2;
            CampaignPhaseDuration = seconds;
            collisionAttackActive = false;
            SetPlanar(Vector2.zero, true);
            yield return new WaitForSeconds(seconds);
            EndAction();
            specialTimer = .3f;
        }
        private IEnumerator CallTide(bool boss)
        {
            IsTideLaneCast = false;
            BeginCampaignAction(player.transform.position - transform.position, CampaignBossAction.TidePools, .95f);
            var center = (Vector2)player.transform.position;
            var arena = arenaBounds ?? new Rect(center - Vector2.one * 10, Vector2.one * 20);
            var pattern = new TidePoolPattern(arena, center, boss, navigationTerrain);
            foreach (var point in pattern.Centers)
                ArenaHazard.Create(this, player, point, Vector2.one * (pattern.Radius * 2), 0, true, .95f, .48f, contactDamage, DangerTint, ArenaHazardStyle.TidePool);
            yield return new WaitForSeconds(.95f);
            CampaignRelease(.48f);
            CombatAudio.Native("hazard-water", AudioChannel.Monster, boss ? .42f : .28f);
            yield return new WaitForSeconds(.5f);
            yield return Recover(boss ? 1.25f : .65f);
        }
        private IEnumerator SentinelCut(Vector2 direction)
        {
            BeginCampaignAction(direction);
            var target = (Vector2)transform.position + direction * 1.8f;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            ArenaHazard.Create(this, player, target, new Vector2(3.8f, 1.25f), angle, false, .65f, .22f, contactDamage, DangerTint);
            yield return new WaitForSeconds(.65f);
            CampaignRelease(.22f);
            CutAlong("ESentinelCut", target, 3.8f, angle, .26f);
            yield return new WaitForSeconds(.24f);
            AttackVisualPhase = 2;
            yield return Rush(-direction, 1.4f, .25f);
            yield return Recover(.8f);
        }
        private IEnumerator TideLanes()
        {
            IsTideLaneCast = true;
            BeginCampaignAction(player.transform.position - transform.position, CampaignBossAction.TideLanes, TideLanePattern.WarningSeconds);
            var arena = arenaBounds ?? new Rect((Vector2)transform.position - Vector2.one * 10, Vector2.one * 20);
            // The second phase changes lane orientation and safe-space ordering,
            // not just damage. Never overlap this active wave with the next tell.
            var vertical = CampaignSecondPhase && bossPatternIndex % 2 != 0;
            var pattern = new TideLanePattern(arena, player.transform.position, vertical, bossPatternIndex % 2 == 0);
            foreach (var flooded in new[] { pattern.First, pattern.Second })
                ArenaHazard.Create(this, player, flooded.center, flooded.size, 0, false, TideLanePattern.WarningSeconds, .58f, contactDamage, DangerTint, ArenaHazardStyle.TideLane);
            yield return new WaitForSeconds(TideLanePattern.WarningSeconds);
            CampaignRelease(.58f);
            CombatAudio.Native("hazard-water", AudioChannel.Monster, .42f, .9f);
            yield return new WaitForSeconds(.62f);
            yield return Recover(1.2f);
        }
        private IEnumerator TideFan(Vector2 direction)
        {
            IsTideLaneCast = true;
            bool secondPhase = CampaignSecondPhase;
            BeginCampaignAction(direction, CampaignBossAction.TideFan, .85f);
            var origin = (Vector2)transform.position + direction * .6f;
            int[] offsets = secondPhase ? new[] { -36, -18, 0, 18, 36 } : new[] { -24, 0, 24 };
            // The shots fly until a wall stops them, so the warning follows each real path.
            foreach (int angle in offsets)
                AimLine(origin, Quaternion.Euler(0, 0, angle) * direction, .85f);
            Fx("ECastSeal", origin, .85f);
            yield return new WaitForSeconds(.85f);
            CampaignRelease(secondPhase ? .65f : .35f);
            foreach (int angle in offsets)
                BossRuneProjectile.Create(this, player, origin, Quaternion.Euler(0, 0, angle) * direction, 6.5f, contactDamage, true);
            CombatAudio.Native("hazard-water", AudioChannel.Monster, .35f, 1.15f);
            if (secondPhase)
            {
                foreach (int angle in new[] { -27, -9, 9, 27 })
                    AimLine(origin, Quaternion.Euler(0, 0, angle) * direction, .3f);
                yield return new WaitForSeconds(.3f);
                foreach (int angle in new[] { -27, -9, 9, 27 })
                    BossRuneProjectile.Create(this, player, origin, Quaternion.Euler(0, 0, angle) * direction, 6.5f, contactDamage, true);
            }
            yield return new WaitForSeconds(.35f);
            yield return Recover(1.15f);
        }
        private IEnumerator TideSurge(Vector2 direction)
        {
            IsTideLaneCast = false;
            BeginCampaignAction(direction, CampaignBossAction.TideSurge, .85f);
            float distance = GetSafeTravelDistance(direction, 4.5f);
            var origin = (Vector2)transform.position;
            ArenaHazard.CreateChargeRoute(this, player, origin, direction, distance, .85f, .5f, DangerTint, ContactRadius);
            yield return new WaitForSeconds(.85f);
            CampaignRelease(.5f);
            collisionAttackActive = true;
            BossNativeEffect.Create(this, "TideSplash", origin, .55f);
            yield return CampaignRush(direction, distance, .5f, true);
            collisionAttackActive = false;
            if (CampaignSecondPhase)
                ArenaHazard.Create(this, player, transform.position, Vector2.one * 3, 0, true, .5f, .4f, contactDamage, DangerTint, ArenaHazardStyle.TidePool);
            yield return Recover(1.25f);
        }
        private IEnumerator SealSweep(Vector2 direction)
        {
            IsSealCrossCast = false;
            BeginCampaignAction(direction, CampaignBossAction.SealSweep, .7f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            ArenaHazard.Create(this, player, transform.position, Vector2.one * 4.8f, angle, true, .7f, .3f,
                contactDamage, DangerTint, ArenaHazardStyle.SealSweep, false, 135);
            yield return new WaitForSeconds(.7f);
            CampaignRelease(.3f);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .4f, 1.05f);
            yield return new WaitForSeconds(.34f);
            // Phase two's short counterstep opens a different punish angle while
            // the ended sweep no longer hurts someone moving into the opening.
            if (CampaignSecondPhase)
                yield return Rush(-direction, 1.15f, .22f);
            yield return Recover(1.05f);
        }
        private IEnumerator SealCharge(Vector2 direction)
        {
            IsSealCrossCast = false;
            BeginCampaignAction(direction, CampaignBossAction.SealCharge, .85f);
            float distance = GetSafeTravelDistance(direction, 6.5f);
            var origin = (Vector2)transform.position;
            ArenaHazard.CreateChargeRoute(this, player, origin, direction, distance, .85f, .44f, DangerTint, ContactRadius);
            yield return new WaitForSeconds(.85f);
            CampaignRelease(.44f);
            collisionAttackActive = true;
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .3f, .8f);
            yield return CampaignRush(direction, distance, .44f, false);
            collisionAttackActive = false;
            BossNativeEffect.Create(this, "SlamDust", transform.position, .48f);
            yield return Recover(CampaignSecondPhase ? 1.1f : 1.35f);
        }
        private IEnumerator CampaignRush(Vector2 direction, float desiredDistance, float duration, bool water)
        {
            var start = body.position;
            float distance = GetSafeTravelDistance(direction, desiredDistance);
            float elapsed = 0;
            var lastTrail = start;
            BossNativeEffect.Create(this, water ? "TideSplash" : "GroundDust", start, .3f);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                MoveSafely(start + direction * (distance * Mathf.Clamp01(elapsed / duration)));
                // Travel, rather than frame count, spaces the native wake. A
                // stopped charge cannot stack a pile of effects against a wall.
                if (Vector2.Distance(body.position, lastTrail) >= .55f)
                {
                    BossNativeEffect.Create(this, water ? "TideSplash" : "GroundDust", body.position, .3f);
                    lastTrail = body.position;
                }
                yield return new WaitForFixedUpdate();
            }
            MoveSafely(start + direction * distance);
        }
        private IEnumerator SealCross()
        {
            IsSealCrossCast = true;
            BeginCampaignAction(player.transform.position - transform.position, CampaignBossAction.SealCross, 1.05f);
            var position = ClampToArena(player.transform.position);
            var angles = CampaignSecondPhase ? new[] { 45f, 135f } : new[] { 0f, 90f };
            var arena = arenaBounds ?? new Rect(position - Vector2.one * 8, Vector2.one * 16);
            float length = Mathf.Min(14, Mathf.Max(8, Mathf.Min(arena.width, arena.height) - 2));
            for (int i = 0; i < angles.Length; i++)
                ArenaHazard.Create(this, player, position, new Vector2(length, 1.35f), angles[i], false, 1.05f, .4f,
                    contactDamage, DangerTint, ArenaHazardStyle.SealRift, i > 0);
            yield return new WaitForSeconds(1.05f);
            CampaignRelease(.4f);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .4f, .9f);
            yield return new WaitForSeconds(.45f);
            yield return Recover(1.25f);
        }
        private IEnumerator SealVolley(Vector2 direction)
        {
            IsSealCrossCast = false;
            bool secondPhase = CampaignSecondPhase;
            BeginCampaignAction(direction, CampaignBossAction.SealVolley, .9f);
            var origin = (Vector2)transform.position + direction * .65f;
            foreach (int spread in new[] { -16, 0, 16 })
                AimLine(origin, Quaternion.Euler(0, 0, spread) * direction, .9f);
            Fx("ECastSeal", origin, .9f);
            yield return new WaitForSeconds(.9f);
            CampaignRelease(secondPhase ? .66f : .36f);
            foreach (int spread in new[] { -16, 0, 16 })
                BossRuneProjectile.Create(this, player, origin, Quaternion.Euler(0, 0, spread) * direction, 7, contactDamage);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .27f, 1.35f);
            if (secondPhase)
            {
                foreach (int spread in new[] { -24, -8, 8, 24 })
                    AimLine(origin, Quaternion.Euler(0, 0, spread) * direction, .3f);
                yield return new WaitForSeconds(.3f);
                foreach (int spread in new[] { -24, -8, 8, 24 })
                    BossRuneProjectile.Create(this, player, origin, Quaternion.Euler(0, 0, spread) * direction, 7, contactDamage);
            }
            yield return new WaitForSeconds(.36f);
            yield return Recover(1.2f);
        }
    }
}
