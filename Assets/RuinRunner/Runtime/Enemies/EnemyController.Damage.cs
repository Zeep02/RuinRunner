using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Damage, health bar updates and death sequences.</summary>
    public sealed partial class EnemyController
    {
        public void MuteNextHitFx() => muteNextHitFx = true;
        public void Stagger(float seconds)
        {
            if (!IsBoss && Type != EnemyType.RuneTotem && !Unstoppable)
                hitStun = Mathf.Max(hitStun, seconds);
        }

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
            if (dead || IntroLocked || roarInvulnerable)
                return;

            if (player != null)
                amount *= player.TalentDamageAgainst(this);
            amount = StageMobDamage(amount);
            var appliedDamage = Mathf.Max(1f, amount * 100f / (100f + Armor));
            DiagnosticsHooks.EnemyDamaged?.Invoke(this, appliedDamage, originalImpact, critical);
            Health -= appliedDamage;
            DamageNumber.Show(transform.position, appliedDamage, critical, originalImpact);
            if (!collisionAttackActive && !IsBoss)
                LinearVelocity = knockback;
            UpdateHealthBar();
            // Only the weapon's own hit plays the hit cue and camera impact. Item effects
            // have their own cue and never use up the camera's impact throttle.
            if (originalImpact)
            {
                GameAudio.Play(GameSound.Hit, AudioChannel.Monster, critical || Type == EnemyType.Boss ? .55f : .35f);
                if (cameraFollow != null)
                    cameraFollow.Impact(critical, IsBoss);
                if (Health > 0f && !muteHitFx)
                    Fx(critical ? "FxBluntHit" : "FxHitSpark", (Vector2)transform.position + Vector2.up * .4f, critical ? .3f : .2f, AngleOf(knockback));
                hitPunch = critical ? 1f : .65f;
                if (!IsBoss && Type != EnemyType.RuneTotem && !Unstoppable && !acting)
                    hitStun = critical ? .32f : .22f;
            }
            // One owned clock: an older contact must not clear a newer flash,
            // and a hit must not permanently replace an attack's warning tint.
            hitFlashRemaining = .055f;
            hitFlashFrame = Time.frameCount;
            ApplyPresentationTint();
            if (bossAnimator != null && Health > 0f)
                bossAnimator.Hit();
            if (Health > 0f && (ExternalCreatureArt.Supports(Type) || Type == EnemyType.Tidekeeper || Type == EnemyType.SealHeart))
                GetComponent<EnemyPixelAnimator>().Hit();
            if (Health <= 0f)
            {
                if (IsBoss)
                    BeginBossDeath(Mathf.Max(1f, Health + appliedDamage));
                else
                {
                    muteDeathFlash = muteHitFx;
                    Die();
                }
            }
        }

        private void UpdateHealthBar()
        {
            var ratio = Mathf.Clamp01(Health / MaxHealth);
            healthFill.localScale = new Vector3(ratio * HealthInner, .6f, 1f);
            healthFill.localPosition = new Vector3((ratio - 1f) * .5f * HealthInner, 0f, -.01f);
            if (ratio < healthShownRatio && !IsBoss && !dead && healthBarRoot != null)
            {
                healthBarHideAt = Time.time + 3f;
                healthBarRoot.gameObject.SetActive(true);
            }
            if (ratio > healthShownRatio)
                healthShownRatio = ratio;
            PlaceHealthTrail();
        }
        private void PlaceHealthTrail()
        {
            healthTrail.localScale = new Vector3(healthShownRatio * HealthInner, .6f, 1f);
            healthTrail.localPosition = new Vector3((healthShownRatio - 1f) * .5f * HealthInner, 0f, -.005f);
        }
        private void TickHealthBar()
        {
            if (healthBarRoot == null || !healthBarRoot.gameObject.activeSelf)
                return;
            var ratio = Mathf.Clamp01(Health / MaxHealth);
            if (healthShownRatio > ratio)
            {
                healthShownRatio = Mathf.MoveTowards(healthShownRatio, ratio, Time.deltaTime * .9f);
                PlaceHealthTrail();
            }
            if (Time.time >= healthBarHideAt)
                healthBarRoot.gameObject.SetActive(false);
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
            if (!muteDeathFlash)
                BossNativeEffect.CreateOverActors(null, "FxBluntHit", (Vector2)transform.position + Vector2.up * .3f, .36f);
            if (cameraFollow != null && Type != EnemyType.Sporeling)
                cameraFollow.KillImpact();
            if (onDied != null)
                onDied(this);
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
            var animator = GetComponent<EnemyPixelAnimator>();
            var frames = ExternalCreatureArt.Supports(Type) ? ExternalCreatureArt.Frames(Type, 3, animator.ArtDirection) : PixelArtAtlas.GetEnemyFrames(Type, 3);
            spriteRenderer.color = Color.white;
            if (frames == null)
            {
                // Only art without an authored death clip uses this fallback; do not synthesize a new pose.
                var elapsed = 0f;
                while (elapsed < .25f)
                {
                    if (AnimationPaused)
                    {
                        yield return null;
                        continue;
                    }
                    elapsed += Time.deltaTime;
                    SettleAirborneDeath(deathVisualStart, elapsed);
                    spriteRenderer.color = new Color(1, 1, 1, 1 - elapsed / .25f);
                    yield return null;
                }
                Destroy(gameObject);
                yield break;
            }
            // Use one elapsed clock. Per-frame WaitForSeconds adds a render-frame
            // delay ten times, stretching death noticeably on slower machines.
            var deathElapsed = 0f;
            var duration = frames.Length * .1f;
            while (deathElapsed < duration)
            {
                if (AnimationPaused)
                {
                    yield return null;
                    continue;
                }
                SettleAirborneDeath(deathVisualStart, deathElapsed);
                spriteRenderer.sprite = frames[Mathf.Min(frames.Length - 1, (int)(deathElapsed / .1f))];
                yield return null;
                deathElapsed += Time.deltaTime;
            }
            Destroy(gameObject);
        }

        private void SettleAirborneDeath(Vector3 start, float elapsed)
        {
            if (!airborneVisual || visualRoot == null)
                return;
            // Keep the current pose at the lethal hit, then fall into the authored
            // collapse. Neither the physics root nor reward timing is moved.
            var progress = Mathf.Clamp01(elapsed / .22f);
            visualRoot.localPosition = Vector3.Lerp(start, leapVisualBasePosition, progress * progress);
            if (progress >= 1f)
                airborneVisual = false;
        }

        private void BeginBossDeath(float drainFromHealth)
        {
            dead = true;
            if (damageCollider != null)
                damageCollider.enabled = false;
            if (enemyCollider != null)
                enemyCollider.enabled = false;
            ClearLandingTelegraph();
            acting = true;
            AttackVisualPhase = 0;
            collisionAttackActive = false;
            AttackVisualPhase = 2;
            StopAllCoroutines();
            LinearVelocity = Vector2.zero;
            if (body != null)
                body.simulated = false;
            if (cameraFollow != null)
                cameraFollow.BossDefeated();
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
            if (bossAnimator != null)
                bossAnimator.Death(0);
            while (elapsed < duration)
            {
                // Boss rewards settle after this sequence. Pause must suspend
                // both the visible death and its eventual completion callback.
                if (AnimationPaused)
                {
                    yield return null;
                    continue;
                }
                SettleAirborneDeath(deathVisualStart, elapsed);
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                Health = Mathf.Lerp(drainFromHealth, 0f, progress);
                UpdateHealthBar();

                if (bossAnimator != null)
                    bossAnimator.Death(progress);
                else if ((int)Type >= (int)EnemyType.Tidekeeper)
                {
                    if (Type == EnemyType.SealHeart)
                    {
                        spriteRenderer.sprite = SealHeartArt.Death(GetComponent<EnemyPixelAnimator>().ArtDirection, progress);
                        // The separate collapse stays visible through its falling
                        // crown/core and inert rubble poses, then settles away.
                        spriteRenderer.color = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .9f) / .1f)));
                        if (elapsed - Time.deltaTime < duration * .7f && elapsed >= duration * .7f)
                        {
                            BossNativeEffect.Create(this, "SlamDust", transform.position, .3f);
                        }
                    }
                    else
                    {
                        var frames = PixelArtAtlas.GetEnemyFrames(Type, 3);
                        spriteRenderer.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(progress * frames.Length))];
                    }
                }

                yield return null;
            }

            Health = 0f;
            UpdateHealthBar();
            if (onDied != null)
                onDied(this);
            Destroy(gameObject);
        }
    }
}
