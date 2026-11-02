using UnityEngine;

namespace RuinRunner
{
    public enum EnvironmentPropKind { Bush, Grass, Pillar, Urn, Banner, Brazier, Tree }

    [System.Serializable]
    public struct RoomDecorationData
    {
        public EnvironmentPropKind kind;
        public Vector2 position;
        [Range(.5f, 3f)] public float scale;
        public RoomDecorationData(EnvironmentPropKind type, float x, float y, float size = 1f)
        { kind = type; position = new Vector2(x, y); scale = size; }
    }

    public sealed class EnvironmentProp : MonoBehaviour
    {
        private EnvironmentPropKind kind;
        private SpriteRenderer visual;
        private int firstFrame;
        private bool animated;
        private bool broken;
        private float phase;
        private float rustleUntil;
        private PlayerController player;
        private float nextPlayerSearch;

        public static EnvironmentProp Create(Transform parent, Vector2 position, EnvironmentPropKind kind, float scale = 1f)
        {
            var root = new GameObject("Environment_" + kind);
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            var prop = root.AddComponent<EnvironmentProp>();
            prop.kind = kind;
            prop.firstFrame = kind == EnvironmentPropKind.Bush ? 0 : kind == EnvironmentPropKind.Grass ? 2 :
                kind == EnvironmentPropKind.Pillar ? 4 : kind == EnvironmentPropKind.Urn ? 5 : kind == EnvironmentPropKind.Banner ? 8 : 10;
            prop.animated = kind == EnvironmentPropKind.Bush || kind == EnvironmentPropKind.Grass || kind == EnvironmentPropKind.Banner || kind == EnvironmentPropKind.Brazier;
            prop.visual = root.AddComponent<SpriteRenderer>();
            prop.visual.sprite = kind == EnvironmentPropKind.Tree ? IndividualArt.Sprite("ContinuousWorld/MeadowTree") : EnvironmentArt.Prop(prop.firstFrame);
            prop.visual.sortingOrder = 900 - Mathf.RoundToInt(position.y * 100f);
            prop.phase = Mathf.Abs(position.x * .37f + position.y * .73f) % 1f;
            if (kind == EnvironmentPropKind.Urn || kind == EnvironmentPropKind.Grass)
            {
                root.layer = 10;
                var trigger = root.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(.7f, .75f);
                trigger.offset = new Vector2(0f, .32f);
            }
            return prop;
        }

        public void Hit()
        {
            if (broken) return;
            if (kind == EnvironmentPropKind.Grass) { rustleUntil = Time.time + .6f; return; }
            if (kind != EnvironmentPropKind.Urn) return;
            broken = true;
            visual.sprite = EnvironmentArt.Prop(6);
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.enabled = false;
            PrototypeVisuals.Burst(transform.position + Vector3.up * .3f, new Color(.72f, .43f, .25f), 9);
            PrototypeAudio.Play(PrototypeSound.Hit, AudioChannel.Ambience, .35f);
        }

        private void Update()
        {
            if (broken || !animated) return;
            if (player == null && Time.time >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.time + 1f;
                player = Object.FindAnyObjectByType<PlayerController>();
            }
            if ((kind == EnvironmentPropKind.Grass || kind == EnvironmentPropKind.Bush) && player != null &&
                Vector2.Distance(player.transform.position, transform.position) < .8f && player.MovementInput.sqrMagnitude > .1f)
                rustleUntil = Time.time + .35f;
            var rate = kind == EnvironmentPropKind.Brazier ? 7f : Time.time < rustleUntil ? 10f : 2f;
            visual.sprite = EnvironmentArt.Prop(firstFrame + (Mathf.FloorToInt((Time.time + phase) * rate) & 1));
        }
    }
}
