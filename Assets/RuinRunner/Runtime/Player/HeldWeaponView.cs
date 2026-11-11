using UnityEngine;

namespace RuinRunner
{
    /// <summary>
    /// 캐릭터 애니메이션과 장착 무기 이미지를 분리한다. 인벤토리와 같은 아이콘 셀을
    /// 사용하므로 퀵슬롯을 바꾸면 별도의 캐릭터 스프라이트 없이 손의 무기도 바뀐다.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class HeldWeaponView : MonoBehaviour
    {
        private PlayerController player;
        private PlayerAnimationController bodyAnimation;
        private Transform weaponPivot;
        private SpriteRenderer weaponRenderer;
        private SpriteRenderer bodyRenderer;
        private string currentWeaponId;
        private int lastAttackSequence;
        private float attackTime;
        private float displayedAngle;
        private bool angleInitialized;

        public string CurrentWeaponId => currentWeaponId;
        public Sprite RendererSprite => weaponRenderer == null ? null : weaponRenderer.sprite;
        internal SpriteRenderer VisualRenderer => weaponRenderer;
        public Vector2 BladePoint(float distance)
        {
            if (weaponPivot == null || player.EquippedWeapon == null)
                return transform.position;
            var forward = NativeWeaponArt.TryGet(player.EquippedWeapon.ArtId, out var pose) ? pose.ForwardAngle : 45f;
            var axis = new Vector2(Mathf.Cos(forward * Mathf.Deg2Rad), Mathf.Sin(forward * Mathf.Deg2Rad));
            if (weaponRenderer.flipY)
                axis.y = -axis.y;
            return weaponPivot.TransformPoint(axis * distance);
        }

        public void Initialize(PlayerController owner)
        {
            player = owner;
            bodyAnimation = owner.GetComponent<PlayerAnimationController>();
            var bodyVisual = transform.Find("VisualRoot");
            bodyRenderer = bodyVisual == null ? null : bodyVisual.GetComponent<SpriteRenderer>();

            var pivotObject = new GameObject("HeldWeaponPivot");
            pivotObject.transform.SetParent(transform, false);
            weaponPivot = pivotObject.transform;

            var visualObject = new GameObject("HeldWeaponSprite");
            visualObject.transform.SetParent(weaponPivot, false);
            weaponRenderer = visualObject.AddComponent<SpriteRenderer>();
            weaponRenderer.sortingOrder = 12;
            weaponRenderer.color = Color.white;

            lastAttackSequence = player == null ? 0 : player.AttackAnimationSequence;
            RefreshWeapon(true);
        }

        private void LateUpdate()
        {
            if (player == null || weaponPivot == null || weaponRenderer == null)
                return;
            if (player.IsDead)
            {
                weaponRenderer.enabled = false;
                return;
            }
            if (player.AnimationPaused)
                return;

            RefreshWeapon(false);
            weaponRenderer.enabled = weaponRenderer.sprite != null && (bodyRenderer == null || bodyRenderer.enabled);
            if (!weaponRenderer.enabled)
                return;
            weaponRenderer.color = bodyRenderer == null ? Color.white : bodyRenderer.color;

            if (lastAttackSequence != player.AttackAnimationSequence)
            {
                lastAttackSequence = player.AttackAnimationSequence;
                attackTime = 0f;
            }
            else
            {
                attackTime += Time.deltaTime;
            }

            var weapon = player.EquippedWeapon;
            var aim = player.WeaponAimDirection.sqrMagnitude > .001f
                ? player.WeaponAimDirection.normalized
                : Vector2.right * player.Facing;
            weaponRenderer.sortingOrder = (bodyRenderer == null ? 1000 : bodyRenderer.sortingOrder)
                + (weapon.AttackType == WeaponAttackType.Ranged || aim.y > .12f || bodyAnimation != null && bodyAnimation.HandBehindBody ? -1 : 1);
            // Native ranged hands wrap the grip in front. Stocks and bow strings
            // crossing the upper body remain behind the unchanged face/backpack.
            var flipped = aim.x < 0f;
            weaponRenderer.flipY = flipped;

            var sourceForwardAngle = NativeWeaponArt.TryGet(weapon.ArtId, out var nativePose) ? nativePose.ForwardAngle : RangedWeaponArt.TryGet(weapon.ArtId, out var landmarks)
                ? landmarks.ForwardAngle : IsBladeFamily(weapon.Family) ? 45f : 0f;
            if (flipped)
                sourceForwardAngle = -sourceForwardAngle;
            var targetAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - sourceForwardAngle;
            // Carry the barrel down during evasion; the native dash arm is raised
            // near the shoulder and a level barrel would cross the face.
            var dashCarry = player.MotionState == PlayerMotionState.Dash;
            if (dashCarry)
            {
                targetAngle = (flipped ? -115f : -65f) - sourceForwardAngle;
                // The raised dash hand overlaps stocks/handles. Keep the same
                // body occlusion as native armed poses in every dash direction.
                weaponRenderer.sortingOrder = (bodyRenderer == null ? 1000 : bodyRenderer.sortingOrder)
                    + (weapon.AttackType == WeaponAttackType.Ranged || player.DashDirection.y > .12f ? -1 : 1);
            }
            DisplayedActionRotation = GetActionRotation(weapon);
            targetAngle += DisplayedActionRotation;
            if (player.IsMeleeSwingActive)
                targetAngle += player.MeleeAngleOffset;

            if (!angleInitialized || player.IsMeleeSwingActive || player.IsRangedShotActive || dashCarry || bodyAnimation != null && bodyAnimation.AuthoredWeaponRotationActive)
            {
                displayedAngle = targetAngle;
                angleInitialized = true;
            }
            else
            {
                var responsiveness = player.MotionState == PlayerMotionState.Attack || player.MotionState == PlayerMotionState.HeavyAttack
                    ? 30f
                    : 20f;
                var smoothing = 1f - Mathf.Exp(-responsiveness * Time.deltaTime);
                displayedAngle = Mathf.LerpAngle(displayedAngle, targetAngle, smoothing);
            }

            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, displayedAngle);
            if (!dashCarry && IsBladeFamily(weapon.Family))
            {
                weaponRenderer.sortingOrder = (bodyRenderer == null ? 1000 : bodyRenderer.sortingOrder)
                    - 2;
                // Native hands already wrap each grip. A hilt or broad guard
                // reaches back from the socket even with a forward-facing blade,
                // so direction-based front sorting would cover the face/sleeve
                // during the swing and neutral recovery. Let the authored body
                // occlude that overlap while keeping the exposed blade visible.
                // Reserve the intervening order for melee light: blade < light
                // < the original face/hand silhouette, with no sorting ties.
            }
            if (dashCarry && IsBladeFamily(weapon.Family))
                weaponRenderer.sortingOrder = (bodyRenderer == null ? 1000 : bodyRenderer.sortingOrder) - 2;
            weaponPivot.localPosition = GetHandPosition(weapon, aim);
            weaponPivot.localScale = Vector3.one * GetDisplayScale(weapon) * (1f + SwapPop * Mathf.Clamp01(1f - (Time.unscaledTime - swappedAt) / SwapPopSeconds));
        }

