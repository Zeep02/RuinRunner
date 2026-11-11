using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Dash, melee and ranged attacks.</summary>
    public sealed partial class PlayerController
    {
        private IEnumerator Dash()
        {
            CancelMeleeAttack();
            CancelWeaponAction();
            actionCancelSequence++;
            var sequence = actionCancelSequence;
            dashCharges--;
            if (dashRechargeTimer <= 0f)
                dashRechargeTimer = DashRecharge;
            dashing = true;
            invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(HasTalent(TalentId.PhantomStep) ? .48f : .24f));
            var direction = movementInput.sqrMagnitude > .01f
                ? movementInput.normalized
                : AimDirection.sqrMagnitude > .01f ? AimDirection.normalized : Vector2.right * facing;
            DashDirection = direction;
            var distance = GetSafeTravelDistance(direction, DashDistance);
            var start = body.position;
            var cursor = start;
            float previousProgress = 0;
            ItemEffects.OnDashStarted(start);
            TalentOnDashStart(start);
            LinearVelocity = Vector2.zero;
            GameAudio.Play(GameSound.Dash, AudioChannel.Player, .55f);
            // Dust kicks up behind the body so nothing covers the face at the start of the dash.
            var dust = NativeCombatEffect.Create("GroundDust", start - direction * .15f + Vector2.down * .06f,
                0, .75f, .28f, Color.white);
            if (dust != null)
                dust.BelowActor(spriteRenderer, -3);
            var elapsed = 0f;
            while (elapsed < DashDuration && sequence == actionCancelSequence && !AnimationPaused)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / DashDuration);
                float eased = 1f - (1f - progress) * (1f - progress);
                float step = distance * (eased - previousProgress);
                previousProgress = eased;
                // Cast from the actual foot collider on every segment; turns cannot cross corners.
                step = GetSafeTravelDistance(direction, step);
                cursor = body.position + direction * step;
                // Resolve each render step immediately. Repeated MovePosition calls
                // before a fixed tick would overwrite travel and shorten the dash.
                body.position = cursor;
                yield return null;
                if (sequence == actionCancelSequence && !AnimationPaused)
                {
                    ItemEffects.OnDashMoved(body.position);
                    TalentOnDashStep(body.position);
                }
            }
            dashing = false;
            ItemEffects.OnDashEnded();
            spriteRenderer.color = RestingColor;
        }

        private float GetSafeTravelDistance(Vector2 direction, float desiredDistance)
        {
            if (playerCollider == null)
                return desiredDistance;
            var bounds = playerCollider.bounds;
            var size = new Vector2(bounds.size.x, bounds.size.y);
            // Match the actual horizontal foot capsule. A box can begin inside
            // a corner that the rounded collider legally occupies, blocking an
            // otherwise clear dash away from the corner.
            const float clearance = .06f;
            // Include the clearance in the query itself: a small final thrust step can
            // otherwise miss a wall just outside its cast and consume the safety gap.
            var hit = Physics2D.CapsuleCast(bounds.center, size, CapsuleDirection2D.Horizontal, 0f,
                direction, desiredDistance + clearance, 1 << GameLayers.Ground);
            return hit.collider == null ? desiredDistance : Mathf.Min(desiredDistance, Mathf.Max(0f, hit.distance - clearance));
        }

        private void LightComboAttack()
        {
            katanaCounterSucceeded = false;
            var profile = MeleeComboCatalog.Get(EquippedWeapon);
            if (profile.LightSteps == null || profile.LightSteps.Length == 0)
                return;
            var stepIndex = Mathf.Clamp(comboIndex, 0, profile.LightSteps.Length - 1);
            activeComboAnimationStep = stepIndex;
            PerformMeleeAttack(ApplyFinisherMutation(profile.LightSteps[stepIndex], stepIndex == profile.LightSteps.Length - 1), 1f, false);
            comboIndex = (stepIndex + 1) % profile.LightSteps.Length;
            comboResetTimer = attackTimer + profile.ResetWindow;
        }

        private void StrongAttack(MeleeComboProfile profile, float chargeMultiplier)
        {
            activeComboAnimationStep = -1;
            var weaponMultiplier = EquippedWeapon == null ? 1f : EquippedWeapon.HeavyDamageMultiplier;
            PerformMeleeAttack(profile.HeavyStep, weaponMultiplier * chargeMultiplier, true);
            comboIndex = 0;
            comboResetTimer = 0f;
        }

        private void PerformMeleeAttack(MeleeAttackStep step, float damageMultiplier, bool isHeavy)
        {
            if (step == null)
                return;
            IsGuarding = false;
            heavyAttackPose = isHeavy;
            meleeFinisherImpactPlayed = false;
            chargedReleasePose = isHeavy && greatswordCharging && EquippedWeapon.Family == WeaponFamily.Greatsword;
            parryPresentation = false;
            attackAnimationSequence++;
            comboSwingSequence = isHeavy ? -1 : attackAnimationSequence;
            attackPoseTimer = 1f;
            var bodyCombo = IsBodyComboSwing;
            var actionDuration = AttackDelay * step.DelayMultiplier * SigDelayMult();
            attackTimer = Mathf.Max(attackTimer, actionDuration);
            activeMeleeMotion = step.Motion;
            committedMeleeAim = AimDirection.sqrMagnitude > .01f ? AimDirection.normalized : Vector2.right * facing;
            // The body throws itself into the swing. Input still steers; walls still stop it.
            knockbackVelocity += committedMeleeAim * MeleeLunge(EquippedWeapon.Family, isHeavy) * (bodyCombo ? MeleeComboFrames.Lunge(EquippedWeapon.Family, activeComboAnimationStep) : 1f);
            attackPoseDuration = bodyCombo ? MeleeComboFrames.SwingSeconds(EquippedWeapon.Family, activeComboAnimationStep, actionDuration) : Mathf.Min(isHeavy ? .42f : step.Motion == MeleeAttackMotion.Thrust ? .26f : .30f, actionDuration);
            attackPoseTimer = attackPoseDuration;
            meleeAngleOffset = step.AngleOffset;
            pendingMeleeStep = step;
            pendingMeleeStroke = 0;
            var counter = CounterStrike();
            pendingMeleeCritical = counter || Stats.RollCritical();
            pendingMeleeDamage = Damage * step.DamageMultiplier * damageMultiplier * SigMeleeMult() * (pendingMeleeCritical ? Stats.CriticalDamage : 1f) * (counter ? CounterBonus : 1f);
            // Odd combo steps swing clockwise on screen, the way the trail is drawn; the rest turn the other way.
            SignatureSlashEffect.Create(this, SignatureMeleeFx.Trail(EquippedWeapon, step.Motion, isHeavy), Mathf.Atan2(committedMeleeAim.y, committedMeleeAim.x) * Mathf.Rad2Deg + step.AngleOffset,
                step.Motion == MeleeAttackMotion.Sweep && (bodyCombo ? MeleeComboFrames.TrailMirrored(EquippedWeapon.Family, activeComboAnimationStep, committedMeleeAim.x < 0) : isHeavy || (activeComboAnimationStep & 1) == 0),
                step.Motion == MeleeAttackMotion.Thrust ? MeleeMotionTiming.ThrustStart : bodyCombo && step.Motion != MeleeAttackMotion.CrossCut ? .25f : .18f, SignatureMeleeFx.TrailScale(EquippedWeapon, step.Motion), spriteRenderer);
        }

        private static float MeleeLunge(WeaponFamily family, bool heavy)
        {
            float speed;
            switch (family)
            {
                case WeaponFamily.Dagger:
                    speed = 4.5f;
                    break;
                case WeaponFamily.Katana:
                    speed = 6.5f;
                    break;
                case WeaponFamily.Greatsword:
                    speed = 6f;
                    break;
                default:
                    speed = 5.5f;
                    break;
            }
            return heavy ? speed + 2.5f : speed;
        }

        private void ResolvePendingMelee()
        {
            if (ItemEffectsSuspended || dashing || EquippedWeapon == null)
                return;
            while (pendingMeleeStep != null)
            {
                var step = pendingMeleeStep;
                bool cross = step.Motion == MeleeAttackMotion.CrossCut;
                var contact = cross ? (pendingMeleeStroke == 0 ? MeleeMotionTiming.CrossFirstContact : MeleeMotionTiming.CrossSecondContact) :
                    step.Motion == MeleeAttackMotion.Thrust ? MeleeMotionTiming.ThrustContact : MeleeMotionTiming.Contact;
                if (AttackAnimationProgress < contact)
                    return;
                int stroke = pendingMeleeStroke++;
                if (!cross || pendingMeleeStroke >= 2)
                    pendingMeleeStep = null;
                // Each authored stroke can resolve once, including through pause,
                // hit-stop and late frames. Cancellation removes any pending return.
                ResolveMeleeContact(step, stroke);
            }
        }

        private void ResolveMeleeContact(MeleeAttackStep step, int stroke)
        {
            var profile = MeleeComboCatalog.Get(EquippedWeapon);
            var finisher = activeComboAnimationStep == profile.LightSteps.Length - 1;
            var isHeavy = heavyAttackPose;
            var critical = pendingMeleeCritical;
            MeleeContactsResolved++;
            CombatAudio.PlayWeapon(EquippedWeapon.Family, isHeavy, EquippedWeapon.ArtId);
            var baseDirection = committedMeleeAim;
            var baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            var angle = baseAngle + step.AngleOffset + (step.Motion == MeleeAttackMotion.CrossCut ? MeleeMotionTiming.CrossStrikeOffset(stroke) : 0);
            var radians = angle * Mathf.Deg2Rad;
            var attackDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var center = (Vector2)transform.position + attackDirection * step.Reach;
            var damageSize = step.HitboxSize;
            var animation = GetComponent<PlayerAnimationController>();
            if (animation != null && (step.Motion == MeleeAttackMotion.CrossCut || step.Motion == MeleeAttackMotion.Thrust || step.Motion == MeleeAttackMotion.Sweep || step.Motion == MeleeAttackMotion.Cleave && EquippedWeapon.Family == WeaponFamily.Greatsword))
            {
                var hand = animation.MeleeHandPosition(baseDirection, step.Motion, activeComboAnimationStep, chargedReleasePose, AttackAnimationProgress);
                if (MeleeSweepGeometry.TryEnvelope(step, EquippedWeapon.Family, hand, attackDirection, out var visibleCenter, out var visibleSize, stroke))
                {
                    center = (Vector2)transform.position + visibleCenter;
                    damageSize = visibleSize;
                }
            }
            var cross = step.Motion == MeleeAttackMotion.CrossCut;
            var attackDamage = pendingMeleeDamage * (cross ? .5f : 1f);
            var signature = SignatureMeleeFx.For(EquippedWeapon);
            var hits = Physics2D.OverlapBoxAll(center, damageSize, angle, (1 << GameLayers.Enemy) | (1 << 10));
            var damagedEnemies = new HashSet<EnemyController>();
            bool hitEnemy = false;
            SigOnContact(center, angle, step, heavyAttackPose);
            foreach (var hit in hits)
            {
                var prop = hit.GetComponent<EnvironmentProp>();
                if (prop != null)
                {
                    // A prop's own solid foot stands between the hero and the part of it being swung at. Only something else hides it.
                    var hiding = Physics2D.Linecast(transform.position, hit.ClosestPoint(transform.position), 1 << GameLayers.Ground).collider;
                    if (hiding == null || hiding.transform.IsChildOf(prop.transform))
                        prop.Hit();
                }
                var enemy = EnemyController.DamageTarget(hit);
                if (enemy != null && damagedEnemies.Add(enemy) &&
                    // A tall body's center can project behind the north wall while
                    // its exposed lower edge is reachable. Test the nearest surface;
                    // a fully occluded body remains blocked by the same wall query.
                    Physics2D.Linecast(transform.position, hit.ClosestPoint(transform.position), 1 << GameLayers.Ground).collider == null)
                {
                    // The two contacts belong to one action/target for item marks
                    // and shards; preserve its original aggregate source power.
                    OnDirectCombatHit(enemy, attackAnimationSequence, cross ? pendingMeleeDamage : attackDamage, true, finisher, katanaCounterSucceeded);
                    enemy.MuteNextHitFx();
                    enemy.TakeDamage(attackDamage, attackDirection * step.Knockback * (cross ? .5f : 1f), critical);
                    SigFx.Play(signature.Hit, hit.ClosestPoint(center), angle, SignatureMeleeFx.HitLife, false, false, critical ? 1.3f : 1f);
                    SigOnMeleeHit(enemy, attackDamage, isHeavy, step.Motion);
                    if (cameraFollow != null)
                        cameraFollow.Kick(attackDirection, isHeavy ? .22f : .1f);
                    hitEnemy = true;
                }
            }

            if (IsBodyComboSwing)
            {
                PlayComboStepDust();
                if (hitEnemy)
                    PlayComboImpact(finisher, attackDirection);
            }
            if (isHeavy && hitEnemy && !meleeFinisherImpactPlayed && cameraFollow != null)
            {
                meleeFinisherImpactPlayed = true;
                cameraFollow.MeleeFinisherImpact();
            }
        }

        private void RangedAttack()
        {
            if (!SigShotAllowed())
                return;
            // Holding the trigger through a reload asks every frame; the hero's reload motion is the answer.
            if (inventory == null || !inventory.TryConsumeAmmo())
                return;
            attackTimer = AttackDelay * SigShotDelayMult();
            attackPoseDuration = Mathf.Min(.18f, AttackDelay * .65f);
            attackPoseTimer = attackPoseDuration;
            heavyAttackPose = false;
            attackAnimationSequence++;
            var direction = AimDirection.sqrMagnitude > .01f ? AimDirection.normalized : Vector2.right * facing;
            PrepareRangedShot(direction);
            var counter = CounterStrike();
            var critical = counter || Stats.RollCritical();
            var damage = Damage * (critical ? Stats.CriticalDamage : 1f) * (counter ? CounterBonus : 1f);
            if (sig == null)
                return;
            SigBasicShot(direction, damage, critical);
            SigShotFired(direction);
            CombatAudio.PlayWeapon(EquippedWeapon.Family, false, EquippedWeapon.ArtId);
        }

        private void PrepareRangedShot(Vector2 aim, float presentationDuration = -1f)
        {
            committedShotAim = aim;
            rangedPoseDuration = presentationDuration > 0 ? presentationDuration : attackPoseDuration;
            rangedPoseTimer = rangedPoseDuration;
            RangedPresentationSequence++;
            committedShotOrigin = HeldWeaponView.ShotMuzzlePosition(EquippedWeapon, transform, aim);
            var offset = committedShotOrigin - (Vector2)transform.position;
            var wall = Physics2D.Raycast(transform.position, offset.normalized, offset.magnitude, 1 << GameLayers.Ground);
            shotObstructed = wall.collider != null;
            // A barrel beyond a nearby wall must not emit bullets through it.
            if (shotObstructed)
                committedShotOrigin = wall.point + wall.normal * .02f;
        }
    }
}
