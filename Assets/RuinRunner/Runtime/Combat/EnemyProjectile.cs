using UnityEngine;

namespace RuinRunner
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private Vector2 velocity;
        private float damage;
        private float lifetime = 5f;
        private SpriteRenderer visual;
        private Sprite[] frames;
        private float animationTime;
        private bool consumed;
        private string impactClip;
        public int FrameIndex { get; private set; }
        public Vector2 TravelDirection => velocity.normalized;

        private float frameRate = 15f;

        public static EnemyProjectile Create(Vector2 position, Vector2 direction, float speed, float damage, Color color,
            float size = .22f, string clip = null)
        {
            var shot = new GameObject("EnemyProjectile");
            shot.layer = GameLayers.Projectile;
            shot.transform.position = position;
            var renderer = PrototypeVisuals.AddSprite(shot, Color.white, 2000);
            renderer.sprite=IndividualArt.Sprite("Campaign/Projectiles/EnemyOrb");

            var collider = shot.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = size * .5f;
            var body = shot.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var projectile = shot.AddComponent<EnemyProjectile>();
            projectile.velocity = direction.normalized * speed;
            projectile.damage = damage;
            projectile.visual = renderer;
            if (clip != null)
            {
                projectile.frames = NativeCombatEffect.Frames(clip);
                if (NativeCombatEffect.TryTiming(clip, out var drawnRate, out _)) projectile.frameRate = drawnRate;
                PixelFxMaterial.Apply(renderer, clip);
                projectile.impactClip = clip + "Hit";
                renderer.sprite = projectile.frames[0];
                shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            return projectile;
        }

        private void Update()
        {
            if (frames == null) return;
            animationTime += Time.deltaTime;
            FrameIndex = Mathf.FloorToInt(animationTime * frameRate) % frames.Length;
            visual.sprite = frames[FrameIndex];
        }

        private void FixedUpdate()
        {
            if(consumed)return;
            var step = velocity * Time.fixedDeltaTime;
            var wall = Physics2D.Raycast(transform.position, step.normalized, step.magnitude, 1 << GameLayers.Ground);
            if (wall.collider != null)
            {
                Impact(wall.point);
                Destroy(gameObject);
                return;
            }
            transform.position += (Vector3)step;
            foreach(var actor in PlayerController.ActiveCombatants)
                if(actor!=null&&actor.TryCatchHostileProjectile(transform.position,velocity.normalized,damage))
                {consumed=true;Destroy(gameObject);return;}
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(consumed)return;
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                consumed=true;
                if(player.TryCatchHostileProjectile(transform.position,velocity.normalized,damage)){Destroy(gameObject);return;}
                player.TakeDamage(damage, velocity.normalized);
                Impact(transform.position);
                Destroy(gameObject);
                return;
            }

            if (other.gameObject.layer == GameLayers.Ground) { Impact(transform.position); Destroy(gameObject); }
        }

        private void Impact(Vector2 point)
        {
            if (impactClip != null) BossNativeEffect.CreateOverActors(null, impactClip, point, .24f, Mathf.Atan2(-velocity.y, -velocity.x) * Mathf.Rad2Deg);
        }
    }
}
