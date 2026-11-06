using UnityEngine;

namespace RuinRunner
{
    /// <summary>Sprite, tint and health bar presentation.</summary>
    public sealed partial class EnemyController
    {
        const float BodyMeasureMargin = .03f;
        public void UpdateVisualPresentation()
        {
            if (damageCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
                return;
            var balance = presentationBalance;
            // Authored body envelopes in world units. Neither attack extremities nor
            // a change of PNG can resize the target or move its health bar.
            Vector2 size;
            float centerY;
            switch (Type)
            {
                case EnemyType.Grunt:
                    size = new Vector2(.90f, .65f);
                    centerY = .33f;
                    break;
                case EnemyType.Shooter:
                    size = new Vector2(.55f, .80f);
                    centerY = .40f;
                    break;
                case EnemyType.Charger:
                    size = new Vector2(.95f, .72f);
                    centerY = .36f;
                    break;
                case EnemyType.Leaper:
                    size = new Vector2(.80f, .82f);
                    centerY = .41f;
                    break;
                // Idle art around the foot pivot: a blow that touches the drawn body lands.
                case EnemyType.Boss:
                    size = new Vector2(3.34f, 2.19f);
                    centerY = 1.08f;
                    break;
                case EnemyType.Tidekeeper:
                    size = new Vector2(2.5f, 2.45f);
                    centerY = 1.43f;
                    break;
                case EnemyType.SealHeart:
                    size = new Vector2(2.6f, 2.47f);
                    centerY = 1.19f;
                    break;
                default:
                    size = new Vector2(.65f, .85f);
                    centerY = .35f;
                    break;
            }
            var tuning = balance == null ? 1f : Mathf.Max(.1f, balance.hurtboxScale) / 1.08f;
            if (ExternalCreatureArt.Supports(Type))
            {
                size = ExternalCreatureArt.BodySize(Type) + Vector2.one * BodyMeasureMargin;
                centerY = ExternalCreatureArt.BodyCenterY(Type);
            }
            var rootScale = new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            damageCollider.size = new Vector2(size.x / rootScale.x, size.y / rootScale.y) * tuning;
            damageCollider.offset = (Vector2)visualRoot.localPosition + new Vector2(0, centerY / rootScale.y) +
                (balance == null ? Vector2.zero : balance.hurtboxOffset);
            if (healthBarRoot != null)
            {
                if (ExternalCreatureArt.Supports(Type))
                    UpdateCreatureHealthBarPosition();
                else
                    healthBarRoot.localPosition = new Vector3(0, visualRoot.localPosition.y + (centerY + size.y * .5f + .25f) / rootScale.y, 0);
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
            if (!dead && ExternalCreatureArt.Supports(Type))
                UpdateCreatureHealthBarPosition();
            UpdateHealthBarDepth();
            ApplyHitPunch();
        }

        // Struck bodies squash for a beat. Only the visual changes, never the hurtbox.
        private float hitPunch, hitStun;
        private Vector3 restingVisualScale;
        private void ApplyHitPunch()
        {
            if (visualRoot == null)
                return;
            if (restingVisualScale == Vector3.zero)
                restingVisualScale = visualRoot.localScale;
            hitPunch = Mathf.Max(0f, hitPunch - Time.deltaTime * 7f);
            var strength = IsBoss ? .1f : .26f;
            visualRoot.localScale = Vector3.Scale(restingVisualScale, new Vector3(1f + hitPunch * strength, 1f - hitPunch * strength * .8f, 1f));
        }

        private void UpdateHealthBarDepth()
        {
            if (healthBackRenderer == null || healthFillRenderer == null || spriteRenderer == null)
                return;
            var order = spriteRenderer.sortingOrder + 2;
            // World-space bars follow the enemy's depth. They must never stripe
            // across the hero's face during a close stab or dagger lunge.
            if (heroBodyRenderer != null)
                order = Mathf.Min(order, heroBodyRenderer.sortingOrder - 4);
            healthBackRenderer.sortingOrder = order;
            healthTrailRenderer.sortingOrder = order + 1;
            healthFillRenderer.sortingOrder = order + 2;
            healthShineRenderer.sortingOrder = order + 3;
        }

        private void UpdateCreatureHealthBarPosition()
        {
            if (healthBarRoot == null || visualRoot == null)
                return;
            // Follow intentional leap elevation, never frame-by-frame pixel bounds.
            // Leave the ground collision and damage envelope unchanged.
            var height = ExternalCreatureArt.VisualTop(Type) + 6f / 32f;
            healthBarRoot.localPosition = new Vector3(0,
                visualRoot.localPosition.y + height / Mathf.Abs(transform.lossyScale.y), 0);
        }

        private void OnDrawGizmosSelected()
        {
            if (enemyCollider != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(enemyCollider.bounds.center, enemyCollider.bounds.size);
            }
            if (damageCollider != null)
            {
                Gizmos.color = new Color(1f, .55f, .1f);
                Gizmos.DrawWireCube(damageCollider.bounds.center, damageCollider.bounds.size);
            }
        }

        private void AddHealthBar()
        {
            var back = new GameObject("HealthBack");
            back.transform.SetParent(transform);
            healthBarRoot = back.transform;
            back.transform.localPosition = new Vector3(0f, .7f, 0f);
            back.transform.localScale = new Vector3(.9f * PresentationScale, .16f * PresentationScale, 1f);
            GameVisuals.AddSprite(back, new Color(.07f, .05f, .1f), spriteRenderer.sortingOrder + 2);
            healthBackRenderer = back.GetComponent<SpriteRenderer>();

            var trail = new GameObject("HealthTrail");
            trail.transform.SetParent(back.transform, false);
            trail.transform.localPosition = new Vector3(0f, 0f, -.005f);
            trail.transform.localScale = new Vector3(HealthInner, .6f, 1f);
            GameVisuals.AddSprite(trail, new Color(1f, .93f, .78f), spriteRenderer.sortingOrder + 3);
            healthTrailRenderer = trail.GetComponent<SpriteRenderer>();
            healthTrail = trail.transform;

            var fill = new GameObject("HealthFill");
            fill.transform.SetParent(back.transform, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -.01f);
            fill.transform.localScale = new Vector3(HealthInner, .6f, 1f);
            GameVisuals.AddSprite(fill, new Color(.78f, .16f, .2f), spriteRenderer.sortingOrder + 4);
            healthFillRenderer = fill.GetComponent<SpriteRenderer>();

            var shine = new GameObject("HealthShine");
            shine.transform.SetParent(fill.transform, false);
            shine.transform.localPosition = new Vector3(0f, .28f, -.01f);
            shine.transform.localScale = new Vector3(1f, .44f, 1f);
            GameVisuals.AddSprite(shine, new Color(1f, .42f, .38f), spriteRenderer.sortingOrder + 5);
            healthShineRenderer = shine.GetComponent<SpriteRenderer>();
            heroBodyRenderer = player == null ? null : player.transform.Find("VisualRoot")?.GetComponent<SpriteRenderer>();
            UpdateHealthBarDepth();
            healthFill = fill.transform;
            back.SetActive(false); // Shown once hit. The boss keeps its dedicated screen HUD instead.
        }
    }
}
