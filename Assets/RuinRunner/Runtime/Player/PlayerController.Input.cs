using UnityEngine;

namespace RuinRunner
{
    /// <summary>Per-frame input, timers and motion state.</summary>
    public sealed partial class PlayerController
    {
        private void Update()
        {
            if (dead || entranceLock || (isPaused != null && isPaused()))
            {
                if (!combatWasPaused)
                {
                    // A menu is a cancellation boundary, not a queue for a hit
                    // or burst to happen after the menu closes. Keep paid clocks.
                    combatWasPaused = true;
                    combatInputNeedsRelease = true;
                    actionCancelSequence++;
                    CancelMeleeAttack();
                    CancelWeaponAction();
                    ItemEffects.CancelTransient();
                    if (body != null)
                        LinearVelocity = Vector2.zero;
                }
                return;
            }
            combatWasPaused = false;
            var outOfCombat = isOutOfCombat != null && isOutOfCombat();
            if (combatClock >= manaLockUntil && (outOfCombat || !HasTalent(TalentId.HunterBreath)))
                Stats.Tick(Time.deltaTime, outOfCombat);
            ReadMovementInput();
            ReadAimInput();
            UpdateTimers();
            TickExtendedActions();
            UpdateDashCharges();

            if (settings.GetKeyDown(GameAction.Dash) && !dashing && dashCharges > 0)
                StartCoroutine(Dash());

            ReadCombatInput();
            ResolvePendingMelee();
            UpdateMotionState();
            UpdateSorting();
        }

        private void UpdateTimers()
        {
            attackTimer -= Time.deltaTime;
            attackPoseTimer -= Time.deltaTime;
            rangedPoseTimer -= Time.deltaTime;
            attackBuffer -= Time.deltaTime;
            specialInputBuffer -= Time.deltaTime;
            hurtPoseTimer -= Time.deltaTime;
            comboResetTimer -= Time.deltaTime;
            weaponActionCooldown -= Time.deltaTime;
            weaponActionTimer -= Time.deltaTime;
            parryTimer -= Time.deltaTime;
            brakeTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
                comboIndex = 0;
        }

        private void ReadMovementInput()
        {
            var previousInput = movementInput;
            var input = Vector2.zero;
            if (settings.GetKey(GameAction.MoveLeft))
                input.x -= 1f;
            if (settings.GetKey(GameAction.MoveRight))
                input.x += 1f;
            if (settings.GetKey(GameAction.MoveDown))
                input.y -= 1f;
            if (settings.GetKey(GameAction.MoveUp))
                input.y += 1f;
            movementInput = Vector2.ClampMagnitude(input, 1f);
            if (movementInput.sqrMagnitude > .01f)
                animationDirection = movementInput.normalized;
            else if (previousInput.sqrMagnitude > .01f)
                brakeTimer = .1f;
            if (Mathf.Abs(movementInput.x) > .01f)
                facing = movementInput.x > 0f ? 1 : -1;
        }

