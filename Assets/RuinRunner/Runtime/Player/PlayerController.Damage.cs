using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Damage, death, teleport and presentation locks.</summary>
    public sealed partial class PlayerController
    {
        public bool SandboxInvulnerable { get; set; }
        internal void SandboxRefresh()
        {
            sigReady = 0;
            if (sig == null || sig.Id != "siege_engineer" || sig.Stage != 1)
                sigLockUntil = 0;
            attackTimer = 0;
            weaponActionCooldown = 0;
            Stats.RestoreMana(Stats.MaxMana);
        }

        // Held still while the ink has them, without pausing the world around them.
        bool entranceLock;
        public void SetEntranceLock(bool locked)
        {
            entranceLock = locked;
            if (!locked)
                return;
            movementInput = Vector2.zero;
            CancelMeleeAttack();
            CancelWeaponAction();
        }

        /// <summary>Nothing of the hero is drawn while the ink holds them, and their weapon stays gone until it has run off.</summary>
        public void HideForInk(bool body, bool weapon)
        {
            var view = GetComponentInChildren<HeldWeaponView>(true);
            var held = view == null ? null : view.VisualRenderer;
            foreach (var part in GetComponentsInChildren<Renderer>(true))
                part.forceRenderingOff = part == held ? weapon : body;
        }

        public void ApplyPush(Vector2 velocity)
        {
            if (!dead && !dashing)
                knockbackVelocity += velocity;
        }

        public void TakeDamage(float amount, Vector2 direction)
        {
            if (SandboxInvulnerable)
                return;
            if (amount > 0f && !dead && dashing && invincibilityTicks > 0)
                TalentOnDodge();
            if (amount <= 0f || dead || entranceLock || stageClearSafety || invincibilityTicks > 0 || (isPaused != null && isPaused()))
                return;
            if (SigBlocks(direction))
                return;
            if (IsParrying && EquippedWeapon != null && EquippedWeapon.Family == WeaponFamily.Katana)
            {
                parryTimer = 0f;
                weaponActionTimer = .22f;
                ParryPresentationSucceeded = true;
                invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.FromSeconds(.22f));
                katanaAutoTimer = .04f;
                katanaAutoCutPending = true;
                katanaCounterSucceeded = true;
                OnActiveDefense();
                GameAudio.Play(GameSound.Block, AudioChannel.Player, .6f);
                if (sig != null)
                    SigOnParry();
                if (cameraFollow != null)
                    cameraFollow.Shake(.12f);
                return;
            }

            var received = ItemEffects.AbsorbDamage(ItemEffects.AbsorbManaAegis(amount));
            var shielded = received <= 0f;
            if (received > 0f)
            {
                bloodStacks = 0;
                Stats.ReceiveDamage(received);
                Grow(CombatGrowthKind.DamageTaken);
                ItemEffects.OnHurt();
            }
            if (StageValidationInput.Enabled)
                Debug.Log("[Combat Input] Hit raw=" + amount + " " + " hp=" + Health.ToString("0.0") + " position=" + transform.position + " motion=" + MotionState);
            invincibilityTicks = HitInvincibilityTicks;
            hurtPoseTimer = shielded ? 0f : HurtPresentationSeconds;
            // A shield that fully absorbed the hit holds the character steady.
            // Partial absorption keeps the existing damage/knockback response.
            knockbackVelocity += !shielded && direction.sqrMagnitude > .01f ? direction.normalized * 5f : Vector2.zero;
            if (!shielded)
            {
                var towardAttacker = direction.sqrMagnitude > .01f ? -direction.normalized : AimDirection;
                var contact = (Vector2)transform.position + towardAttacker * .35f + Vector2.up * .08f;
                NativeCombatEffect.Create("PlayerHit", contact, 0, 29f / 32f, .18f, Color.white,
                    spriteRenderer.sortingOrder - 3)?.BelowActor(spriteRenderer, -3);
            }
            GameAudio.Play(shielded ? GameSound.Block : GameSound.Hurt,
                AudioChannel.Player, shielded ? .4f : .6f);
            if (cameraFollow != null)
                cameraFollow.Shake(shielded ? .035f : .2f);
            if (damageFlashRoutine != null)
                StopCoroutine(damageFlashRoutine);
            var flash = shielded ? new Color(.62f, .9f, 1f) : new Color(1f, .62f, .62f);
            damageFlashRoutine = StartCoroutine(DamageFlash(flash, shielded ? .06f : .1f));
            if (Health <= 0f && ItemEffects.TryCheatDeath())
                invincibilityTicks = HitInvincibilityTicks * 2;
            if (Health <= 0f)
                TalentRevive();
            if (Health <= 0f)
            {
                dead = true;
                actionCancelSequence++;
                ItemEffects.CancelTransient();
                CancelMeleeAttack();
                CancelWeaponAction();
                StopAllCoroutines();
                damageFlashRoutine = null;
                LinearVelocity = Vector2.zero;
                movementInput = Vector2.zero;
                knockbackVelocity = Vector2.zero;
                spriteRenderer.enabled = true;
                spriteRenderer.color = RestingColor;
                onDied?.Invoke(); // Account for the defeat immediately, even if the user exits during the pose.
                StartCoroutine(DeathSequence());
            }
        }

        private IEnumerator DeathSequence()
        {
            var elapsed = 0f;
            while (elapsed < .85f)
            {
                elapsed += Time.unscaledDeltaTime;
                DeathProgress = Mathf.Clamp01(elapsed / .85f);
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
                    if (elapsed >= flashSeconds)
                        tint.a = .86f + .14f * Mathf.Cos(elapsed * Mathf.PI * 10f);
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
            if (body != null)
                body.position = position;
            LinearVelocity = Vector2.zero;
            movementInput = Vector2.zero;
            knockbackVelocity = Vector2.zero;
        }

        public void CancelCombatForPresentation()
        {
            actionCancelSequence++;
            observedCancelSequence = actionCancelSequence;
            combatInputNeedsRelease = true;
            CancelMeleeAttack();
            CancelWeaponAction();
            ClearPlantedRound();
            ItemEffects?.CancelTransient();
            GetComponent<DashAfterimageEffect>()?.CancelTransient();
            dashing = false;
            movementInput = Vector2.zero;
            knockbackVelocity = Vector2.zero;
            LinearVelocity = Vector2.zero;
            // Paid cooldowns, dash charges, mana and each owned magazine/reload
            // deliberately survive a camera presentation or encounter transition.
        }

        public void SetStageClearSafety(bool enabled)
        {
            stageClearSafety = enabled;
            if (!enabled)
                return;
            actionCancelSequence++;
            ItemEffects.CancelTransient();
            invincibilityTicks = Mathf.Max(invincibilityTicks, GameTick.TicksPerSecond * 2);
            CancelMeleeAttack();
            CancelWeaponAction();
        }
    }
}
