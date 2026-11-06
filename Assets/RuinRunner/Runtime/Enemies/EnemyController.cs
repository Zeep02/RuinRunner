using System;
using System.Collections;
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
            private set { if (value == 1 && attackVisualPhase != 1) strikeShownAt = Time.time; attackVisualPhase = value; }
        }
        public bool IsDead => dead;
        public bool IntroLocked { get; private set; }
        private bool roarInvulnerable;
        public bool AnimationPaused => IntroLocked || (isPaused != null && isPaused());
        public void SetIntroLocked(bool locked)
        {
            IntroLocked=locked;
            if(locked){SetPlanar(Vector2.zero,true);collisionAttackActive=false;}
            else specialTimer=Mathf.Max(specialTimer,.45f);
        }
        public void SampleEntrance(float time)
        {
            if (spriteRenderer == null) return;
            if (Type == EnemyType.Boss && bossAnimator != null) bossAnimator.Entrance(time);
            else if (Type == EnemyType.Tidekeeper) spriteRenderer.sprite = TidekeeperArt.Entrance(time);
            else if (Type == EnemyType.SealHeart) spriteRenderer.sprite = SealHeartArt.Entrance(time);
        }
        public Vector2 Velocity => body == null ? Vector2.zero : LinearVelocity;
        public float FacingDirection => facingDirection;
        public Vector2 VisualAimDirection => !acting && Velocity.sqrMagnitude>.0225f ? Velocity :
            player==null?Vector2.down:(Vector2)(player.transform.position-transform.position);
        public bool IsBoss => EnemyRoles.IsBoss(Type);
        // How far a rushing body reaches. The route warning is drawn from this, and it follows the body's real width.
        public float ContactRadius
        {
            get
            {
                if (IsBoss) return 1.35f;
                switch (Type)
                {
                    case EnemyType.Grunt: return .75f;
                    case EnemyType.Charger: return 1f;
                    default: return .85f;
                }
            }
        }
        public float PresentationScale => IsBoss || (int)Type >= (int)EnemyType.Tidecaller ? 1f : .75f;
        public static float SlamRadius(bool isBoss) => isBoss ? 2.4f : 1.5f;

        private Rigidbody2D body;
        private BoxCollider2D enemyCollider;
        private BoxCollider2D damageCollider;
        private Transform healthBarRoot;
        private SpriteRenderer healthBackRenderer,healthFillRenderer,healthTrailRenderer,healthShineRenderer,heroBodyRenderer;
        private Transform healthTrail;
        private float healthShownRatio=1,healthBarHideAt;
        // One outline pixel on each side of a bar this many world units wide.
        const float HealthInner=.92f;
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
            if (Type == EnemyType.Tidekeeper) TidekeeperArt.Preload();
            if (Type == EnemyType.SealHeart) SealHeartArt.Preload();
            PreloadGrandMoves();
            var visualTarget = new GameObject("VisualRoot");
            visualTarget.transform.SetParent(transform);
            visualTarget.transform.localPosition = new Vector3(0f, -.35f, 0f);
            visualRoot = visualTarget.transform;
            visualRoot.localScale *= PresentationScale;
            if(ExternalCreatureArt.Supports(Type))
            {
                // Preserve source proportions despite legacy, nonuniform physics root scales.
                var scale=transform.lossyScale;
                visualRoot.localScale=new Vector3(1f/Mathf.Abs(scale.x),1f/Mathf.Abs(scale.y),1);
                visualRoot.localPosition=new Vector3(0,-.2f/Mathf.Abs(scale.y),0);
            }
            if ((int)Type >= (int)EnemyType.Tidekeeper) visualRoot.localPosition = Vector3.zero;
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
            spriteRenderer = PrototypeVisuals.AddSprite(visualTarget, normalColor, 8);
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
            else gameObject.AddComponent<EnemyPixelAnimator>().Initialize(this, spriteRenderer);
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
                    MaxHealth = 1800; speed = 2; contactDamage = 10; ExperienceReward = 65;
                    normalColor = new Color(.25f, .83f, .83f); break;
                case EnemyType.SealHeart:
                    MaxHealth = 2800; speed = 3.2f; contactDamage = 12; ExperienceReward = 90;
                    normalColor = new Color(25/255f, 179/255f, 193/255f); break;
                case EnemyType.Tidecaller:
                    MaxHealth = 65; speed = 2.5f; contactDamage = 7; ExperienceReward = 14;
                    normalColor = new Color(.25f, .75f, .8f); break;
                case EnemyType.Sentinel:
                    MaxHealth = 90; speed = 3.3f; contactDamage = 9; ExperienceReward = 16;
                    normalColor = new Color(.8f, .6f, .3f); break;
                case EnemyType.Sporeling: normalColor = new Color(.35f, .9f, .75f); break;
                case EnemyType.RuneTotem: normalColor = new Color(1f, .72f, .25f); break;
                case EnemyType.TideCrab: normalColor = new Color(.3f, .85f, .9f); break;
                case EnemyType.Wisp: normalColor = new Color(.55f, .95f, 1f); break;
                case EnemyType.SealKnight: normalColor = new Color(.95f, .75f, .3f); break;
                case EnemyType.Hexer: normalColor = new Color(.72f, .4f, 1f); break;
            }
        }

        // Sandbox tours: a target that stays up, so skills that build on repeated hits have something to build on.
        internal void MakeTrainingDummy(float health = 1000000f) { MaxHealth = Health = health; UpdateHealthBar(); }

        public void ApplyBalance(EnemyBalanceData balance)
        {
            if (balance == null) return;
            var healthRatio = MaxHealth <= 0f ? 1f : Health / MaxHealth;
            MaxHealth = Mathf.Max(1f, balance.maxHealth);
            Health = Mathf.Clamp(MaxHealth * healthRatio, 0f, MaxHealth);
            contactDamage = Mathf.Max(0f, balance.attackPower);
            Armor = Mathf.Max(0f, balance.armor);
            speed = Mathf.Max(.1f, balance.moveSpeed);
            ExperienceReward = Mathf.Max(0, balance.experienceReward);
            if (healthFill != null) UpdateHealthBar();
            if (damageCollider != null) ApplyPresentation(balance);
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
        const float BodyMeasureMargin = .03f;
        public void UpdateVisualPresentation()
        {
            if (damageCollider == null || spriteRenderer == null || spriteRenderer.sprite == null) return;
            var balance = presentationBalance;
            // Authored body envelopes in world units. Neither attack extremities nor
            // a change of PNG can resize the target or move its health bar.
            Vector2 size; float centerY;
            switch (Type)
            {
                case EnemyType.Grunt: size=new Vector2(.90f,.65f); centerY=.33f; break;
                case EnemyType.Shooter: size=new Vector2(.55f,.80f); centerY=.40f; break;
                case EnemyType.Charger: size=new Vector2(.95f,.72f); centerY=.36f; break;
                case EnemyType.Leaper: size=new Vector2(.80f,.82f); centerY=.41f; break;
                // Idle art around the foot pivot: a blow that touches the drawn body lands.
                case EnemyType.Boss: size=new Vector2(3.34f,2.19f); centerY=1.08f; break;
                case EnemyType.Tidekeeper: size=new Vector2(2.5f,2.45f); centerY=1.43f; break;
                case EnemyType.SealHeart: size=new Vector2(2.6f,2.47f); centerY=1.19f; break;
                default: size=new Vector2(.65f,.85f); centerY=.35f; break;
            }
            var tuning = balance == null ? 1f : Mathf.Max(.1f,balance.hurtboxScale)/1.08f;
            if(ExternalCreatureArt.Supports(Type))
            {size=ExternalCreatureArt.BodySize(Type)+Vector2.one*BodyMeasureMargin;centerY=ExternalCreatureArt.BodyCenterY(Type);}
            var rootScale = new Vector2(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y));
            damageCollider.size = new Vector2(size.x/rootScale.x,size.y/rootScale.y)*tuning;
            damageCollider.offset = (Vector2)visualRoot.localPosition + new Vector2(0,centerY/rootScale.y) +
                (balance == null ? Vector2.zero : balance.hurtboxOffset);
            if (healthBarRoot != null)
            {
                if (ExternalCreatureArt.Supports(Type)) UpdateCreatureHealthBarPosition();
                else healthBarRoot.localPosition = new Vector3(0,visualRoot.localPosition.y+(centerY+size.y*.5f+.25f)/rootScale.y,0);
            }
        }

        private void LateUpdate()
        {
            if (!dead && !AnimationPaused)
            {
                if (hitFlashFrame != Time.frameCount)
                    hitFlashRemaining = Mathf.Max(0f, hitFlashRemaining - Time.deltaTime);
                ApplyPresentationTint();
            }
            if (!dead && ExternalCreatureArt.Supports(Type)) UpdateCreatureHealthBarPosition();
            UpdateHealthBarDepth();
            ApplyHitPunch();
        }

        // Struck bodies squash for a beat. Only the visual changes, never the hurtbox.
        private float hitPunch, hitStun;
        private Vector3 restingVisualScale;
        private void ApplyHitPunch()
        {
            if (visualRoot == null) return;
            if (restingVisualScale == Vector3.zero) restingVisualScale = visualRoot.localScale;
            hitPunch = Mathf.Max(0f, hitPunch - Time.deltaTime * 7f);
            var strength = IsBoss ? .1f : .26f;
            visualRoot.localScale = Vector3.Scale(restingVisualScale, new Vector3(1f + hitPunch * strength, 1f - hitPunch * strength * .8f, 1f));
        }

        private void UpdateHealthBarDepth()
        {
            if(healthBackRenderer==null||healthFillRenderer==null||spriteRenderer==null)return;
            var order=spriteRenderer.sortingOrder+2;
            // World-space bars follow the enemy's depth. They must never stripe
            // across the hero's face during a close stab or dagger lunge.
            if(heroBodyRenderer!=null)order=Mathf.Min(order,heroBodyRenderer.sortingOrder-4);
            healthBackRenderer.sortingOrder=order;healthTrailRenderer.sortingOrder=order+1;healthFillRenderer.sortingOrder=order+2;healthShineRenderer.sortingOrder=order+3;
        }

        private void UpdateCreatureHealthBarPosition()
        {
            if (healthBarRoot == null || visualRoot == null) return;
            // Follow intentional leap elevation, never frame-by-frame pixel bounds.
            // Leave the ground collision and damage envelope unchanged.
            var height = ExternalCreatureArt.VisualTop(Type) + 6f / 32f;
            healthBarRoot.localPosition = new Vector3(0,
                visualRoot.localPosition.y + height / Mathf.Abs(transform.lossyScale.y), 0);
        }

        private void OnDrawGizmosSelected()
        {
            if (enemyCollider != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireCube(enemyCollider.bounds.center, enemyCollider.bounds.size); }
            if (damageCollider != null) { Gizmos.color = new Color(1f, .55f, .1f); Gizmos.DrawWireCube(damageCollider.bounds.center, damageCollider.bounds.size); }
        }

        private void AddHealthBar()
        {
            var back = new GameObject("HealthBack");
            back.transform.SetParent(transform);
            healthBarRoot = back.transform;
            back.transform.localPosition = new Vector3(0f, .7f, 0f);
            back.transform.localScale = new Vector3(.9f * PresentationScale, .16f * PresentationScale, 1f);
            PrototypeVisuals.AddSprite(back, new Color(.07f, .05f, .1f), spriteRenderer.sortingOrder+2);
            healthBackRenderer=back.GetComponent<SpriteRenderer>();

            var trail = new GameObject("HealthTrail");
            trail.transform.SetParent(back.transform,false);
            trail.transform.localPosition = new Vector3(0f, 0f, -.005f);
            trail.transform.localScale = new Vector3(HealthInner, .6f, 1f);
            PrototypeVisuals.AddSprite(trail, new Color(1f, .93f, .78f), spriteRenderer.sortingOrder+3);
            healthTrailRenderer=trail.GetComponent<SpriteRenderer>();
            healthTrail=trail.transform;

            var fill = new GameObject("HealthFill");
            fill.transform.SetParent(back.transform,false);
            fill.transform.localPosition = new Vector3(0f, 0f, -.01f);
            fill.transform.localScale = new Vector3(HealthInner, .6f, 1f);
            PrototypeVisuals.AddSprite(fill, new Color(.78f, .16f, .2f), spriteRenderer.sortingOrder+4);
            healthFillRenderer=fill.GetComponent<SpriteRenderer>();

            var shine = new GameObject("HealthShine");
            shine.transform.SetParent(fill.transform,false);
            shine.transform.localPosition = new Vector3(0f, .28f, -.01f);
            shine.transform.localScale = new Vector3(1f, .44f, 1f);
            PrototypeVisuals.AddSprite(shine, new Color(1f, .42f, .38f), spriteRenderer.sortingOrder+5);
            healthShineRenderer=shine.GetComponent<SpriteRenderer>();
            heroBodyRenderer=player==null?null:player.transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>();
            UpdateHealthBarDepth();
            healthFill = fill.transform;
            back.SetActive(false); // Shown once hit. The boss keeps its dedicated screen HUD instead.
        }

        private void Update()
        {
            if (dead || player == null || AnimationPaused) return;
            TickHealthBar();

            attackTimer -= Time.deltaTime;
            specialTimer -= Time.deltaTime;
            decisionTimer -= Time.deltaTime;
            var delta = (Vector2)(player.transform.position - transform.position);
            var direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector2.right;

            // Scripted moves carry their momentum in instead of stopping on a dime.
            if (acting) LinearVelocity = Vector2.MoveTowards(LinearVelocity, Vector2.zero, 26f * Time.deltaTime);
            // A struck enemy reels: it slides with the blow and cannot walk or start a move.
            if (hitStun > 0f && !acting)
            {
                hitStun -= Time.deltaTime;
                LinearVelocity = Vector2.MoveTowards(LinearVelocity, Vector2.zero, 20f * Time.deltaTime);
                spriteRenderer.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100f);
                return;
            }

            if (collisionAttackActive && Vector2.Distance(player.FeetPoint, transform.position) < ContactRadius &&
                !Physics2D.Linecast(transform.position, player.transform.position, 1 << GameLayers.Ground).collider)
                ContactAttack(direction);

            // Being in attack range does not mean the target is reachable.
            // Reposition around cover instead of repeatedly charging/shooting
            // into the same solid module forever.
            if (!acting && Type != EnemyType.RuneTotem && !HasLaneToPlayer)
            {
                SetPlanar(NavigateAroundCover(player.transform.position) * speed);
                SetFacing(LinearVelocity.x);
                spriteRenderer.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100f);
                return;
            }

            switch (Type)
            {
                case EnemyType.Boss: UpdateBoss(delta, direction); break;
                case EnemyType.Tidekeeper: UpdateTidekeeper(delta, direction); break;
                case EnemyType.SealHeart: UpdateSealHeart(delta, direction); break;
                default: UpdateBrain(delta, direction); break;
            }

            if (!acting) SetFacing(Mathf.Abs(LinearVelocity.x) > .05f ? LinearVelocity.x : direction.x);
            spriteRenderer.sortingOrder = 900 - Mathf.RoundToInt(transform.position.y * 100f);
        }

        private void UpdateBoss(Vector2 delta, Vector2 direction)
        {
            if (acting) return;

            if (TryBeginPhaseShift()) return;
            var distance = delta.magnitude;
            if (specialTimer <= 0f)
            {
                WardenChooseMove(distance, direction);
                return;
            }

            // It hunts: it stalks in fast from afar, never backs away, and answers anyone hugging it.
            hugTime = distance < 2.7f ? hugTime + Time.deltaTime : 0f;
            if (hugTime > .85f) { hugTime = 0f; StartCoroutine(WardenShove()); return; }
            if (distance > 5.2f) SetPlanar(SteerAround(direction) * speed * 1.55f);
            else if (distance > 2.7f) SetPlanar(SteerAround(direction) * speed);
            else SetPlanar(Vector2.zero);
        }

        private IEnumerator LeapSlam(Vector2 direction, bool isBoss)
        {
            SetFacing(direction.x);
            acting = true;
            AttackVisualPhase = 0;
            specialTimer = isBoss ? 2.4f : 1.85f;
            SetPlanar(Vector2.zero, true);
            if (!isBoss) SetPresentationTint(new Color(1f, .92f, .82f));
            else bossAnimator.Play(BossPose.Leap, 0, 1, .38f);
            var desiredDistance = Mathf.Min(isBoss ? 5.8f : 4.2f, Vector2.Distance(transform.position, player.transform.position));
            var travelDistance = GetSafeTravelDistance(direction, desiredDistance);
            var start = body.position;
            var target = start + direction * travelDistance;
            CreateLeapShadow(target, isBoss);
            // Fills up over the whole wind-up and flight, reaching the rim as the body lands.
            ArenaHazard.Create(this, player, target, Vector2.one * (2f * SlamRadius(isBoss)), 0, true, (isBoss ? .38f + .52f : .44f + .42f), .02f, 0, DangerTint);
            yield return new WaitForSeconds(isBoss ? .38f : .44f);

            AttackVisualPhase = 1;
            if (isBoss) bossAnimator.Play(BossPose.Leap, 2, 4, .52f);
            var baseVisualPosition = visualRoot == null ? Vector3.zero : visualRoot.localPosition;
            leapVisualBasePosition = baseVisualPosition;
            airborneVisual = visualRoot != null;
            var elapsed = 0f;
            var duration = isBoss ? .52f : .42f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                MoveSafely(Vector2.Lerp(start, target, progress));
                if (visualRoot != null)
                {
                    var height = Mathf.Sin(progress * Mathf.PI) * (isBoss ? 1.6f : 1.1f);
                    // The physics root has legacy nonuniform scales. Elevation is
                    // authored in world units, like the landing circle and travel.
                    var parentHeightScale = Mathf.Max(.001f, Mathf.Abs(visualRoot.parent.lossyScale.y));
                    visualRoot.localPosition = baseVisualPosition + Vector3.up * (height / parentHeightScale);
                }
                yield return new WaitForFixedUpdate();
            }
            if (visualRoot != null) visualRoot.localPosition = baseVisualPosition;
            airborneVisual = false;
            ClearLandingTelegraph();
            AttackVisualPhase = 2;
            if (isBoss) bossAnimator.Play(BossPose.Leap, 5, 7, .32f);
            LinearVelocity = Vector2.zero;
            var projectileCount = isBoss ? 0 : 6; // Boss slam is a landing-area attack, with no bullets.
            for (var index = 0; index < projectileCount; index++)
            {
                var angle = index * Mathf.PI * 2f / projectileCount;
                Shoot(transform.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 5.5f, contactDamage * .85f, .25f);
            }

            if (Vector2.Distance(player.FeetPoint, transform.position) < SlamRadius(isBoss))
                ContactAttack(direction);
            GroundSlam(transform.position, SlamRadius(isBoss));
            if (isBoss)
            {
                for (int i = 0; i < 4; i++)
                {
                    float angle = (i * 90f + 45f) * Mathf.Deg2Rad;
                    var ground = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SlamRadius(true) * .65f;
                    BossNativeEffect.Create(this, "StoneChip", ground, .28f);
                }
            }
            PrototypeAudio.Play(PrototypeSound.Hit, AudioChannel.Monster, isBoss ? .65f : .4f);
            if (cameraFollow != null) cameraFollow.Shake(isBoss ? .35f : .18f);
            yield return new WaitForSeconds(.32f);
            EndAction();
        }

        private Rect? arenaBounds;

        public void ConfigureArena(Rect bounds) => arenaBounds = bounds;

        public void ConfigureNavigation(StageTerrainLayout terrain)
        {
            navigationTerrain = terrain;
            coverRoute.Clear();
            nextCoverRouteAt = 0f;
        }

        // Measured feet to feet, thinner than either pair of feet. A body's root sits at the top of its feet, so from the roots
        // anyone standing with their back to a wall or a pillar looked cut off from everything: enemies would crowd a hero
        // doing that without ever attacking.
        private const float LaneRadius = .12f;
        public bool HasLaneToPlayer => StageNavigation.HasCombatLane(enemyCollider.bounds.center, player.FeetPoint, LaneRadius);
        internal string RouteReport()
        {
            var path = navigationTerrain == null ? null : StageNavigation.FindPath(navigationTerrain, body.position, player.transform.position);
            var cast = Physics2D.CircleCast(enemyCollider.bounds.center, LaneRadius, player.FeetPoint - (Vector2)enemyCollider.bounds.center, Vector2.Distance(player.FeetPoint, enemyCollider.bounds.center), 1 << GameLayers.Ground);
            return "foot " + enemyCollider.bounds.size + " velocity " + LinearVelocity + " acting " + acting + " route " + (path == null ? "none" : path.Count + " points, next " + (path.Count > 1 ? path[1] : path[0])) +
                " kept " + coverRoute.Count + (coverRoute.Count > 0 ? " toward " + coverRoute[0] : "") + " wall " + (cast.collider == null ? "none" : cast.collider.name + " at " + cast.point);
        }

        private Vector2 NavigateAroundCover(Vector2 destination)
        {
            // Long cover routes may deliberately pass along a wall. Only the
            // close-contact case needs separation before ordinary route following.
            if ((destination - body.position).sqrMagnitude < 2.25f)
            {
                var clearance = StageNavigation.WallClearanceDirection(body.position, .32f);
                if (clearance.sqrMagnitude > .001f) return clearance;
            }
            if (navigationTerrain == null) return SteerAround(destination - body.position);
            if (Time.time >= nextCoverRouteAt)
            {
                // Spread out, so a room of blocked enemies doesn't all search on one frame.
                nextCoverRouteAt = Time.time + .85f + (GetHashCode() & 7) * .04f;
                var path = StageNavigation.FindPath(navigationTerrain, body.position, destination);
                coverRoute.Clear();
                if (path != null) coverRoute.AddRange(path);
            }
            while (coverRoute.Count > 0 && Vector2.Distance(body.position, coverRoute[0]) < .35f)
                coverRoute.RemoveAt(0);
            if (coverRoute.Count == 0) return SteerAround(destination - body.position);
            // Follow grid-clear waypoints directly; local tangent steering can
            // repeatedly choose the same blocked side of a large island.
            var heading = (coverRoute[0] - body.position).normalized;
            // The grid doesn't know about other bodies. Walking square into a totem would otherwise go on forever.
            if (Time.time - lastRouteStepAt > .2f || (body.position - routeProgressPoint).sqrMagnitude > .04f)
            { routeProgressPoint = body.position; routeProgressAt = Time.time; }
            lastRouteStepAt = Time.time;
            if (Time.time - routeProgressAt > .5f) { routeProgressAt = Time.time; sidestepUntil = Time.time + .35f; strafeDirection *= -1f; }
            return Time.time < sidestepUntil ? Rotate(heading, 75f * strafeDirection) : heading;
        }

        private Vector2 ClampToArena(Vector2 root)
        {
            if (!arenaBounds.HasValue || enemyCollider == null) return root;
            var bounds = arenaBounds.Value;
            var offset = Vector2.Scale(enemyCollider.offset, transform.lossyScale);
            var half = Vector2.Scale(enemyCollider.size, transform.lossyScale) * .5f;
            root.x = Mathf.Clamp(root.x, bounds.xMin + half.x - offset.x + .02f, bounds.xMax - half.x - offset.x - .02f);
            root.y = Mathf.Clamp(root.y, bounds.yMin + half.y - offset.y + .02f, bounds.yMax - half.y - offset.y - .02f);
            return root;
        }

        private void FixedUpdate()
        {
            if (dead || body == null) return;
            StopAtPlayerBody();
            KeepOnFloor();
            if (!arenaBounds.HasValue) return;
            var bounded = ClampToArena(body.position);
            if ((bounded - body.position).sqrMagnitude > .0001f) body.position = bounded;
            LinearVelocity = (ClampToArena(body.position + LinearVelocity * Time.fixedDeltaTime) - body.position) / Time.fixedDeltaTime;
        }

        private Collider2D playerFeet;
        private Vector2 lastFloorPoint;
        private bool hasFloorPoint;
        private float offFloorSeconds;

        private void StopAtPlayerBody()
        {
            if (player == null || enemyCollider == null) return;
            if (playerFeet == null) playerFeet = player.GetComponent<Collider2D>();
            if (((Vector2)player.transform.position - body.position).sqrMagnitude > 36f) return;
            LinearVelocity = BodyContact.Stop(enemyCollider, playerFeet, LinearVelocity, Time.fixedDeltaTime, out _);
        }

        // Nothing should carry a body past the walls, but one that gets out can never be hit again and holds the room shut.
        private void KeepOnFloor()
        {
            if (navigationTerrain == null || enemyCollider == null) return;
            if (navigationTerrain.IsOpen(enemyCollider.bounds.center))
            {
                lastFloorPoint = body.position;
                hasFloorPoint = true;
                offFloorSeconds = 0f;
                return;
            }
            offFloorSeconds += Time.fixedDeltaTime;
            if (!hasFloorPoint || offFloorSeconds < .2f) return;
            body.position = lastFloorPoint;
            LinearVelocity = Vector2.zero;
            offFloorSeconds = 0f;
        }

        private void MoveSafely(Vector2 destination)
        {
            destination = ClampToArena(destination);
            var delta = destination - body.position;
            if (delta.sqrMagnitude < .000001f) return;
            var distance = GetSafeTravelDistance(delta.normalized, delta.magnitude);
            body.MovePosition(body.position + delta.normalized * distance);
        }

        private void EndAction()
        {
            ClearLandingTelegraph();
            acting = false;
            collisionAttackActive = false;
            AttackVisualPhase = 2;
            if (!dead && spriteRenderer != null) SetPresentationTint(RestingColor);
        }

        private void SetPlanar(Vector2 target, bool immediate = false)
        {
            var next = immediate
                ? target
                : Vector2.MoveTowards(LinearVelocity, target, speed * 7f * Time.deltaTime);
            LinearVelocity = next;
        }

        private Vector2 SteerAround(Vector2 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude < .001f) return Vector2.zero;
            desiredDirection.Normalize();
            var origin = enemyCollider == null ? (Vector2)transform.position : (Vector2)enemyCollider.bounds.center;
            if (!Physics2D.Raycast(origin, desiredDirection, .85f, 1 << GameLayers.Ground)) return desiredDirection;
            var left = Rotate(desiredDirection, 72f);
            var right = Rotate(desiredDirection, -72f);
            var leftBlocked = Physics2D.Raycast(origin, left, .75f, 1 << GameLayers.Ground);
            var rightBlocked = Physics2D.Raycast(origin, right, .75f, 1 << GameLayers.Ground);
            if (!leftBlocked && !rightBlocked) return strafeDirection > 0f ? left : right;
            if (!leftBlocked) return left;
            if (!rightBlocked) return right;
            return -desiredDirection;
        }

        private IEnumerator Rush(Vector2 direction, float desiredDistance, float duration)
        {
            SetFacing(direction.x);
            direction = direction.sqrMagnitude < .001f ? Vector2.right : direction.normalized;
            var distance = GetSafeTravelDistance(direction, desiredDistance);
            var start = body.position;
            var target = start + direction * distance;
            var elapsed = 0f;
            var lastDust = start;
            var trailClip = collisionAttackActive && distance > 1f ? "ERushDust" : null;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                // Bursts out and bleeds speed at the end, rather than gliding at one rate.
                var progress = Mathf.Clamp01(elapsed / duration);
                MoveSafely(Vector2.Lerp(start, target, 1f - Mathf.Pow(1f - progress, 2.4f)));
                if (trailClip != null && Vector2.Distance(body.position, lastDust) >= .7f)
                {
                    BossNativeEffect.CreateOnFloor(trailClip, body.position, .34f);
                    lastDust = body.position;
                }
                yield return new WaitForFixedUpdate();
            }
            MoveSafely(target);
        }

        private float GetSafeTravelDistance(Vector2 direction, float desiredDistance)
        {
            if (enemyCollider == null) return desiredDistance;
            var size = Vector2.Scale(enemyCollider.size, transform.lossyScale);
            var center = body.position + Vector2.Scale(enemyCollider.offset, transform.lossyScale);
            var boundedDestination = ClampToArena(body.position + direction * desiredDistance);
            if (Mathf.Abs(direction.x) > .0001f)
                desiredDistance = Mathf.Min(desiredDistance, Mathf.Abs((boundedDestination.x - body.position.x) / direction.x));
            if (Mathf.Abs(direction.y) > .0001f)
                desiredDistance = Mathf.Min(desiredDistance, Mathf.Abs((boundedDestination.y - body.position.y) / direction.y));
            var hit = Physics2D.BoxCast(center, size, 0f, direction, desiredDistance, 1 << GameLayers.Ground);
            return hit.collider == null ? desiredDistance : Mathf.Max(0f, hit.distance - .06f);
        }

        private GameObject CreateLeapShadow(Vector2 target, bool isBoss)
        {
            ClearLandingTelegraph();
            var shadow = new GameObject("LeapLandingTelegraph");
            landingTelegraph = shadow;
            shadow.transform.position = new Vector3(target.x, target.y, 0f);
            shadow.transform.localScale = Vector3.one * (2f * SlamRadius(isBoss));
            var renderer = PrototypeVisuals.AddSprite(shadow, new Color(1f, .55f, .2f, 1f), -350);
            renderer.sprite = PrototypeVisuals.AttackRing;
            return shadow;
        }

        private void ClearLandingTelegraph()
        {
            if (landingTelegraph != null) Destroy(landingTelegraph);
            landingTelegraph = null;
        }

        private void OnDestroy() { ClearLandingTelegraph(); LeaveSquad(); }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            return new Vector2(direction.x * cosine - direction.y * sine, direction.x * sine + direction.y * cosine);
        }

        private void SetFacing(float direction)
        {
            // Vertical actions retain the last horizontal facing. Do not pivot
            // toward a player who crosses behind an already committed attack.
            if (Mathf.Abs(direction) > .05f) facingDirection = Mathf.Sign(direction);
        }

        private void ContactAttack(Vector2 direction)
        {
            if (attackTimer > 0f) return;
            attackTimer = .75f;
            player.TakeDamage(contactDamage, direction.sqrMagnitude > .001f ? direction.normalized : Vector2.zero);
        }

        // A weapon that draws its own contact asks for the next hit to skip the shared spark and death flash.
        public void MuteNextHitFx() => muteNextHitFx = true;
        public void Stagger(float seconds) { if (!IsBoss && Type != EnemyType.RuneTotem && !Unstoppable) hitStun = Mathf.Max(hitStun, seconds); }

        public void TakeDamage(float amount, Vector2 knockback, bool critical = false)
        {
            ApplyDamage(amount, knockback, critical, true);
        }

        public void TakeItemDamage(float amount, Vector2 knockback)
        {
            ApplyDamage(amount, knockback, false, false);
        }

        private void ApplyDamage(float amount, Vector2 knockback, bool critical, bool originalImpact)
        {
            var muteHitFx = muteNextHitFx;
            muteNextHitFx = false;
            if (dead || IntroLocked || roarInvulnerable) return;

            if (player != null) amount *= player.TalentDamageAgainst(this);
            amount = StageMobDamage(amount);
            var appliedDamage = Mathf.Max(1f, amount * 100f / (100f + Armor));
            BossCombatObservation.RecordDamage(this, appliedDamage, originalImpact, critical);
            Health -= appliedDamage;
            DamageNumber.Show(transform.position, appliedDamage, critical, originalImpact);
            if (!collisionAttackActive && !IsBoss) LinearVelocity = knockback;
            UpdateHealthBar();
            // Weapon contact/projectile impact and item actions already emit
            // their own authored cues. The old fixed-order RuntimeSquare burst
            // duplicated every hit and floated across the attacker's face.
            // Item actions have their own bounded cue. They never occupy the camera's
            // impact throttle before a subsequent primary critical hit can reach it.
            if (originalImpact)
            {
                PrototypeAudio.Play(PrototypeSound.Hit, AudioChannel.Monster, critical || Type == EnemyType.Boss ? .55f : .35f);
                if (cameraFollow != null) cameraFollow.Impact(critical, IsBoss);
                if (Health > 0f && !muteHitFx) Fx(critical ? "FxBluntHit" : "FxHitSpark", (Vector2)transform.position + Vector2.up * .4f, critical ? .3f : .2f, AngleOf(knockback));
                hitPunch = critical ? 1f : .65f;
                if (!IsBoss && Type != EnemyType.RuneTotem && !Unstoppable && !acting) hitStun = critical ? .32f : .22f;
            }
            // One owned clock: an older contact must not clear a newer flash,
            // and a hit must not permanently replace an attack's warning tint.
            hitFlashRemaining = .055f;
            hitFlashFrame = Time.frameCount;
            ApplyPresentationTint();
            if (bossAnimator != null && Health > 0f) bossAnimator.Hit();
            if(Health>0f && (ExternalCreatureArt.Supports(Type) || Type==EnemyType.Tidekeeper || Type==EnemyType.SealHeart))GetComponent<EnemyPixelAnimator>().Hit();
            if (Health <= 0f)
            {
                if (IsBoss) BeginBossDeath(Mathf.Max(1f, Health + appliedDamage));
                else { muteDeathFlash = muteHitFx; Die(); }
            }
        }

        private void UpdateHealthBar()
        {
            var ratio = Mathf.Clamp01(Health / MaxHealth);
            healthFill.localScale = new Vector3(ratio * HealthInner, .6f, 1f);
            healthFill.localPosition = new Vector3((ratio - 1f) * .5f * HealthInner, 0f, -.01f);
            if (ratio < healthShownRatio && !IsBoss && !dead && healthBarRoot != null)
            { healthBarHideAt = Time.time + 3f; healthBarRoot.gameObject.SetActive(true); }
            if (ratio > healthShownRatio) healthShownRatio = ratio;
            PlaceHealthTrail();
        }
        private void PlaceHealthTrail()
        {
            healthTrail.localScale = new Vector3(healthShownRatio * HealthInner, .6f, 1f);
            healthTrail.localPosition = new Vector3((healthShownRatio - 1f) * .5f * HealthInner, 0f, -.005f);
        }
        private void TickHealthBar()
        {
            if (healthBarRoot == null || !healthBarRoot.gameObject.activeSelf) return;
            var ratio = Mathf.Clamp01(Health / MaxHealth);
            if (healthShownRatio > ratio) { healthShownRatio = Mathf.MoveTowards(healthShownRatio, ratio, Time.deltaTime * .9f); PlaceHealthTrail(); }
            if (Time.time >= healthBarHideAt) healthBarRoot.gameObject.SetActive(false);
        }

        private void SetPresentationTint(Color color)
        {
            presentationTint = color;
            ApplyPresentationTint();
        }

        private void ApplyPresentationTint() => spriteRenderer.color = hitFlashRemaining > 0f ? HitTint : presentationTint;

        private void Die()
        {
            dead = true;
            LeaveSquad();
            ClearLandingTelegraph();
            if (!IsBoss)
            {
                SigFx.Play("DeathPuff" + StageFx.Theme, transform.position, 0, -1, true, false, .7f);
                CombatStain.Place(transform.position, "Stain" + StageFx.Theme, Mathf.Clamp(MaxHealth / 120f, .7f, 1.3f));
            }
            StopAllCoroutines();
            if (!muteDeathFlash) BossNativeEffect.CreateOverActors(null, "FxBluntHit", (Vector2)transform.position + Vector2.up * .3f, .36f);
            if (cameraFollow != null && Type != EnemyType.Sporeling) cameraFollow.KillImpact();
            if (onDied != null) onDied(this);
            if (ExternalCreatureArt.Supports(Type) || (int)Type >= (int)EnemyType.Tidecaller)
            {
                body.simulated = false;
                enemyCollider.enabled = damageCollider.enabled = false;
                healthBarRoot.gameObject.SetActive(false);
                StartCoroutine(MinorDeathSequence());
                return;
            }
            Destroy(gameObject);
        }

        private IEnumerator MinorDeathSequence()
        {
            var deathVisualStart = visualRoot == null ? Vector3.zero : visualRoot.localPosition;
            var animator=GetComponent<EnemyPixelAnimator>();
            var frames = ExternalCreatureArt.Supports(Type)?ExternalCreatureArt.Frames(Type,3,animator.ArtDirection):PixelArtAtlas.GetEnemyFrames(Type,3);
            spriteRenderer.color = Color.white;
            if(frames==null)
            {
                // Only art without an authored death clip uses this fallback; do not synthesize a new pose.
                var elapsed=0f;
                while(elapsed<.25f){if(AnimationPaused){yield return null;continue;}elapsed+=Time.deltaTime;SettleAirborneDeath(deathVisualStart,elapsed);spriteRenderer.color=new Color(1,1,1,1-elapsed/.25f);yield return null;}
                Destroy(gameObject);yield break;
            }
            // Use one elapsed clock. Per-frame WaitForSeconds adds a render-frame
            // delay ten times, stretching death noticeably on slower machines.
            var deathElapsed=0f;var duration=frames.Length*.1f;
            while(deathElapsed<duration)
            {
                if(AnimationPaused){yield return null;continue;}
                SettleAirborneDeath(deathVisualStart, deathElapsed);
                spriteRenderer.sprite=frames[Mathf.Min(frames.Length-1,(int)(deathElapsed/.1f))];
                yield return null;deathElapsed+=Time.deltaTime;
            }
            Destroy(gameObject);
        }

        private void SettleAirborneDeath(Vector3 start, float elapsed)
        {
            if (!airborneVisual || visualRoot == null) return;
            // Keep the current pose at the lethal hit, then fall into the authored
            // collapse. Neither the physics root nor reward timing is moved.
            var progress = Mathf.Clamp01(elapsed / .22f);
            visualRoot.localPosition = Vector3.Lerp(start, leapVisualBasePosition, progress * progress);
            if (progress >= 1f) airborneVisual = false;
        }

        private void BeginBossDeath(float drainFromHealth)
        {
            dead = true;
            if (damageCollider != null) damageCollider.enabled = false;
            if (enemyCollider != null) enemyCollider.enabled = false;
            ClearLandingTelegraph();
            acting = true;
            AttackVisualPhase = 0;
            collisionAttackActive = false;
            AttackVisualPhase = 2;
            StopAllCoroutines();
            LinearVelocity = Vector2.zero;
            if (body != null) body.simulated = false;
            if (cameraFollow != null) cameraFollow.BossDefeated();
            BossFallPresentation.Play(transform.position, cameraFollow);
            // The stage is only declared clear once the collapse ends. What it already fired must not win the fight in the meantime.
            RuntimeSceneObjects.ClearProjectiles();
            StartCoroutine(BossDeathSequence(drainFromHealth));
        }

        private IEnumerator BossDeathSequence(float drainFromHealth)
        {
            const float duration = 1.05f;
            var elapsed = 0f;
            var deathVisualStart = visualRoot == null ? Vector3.zero : visualRoot.localPosition;
            spriteRenderer.color = Color.white;
            if (bossAnimator != null) bossAnimator.Death(0);
            while (elapsed < duration)
            {
                // Boss rewards settle after this sequence. Pause must suspend
                // both the visible death and its eventual completion callback.
                if (AnimationPaused) { yield return null; continue; }
                SettleAirborneDeath(deathVisualStart, elapsed);
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                Health = Mathf.Lerp(drainFromHealth, 0f, progress);
                UpdateHealthBar();

                if (bossAnimator != null) bossAnimator.Death(progress);
                else if ((int)Type >= (int)EnemyType.Tidekeeper)
                {
                    if(Type==EnemyType.SealHeart)
                    {
                        spriteRenderer.sprite=SealHeartArt.Death(GetComponent<EnemyPixelAnimator>().ArtDirection,progress);
                        // The separate collapse stays visible through its falling
                        // crown/core and inert rubble poses, then settles away.
                        spriteRenderer.color=new Color(1,1,1,1-Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.9f)/.1f)));
                        if(elapsed-Time.deltaTime<duration*.7f && elapsed>=duration*.7f)
                        {
                            BossNativeEffect.Create(this,"SlamDust",transform.position,.3f);
                        }
                    }
                    else
                    {
                        var frames=PixelArtAtlas.GetEnemyFrames(Type,3);
                        spriteRenderer.sprite=frames[Mathf.Min(frames.Length-1,Mathf.FloorToInt(progress*frames.Length))];
                    }
                }

                yield return null;
            }

            Health = 0f;
            UpdateHealthBar();
            if (onDied != null) onDied(this);
            Destroy(gameObject);
        }
    }
}
