using UnityEngine;

namespace RuinRunner
{
    /// <summary>A solid piece of furniture that keeps moving: the landmark that says who stands next to it.</summary>
    public sealed class LivingProp : MonoBehaviour
    {
        const float FramesPerSecond = 10f;
        SpriteRenderer view;
        Sprite[] frames;
        float phase;
        int shown = -1;

        public static Vector2 Footprint(string clip) => clip == "PropWagon" ? new Vector2(1.5f, .55f) : clip == "PropBundle" ? new Vector2(.8f, .4f) : new Vector2(.45f, .3f);

        public static LivingProp Create(Transform parent, string clip, Vector2 feet, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            go.layer = GameLayers.Ground;
            var prop = go.AddComponent<LivingProp>();
            prop.frames = NativeCombatEffect.Frames(clip);
            prop.view = go.AddComponent<SpriteRenderer>();
            prop.view.sprite = prop.frames[0];
            prop.view.sortingOrder = 900 - Mathf.RoundToInt(feet.y * 100f);
            prop.phase = Mathf.Abs(feet.x * .37f + feet.y * .71f);
            var box = go.AddComponent<BoxCollider2D>();
            var size = Footprint(clip);
            box.size = size;
            box.offset = new Vector2(0f, size.y * .5f - .1f);
            return prop;
        }

        void Update()
        {
            var frame = Mathf.FloorToInt((Time.time + phase) * FramesPerSecond) % frames.Length;
            if (frame == shown)
                return;
            shown = frame;
            view.sprite = frames[frame];
        }
    }
}
