using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Top-down XY motor and weapon-family combat state machine.</summary>
    public sealed partial class PlayerController : MonoBehaviour
    {
        public PlayerStats Stats { get; private set; }
        public WeaponDefinition EquippedWeapon { get; private set; }
        public float MaxHealth => Stats == null ? 0f : Stats.MaxHealth;
        public float Health => Stats == null ? 0f : Stats.Health;
        public float MaxMana => Stats == null ? 0f : Stats.MaxMana;
        public float Mana => Stats == null ? 0f : Stats.Mana;
        public float Damage => Stats == null || EquippedWeapon == null ? 0f : EquippedWeapon.AttackPower * Stats.WeaponDamageMultiplier * TalentDamage;
        public float AttackDelay => Mathf.Max(.12f, .38f * weaponAttackDelayMultiplier);
        public int Facing => facing;
        public Vector2 AimDirection { get; private set; }
        public Vector2 MovementInput => movementInput;
        public Vector2 DashDirection { get; private set; } = Vector2.right;
        public bool IsDashing => dashing;
        public bool IsMeleeSwingActive => EquippedWeapon != null && EquippedWeapon.AttackType == WeaponAttackType.Melee && attackPoseTimer > 0f;
        public bool IsRangedShotActive => EquippedWeapon != null && EquippedWeapon.AttackType == WeaponAttackType.Ranged && attackPoseTimer > 0f;
        public Vector2 WeaponAimDirection => IsRangedPresentationActive ? committedShotAim : MeleeAimDirection;
        public bool IsRangedPresentationActive => EquippedWeapon != null && EquippedWeapon.AttackType == WeaponAttackType.Ranged && rangedPoseTimer > 0f;
        public const float HurtPresentationSeconds = .18f;
        public float HurtPresentationProgress => Mathf.Clamp01(1f-Mathf.Max(0f,hurtPoseTimer)/HurtPresentationSeconds);
        public float RangedPresentationProgress => rangedPoseDuration <= 0f ? 1f : Mathf.Clamp01(1f - Mathf.Max(0f,rangedPoseTimer)/rangedPoseDuration);
        public int RangedPresentationSequence { get; private set; }
        public float MeleeAngleOffset => meleeAngleOffset;
        public bool IsThrustActive => IsMeleeSwingActive && activeMeleeMotion == MeleeAttackMotion.Thrust;
        public MeleeAttackMotion ActiveMeleeMotion => activeMeleeMotion;
        public Vector2 MeleeAimDirection => IsMeleeSwingActive ? committedMeleeAim : AimDirection;
        public bool AnimationPaused => dead || (isPaused != null && isPaused());
        public Vector2 FeetPoint => (Vector2)transform.position + Vector2.down * .28f;
        public bool IsDead => dead;
        public bool ItemEffectsSuspended => AnimationPaused || stageClearSafety;
        public ItemCombatEffects ItemEffects { get; private set; }
        public float DeathProgress { get; private set; }
        public int MeleeContactsResolved { get; private set; }
        public Vector2 AnimationDirection => animationDirection.sqrMagnitude > .01f ? animationDirection : AimDirection;
        public float AttackAnimationProgress => attackPoseDuration <= 0f
            ? 1f
            : Mathf.Clamp01(1f - Mathf.Max(0f, attackPoseTimer) / attackPoseDuration);
        public PlayerMotionState MotionState { get; private set; }
        public int DashCharges => dashCharges;
        public float DashRechargeProgress => dashCharges >= MaxDashes ? 1f : 1f - dashRechargeTimer / DashRecharge;
        public WeaponSlotState ActiveWeaponState => inventory == null ? null : inventory.ActiveSlot;
        public int DashCapacity => MaxDashes;
        public int WeaponUpgradeLevel => ActiveWeaponState?.Item?.UpgradeLevel ?? 0;
        public bool IsGuarding { get; private set; }
        public bool IsParrying => parryTimer > 0f;
        // Presentation follows the real action lifetime, including the successful
        // deflection's recovery. These properties never extend the defense window.
        public bool IsParryPresentationActive => parryPresentation && weaponActionTimer > 0f && !IsMeleeSwingActive;
        public bool ParryPresentationSucceeded { get; private set; }
        public float ParryPresentationProgress => Mathf.Clamp01(1f-weaponActionTimer/(ParryPresentationSucceeded?.22f:.28f));
        public bool IsChargedCleaveActive => IsMeleeSwingActive && chargedReleasePose;
        public bool IsGreatswordCharging => greatswordCharging;
        public float GreatswordChargeProgress => Mathf.Clamp01(greatswordChargeTimer/.6f);
        public bool IsWeaponActionActive => IsGuarding || IsParrying || greatswordCharging || weaponActionTimer > 0f;
        public int ActiveComboAnimationStep => activeComboAnimationStep;
        public int AttackAnimationSequence => attackAnimationSequence;
        public int InvincibilityTicksRemaining => invincibilityTicks;
        public const int HitInvincibilityTicks = GameTick.TicksPerSecond;

        private const int MaxStoredDashes = 2;
        private const float InputBufferSeconds = .16f;
        private const float DashRechargeSeconds = 1.45f;
        private const float DashDuration = .16f;
        private const float DashDistance = 2.55f;
        private const float ParryWindow = .16f;

        private Rigidbody2D body;
        private Collider2D playerCollider;
        private SpriteRenderer spriteRenderer;
        internal SpriteRenderer BodyRenderer => spriteRenderer;
        private Func<bool> isPaused;
        private Func<bool> isOutOfCombat;
        private Action onDied;
        private CameraFollow cameraFollow;
        private InventorySystem inventory;
        private GameSettingsService settings;
        private float weaponAttackDelayMultiplier = 1f;
        private float attackTimer;
        private float attackPoseTimer;
        private float attackPoseDuration;
        private float attackBuffer;
        private bool meleeFinisherImpactPlayed;
        private bool combatWasPaused, combatInputNeedsRelease;
        private MeleeAttackStep pendingMeleeStep;
        private int pendingMeleeStroke;
        private MeleeAttackMotion activeMeleeMotion;
        private Vector2 committedMeleeAim;
        private Vector2 committedShotAim = Vector2.right;
        private Vector2 committedShotOrigin;
        private bool shotObstructed;
        private float rangedPoseTimer,rangedPoseDuration;
        private float pendingMeleeDamage;
        private bool pendingMeleeCritical;
        private float meleeAngleOffset;
        private float brakeTimer;
        private float hurtPoseTimer;
        private float comboResetTimer;
        private float dashRechargeTimer;
        private float weaponActionCooldown;
        private float weaponActionTimer;
        private float parryTimer;
        private float greatswordChargeTimer;
        private Vector2 movementInput;
        private Vector2 animationDirection = Vector2.right;
        private Vector2 knockbackVelocity;
        private int dashCharges = MaxStoredDashes;
        private int invincibilityTicks;
        private Coroutine damageFlashRoutine;
        private int comboIndex;
        private int activeComboAnimationStep;
        private int attackAnimationSequence;
        private int actionCancelSequence;
        private int facing = 1;
        private bool dashing;
        private bool greatswordCharging;
        private bool heavyAttackPose;
        private bool parryPresentation;
        private bool chargedReleasePose;
        private bool dead;
        private bool stageClearSafety;
        private readonly Color normalColor = new Color(.2f, .85f, 1f);
        private bool usesPixelArt;
        private Color RestingColor => usesPixelArt ? Color.white : normalColor;

        private Vector2 LinearVelocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return body.linearVelocity;
#else
                return body.velocity;
#endif
            }
            set
            {
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity = value;
#else
                body.velocity = value;
#endif
            }
        }

        public static PlayerController Create(Vector2 position, Func<bool> pauseQuery, Func<bool> outOfCombatQuery, Action deathCallback, CameraFollow camera, InventorySystem playerInventory, GameSettingsService gameSettings)
        {
            Physics2D.IgnoreLayerCollision(GameLayers.Player, GameLayers.Enemy, true);
            var playerObject = new GameObject("Player");
            playerObject.layer = GameLayers.Player;
            playerObject.transform.position = position;

            var collider = playerObject.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Horizontal;
            collider.size = new Vector2(.68f, .46f);
            collider.offset = new Vector2(0f, -.28f);
            collider.sharedMaterial = GameLayers.Frictionless;

            var rigidbody = playerObject.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0f;
            rigidbody.freezeRotation = true;
            rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(playerObject.transform);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localScale = Vector3.one;
            var renderer = PrototypeVisuals.AddSprite(visualRoot, new Color(.2f, .85f, 1f), 10);

            var controller = playerObject.AddComponent<PlayerController>();
            var details = controller.AddFace(visualRoot.transform);
            controller.Initialize(rigidbody, renderer, pauseQuery, outOfCombatQuery, deathCallback, camera, playerInventory, gameSettings);
            playerObject.AddComponent<PlayerAnimationController>().Initialize(controller, visualRoot.transform, renderer, details);
            playerObject.AddComponent<HeldWeaponView>().Initialize(controller);
            playerObject.AddComponent<DashAfterimageEffect>().Initialize(controller, renderer);
            playerObject.AddComponent<PlayerFootfalls>().Initialize(controller);
            playerObject.AddComponent<WeaponPreparationEffect>().Initialize(controller);
            playerObject.AddComponent<PlayerResourceDisplay>().Initialize(controller);
            return controller;
        }

        private void Initialize(Rigidbody2D rigidbody, SpriteRenderer renderer, Func<bool> pauseQuery, Func<bool> outOfCombatQuery, Action deathCallback, CameraFollow camera, InventorySystem playerInventory, GameSettingsService gameSettings)
        {
            body = rigidbody;
            playerCollider = GetComponent<Collider2D>();
            spriteRenderer = renderer;
            usesPixelArt = IndividualArt.Exists(PlayerAnimationController.ArtRoot+"idle/down/00");
            spriteRenderer.color = RestingColor;
            NativeCombatEffect.Frames("PlayerHit");
            isPaused = pauseQuery;
            isOutOfCombat = outOfCombatQuery;
            onDied = deathCallback;
            cameraFollow = camera;
            inventory = playerInventory;
            settings = gameSettings;
            Stats = new PlayerStats();
            Stats.BindInventory(inventory);
            ItemEffects = gameObject.AddComponent<ItemCombatEffects>();
            ItemEffects.Initialize(this, inventory);
            ActiveCombatants.Add(this);
            if(inventory!=null)inventory.ReloadCompleted+=OnCombatReload;
            if (inventory != null) inventory.Changed += OwnedEquipmentChanged;
            MotionState = PlayerMotionState.Idle;
            AimDirection = Vector2.right;
            activeComboAnimationStep = -1;
        }

        void OwnedEquipmentChanged(InventoryChangeKind change)
        {
            ItemEffects.ReconcileOwnedEffects();
            if (!ReferenceEquals(EquippedWeapon, inventory.EquippedWeapon)) EquipWeapon(inventory.EquippedWeapon);
        }
        void OnDestroy()
        {
            ActiveCombatants.Remove(this);
            if(inventory!=null)inventory.ReloadCompleted-=OnCombatReload;
            if (inventory != null) inventory.Changed -= OwnedEquipmentChanged;
            Stats?.Dispose();
        }

        private GameObject AddFace(Transform visualRoot)
        {
            var eye = new GameObject("FallbackFace");
            eye.transform.SetParent(visualRoot);
            eye.transform.localPosition = new Vector3(.23f, .18f, 0f);
            eye.transform.localScale = new Vector3(.12f, .15f, 1f);
            PrototypeVisuals.AddSprite(eye, new Color(.02f, .05f, .09f), 11);
            return eye;
        }

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
                    if (body != null) LinearVelocity = Vector2.zero;
                }
                return;
            }
            combatWasPaused = false;
            var outOfCombat = isOutOfCombat != null && isOutOfCombat();
            if (combatClock >= manaLockUntil && (outOfCombat || !HasTalent(TalentId.HunterBreath))) Stats.Tick(Time.deltaTime, outOfCombat);
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
            if (comboResetTimer <= 0f) comboIndex = 0;
        }

        private void ReadMovementInput()
        {
            var previousInput = movementInput;
            var input = Vector2.zero;
            if (settings.GetKey(GameAction.MoveLeft)) input.x -= 1f;
            if (settings.GetKey(GameAction.MoveRight)) input.x += 1f;
            if (settings.GetKey(GameAction.MoveDown)) input.y -= 1f;
            if (settings.GetKey(GameAction.MoveUp)) input.y += 1f;
            movementInput = Vector2.ClampMagnitude(input, 1f);
            if (movementInput.sqrMagnitude > .01f)
                animationDirection = movementInput.normalized;
            else if (previousInput.sqrMagnitude > .01f)
                brakeTimer = .1f;
            if (Mathf.Abs(movementInput.x) > .01f) facing = movementInput.x > 0f ? 1 : -1;
        }

        private void ReadAimInput()
        {
            if (StageValidationInput.Enabled)
            {
                var automatedAim = StageValidationInput.AimDirection;
                if (automatedAim.sqrMagnitude > .01f) AimDirection = automatedAim.normalized;
            }
            else
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    var mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    var direction = (Vector2)(mouseWorld - transform.position);
                    if (direction.sqrMagnitude > .01f) AimDirection = direction.normalized;
                }
            }
            if (Mathf.Abs(AimDirection.x) > .08f) facing = AimDirection.x > 0f ? 1 : -1;
        }

        private void ReadCombatInput()
        {
            if (stageClearSafety) return;
            if (combatInputNeedsRelease)
            {
                if (!settings.GetKey(GameAction.Attack) && !settings.GetKey(GameAction.Guard))
                    combatInputNeedsRelease = false;
                return;
            }
            if (settings.GetKeyDown(GameAction.Attack) && EquippedWeapon != null &&
                EquippedWeapon.AttackType == WeaponAttackType.Melee)
                attackBuffer = InputBufferSeconds;
            if(settings.GetKeyDown(GameAction.Guard))specialInputBuffer=InputBufferSeconds;
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
                if(TryUniqueInput())return;
                if (settings.GetKey(GameAction.Attack) && attackTimer <= 0f) RangedAttack();
                return;
            }
            if(TryUniqueInput())return;

            ReadMeleeWeaponAction();
            if(weaponActionTimer>0||greatswordCharging)return;

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
            var actionSpec=WeaponActionCatalog.Get(EquippedWeapon);
            switch (EquippedWeapon.Family)
            {
                case WeaponFamily.Katana:
                    if (specialInputBuffer>0 && weaponActionCooldown <= 0f&&CanBeginSpecial()&&PayForSkill(actionSpec.Mana))
                    {
                        specialInputBuffer=0;ItemEffects.OnManaSpent(actionSpec.Mana,AimDirection);
                        CancelMeleeAttack();
                        parryTimer = ParryWindow;
                        weaponActionTimer = .28f;
                        weaponActionCooldown = actionSpec.Cooldown;
                        attackTimer=Mathf.Max(attackTimer,.42f);katanaAutoCutPending=true;katanaAutoTimer=.18f;katanaCounterSucceeded=false;
                        attackAnimationSequence++;
                        parryPresentation = true;
                        ParryPresentationSucceeded = false;
                        PrototypeAudio.Play(PrototypeSound.Block, AudioChannel.Player, .45f);
                    }
                    break;
                case WeaponFamily.Greatsword:
                    if (specialInputBuffer>0&&weaponActionCooldown<=0 && CanBeginSpecial()&&PayForSkill(actionSpec.Mana))
                    {
                        specialInputBuffer=0;ItemEffects.OnManaSpent(actionSpec.Mana,AimDirection);CancelMeleeAttack();weaponActionCooldown=actionSpec.Cooldown;
                        greatswordCharging = true;
                        greatswordChargeTimer = 0f;
                        weaponActionTimer = 10f;
                    }
                    if (greatswordCharging && settings.GetKey(GameAction.Guard))
                        greatswordChargeTimer = Mathf.Min(.6f, greatswordChargeTimer + Time.deltaTime);
                    if (greatswordCharging && settings.GetKeyUp(GameAction.Guard))
                    {
                        var profile = MeleeComboCatalog.Get(EquippedWeapon);
                        var charge = Mathf.InverseLerp(.05f,.6f, greatswordChargeTimer);
                        StrongAttack(profile, Mathf.Lerp(.8f, 1.35f, charge));
                        StartCoroutine(ActionStep(AimDirection,Mathf.Lerp(.15f,.65f,charge),.15f,actionCancelSequence));
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
            if (dashRechargeTimer > 0f) return;
            dashCharges++;
            dashRechargeTimer = dashCharges < MaxDashes ? DashRecharge : 0f;
        }

        private void FixedUpdate()
        {
            if (invincibilityTicks > 0) invincibilityTicks--;
            if (body == null || dashing) return;
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
            if (dashing) MotionState = PlayerMotionState.Dash;
            else if (greatswordCharging) MotionState = PlayerMotionState.Charge;
            else if (IsGuarding || IsParrying) MotionState = PlayerMotionState.Guard;
            else if (attackPoseTimer > 0f || weaponActionTimer > 0f)
                MotionState = heavyAttackPose ? PlayerMotionState.HeavyAttack : PlayerMotionState.Attack;
            else if (hurtPoseTimer > 0f) MotionState = PlayerMotionState.Hurt;
            else if (movementInput.sqrMagnitude > .01f) MotionState = PlayerMotionState.Run;
            else if (brakeTimer > 0f) MotionState = PlayerMotionState.Brake;
            else MotionState = PlayerMotionState.Idle;
        }

        private void UpdateSorting()
        {
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private IEnumerator Dash()
        {
            CancelMeleeAttack();
            CancelWeaponAction();
            actionCancelSequence++;
            var sequence = actionCancelSequence;
            dashCharges--;
            if (dashRechargeTimer <= 0f) dashRechargeTimer = DashRecharge;
            dashing = true;
            invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(HasTalent(TalentId.PhantomStep) ? .48f : .24f));
            var direction = movementInput.sqrMagnitude > .01f
                ? movementInput.normalized
                : AimDirection.sqrMagnitude > .01f ? AimDirection.normalized : Vector2.right * facing;
            DashDirection = direction;
            var distance = GetSafeTravelDistance(direction, DashDistance);
            var start = body.position;
            var cursor=start;
            float previousProgress=0;
            ItemEffects.OnDashStarted(start);
            TalentOnDashStart(start);
            LinearVelocity = Vector2.zero;
            PrototypeAudio.Play(PrototypeSound.Dash, AudioChannel.Player, .55f);
            // A grounded native dust kick stays behind the body. Foreground
            // square sparks used to cover the face during the first dash frames.
            var dust=NativeCombatEffect.Create("GroundDust",start-direction*.15f+Vector2.down*.06f,
                0,.75f,.28f,Color.white);
            if(dust!=null)dust.BelowActor(spriteRenderer,-3);
            var elapsed = 0f;
            while (elapsed < DashDuration && sequence == actionCancelSequence && !AnimationPaused)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / DashDuration);
                float eased=1f-(1f-progress)*(1f-progress);
                float step=distance*(eased-previousProgress);previousProgress=eased;
                // Cast from the actual foot collider on every segment; turns cannot cross corners.
                step=GetSafeTravelDistance(direction,step);cursor=body.position+direction*step;
                // Resolve each render step immediately. Repeated MovePosition calls
                // before a fixed tick would overwrite travel and shorten the dash.
                body.position=cursor;
                yield return null;
                if (sequence == actionCancelSequence && !AnimationPaused) { ItemEffects.OnDashMoved(body.position); TalentOnDashStep(body.position); }
            }
            dashing = false;
            ItemEffects.OnDashEnded();
            spriteRenderer.color = RestingColor;
        }

        private float GetSafeTravelDistance(Vector2 direction, float desiredDistance)
        {
            if (playerCollider == null) return desiredDistance;
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
            katanaCounterSucceeded=false;
            var profile = MeleeComboCatalog.Get(EquippedWeapon);
            if (profile.LightSteps == null || profile.LightSteps.Length == 0) return;
            var stepIndex = Mathf.Clamp(comboIndex, 0, profile.LightSteps.Length - 1);
            activeComboAnimationStep = stepIndex;
            PerformMeleeAttack(ApplyFinisherMutation(profile.LightSteps[stepIndex],stepIndex==profile.LightSteps.Length-1), 1f, false);
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
            if (step == null) return;
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
            pendingMeleeStroke=0;
            pendingMeleeCritical = Stats.RollCritical();
            pendingMeleeDamage = Damage * step.DamageMultiplier * damageMultiplier * SigMeleeMult() * (pendingMeleeCritical ? Stats.CriticalDamage : 1f);
            // Odd combo steps swing clockwise on screen, the way the trail is drawn; the rest turn the other way.
            SignatureSlashEffect.Create(this,SignatureMeleeFx.Trail(EquippedWeapon,step.Motion,isHeavy),Mathf.Atan2(committedMeleeAim.y,committedMeleeAim.x)*Mathf.Rad2Deg+step.AngleOffset,
                step.Motion==MeleeAttackMotion.Sweep&&(bodyCombo?MeleeComboFrames.TrailMirrored(EquippedWeapon.Family,activeComboAnimationStep,committedMeleeAim.x<0):isHeavy||(activeComboAnimationStep&1)==0),
                step.Motion==MeleeAttackMotion.Thrust?MeleeMotionTiming.ThrustStart:bodyCombo&&step.Motion!=MeleeAttackMotion.CrossCut?.25f:.18f,SignatureMeleeFx.TrailScale(EquippedWeapon,step.Motion),spriteRenderer);
        }

        private static float MeleeLunge(WeaponFamily family, bool heavy)
        {
            float speed;
            switch (family)
            {
                case WeaponFamily.Dagger: speed = 4.5f; break;
                case WeaponFamily.Katana: speed = 6.5f; break;
                case WeaponFamily.Greatsword: speed = 6f; break;
                default: speed = 5.5f; break;
            }
            return heavy ? speed + 2.5f : speed;
        }

        private void ResolvePendingMelee()
        {
            if (ItemEffectsSuspended || dashing || EquippedWeapon == null) return;
            while(pendingMeleeStep!=null)
            {
                var step=pendingMeleeStep;bool cross=step.Motion==MeleeAttackMotion.CrossCut;
                var contact=cross?(pendingMeleeStroke==0?MeleeMotionTiming.CrossFirstContact:MeleeMotionTiming.CrossSecondContact):
                    step.Motion==MeleeAttackMotion.Thrust?MeleeMotionTiming.ThrustContact:MeleeMotionTiming.Contact;
                if(AttackAnimationProgress<contact)return;
                int stroke=pendingMeleeStroke++;
                if(!cross||pendingMeleeStroke>=2)pendingMeleeStep=null;
                // Each authored stroke can resolve once, including through pause,
                // hit-stop and late frames. Cancellation removes any pending return.
                ResolveMeleeContact(step,stroke);
            }
        }

        private void ResolveMeleeContact(MeleeAttackStep step,int stroke)
        {
            var profile = MeleeComboCatalog.Get(EquippedWeapon);
            var finisher = activeComboAnimationStep == profile.LightSteps.Length - 1;
            var isHeavy = heavyAttackPose;
            var critical = pendingMeleeCritical;
            MeleeContactsResolved++;
            CombatAudio.PlayWeapon(EquippedWeapon.Family,isHeavy,EquippedWeapon.Id);
            var baseDirection = committedMeleeAim;
            var baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            var angle = baseAngle + step.AngleOffset+(step.Motion==MeleeAttackMotion.CrossCut?MeleeMotionTiming.CrossStrikeOffset(stroke):0);
            var radians = angle * Mathf.Deg2Rad;
            var attackDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var center = (Vector2)transform.position + attackDirection * step.Reach;
            var damageSize=step.HitboxSize;
            var animation=GetComponent<PlayerAnimationController>();
            if(animation!=null&&(step.Motion==MeleeAttackMotion.CrossCut||step.Motion==MeleeAttackMotion.Thrust||step.Motion==MeleeAttackMotion.Sweep||step.Motion==MeleeAttackMotion.Cleave&&EquippedWeapon.Family==WeaponFamily.Greatsword))
            {
                var hand=animation.MeleeHandPosition(baseDirection,step.Motion,activeComboAnimationStep,chargedReleasePose,AttackAnimationProgress);
                if(MeleeSweepGeometry.TryEnvelope(step,EquippedWeapon.Family,hand,attackDirection,out var visibleCenter,out var visibleSize,stroke))
                {center=(Vector2)transform.position+visibleCenter;damageSize=visibleSize;}
            }
            var cross=step.Motion==MeleeAttackMotion.CrossCut;
            var attackDamage=pendingMeleeDamage*(cross?.5f:1f);
            var signature = SignatureMeleeFx.For(EquippedWeapon);
            var hits = Physics2D.OverlapBoxAll(center, damageSize, angle, (1 << GameLayers.Enemy) | (1 << 10));
            var damagedEnemies = new HashSet<EnemyController>();
            bool hitEnemy = false;
            SigOnContact(center,angle,step,heavyAttackPose);
            foreach (var hit in hits)
            {
                var prop = hit.GetComponent<EnvironmentProp>();
                if (prop != null)
                {
                    // A prop's own solid foot stands between the hero and the part of it being swung at. Only something else hides it.
                    var hiding = Physics2D.Linecast(transform.position, hit.ClosestPoint(transform.position), 1 << GameLayers.Ground).collider;
                    if (hiding == null || hiding.transform.IsChildOf(prop.transform)) prop.Hit();
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
                    OnDirectCombatHit(enemy, attackAnimationSequence, cross?pendingMeleeDamage:attackDamage,true,finisher,katanaCounterSucceeded);
                    enemy.MuteNextHitFx();
                    enemy.TakeDamage(attackDamage, attackDirection * step.Knockback*(cross?.5f:1f), critical);
                    SigFx.Play(signature.Hit, hit.ClosestPoint(center), angle, SignatureMeleeFx.HitLife, false, false, critical ? 1.3f : 1f);
                    SigOnMeleeHit(enemy, attackDamage, isHeavy, step.Motion);
                    if (cameraFollow != null) cameraFollow.Kick(attackDirection, isHeavy ? .22f : .1f);
                    hitEnemy = true;
                }
            }

            if (IsBodyComboSwing)
            {
                PlayComboStepDust();
                if (hitEnemy) PlayComboImpact(finisher, attackDirection);
            }
            if (isHeavy && hitEnemy && !meleeFinisherImpactPlayed && cameraFollow != null)
            {
                meleeFinisherImpactPlayed = true;
                cameraFollow.MeleeFinisherImpact();
            }
        }

        private void RangedAttack()
        {
            if (!SigShotAllowed()) return;
            // Holding the trigger through a reload asks every frame; the hero's reload motion is the answer.
            if (inventory == null || !inventory.TryConsumeAmmo()) return;
            attackTimer = AttackDelay * SigShotDelayMult();
            attackPoseDuration = Mathf.Min(.18f, AttackDelay * .65f);
            attackPoseTimer = attackPoseDuration;
            heavyAttackPose = false;
            attackAnimationSequence++;
            var direction = AimDirection.sqrMagnitude > .01f ? AimDirection.normalized : Vector2.right * facing;
            PrepareRangedShot(direction);
            var critical = Stats.RollCritical();
            var damage = Damage * (critical ? Stats.CriticalDamage : 1f);
            if (sig == null) return;
            SigBasicShot(direction, damage, critical);
            SigShotFired(direction);
            CombatAudio.PlayWeapon(EquippedWeapon.Family, false, EquippedWeapon.Id);
        }

        private void PrepareRangedShot(Vector2 aim,float presentationDuration=-1f)
        {
            committedShotAim=aim;
            rangedPoseDuration=presentationDuration>0?presentationDuration:attackPoseDuration;
            rangedPoseTimer=rangedPoseDuration;
            RangedPresentationSequence++;
            committedShotOrigin=HeldWeaponView.ShotMuzzlePosition(EquippedWeapon,transform,aim);
            var offset=committedShotOrigin-(Vector2)transform.position;
            var wall=Physics2D.Raycast(transform.position,offset.normalized,offset.magnitude,1<<GameLayers.Ground);
            shotObstructed=wall.collider!=null;
            // A barrel beyond a nearby wall must not emit bullets through it.
            if(shotObstructed)committedShotOrigin=wall.point+wall.normal*.02f;
        }

        public bool SandboxInvulnerable { get; set; }
        internal void SandboxRefresh() { sigReady = 0; if (sig == null || sig.Id != "siege_engineer" || sig.Stage != 1) sigLockUntil = 0; attackTimer = 0; weaponActionCooldown = 0; Stats.RestoreMana(Stats.MaxMana); }

        // Held still while the ink has them, without pausing the world around them.
        bool entranceLock;
        public void SetEntranceLock(bool locked)
        {
            entranceLock = locked;
            if (!locked) return;
            movementInput = Vector2.zero;
            CancelMeleeAttack();
            CancelWeaponAction();
        }

        /// <summary>Nothing of the hero is drawn while the ink holds them, and their weapon stays gone until it has run off.</summary>
        public void HideForInk(bool body, bool weapon)
        {
            var view = GetComponentInChildren<HeldWeaponView>(true);
            var held = view == null ? null : view.VisualRenderer;
            foreach (var part in GetComponentsInChildren<Renderer>(true)) part.forceRenderingOff = part == held ? weapon : body;
        }

        public void ApplyPush(Vector2 velocity) { if (!dead && !dashing) knockbackVelocity += velocity; }

        public void TakeDamage(float amount, Vector2 direction)
        {
            if (SandboxInvulnerable) return;
            if (amount > 0f && !dead && dashing && invincibilityTicks > 0) TalentOnDodge();
            if (amount <= 0f || dead || entranceLock || stageClearSafety || invincibilityTicks > 0 || (isPaused != null && isPaused())) return;
            if (SigBlocks(direction)) return;
            if (IsParrying && EquippedWeapon != null && EquippedWeapon.Family == WeaponFamily.Katana)
            {
                parryTimer = 0f;
                weaponActionTimer = .22f;
                ParryPresentationSucceeded = true;
                invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(.22f));
                katanaAutoTimer=.04f;katanaAutoCutPending=true;katanaCounterSucceeded=true;
                OnActiveDefense();
                PrototypeAudio.Play(PrototypeSound.Block, AudioChannel.Player, .6f);
                if (sig != null) SigOnParry();
                if (cameraFollow != null) cameraFollow.Shake(.12f);
                return;
            }

            var received = ItemEffects.AbsorbDamage(ItemEffects.AbsorbManaAegis(amount));
            var shielded = received <= 0f;
            if (received > 0f){bloodStacks=0;Stats.ReceiveDamage(received);Grow(CombatGrowthKind.DamageTaken);ItemEffects.OnHurt();}
            if (StageValidationInput.Enabled) Debug.Log("[Combat Input] Hit raw="+amount+" "+" hp="+Health.ToString("0.0")+" position="+transform.position+" motion="+MotionState);
            invincibilityTicks = HitInvincibilityTicks;
            hurtPoseTimer = shielded ? 0f : HurtPresentationSeconds;
            // A shield that fully absorbed the hit holds the character steady.
            // Partial absorption keeps the existing damage/knockback response.
            knockbackVelocity += !shielded && direction.sqrMagnitude > .01f ? direction.normalized * 5f : Vector2.zero;
            if(!shielded)
            {
                var towardAttacker = direction.sqrMagnitude > .01f ? -direction.normalized : AimDirection;
                var contact = (Vector2)transform.position + towardAttacker * .35f + Vector2.up * .08f;
                NativeCombatEffect.Create("PlayerHit",contact,0,29f/32f,.18f,Color.white,
                    spriteRenderer.sortingOrder-3)?.BelowActor(spriteRenderer,-3);
            }
            PrototypeAudio.Play(shielded ? PrototypeSound.Block : PrototypeSound.Hurt,
                AudioChannel.Player, shielded ? .4f : .6f);
            if (cameraFollow != null) cameraFollow.Shake(shielded ? .035f : .2f);
            if (damageFlashRoutine != null) StopCoroutine(damageFlashRoutine);
            var flash = shielded ? new Color(.62f,.9f,1f) : new Color(1f,.62f,.62f);
            damageFlashRoutine = StartCoroutine(DamageFlash(flash, shielded ? .06f : .1f));
            if (Health <= 0f && ItemEffects.TryCheatDeath()) invincibilityTicks = HitInvincibilityTicks * 2;
            if (Health <= 0f) TalentRevive();
            if (Health <= 0f)
            {
                dead = true;
                actionCancelSequence++;
                ItemEffects.CancelTransient();
                CancelMeleeAttack();
                CancelWeaponAction();
                StopAllCoroutines();
                damageFlashRoutine = null;
                LinearVelocity=Vector2.zero;movementInput=Vector2.zero;knockbackVelocity=Vector2.zero;
                spriteRenderer.enabled=true;spriteRenderer.color=RestingColor;
                onDied?.Invoke(); // Account for the defeat immediately, even if the user exits during the pose.
                StartCoroutine(DeathSequence());
            }
        }

        private IEnumerator DeathSequence()
        {
            var elapsed=0f;
            while(elapsed<.85f)
            {
                elapsed+=Time.unscaledDeltaTime;
                DeathProgress=Mathf.Clamp01(elapsed/.85f);
                yield return null;
            }
        }

        private IEnumerator DamageFlash(Color flash, float flashSeconds)
        {
            // A player must remain locatable while moving through danger. Pulse
            // the visible body/weapon together instead of hiding both renderers.
            // Use the existing immunity clock so pause and slow motion agree.
            spriteRenderer.enabled = true;
            while (invincibilityTicks > 0 && !dead)
            {
                if (!AnimationPaused)
                {
                    var elapsed = (HitInvincibilityTicks - Mathf.Min(HitInvincibilityTicks, invincibilityTicks)) * GameTick.Duration;
                    var tint = elapsed < flashSeconds ? flash : RestingColor;
                    if (elapsed >= flashSeconds) tint.a = .86f + .14f * Mathf.Cos(elapsed * Mathf.PI * 10f);
                    spriteRenderer.color = tint;
                }
                yield return null;
            }
            spriteRenderer.enabled = true;
            spriteRenderer.color = RestingColor;
            damageFlashRoutine = null;
        }

        public void Teleport(Vector2 position)
        {
            ItemEffects.CancelTransient();
            GetComponent<DashAfterimageEffect>()?.CancelTransient();
            actionCancelSequence++;
            CancelMeleeAttack();
            CancelWeaponAction();
            transform.position = position;
            if (body != null) body.position = position;
            LinearVelocity = Vector2.zero;
            movementInput = Vector2.zero;
            knockbackVelocity = Vector2.zero;
        }

        public void CancelCombatForPresentation()
        {
            actionCancelSequence++;
            observedCancelSequence=actionCancelSequence;
            combatInputNeedsRelease=true;
            CancelMeleeAttack();CancelWeaponAction();
            ClearPlantedRound();
            ItemEffects?.CancelTransient();
            GetComponent<DashAfterimageEffect>()?.CancelTransient();
            dashing=false;movementInput=Vector2.zero;knockbackVelocity=Vector2.zero;
            LinearVelocity=Vector2.zero;
            // Paid cooldowns, dash charges, mana and each owned magazine/reload
            // deliberately survive a camera presentation or encounter transition.
        }

        public void SetStageClearSafety(bool enabled)
        {
            stageClearSafety = enabled;
            if (!enabled) return;
            actionCancelSequence++;
            ItemEffects.CancelTransient();
            invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.TicksPerSecond * 2);
            CancelMeleeAttack();
            CancelWeaponAction();
        }

        public void ApplyBalance(PlayerBalanceData balance)
        {
            if (Stats == null) return;
            Stats.ApplyBalance(balance);
            inventory?.RefreshWeaponBalance();
            if (EquippedWeapon == null) return;
            weaponAttackDelayMultiplier = EquippedWeapon.AttackDelayMultiplier;
        }

        public void EquipWeapon(WeaponDefinition weapon)
        {
            var swappedFrom=EquippedWeapon;
            previousWeaponFamily=EquippedWeapon==null?WeaponFamily.Katana:EquippedWeapon.Family;
            crossAwakeReady=true;ClearPlantedRound();
            actionCancelSequence++;
            CancelMeleeAttack();
            EquippedWeapon = weapon;
            SignatureMeleeFx.Preload(weapon);
            weaponAttackDelayMultiplier = weapon == null ? 1f : weapon.AttackDelayMultiplier;
            comboIndex = 0;
            activeComboAnimationStep = -1;
            comboResetTimer = 0f;
            attackPoseDuration = 0f;
            attackPoseTimer = 0f;
            heavyAttackPose = false;
            CancelWeaponAction();
            SigOnEquip(weapon);
            Grow(CombatGrowthKind.WeaponSwap);
            if(swappedFrom!=null&&weapon!=null&&swappedFrom!=weapon&&ItemEffects!=null)ItemEffects.OnWeaponSwapped();
        }

        private void CancelWeaponAction()
        {
            katanaAutoCutPending=false;katanaCounterSucceeded=false;specialInputBuffer=0;
            IsGuarding = false;
            parryTimer = 0f;
            parryPresentation = false;
            ParryPresentationSucceeded = false;
            greatswordCharging = false;
            greatswordChargeTimer = 0f;
            weaponActionTimer = 0f;
            rangedPoseTimer = 0f;
        }

        private void CancelMeleeAttack()
        {
            pendingMeleeStep = null;
            pendingMeleeStroke=0;
            activeMeleeMotion = MeleeAttackMotion.Sweep;
            attackPoseTimer = 0f;
            attackBuffer = 0f;
            chargedReleasePose = false;
            // The attack cooldown is intentionally retained when cancelling.
        }

        public void ApplyAccessory(AccessoryDefinition accessory) { if (accessory != null) Stats.ApplyAccessory(accessory.Type); }
        public void ApplySetBonus(SetBonusDefinition bonus) { if (bonus != null) Stats.ApplySetBonus(bonus.SetType, bonus.RequiredPieces); }
        public void OnEnemyDefeated(EnemyController enemy = null) { Stats.RestoreAfterKill(); ItemEffects.OnEnemyDefeated(enemy); TalentOnKill(enemy); }
    }
}
