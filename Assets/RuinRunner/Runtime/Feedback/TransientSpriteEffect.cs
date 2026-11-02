using UnityEngine;

namespace RuinRunner
{
    /// <summary>Visual-only sprite echoes. Never owns gameplay callbacks or collision.</summary>
    public sealed class TransientSpriteEffect : MonoBehaviour
    {
        private SpriteRenderer view;
        private Color tint;
        private float age;
        private float duration;
        private Vector3 startScale;
        private Vector2 velocity;
        private float endScale;

        public static void Create(Sprite sprite, Vector3 position, Vector3 scale, Color color,
            float lifetime, int sortingOrder, Vector2 drift = default, float finalScale = .2f)
        {
            if (sprite == null) return;
            var root = new GameObject("SpriteEcho"); root.transform.position = position;
            root.transform.localScale = scale;
            var effect = root.AddComponent<TransientSpriteEffect>();
            effect.view = root.AddComponent<SpriteRenderer>(); effect.view.sprite = sprite;
            effect.view.sortingOrder = sortingOrder; effect.view.color = color;
            effect.tint = color; effect.duration = Mathf.Max(.01f, lifetime);
            effect.startScale = scale; effect.velocity = drift; effect.endScale = finalScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var progress = Mathf.Clamp01(age / duration);
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.localScale = startScale * Mathf.Lerp(1f, endScale, progress);
            var color = tint; color.a *= 1f - progress; view.color = color;
            if (age >= duration) Destroy(gameObject);
        }
    }
}
