using UnityEngine;

namespace RuinRunner
{
    public static class PrototypeVisuals
    {
        private static Sprite square;
        private static Sprite capsule;
        private static Sprite attackRing;

        // A geometry-derived area indicator, distinct from the small grounding
        // shadow. Full sprite width is one unit so diameter maps exactly to XY.
        public static Sprite AttackRing
        {
            get
            {
                if (attackRing != null) return attackRing;
                const int resolution = 64;
                var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
                    { name = "PixelAttackAreaRing", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[resolution * resolution];
                for (var y = 0; y < resolution; y++)
                for (var x = 0; x < resolution; x++)
                {
                    var radius = new Vector2(x + .5f - resolution * .5f, y + .5f - resolution * .5f).magnitude;
                    var alpha = radius <= 32f ? (byte)(radius >= 29.5f ? 210 : 32) : (byte)0;
                    pixels[y * resolution + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                attackRing = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(.5f, .5f), resolution);
                attackRing.name = "PixelAttackAreaRing";
                return attackRing;
            }
        }

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var texture = new Texture2D(1, 1);
                    texture.name = "RuntimePixel";
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
                    square.name = "RuntimeSquare";
                }
                return square;
            }
        }

        public static Sprite Capsule
        {
            get
            {
                if (capsule == null)
                {
                    const int width = 32;
                    const int height = 8;
                    var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    texture.name = "RuntimeCapsule";
                    texture.filterMode = FilterMode.Point;
                    var radius = (height - 1) * .5f;
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var centerY = (height - 1) * .5f;
                        var centerX = x < height * .5f ? (height - 1) * .5f : width - 1 - (height - 1) * .5f;
                        var insideMiddle = x >= height * .5f && x < width - height * .5f;
                        var insideCap = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY)) <= radius;
                        texture.SetPixel(x, y, insideMiddle || insideCap ? Color.white : Color.clear);
                    }
                    texture.Apply();
                    capsule = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), width);
                    capsule.name = "RuntimeCapsuleSprite";
                }
                return capsule;
            }
        }

        public static SpriteRenderer AddSprite(GameObject target, Color color, int order = 0)
        {
            var renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static GameObject Block(string name, Vector2 position, Vector2 size, Color color, int layer, int order = 0)
        {
            var block = new GameObject(name);
            block.layer = layer;
            block.transform.position = position;
            block.transform.localScale = size;
            AddSprite(block, color, order);
            block.AddComponent<BoxCollider2D>();
            return block;
        }

        public static void Burst(Vector2 position, Color color, int count = 8)
        {
            for (var i = 0; i < count; i++)
            {
                var spark = new GameObject("HitSpark");
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * Random.Range(.08f, .18f);
                AddSprite(spark, color, 20);
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2f, 6f);
                spark.AddComponent<FadingSpark>().Initialize(velocity);
            }
        }
    }

    public sealed class FadingSpark : MonoBehaviour
    {
        private const float InitialLifetime = .35f;
        private Vector2 velocity;
        private float lifetime = InitialLifetime;
        private SpriteRenderer spriteRenderer;

        public void Initialize(Vector2 initialVelocity)
        {
            velocity = initialVelocity;
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            velocity *= .9f;
            transform.localScale *= .92f;

            if (spriteRenderer != null)
            {
                var color = spriteRenderer.color;
                color.a = Mathf.Clamp01(lifetime / InitialLifetime);
                spriteRenderer.color = color;
            }

            if (lifetime <= 0f) Destroy(gameObject);
        }
    }
}
