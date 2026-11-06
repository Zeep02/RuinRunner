using UnityEngine;

namespace RuinRunner
{
    /// <summary>Committed, animated hostile rune. Owner/pause cancellation never leaves invisible damage.</summary>
    public sealed class BossRuneProjectile : MonoBehaviour
    {
        EnemyController owner;
        PlayerController target;
        Vector2 velocity;
        float damage, age;
        Sprite[] frames;
        SpriteRenderer view;
        bool consumed;
        string impactClip;
        public static BossRuneProjectile Create(EnemyController owner, PlayerController player, Vector2 position,
            Vector2 direction, float speed, float damage, bool water = false)
        {
            var root = new GameObject(water ? "TideCrestProjectile" : "SealRuneProjectile");
            root.transform.position = position;
            var shot = root.AddComponent<BossRuneProjectile>();
            shot.owner = owner;
            shot.target = player;
            shot.velocity = direction.normalized * speed;
            shot.damage = damage;
            shot.frames = NativeCombatEffect.Frames(water ? "EShotTide" : "EShotSeal");
            shot.impactClip = water ? "EShotTideHit" : "EShotSealHit";
            shot.view = root.AddComponent<SpriteRenderer>();
            shot.view.sprite = shot.frames[0];
            shot.view.sharedMaterial = PixelFxMaterial.Shared;
            shot.view.sortingOrder = 900 - Mathf.RoundToInt(position.y * 100) - 1;
            root.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            return shot;
        }
        void Update()
        {
            if (consumed)
                return;
            if (owner == null || owner.IsDead || owner.IntroLocked || target == null)
            {
                Destroy(gameObject);
                return;
            }
            if (owner.AnimationPaused)
                return;
            float delta = Time.deltaTime;
            age += delta;
            if (age > 4.5f)
            {
                Destroy(gameObject);
                return;
            }
            var start = (Vector2)transform.position;
            var step = velocity * delta;
            var wall = Physics2D.Raycast(start, velocity.normalized, step.magnitude + .1f, 1 << GameLayers.Ground);
            if (wall.collider != null)
            {
                End(wall.point);
                return;
            }
            var end = start + step;
            transform.position = end;
            view.sprite = frames[Mathf.FloorToInt(age * 30) % frames.Length];
            view.sortingOrder = 900 - Mathf.RoundToInt(end.y * 100) - 1;
            if (target.TryCatchHostileProjectile(end, velocity.normalized, damage))
            {
                End(end);
                return;
            }
            // Test the swept segment against the hero's feet envelope so a low
            // render rate cannot let fast shots skip through the player.
            var center = (Vector2)target.transform.position;
            float t = step.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(center - start, step) / step.sqrMagnitude);
            if (Vector2.Distance(start + step * t, center) > .32f)
                return;
            target.TakeDamage(damage, velocity.normalized);
            End(end);
        }
        void End(Vector2 point)
        {
            consumed = true;
            BossNativeEffect.CreateOverActors(null, impactClip, point, .26f, Mathf.Atan2(-velocity.y, -velocity.x) * Mathf.Rad2Deg);
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .12f, 1.2f);
            Destroy(gameObject);
        }
    }
}