        const float SwapPop = .45f, SwapPopSeconds = .16f;
        float swappedAt = -9f;
        private void RefreshWeapon(bool force)
        {
            var weapon = player == null ? null : player.EquippedWeapon;
            var nextId = weapon == null ? null : weapon.Id;
            if (!force && nextId == currentWeaponId)
                return;

            // A new weapon arrives in the hand with a flourish, not by being already there.
            if (!force && currentWeaponId != null && weapon != null)
            {
                swappedAt = Time.unscaledTime;
                NativeCombatEffect.Create("RewardRing", (Vector2)transform.position + Vector2.down * .2f, 0, .55f, .24f, RewardPresentation.Color(weapon.Rarity));
            }
            currentWeaponId = nextId;
            // An orb floats on its own; there is nothing in the hand.
            weaponRenderer.sprite = weapon != null && weapon.Family == WeaponFamily.Orb ? null : PixelArtAtlas.GetHeldWeaponSprite(weapon);
            weaponRenderer.enabled = weaponRenderer.sprite != null;
            angleInitialized = false;
            attackTime = 10f;
        }

        private Vector3 GetHandPosition(WeaponDefinition weapon, Vector2 aim)
        {
            var facing = aim.x < 0f ? -1f : 1f;
            var position = bodyAnimation == null
                ? new Vector3(.1f * facing, .02f, 0f)
                : (Vector3)bodyAnimation.HandLocalPosition;

            // The authored body socket already follows its extended glove.

            if (bodyAnimation == null && weapon.AttackType == WeaponAttackType.Ranged && attackTime < .18f)
            {
                var recoil = 1f - Mathf.Clamp01(attackTime / .18f);
                position -= (Vector3)(aim * (.09f * recoil));
            }

            return position;
        }

