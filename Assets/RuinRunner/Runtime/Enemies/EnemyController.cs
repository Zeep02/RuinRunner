using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class EnemyController : MonoBehaviour
    {
        public EnemyType Type { get; private set; }
        private bool muteNextHitFx, muteDeathFlash;
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Armor { get; private set; }
        public int ExperienceReward { get; private set; }
        public bool IsActing => acting;
        private const float StrikeHoldSeconds = .2f, MaxAimLine = 14f;
        // Every attack warning reads as one colour, whatever the enemy's own colour or the stage's floor.
        private static readonly Color DangerTint = new Color(1f, .3f, .2f);
        private int attackVisualPhase;
        private float strikeShownAt;
        public int AttackVisualPhase // 0 anticipation, 1 active, 2 recovery
        {
            get => attackVisualPhase;
            private set
            {
                if (value == 1 && attackVisualPhase != 1)
                    strikeShownAt = Time.time;
                attackVisualPhase = value;
            }
        }
        public bool IsDead => dead;
        public bool IntroLocked { get; private set; }
        private bool roarInvulnerable;
        public bool AnimationPaused => IntroLocked || (isPaused != null && isPaused());
        public void SetIntroLocked(bool locked)
        {
            IntroLocked = locked;
            if (locked)
            {
                SetPlanar(Vector2.zero, true);
                collisionAttackActive = false;
            }
            else
                specialTimer = Mathf.Max(specialTimer, .45f);
        }
        public void SampleEntrance(float time)
        {
            if (spriteRenderer == null)
                return;
            if (Type == EnemyType.Boss && bossAnimator != null)
                bossAnimator.Entrance(time);
            else if (Type == EnemyType.Tidekeeper)
                spriteRenderer.sprite = TidekeeperArt.Entrance(time);
            else if (Type == EnemyType.SealHeart)
                spriteRenderer.sprite = SealHeartArt.Entrance(time);
        }
        public Vector2 Velocity => body == null ? Vector2.zero : LinearVelocity;
        public float FacingDirection => facingDirection;
        public Vector2 VisualAimDirection => !acting && Velocity.sqrMagnitude > .0225f ? Velocity :
            player == null ? Vector2.down : (Vector2)(player.transform.position - transform.position);
        public bool IsBoss => EnemyRoles.IsBoss(Type);
        // How far a rushing body reaches. The route warning is drawn from this, and it follows the body's real width.
        public float ContactRadius
        {
            get
            {
                if (IsBoss)
                    return 1.35f;
                switch (Type)
                {
                    case EnemyType.Grunt:
                        return .75f;
                    case EnemyType.Charger:
                        return 1f;
                    default:
                        return .85f;
                }
            }
        }
        public float PresentationScale => IsBoss || (int)Type >= (int)EnemyType.Tidecaller ? 1f : .75f;
        public static float SlamRadius(bool isBoss) => isBoss ? 2.4f : 1.5f;

        private Rigidbody2D body;
        private BoxCollider2D enemyCollider;
        private BoxCollider2D damageCollider;
        private Transform healthBarRoot;
        private SpriteRenderer healthBackRenderer, healthFillRenderer, healthTrailRenderer, healthShineRenderer, heroBodyRenderer;
        private Transform healthTrail;
        private float healthShownRatio = 1, healthBarHideAt;
        // One outline pixel on each side of a bar this many world units wide.
        const float HealthInner = .92f;
        private EnemyBalanceData presentationBalance;
        public Collider2D DamageCollider => damageCollider;
        public static EnemyController DamageTarget(Collider2D collider)
        {
            var enemy = collider.GetComponent<EnemyController>();
            return enemy != null && collider == enemy.damageCollider ? enemy : null;
        }
        private SpriteRenderer spriteRenderer;
        private BossSpriteAnimator bossAnimator;
        private Transform visualRoot;
        private Vector3 leapVisualBasePosition;
        private bool airborneVisual;
        private Transform healthFill;
        private GameObject landingTelegraph;
        private PlayerController player;
        private Func<bool> isPaused;
        private Action<EnemyController> onDied;
        private CameraFollow cameraFollow;
        private float speed;
        private float contactDamage;
        private float attackTimer;
        private float specialTimer;
        private float decisionTimer;
        private float strafeDirection = 1f;
        private int bossPatternIndex;
        private bool dead;
        private bool acting;
        private bool collisionAttackActive;
        private float facingDirection = 1f;
        private Color normalColor;
        private Color presentationTint = Color.white;
        private float hitFlashRemaining;
        private int hitFlashFrame;
        private static readonly Color HitTint = new Color(1f, .68f, .5f);
        private StageTerrainLayout navigationTerrain;
        private readonly List<Vector2> coverRoute = new List<Vector2>();
        private float nextCoverRouteAt, lastRouteStepAt, routeProgressAt, sidestepUntil;
        private Vector2 routeProgressPoint;
        private static Color RestingColor => Color.white;
        private const float WardenVisualScale = 1f; // Clips are drawn at their real on-screen size.

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

        public static EnemyController Create(
            EnemyType type,
            Vector2 position,
            PlayerController target,
            Func<bool> pauseQuery,
            Action<EnemyController> deathCallback,
            CameraFollow camera)
        {
            var enemyObject = new GameObject(type.ToString());
            enemyObject.layer = GameLayers.Enemy;
            enemyObject.transform.position = position;

            var controller = enemyObject.AddComponent<EnemyController>();
            controller.Initialize(type, target, pauseQuery, deathCallback, camera);
            return controller;
        }

        private void Initialize(
            EnemyType type,
            PlayerController target,
            Func<bool> pauseQuery,
            Action<EnemyController> deathCallback,
            CameraFollow camera)
        {
            Type = type;
            player = target;
            isPaused = pauseQuery;
            onDied = deathCallback;
            cameraFollow = camera;
            ConfigureStats();
            Health = MaxHealth;
            ApplyBalance(GameBalance.Current.GetEnemy(Type));
            if (Type == EnemyType.Tidekeeper)
                TidekeeperArt.Preload();
            if (Type == EnemyType.SealHeart)
                SealHeartArt.Preload();
            PreloadGrandMoves();
            var visualTarget = new GameObject("VisualRoot");
            visualTarget.transform.SetParent(transform);
            visualTarget.transform.localPosition = new Vector3(0f, -.35f, 0f);
            visualRoot = visualTarget.transform;
            visualRoot.localScale *= PresentationScale;
            if (ExternalCreatureArt.Supports(Type))
            {
                // Preserve source proportions despite legacy, nonuniform physics root scales.
                var scale = transform.lossyScale;
                visualRoot.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1);
                visualRoot.localPosition = new Vector3(0, -.2f / Mathf.Abs(scale.y), 0);
            }
            if ((int)Type >= (int)EnemyType.Tidekeeper)
                visualRoot.localPosition = Vector3.zero;
            if (Type == EnemyType.Boss)
            {
                // Uniform on screen despite the legacy nonuniform physics root: a boss should tower over the hero.
                visualRoot.localScale = new Vector3(WardenVisualScale / transform.lossyScale.x, WardenVisualScale / transform.lossyScale.y, 1f);
                visualRoot.localPosition = new Vector3(0f, -.42f / transform.lossyScale.y, 0f);
            }
            if (Type == EnemyType.Tidekeeper || Type == EnemyType.SealHeart)
            {
                var shadow = new GameObject("NativeGroundShadow");
                shadow.transform.SetParent(transform, false);
                var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
                shadowRenderer.sprite = IndividualArt.Sprite(PlayerAnimationController.ArtRoot + "shadow");
                shadowRenderer.sortingLayerName = "WorldFloor";
                shadowRenderer.sortingOrder = 12;
            }
            spriteRenderer = GameVisuals.AddSprite(visualTarget, normalColor, 8);
            SetPresentationTint(RestingColor);
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.78f, .46f);
            collider.offset = new Vector2(0f, -.2f);
            collider.sharedMaterial = GameLayers.Frictionless;
            enemyCollider = collider;
            body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            specialTimer = .9f;
            decisionTimer = .55f;
            strafeDirection = (int)Type % 2 == 0 ? 1f : -1f;
            spriteRenderer.sprite = PixelArtAtlas.GetEnemyFrames(Type, 0)[0];
            spriteRenderer.color = Color.white;
            if (Type == EnemyType.Boss)
            {
                bossAnimator = gameObject.AddComponent<BossSpriteAnimator>();
                bossAnimator.Initialize(this, spriteRenderer);
            }
            else
                gameObject.AddComponent<EnemyPixelAnimator>().Initialize(this, spriteRenderer);
            damageCollider = gameObject.AddComponent<BoxCollider2D>();
            damageCollider.isTrigger = true;
            AddHealthBar();
            ApplyPresentation(GameBalance.Current.GetEnemy(Type));
        }
        private void ConfigureStats()
        {
            switch (Type)
            {
                case EnemyType.Grunt:
                    MaxHealth = 48f;
                    speed = 3.4f;
                    contactDamage = 13f;
                    ExperienceReward = 9;
                    normalColor = new Color(.34f, .72f, .31f);
                    transform.localScale = new Vector3(.85f, .85f, 1f);
                    break;
                case EnemyType.Shooter:
                    MaxHealth = 38f;
                    speed = 2.5f;
                    contactDamage = 9f;
                    ExperienceReward = 11;
                    normalColor = new Color(1f, .53f, .13f);
                    transform.localScale = new Vector3(.8f, 1f, 1f);
                    break;
                case EnemyType.Charger:
                    MaxHealth = 72f;
                    speed = 3f;
                    contactDamage = 17f;
                    ExperienceReward = 14;
                    normalColor = new Color(.71f, .5f, .25f);
                    transform.localScale = new Vector3(1.1f, .82f, 1f);
                    break;
                case EnemyType.Leaper:
                    MaxHealth = 56f;
                    speed = 2.7f;
                    contactDamage = 15f;
                    ExperienceReward = 12;
                    normalColor = new Color(.37f, .66f, 1f);
                    transform.localScale = new Vector3(.78f, 1.05f, 1f);
                    break;
                case EnemyType.Boss:
                    MaxHealth = 430f;
                    speed = 2.8f;
                    contactDamage = 20f;
                    ExperienceReward = 45;
                    normalColor = new Color(.25f, .86f, 1f);
                    transform.localScale = new Vector3(1.75f, 2.1f, 1f);
                    break;
                case EnemyType.Tidekeeper:
                    MaxHealth = 1800;
                    speed = 2;
                    contactDamage = 10;
                    ExperienceReward = 65;
                    normalColor = new Color(.25f, .83f, .83f);
                    break;
                case EnemyType.SealHeart:
                    MaxHealth = 2800;
                    speed = 3.2f;
                    contactDamage = 12;
                    ExperienceReward = 90;
                    normalColor = new Color(25 / 255f, 179 / 255f, 193 / 255f);
                    break;
                case EnemyType.Tidecaller:
                    MaxHealth = 65;
                    speed = 2.5f;
                    contactDamage = 7;
                    ExperienceReward = 14;
                    normalColor = new Color(.25f, .75f, .8f);
                    break;
                case EnemyType.Sentinel:
                    MaxHealth = 90;
                    speed = 3.3f;
                    contactDamage = 9;
                    ExperienceReward = 16;
                    normalColor = new Color(.8f, .6f, .3f);
                    break;
                case EnemyType.Sporeling:
                    normalColor = new Color(.35f, .9f, .75f);
                    break;
                case EnemyType.RuneTotem:
                    normalColor = new Color(1f, .72f, .25f);
                    break;
                case EnemyType.TideCrab:
                    normalColor = new Color(.3f, .85f, .9f);
                    break;
                case EnemyType.Wisp:
                    normalColor = new Color(.55f, .95f, 1f);
                    break;
                case EnemyType.SealKnight:
                    normalColor = new Color(.95f, .75f, .3f);
                    break;
                case EnemyType.Hexer:
                    normalColor = new Color(.72f, .4f, 1f);
                    break;
            }
        }

        // Sandbox tours: a target that stays up, so skills that build on repeated hits have something to build on.
        internal void MakeTrainingDummy(float health = 1000000f)
        {
            MaxHealth = Health = health;
            UpdateHealthBar();
        }

        public void ApplyBalance(EnemyBalanceData balance)
        {
            if (balance == null)
                return;
            var healthRatio = MaxHealth <= 0f ? 1f : Health / MaxHealth;
            MaxHealth = Mathf.Max(1f, balance.maxHealth);
            Health = Mathf.Clamp(MaxHealth * healthRatio, 0f, MaxHealth);
            contactDamage = Mathf.Max(0f, balance.attackPower);
            Armor = Mathf.Max(0f, balance.armor);
            speed = Mathf.Max(.1f, balance.moveSpeed);
            ExperienceReward = Mathf.Max(0, balance.experienceReward);
            if (healthFill != null)
                UpdateHealthBar();
            if (damageCollider != null)
                ApplyPresentation(balance);
        }

        private void ApplyPresentation(EnemyBalanceData balance)
        {
            presentationBalance = balance;
            if (balance != null)
            {
                enemyCollider.size = Vector2.Max(new Vector2(.1f, .1f), balance.movementFootSize * PresentationScale);
                enemyCollider.offset = balance.movementFootOffset;
            }
            UpdateVisualPresentation();
        }

        // The measured body table is rounded to hundredths; this keeps its edge outside the art's.
    }
}
