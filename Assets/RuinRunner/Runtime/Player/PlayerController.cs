using System;
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
        public float HurtPresentationProgress => Mathf.Clamp01(1f - Mathf.Max(0f, hurtPoseTimer) / HurtPresentationSeconds);
        public float RangedPresentationProgress => rangedPoseDuration <= 0f ? 1f : Mathf.Clamp01(1f - Mathf.Max(0f, rangedPoseTimer) / rangedPoseDuration);
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
        public float ParryPresentationProgress => Mathf.Clamp01(1f - weaponActionTimer / (ParryPresentationSucceeded ? .22f : .28f));
        public bool IsChargedCleaveActive => IsMeleeSwingActive && chargedReleasePose;
        public bool IsGreatswordCharging => greatswordCharging;
        public float GreatswordChargeProgress => Mathf.Clamp01(greatswordChargeTimer / .6f);
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
        private float rangedPoseTimer, rangedPoseDuration;
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
            var renderer = GameVisuals.AddSprite(visualRoot, new Color(.2f, .85f, 1f), 10);

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
            usesPixelArt = IndividualArt.Exists(PlayerAnimationController.ArtRoot + "idle/down/00");
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
            if (inventory != null)
                inventory.ReloadCompleted += OnCombatReload;
            if (inventory != null)
                inventory.Changed += OwnedEquipmentChanged;
            MotionState = PlayerMotionState.Idle;
            AimDirection = Vector2.right;
            activeComboAnimationStep = -1;
        }

        void OwnedEquipmentChanged(InventoryChangeKind change)
        {
            ItemEffects.ReconcileOwnedEffects();
            if (!ReferenceEquals(EquippedWeapon, inventory.EquippedWeapon))
                EquipWeapon(inventory.EquippedWeapon);
        }
        void OnDestroy()
        {
            ActiveCombatants.Remove(this);
            if (inventory != null)
                inventory.ReloadCompleted -= OnCombatReload;
            if (inventory != null)
                inventory.Changed -= OwnedEquipmentChanged;
            Stats?.Dispose();
        }

        private GameObject AddFace(Transform visualRoot)
        {
            var eye = new GameObject("FallbackFace");
            eye.transform.SetParent(visualRoot);
            eye.transform.localPosition = new Vector3(.23f, .18f, 0f);
            eye.transform.localScale = new Vector3(.12f, .15f, 1f);
            GameVisuals.AddSprite(eye, new Color(.02f, .05f, .09f), 11);
            return eye;
        }
    }
}