        public float DisplayedActionRotation { get; private set; }
        private float GetActionRotation(WeaponDefinition weapon)
        {
            if (bodyAnimation != null && bodyAnimation.AuthoredWeaponRotationActive)
                return bodyAnimation.AuthoredWeaponRotation;
            if (weapon.AttackType == WeaponAttackType.Ranged)
                return 0f;
            if (player.IsThrustActive)
                return 0f; // Blade stays aligned with the committed stab, never sweeps.

            if (player.IsGuarding || player.IsParrying)
                return player.IsParrying ? -28f : 58f;

            if (player.IsGreatswordCharging)
                return 120f;
            if (!player.IsMeleeSwingActive)
                return 0f;

            var heavy = player.MotionState == PlayerMotionState.HeavyAttack;
            var light = player.MotionState == PlayerMotionState.Attack;
            if (!heavy && !light)
                return 0f;

            return MeleeMotionTiming.ActionAngle(player.AttackAnimationProgress, heavy, player.ActiveComboAnimationStep, player.ActiveMeleeMotion);
        }

        private static bool IsBladeFamily(WeaponFamily family)
        {
            return family == WeaponFamily.Dagger ||
                   family == WeaponFamily.Longsword ||
                   family == WeaponFamily.Mace ||
                   family == WeaponFamily.Katana ||
                   family == WeaponFamily.Greatsword ||
                   family == WeaponFamily.Spear;
        }

        private static float GetDisplayScale(WeaponDefinition weapon)
        {
            // Reviewed greatswords already have a longer, wider native silhouette.
            // Keep their texel density equal to guns and the hero instead of stretching them.
            if (NativeWeaponArt.Supports(weapon.ArtId))
                return weapon.Family == WeaponFamily.Bow ? .9f : 1f;
            switch (weapon.Family)
            {
                case WeaponFamily.Dagger:
                    return .56f;
                case WeaponFamily.Longsword:
                    return .76f;
                case WeaponFamily.Mace:
                    return .72f;
                case WeaponFamily.Katana:
                    return .78f;
                case WeaponFamily.Greatsword:
                    return .98f;
                case WeaponFamily.Bow:
                    return .82f;
                case WeaponFamily.Crossbow:
                    return .72f;
                case WeaponFamily.Pistol:
                    return .58f;
                case WeaponFamily.Rifle:
                    return .83f;
                case WeaponFamily.Shotgun:
                    return .86f;
                default:
                    return .96f;
            }
        }

        public static Vector2 ShotMuzzlePosition(WeaponDefinition weapon, Transform owner, Vector2 aim)
        {
            var sprite = PixelArtAtlas.GetHeldWeaponSprite(weapon);
            if (sprite == null || !RangedWeaponArt.TryGet(weapon.ArtId, out var landmarks))
                return (Vector2)owner.position + aim * .75f;
            var muzzle = (new Vector2(landmarks.Muzzle.x, sprite.rect.height - landmarks.Muzzle.y) - sprite.pivot) / sprite.pixelsPerUnit;
            var flipped = aim.x < 0;
            if (flipped)
                muzzle.y = -muzzle.y;
            var angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - (flipped ? -landmarks.ForwardAngle : landmarks.ForwardAngle);
            var offset = (Vector2)(Quaternion.Euler(0, 0, angle) * muzzle) * GetDisplayScale(weapon);
            // Emission happens in Update, before LateUpdate selects the shot body.
            // Use that same authored emission socket instead of last frame's pose.
            var animation = owner.GetComponent<PlayerAnimationController>();
            var hand = animation == null ? PlayerAnimationController.ShootingHandPosition(aim, 0) : animation.EmissionHandPosition(aim);
            return owner.TransformPoint(hand + offset);
        }
    }
}
