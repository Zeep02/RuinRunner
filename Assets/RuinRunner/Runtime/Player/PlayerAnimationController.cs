using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace RuinRunner
{
    /// <summary>Selected Sscary character only. Missing combat poses never load a different hero.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        public const string ArtRoot = "External/SscaryMale/";
        public const string FrameRoot = ArtRoot + "walk/";
        public const float IdleCycleSeconds = 1.6f;
        public const float WalkCyclesPerSecond = 1.5f;
        public static readonly string[] SourceDirections = { "right_up", "right_up", "right_down", "right_down", "left_down", "left_down", "left_up", "left_up" };
        private readonly Sprite[][] idle = new Sprite[8][], move = new Sprite[8][], dash = new Sprite[8][], death = new Sprite[8][];
        private PlayerController player;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer groundShadow;
        private PlayerMotionState currentState;
        private float stateTime, stride, idleTime, standingTime;
        private int poseClockFrame = -1;
        private WeaponDefinition poseWeapon;
        private long reloadOwner;
        private int reloadSequence, reloadEntry;
        private float reloadStartProgress, reloadInitialAngle;
        public float ReloadPresentationProgress { get; private set; }
        private bool guardedLastFrame, chargedLastFrame, parriedLastFrame, parrySucceededLastFrame;
        private float guardEntryTime, guardRecoveryTime, guardRecoveryPhase, guardInitialAngle;
        private float chargeInitialAngle, releaseInitialAngle, parryInitialAngle;
        private int chargeKey, releaseChargeKey, releaseSequence = -1, parryRecoveryKey;
        private int guardCatchClip = -1, guardCatchKey;
        private const float GuardCatchSeconds = .065f;
        public bool AuthoredWeaponRotationActive { get; private set; }
        public float AuthoredWeaponRotation { get; private set; }
        public Vector2 HandLocalPosition { get; private set; }
        public bool HandBehindBody { get; private set; }
        public int FacingDirection { get; private set; } = DiagonalHeroFacing.SouthEast;
        public int FrameIndex { get; private set; }
        public string PoseName { get; private set; }
        private static readonly Vector3 FootOffset = new Vector3(0, -.28f, 0);
        static bool artPrepared;

        internal static async Task PrepareAsync(CancellationToken cancellation)
        {
            if (artPrepared)
                return;
            // These generated type initializers contain only numeric
            // arrays/Vector2 values and a null sprite-cache field. Their large
            // constructors otherwise compile on the first rendered Town frame.
            // Never put Resources, Sprite or other Unity object calls in this job.
            var tableTimes = await Task.Run(() =>
            {
                var types = new[] {
                    typeof(SscaryArmedFrames), typeof(WeaponStandingFrames), typeof(WeaponReloadFrames), typeof(SscaryHurtFrames),
                    // Only their timing helpers are used in current poses. Do
                    // not load any archived sprites while preparing these tables.
                    typeof(SscaryMeleeFrames), typeof(SscaryShortBladeFrames), typeof(SscaryThrustFrames), typeof(SscaryDefenseFrames), typeof(SscaryRangedFrames)
                };
                var times = new double[types.Length];
                for (int i = 0; i < types.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    RuntimeHelpers.RunClassConstructor(types[i].TypeHandle);
                    times[i] = timer.Elapsed.TotalMilliseconds;
                }
                return times;
            }, cancellation);
            if (StageValidationInput.Enabled)
                Debug.Log("[Town Preparation] numeric tables worker ms: armed=" + tableTimes[0].ToString("F3") +
                    " standing=" + tableTimes[1].ToString("F3") + " reload=" + tableTimes[2].ToString("F3") + " hurt=" + tableTimes[3].ToString("F3") +
                    " timingHelpers=" + (tableTimes[4] + tableTimes[5] + tableTimes[6] + tableTimes[7] + tableTimes[8]).ToString("F3"));
            await NativeHeroFrameCatalog.PrepareAsync(cancellation);
            foreach (string pose in new[] { "idle", "walk", "dash", "death" })
                foreach (string direction in SourceDirections)
                    await IndividualArt.PrepareFramesAsync(ArtRoot + pose + "/" + direction, 8, cancellation);
            await Awaitable.NextFrameAsync(cancellation);
            foreach (var clip in NativeCombatEffect.SharedPixelFxClips())
            {
                NativeCombatEffect.Frames(clip);
                await Awaitable.NextFrameAsync(cancellation);
            }
            SscaryArmedFrames.Preload();
            await Awaitable.NextFrameAsync(cancellation);
            WeaponStandingFrames.Preload();
            await Awaitable.NextFrameAsync(cancellation);
            WeaponReloadFrames.Preload();
            await Awaitable.NextFrameAsync(cancellation);
            SscaryHurtFrames.Preload();
            cancellation.ThrowIfCancellationRequested();
            artPrepared = true;
        }

        public void Initialize(PlayerController owner, Transform visuals, SpriteRenderer renderer, GameObject details)
        {
            player = owner;
            body = owner.GetComponent<Rigidbody2D>();
            spriteRenderer = renderer;
            for (var direction = 0; direction < 8; direction++)
            {
                var id = SourceDirections[direction];
                idle[direction] = IndividualArt.Frames(ArtRoot + "idle/" + id, 8);
                move[direction] = IndividualArt.Frames(ArtRoot + "walk/" + id, 8);
                dash[direction] = IndividualArt.Frames(ArtRoot + "dash/" + id, 8);
                death[direction] = IndividualArt.Frames(ArtRoot + "death/" + id, 8);
            }
            foreach (var clip in NativeCombatEffect.SharedPixelFxClips())
                NativeCombatEffect.Frames(clip);
            SscaryArmedFrames.Preload();
            WeaponStandingFrames.Preload();
            WeaponReloadFrames.Preload();
            SscaryHurtFrames.Preload();
            if (details != null)
                details.SetActive(false);
            visuals.localScale = Vector3.one;
            visuals.localPosition = FootOffset;
            visuals.localRotation = Quaternion.identity;
            spriteRenderer.sprite = idle[FacingDirection][0];
            spriteRenderer.flipX = false;
            PoseName = "idle";
            HandLocalPosition = SourceHand("idle", FacingDirection, 0);
            var shadow = new GameObject("NativeGroundShadow");
            shadow.transform.SetParent(visuals, false);
            groundShadow = shadow.AddComponent<SpriteRenderer>();
            groundShadow.sprite = IndividualArt.Sprite(ArtRoot + "shadow");
            groundShadow.sortingLayerName = "WorldFloor";
            groundShadow.sortingOrder = 12;
        }

        private void LateUpdate()
        {
            if (player == null || spriteRenderer == null)
                return;
            if (player.IsDead)
            {
                ClearPreparation();
                PoseName = "death";
                FrameIndex = Mathf.Min(7, Mathf.FloorToInt(player.DeathProgress * 8));
                groundShadow.enabled = false;
                spriteRenderer.enabled = true;
                spriteRenderer.sprite = death[FacingDirection][FrameIndex];
                return;
            }
            if (player.AnimationPaused)
                return;
            if (currentState != player.MotionState)
            {
                currentState = player.MotionState;
                stateTime = 0;
            }
            else
                stateTime += Time.deltaTime;
            AdvancePoseClock();
            var weaponChanged = poseWeapon != player.EquippedWeapon;
            if (weaponChanged)
            {
                ClearPreparation();
                poseWeapon = player.EquippedWeapon;
            }
            if (currentState == PlayerMotionState.Dash)
                ClearPreparation();
            if (player.IsGuarding && !guardedLastFrame)
            {
                guardEntryTime = 0;
                guardRecoveryTime = 0;
                guardInitialAngle = PreviousWeaponAngle();
                guardCatchKey = FrameIndex;
                guardCatchClip = weaponChanged ? -1 : PoseName.StartsWith("reverse-sweep") ? 1 : PoseName.StartsWith("sweep") ? 0 : PoseName.StartsWith("thrust") ? 2 : -1;
            }
            if (player.IsGuarding || guardCatchClip >= 0 && guardEntryTime < GuardCatchSeconds && guardRecoveryTime > 0)
                guardEntryTime += Time.deltaTime;
            if (!player.IsGuarding && guardedLastFrame)
            {
                guardRecoveryTime = .14f;
                guardRecoveryPhase = GuardPoseProgress();
            }
            if (player.IsMeleeSwingActive || player.IsGreatswordCharging || player.IsParryPresentationActive)
                guardRecoveryTime = 0;
            if (player.IsGreatswordCharging && !chargedLastFrame)
                chargeInitialAngle = PreviousWeaponAngle();
            if (player.IsParryPresentationActive && !parriedLastFrame)
                parryInitialAngle = PreviousWeaponAngle();
            if (player.IsChargedCleaveActive && releaseSequence != player.AttackAnimationSequence)
            {
                releaseSequence = player.AttackAnimationSequence;
                releaseChargeKey = chargedLastFrame ? chargeKey : 0;
                releaseInitialAngle = PreviousWeaponAngle();
            }
            if (player.ParryPresentationSucceeded && !parrySucceededLastFrame)
                parryRecoveryKey = parriedLastFrame ? FrameIndex : 0;
            AuthoredWeaponRotationActive = false;
            var slot = player.ActiveWeaponState;
            var reloading = slot != null && slot.IsReloading;
            if (reloading)
            {
                if (reloadOwner != slot.Item.Identity || reloadSequence != slot.Item.ReloadSequence)
                {
                    reloadOwner = slot.Item.Identity;
                    reloadSequence = slot.Item.ReloadSequence;
                    reloadEntry = slot.Item.ReloadStartedFromEmpty || player.IsRangedPresentationActive ? 1 : 0;
                    reloadStartProgress = 0;
                    reloadInitialAngle = PreviousWeaponAngle();
                }
                // The last shot finishes its own recoil first. Fit the loading
                // motion into the remaining real timer instead of skipping its
                // retrieval frames or adding a gameplay delay. A swapped-in
                // ongoing reload resumes its real progress without restarting it.
                if (player.IsRangedPresentationActive && reloadEntry == 1)
                {
                    reloadStartProgress = slot.ReloadProgress;
                    reloadInitialAngle = PreviousWeaponAngle();
                }
                ReloadPresentationProgress = Mathf.InverseLerp(reloadStartProgress, 1, slot.ReloadProgress);
            }
            else
                ReloadPresentationProgress = 0;
            var action = player.IsMeleeSwingActive || player.IsRangedPresentationActive || player.IsWeaponActionActive || guardRecoveryTime > 0 || reloading;
            // The short visual return never blocks attack, movement or defense input.
            // Key zero is the action's native ready pose, then the arms relax to
            // the selected family stance while the same gait keeps advancing.
            if (action || weaponChanged || currentState == PlayerMotionState.Dash)
                standingTime = 0;
            else
                standingTime = Mathf.Min(WeaponStandingFrames.ReturnSeconds, standingTime + Time.deltaTime);
            var moving = body.linearVelocity.sqrMagnitude > .04f;
            // Keep a ranged stance aimed between shots as well as during recoil.
            // Otherwise strafing alternates movement-facing and aim-facing every shot.
            var aiming = player.EquippedWeapon != null && player.EquippedWeapon.AttackType == WeaponAttackType.Ranged;
            var direction = currentState == PlayerMotionState.Dash ? player.DashDirection :
                action || player.EquippedWeapon != null ? player.WeaponAimDirection : moving ? player.AnimationDirection : player.WeaponAimDirection;
            FacingDirection = DiagonalHeroFacing.Resolve(direction.x, direction.y, FacingDirection);
            Sprite selected;
            var authoredThrust = false;
            Vector2 thrustHand = Vector2.zero;
            HandBehindBody = false;
            if (currentState == PlayerMotionState.Dash)
            {
                PoseName = "dash";
                FrameIndex = Mathf.Min(7, Mathf.FloorToInt(stateTime / .16f * 8));
                selected = dash[FacingDirection][FrameIndex];
            }
            else if (player.IsBodyComboSwing)
            {
                int frame;
                selected = MeleeComboFrames.Sample(player.EquippedWeapon.Family, player.ActiveComboAnimationStep, FacingDirection, player.AttackAnimationProgress, out thrustHand, out var bladeAngle, out frame);
                FrameIndex = frame;
                authoredThrust = true;
                AuthoredWeaponRotationActive = true;
                AuthoredWeaponRotation = bladeAngle;
                PoseName = player.EquippedWeapon.Family + "-combo-" + (player.ActiveComboAnimationStep + 1);
            }
            else if (player.IsThrustActive)
            {
                // Sample the committed attack's real contact clock. The original
                // independent gait keeps advancing while the arm extends/retracts.
                FrameIndex = SscaryThrustFrames.PhaseIndex(player.AttackAnimationProgress);
                selected = SscaryArmedFrames.Thrust(FacingDirection, moving ? Mathf.FloorToInt(stride * 8) : -1, FrameIndex, out thrustHand);
                authoredThrust = true;
                PoseName = moving ? "thrust-move" : "thrust";
            }
            else if (player.IsMeleeSwingActive)
            {
                // Separate windup/forward arc/reverse arc/two-handed downward cut.
                // The contact key is the damage clock, independent of gait or FPS.
                var downward = player.ActiveMeleeMotion == MeleeAttackMotion.Cleave || player.ActiveMeleeMotion == MeleeAttackMotion.Impact;
                var clip = downward ? 2 : (player.ActiveComboAnimationStep & 1) == 1 ? 1 : 0;
                FrameIndex = SscaryMeleeFrames.PhaseIndex(player.AttackAnimationProgress);
                var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
                var compact = !downward && player.EquippedWeapon.Family == WeaponFamily.Dagger;
                bool behind;
                if (player.IsChargedCleaveActive)
                {
                    var p = player.AttackAnimationProgress;
                    var preparation = p < .18f;
                    FrameIndex = preparation ? Mathf.RoundToInt(Mathf.Lerp(releaseChargeKey, 15, p / .18f)) : SscaryDefenseFrames.PhaseIndex(3, p);
                    selected = SscaryArmedFrames.Defense(preparation ? 2 : 3, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                    AuthoredWeaponRotationActive = true;
                    AuthoredWeaponRotation = preparation ? Mathf.Lerp(releaseInitialAngle, 120, p / .18f) : MeleeMotionTiming.ActionAngle(p, true, -1, MeleeAttackMotion.Cleave);
                }
                else if (player.ActiveMeleeMotion == MeleeAttackMotion.CrossCut)
                {
                    var p = player.AttackAnimationProgress;
                    clip = MeleeMotionTiming.CrossStroke(p);
                    FrameIndex = SscaryShortBladeFrames.PhaseIndex(MeleeMotionTiming.CrossPoseProgress(p));
                    selected = SscaryArmedFrames.Short(clip, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                    AuthoredWeaponRotationActive = true;
                    AuthoredWeaponRotation = MeleeMotionTiming.CrossBladeAngle(p);
                }
                else
                    selected = compact ? SscaryArmedFrames.Short(clip, FacingDirection, variant, FrameIndex, out thrustHand, out behind)
                    : SscaryArmedFrames.Melee(clip, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                authoredThrust = true;
                HandBehindBody = behind;
                PoseName = (compact ? clip == 1 ? "dagger-backcut" : "dagger-cut" : downward ? "cleave" : clip == 1 ? "reverse-sweep" : "sweep") + (moving ? "-move" : "");
                if (player.IsChargedCleaveActive)
                    PoseName = "charge-cleave" + (moving ? "-move" : "");
                if (player.ActiveMeleeMotion == MeleeAttackMotion.CrossCut)
                    PoseName = (clip == 0 ? "cross-first" : "cross-return") + (moving ? "-move" : "");
            }
            else if ((player.IsGuarding || guardRecoveryTime > 0) && guardCatchClip >= 0 && guardEntryTime < GuardCatchSeconds)
            {
                // Finish the cancelled slash's existing native recovery before
                // raising guard. Defense is immediate; this never gates input.
                var phase = Mathf.Clamp01(guardEntryTime / GuardCatchSeconds);
                FrameIndex = Mathf.RoundToInt(Mathf.Lerp(guardCatchKey, 15, phase));
                var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
                if (guardCatchClip == 2)
                    selected = SscaryArmedFrames.Thrust(FacingDirection, moving ? Mathf.FloorToInt(stride * 8) : -1, FrameIndex, out thrustHand);
                else
                {
                    bool behind;
                    selected = SscaryArmedFrames.Melee(guardCatchClip, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                    HandBehindBody = behind;
                }
                authoredThrust = true;
                AuthoredWeaponRotationActive = true;
                AuthoredWeaponRotation = Mathf.Lerp(guardInitialAngle, 0, phase);
                PoseName = "guard-catch" + (moving ? "-move" : "");
                if (!player.IsGuarding)
                    guardRecoveryTime = Mathf.Max(0, guardRecoveryTime - Time.deltaTime);
            }
            else if (player.IsGuarding || guardRecoveryTime > 0 || player.IsParryPresentationActive || player.IsGreatswordCharging)
            {
                var clip = player.IsGreatswordCharging ? 2 : player.IsParryPresentationActive ? 1 : 0;
                var phase = clip == 2 ? Mathf.Clamp01(player.GreatswordChargeProgress * 2) : clip == 1 ? player.ParryPresentationProgress :
                    player.IsGuarding ? GuardPoseProgress() : guardRecoveryPhase * Mathf.Clamp01(guardRecoveryTime / .14f);
                FrameIndex = clip == 1 && player.ParryPresentationSucceeded ? Mathf.RoundToInt(Mathf.Lerp(parryRecoveryKey, 15, phase)) : SscaryDefenseFrames.PhaseIndex(clip, phase);
                var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
                bool behind;
                selected = SscaryArmedFrames.Defense(clip, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                authoredThrust = true;
                HandBehindBody = behind;
                AuthoredWeaponRotationActive = true;
                AuthoredWeaponRotation = clip == 2 ? Mathf.Lerp(chargeInitialAngle, 120, phase) : clip == 0 ? Mathf.Lerp(player.IsGuarding && guardCatchClip < 0 ? guardInitialAngle : 0, 58, phase) : FrameIndex <= 4 ? Mathf.Lerp(parryInitialAngle, -38, FrameIndex / 4f) : ParryAngle(FrameIndex);
                PoseName = (clip == 2 ? "charge" : clip == 1 ? player.ParryPresentationSucceeded ? "parry-success" : "parry" : player.IsGuarding ? "guard" : "guard-return") + (moving ? "-move" : "");
                if (clip == 2)
                    chargeKey = FrameIndex;
                if (!player.IsGuarding && clip == 0)
                    guardRecoveryTime = Mathf.Max(0, guardRecoveryTime - Time.deltaTime);
            }
            else if (currentState == PlayerMotionState.Hurt && !player.IsRangedPresentationActive && !reloading)
            {
                // Observe the real damage timer without cancelling attacks or
                // changing movement, ammo, reload, cooldown or commitment clocks.
                var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
                var profile = player.EquippedWeapon == null ? 6 : WeaponStandingFrames.Profile(player.EquippedWeapon.Family);
                FrameIndex = SscaryHurtFrames.PhaseIndex(player.HurtPresentationProgress);
                bool behind;
                selected = SscaryHurtFrames.Sample(profile, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                authoredThrust = true;
                HandBehindBody = behind;
                AuthoredWeaponRotationActive = player.EquippedWeapon != null;
                AuthoredWeaponRotation = profile == 6 ? 0 : WeaponStandingFrames.Angle(profile, 1, player.WeaponAimDirection);
                standingTime = WeaponStandingFrames.ReturnSeconds;
                PoseName = "hurt-" + (profile == 6 ? "unarmed" : player.EquippedWeapon.Family.ToString().ToLowerInvariant()) + (moving ? "-move" : "");
            }
            else if (aiming)
            {
                // Readying, release and recovery use one native body/socket clock.
                // Independent gait and breathing remain active between shots.
                var clip = RangedClip(player.EquippedWeapon.Family);
                var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
                bool behind;
                if (player.IsRangedPresentationActive)
                {
                    FrameIndex = SscaryRangedFrames.PhaseIndex(clip, player.RangedPresentationProgress);
                    selected = SscaryArmedFrames.Ranged(clip, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                }
                else if (reloading)
                {
                    FrameIndex = Mathf.Clamp(Mathf.FloorToInt(ReloadPresentationProgress * 16), 0, 15);
                    selected = WeaponReloadFrames.Sample(clip, reloadEntry, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                }
                else
                {
                    FrameIndex = StandingKey();
                    selected = WeaponStandingFrames.Sample(WeaponStandingFrames.Profile(player.EquippedWeapon.Family), FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                }
                authoredThrust = true;
                HandBehindBody = behind;
                AuthoredWeaponRotationActive = true;
                AuthoredWeaponRotation = player.IsRangedPresentationActive
                    ? RangedRecoilAngle(clip, player.RangedPresentationProgress, player.WeaponAimDirection)
                    : reloading ? Mathf.Lerp(reloadInitialAngle, 0, Mathf.SmoothStep(0, 1, Mathf.Clamp01(ReloadPresentationProgress / .28f)))
                    : WeaponStandingFrames.Angle(WeaponStandingFrames.Profile(player.EquippedWeapon.Family), standingTime / WeaponStandingFrames.ReturnSeconds, player.WeaponAimDirection);
                PoseName = (player.IsRangedPresentationActive ? "shoot-" : reloading ? "reload-" : "aim-") + RangedPoseName(clip) + (moving ? "-move" : "");
            }
            else if (player.EquippedWeapon != null)
            {
                var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
                bool behind;
                var family = player.EquippedWeapon.Family;
                var profile = WeaponStandingFrames.Profile(family);
                FrameIndex = StandingKey();
                selected = WeaponStandingFrames.Sample(profile, FacingDirection, variant, FrameIndex, out thrustHand, out behind);
                authoredThrust = true;
                HandBehindBody = behind;
                AuthoredWeaponRotationActive = true;
                AuthoredWeaponRotation = WeaponStandingFrames.Angle(profile, standingTime / WeaponStandingFrames.ReturnSeconds, player.WeaponAimDirection);
                PoseName = moving ? "armed-carry-move" : "armed-carry";
            }
            else if (moving)
            {
                // Preserve gait phase across aim/state transitions. Combat remains mobile.
                FrameIndex = Mathf.FloorToInt(stride * 8);
                selected = move[FacingDirection][FrameIndex];
                PoseName = action ? "combat-walk-pending" : "move";
            }
            else
            {
                FrameIndex = IdleFrame(idleTime);
                selected = idle[FacingDirection][FrameIndex];
                // Temporary same-character stance until authored combat frames pass review.
                PoseName = action ? "combat-stance-pending" : currentState == PlayerMotionState.Hurt ? "hurt-stance-pending" : "idle";
            }
            spriteRenderer.sprite = selected;
            spriteRenderer.flipX = false;
            HandLocalPosition = authoredThrust ? PixelHand(thrustHand) : SourceHand(currentState == PlayerMotionState.Dash ? "dash" : moving ? "walk" : "idle", FacingDirection, FrameIndex);
            guardedLastFrame = player.IsGuarding;
            chargedLastFrame = player.IsGreatswordCharging;
            parriedLastFrame = player.IsParryPresentationActive;
            parrySucceededLastFrame = player.ParryPresentationSucceeded;
        }

        private float PreviousWeaponAngle() => GetComponent<HeldWeaponView>()?.DisplayedActionRotation ?? 0f;
        private int StandingKey() => Mathf.Clamp(Mathf.RoundToInt(standingTime / WeaponStandingFrames.ReturnSeconds * 8), 0, 8);
        private void ClearPreparation()
        {
            guardedLastFrame = chargedLastFrame = parriedLastFrame = parrySucceededLastFrame = false;
            guardEntryTime = guardRecoveryTime = guardRecoveryPhase = 0;
            chargeKey = 0;
            releaseSequence = -1;
            guardCatchClip = -1;
            standingTime = 0;
            AuthoredWeaponRotationActive = false;
            AuthoredWeaponRotation = 0;
        }
        private float GuardPoseProgress() => guardCatchClip < 0 ? Mathf.Clamp01(guardEntryTime / .22f) : Mathf.Clamp01((guardEntryTime - GuardCatchSeconds) / (.22f - GuardCatchSeconds));
        private static float ParryAngle(int key)
        {
            if (key <= 4)
                return Mathf.Lerp(0, -38, key / 4f);
            if (key <= 7)
                return Mathf.Lerp(-38, -28, (key - 4) / 3f);
            if (key <= 10)
                return Mathf.Lerp(-28, 22, (key - 7) / 3f);
            return Mathf.Lerp(22, 0, (key - 10) / 5f);
        }

        public static int IdleFrame(float seconds) => Mathf.FloorToInt(Mathf.Repeat(seconds, IdleCycleSeconds) / IdleCycleSeconds * 8);
        private void AdvancePoseClock()
        {
            if (poseClockFrame == Time.frameCount || player.AnimationPaused)
                return;
            poseClockFrame = Time.frameCount;
            idleTime += Time.deltaTime;
            if (body.linearVelocity.sqrMagnitude > .04f && player.MotionState != PlayerMotionState.Dash)
            {
                // Reverse the existing footfall sequence while backing away; changing
                // direction preserves its phase and does not change physical movement.
                var backpedal = player.EquippedWeapon != null &&
                    Vector2.Dot(body.linearVelocity.normalized, player.WeaponAimDirection) < -.25f;
                var step = WalkCyclesPerSecond * Mathf.Clamp(body.linearVelocity.magnitude / 7f, 0f, 1.2f) * Time.deltaTime;
                stride = Mathf.Repeat(stride + (backpedal ? -step : step), 1);
            }
        }
        private static Vector2 SourceHand(string action, int direction, int frame)
        {
            var point = SscaryHandSockets.Pixel(action, direction, frame);
            return PixelHand(point);
        }
        private static Vector2 PixelHand(Vector2 point) => (Vector2)FootOffset + new Vector2((point.x - 24) / 32f, (42 - point.y) / 32f);
        public Vector2 MeleeHandPosition(Vector2 aim, MeleeAttackMotion motion, int comboStep, bool charged, float progress)
        {
            // Damage is resolved in Update, before body/held-weapon LateUpdate.
            // Read the same authored socket and idempotent gait clock that the
            // visible body will use, instead of last frame's renderer transform.
            AdvancePoseClock();
            var moving = body.linearVelocity.sqrMagnitude > .04f;
            var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
            var direction = DiagonalHeroFacing.Resolve(aim.x, aim.y, FacingDirection);
            if (player.IsBodyComboSwing)
                return PixelHand(MeleeComboFrames.HandFor(player.EquippedWeapon.Family, comboStep, direction, progress));
            if (motion == MeleeAttackMotion.CrossCut)
            {
                Vector2 crossHand;
                bool crossBehind;
                SscaryArmedFrames.Short(MeleeMotionTiming.CrossStroke(progress), direction, variant,
                    SscaryShortBladeFrames.PhaseIndex(MeleeMotionTiming.CrossPoseProgress(progress)), out crossHand, out crossBehind);
                return PixelHand(crossHand);
            }
            if (motion == MeleeAttackMotion.Thrust)
            {
                Vector2 thrustHand;
                SscaryArmedFrames.Thrust(direction, moving ? Mathf.FloorToInt(stride * 8) : -1, SscaryThrustFrames.PhaseIndex(progress), out thrustHand);
                return PixelHand(thrustHand);
            }
            var downward = motion == MeleeAttackMotion.Cleave || motion == MeleeAttackMotion.Impact;
            var clip = downward ? 2 : (comboStep & 1) == 1 ? 1 : 0;
            var frame = SscaryMeleeFrames.PhaseIndex(progress);
            Vector2 hand;
            bool behind;
            if (charged)
                SscaryArmedFrames.Defense(3, direction, variant, SscaryDefenseFrames.PhaseIndex(3, progress), out hand, out behind);
            else if (!downward && player.EquippedWeapon.Family == WeaponFamily.Dagger)
                SscaryArmedFrames.Short(clip, direction, variant, frame, out hand, out behind);
            else
                SscaryArmedFrames.Melee(clip, direction, variant, frame, out hand, out behind);
            return PixelHand(hand);
        }
        public Vector2 EmissionHandPosition(Vector2 aim)
        {
            // Shot emission occurs in Player.Update. Sample the same clock once so
            // this origin and the later visible body/held barrel use one frame.
            AdvancePoseClock();
            var moving = body.linearVelocity.sqrMagnitude > .04f;
            var variant = moving ? 8 + Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime);
            if (player.EquippedWeapon != null && player.EquippedWeapon.AttackType == WeaponAttackType.Ranged)
            {
                Vector2 hand;
                bool behind;
                SscaryArmedFrames.Ranged(RangedClip(player.EquippedWeapon.Family), DiagonalHeroFacing.Resolve(aim.x, aim.y, FacingDirection), variant, 0, out hand, out behind);
                return PixelHand(hand);
            }
            return SourceHand(moving ? "walk" : "idle", DiagonalHeroFacing.Resolve(aim.x, aim.y, FacingDirection), moving ? Mathf.FloorToInt(stride * 8) : IdleFrame(idleTime));
        }
        public static int RangedClip(WeaponFamily family) => family == WeaponFamily.Staff || family == WeaponFamily.Orb ? 2 : family == WeaponFamily.Bow ? 0 : family == WeaponFamily.Crossbow ? 1 : family == WeaponFamily.Pistol ? 2 : family == WeaponFamily.Rifle ? 3 : family == WeaponFamily.Shotgun ? 4 : 5;
        private static string RangedPoseName(int clip) => clip == 0 ? "bow" : clip == 1 ? "crossbow" : clip == 2 ? "pistol" : clip == 3 ? "rifle" : clip == 4 ? "shotgun" : "cannon";
        private static float RangedRecoilAngle(int clip, float phase, Vector2 aim)
        {
            // Barrel kick follows the same release/recovery keys; zero at emission.
            float kick = phase < .18f ? Mathf.SmoothStep(0, 1, phase / .18f) : 1 - Mathf.SmoothStep(0, 1, (phase - .18f) / .82f);
            float degrees = clip == 0 ? 2 : clip == 1 ? 3 : clip == 2 ? 9 : clip == 3 ? 4 : clip == 4 ? 8 : 12;
            return degrees * kick * (aim.x < 0 ? -1 : 1);
        }
        public static int DirectionIndex(Vector2 direction)
        {
            return DiagonalHeroFacing.Resolve(direction.x, direction.y, DiagonalHeroFacing.SouthEast, 0);
        }
        public static Vector2 ShootingHandPosition(Vector2 aim, int frame) => SourceHand("idle", DirectionIndex(aim), frame);
    }
}