        private void ReadAimInput()
        {
            if (StageValidationInput.Enabled)
            {
                var automatedAim = StageValidationInput.AimDirection;
                if (automatedAim.sqrMagnitude > .01f)
                    AimDirection = automatedAim.normalized;
            }
            else
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    var mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    var direction = (Vector2)(mouseWorld - transform.position);
                    if (direction.sqrMagnitude > .01f)
                        AimDirection = direction.normalized;
                }
            }
            if (Mathf.Abs(AimDirection.x) > .08f)
                facing = AimDirection.x > 0f ? 1 : -1;
        }

        private void ReadCombatInput()
        {
            if (stageClearSafety)
                return;
            if (combatInputNeedsRelease)
            {
                if (!settings.GetKey(GameAction.Attack) && !settings.GetKey(GameAction.Guard))
                    combatInputNeedsRelease = false;
                return;
            }
            if (settings.GetKeyDown(GameAction.Attack) && EquippedWeapon != null &&
                EquippedWeapon.AttackType == WeaponAttackType.Melee)
                attackBuffer = InputBufferSeconds;
            if (settings.GetKeyDown(GameAction.Guard))
                specialInputBuffer = InputBufferSeconds;
            if (dashing)
            {
                CancelWeaponAction();
                return;
            }
            if (EquippedWeapon == null)
            {
                CancelWeaponAction();
                comboIndex = 0;
                return;
            }

            if (EquippedWeapon.AttackType == WeaponAttackType.Ranged)
            {
                IsGuarding = false;
                greatswordCharging = false;
                comboIndex = 0;
                if (TryUniqueInput())
                    return;
                if (settings.GetKey(GameAction.Attack) && attackTimer <= 0f)
                    RangedAttack();
                return;
            }
            if (TryUniqueInput())
                return;

            ReadMeleeWeaponAction();
            if (weaponActionTimer > 0 || greatswordCharging)
                return;

            // Holding primary attack repeats the combo; releasing it never schedules another swing.
            // A short click can still use the existing late-input buffer.
            if ((attackBuffer > 0f || settings.GetKey(GameAction.Attack)) && attackTimer <= 0f && !greatswordCharging)
            {
                attackBuffer = 0f;
                LightComboAttack();
            }
        }

        private void ReadMeleeWeaponAction()
        {
            IsGuarding = false;
            var actionSpec = WeaponActionCatalog.Get(EquippedWeapon);
            switch (EquippedWeapon.Family)
            {
                case WeaponFamily.Katana:
                    if (specialInputBuffer > 0 && weaponActionCooldown <= 0f && CanBeginSpecial() && PayForSkill(actionSpec.Mana))
                    {
                        specialInputBuffer = 0;
                        ItemEffects.OnManaSpent(actionSpec.Mana, AimDirection);
                        CancelMeleeAttack();
                        parryTimer = ParryWindow;
                        weaponActionTimer = .28f;
                        weaponActionCooldown = actionSpec.Cooldown;
                        attackTimer = Mathf.Max(attackTimer, .42f);
                        katanaAutoCutPending = true;
                        katanaAutoTimer = .18f;
                        katanaCounterSucceeded = false;
                        attackAnimationSequence++;
                        parryPresentation = true;
                        ParryPresentationSucceeded = false;
                        GameAudio.Play(GameSound.Block, AudioChannel.Player, .45f);
                    }
                    break;
                case WeaponFamily.Greatsword:
                    if (specialInputBuffer > 0 && weaponActionCooldown <= 0 && CanBeginSpecial() && PayForSkill(actionSpec.Mana))
                    {
                        specialInputBuffer = 0;
                        ItemEffects.OnManaSpent(actionSpec.Mana, AimDirection);
                        CancelMeleeAttack();
                        weaponActionCooldown = actionSpec.Cooldown;
                        greatswordCharging = true;
                        greatswordChargeTimer = 0f;
                        weaponActionTimer = 10f;
                    }
                    if (greatswordCharging && settings.GetKey(GameAction.Guard))
                        greatswordChargeTimer = Mathf.Min(.6f, greatswordChargeTimer + Time.deltaTime);
                    if (greatswordCharging && settings.GetKeyUp(GameAction.Guard))
                    {
                        var profile = MeleeComboCatalog.Get(EquippedWeapon);
                        var charge = Mathf.InverseLerp(.05f, .6f, greatswordChargeTimer);
                        StrongAttack(profile, Mathf.Lerp(.8f, 1.35f, charge));
                        StartCoroutine(ActionStep(AimDirection, Mathf.Lerp(.15f, .65f, charge), .15f, actionCancelSequence));
                        greatswordCharging = false;
                        weaponActionTimer = 0f;
                    }
                    break;
            }
        }

        private void UpdateDashCharges()
        {
            if (dashCharges >= MaxDashes)
            {
                dashRechargeTimer = 0f;
                return;
            }
            dashRechargeTimer -= Time.deltaTime;
            if (dashRechargeTimer > 0f)
                return;
            dashCharges++;
            dashRechargeTimer = dashCharges < MaxDashes ? DashRecharge : 0f;
        }

        private void FixedUpdate()
        {
            if (invincibilityTicks > 0)
                invincibilityTicks--;
            if (body == null || dashing)
                return;
            var inputAllowed = !dead && !entranceLock && (isPaused == null || !isPaused());
            // A basic thrust never takes ownership of locomotion. The player can
            // strafe, retreat or stand still throughout anticipation and recovery.
            var target = inputAllowed && !SigMoveLocked ? movementInput * Stats.MoveSpeed : Vector2.zero;
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, 22f * Time.fixedDeltaTime);
            var desired = target + knockbackVelocity;
            var acceleration = movementInput.sqrMagnitude > .01f ? 44f : 64f;
            LinearVelocity = Vector2.MoveTowards(LinearVelocity, desired, acceleration * Time.fixedDeltaTime);
            StopAtEnemyBodies(desired);
        }

        private void UpdateMotionState()
        {
            if (dashing)
                MotionState = PlayerMotionState.Dash;
            else if (greatswordCharging)
                MotionState = PlayerMotionState.Charge;
            else if (IsGuarding || IsParrying)
                MotionState = PlayerMotionState.Guard;
            else if (attackPoseTimer > 0f || weaponActionTimer > 0f)
                MotionState = heavyAttackPose ? PlayerMotionState.HeavyAttack : PlayerMotionState.Attack;
            else if (hurtPoseTimer > 0f)
                MotionState = PlayerMotionState.Hurt;
            else if (movementInput.sqrMagnitude > .01f)
                MotionState = PlayerMotionState.Run;
            else if (brakeTimer > 0f)
                MotionState = PlayerMotionState.Brake;
            else
                MotionState = PlayerMotionState.Idle;
        }

        private void UpdateSorting()
        {
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
        }
    }
}
