using UnityEngine;

namespace RuinRunner
{
    public sealed class EnemySpawnTelegraph : MonoBehaviour
    {
        public const float Duration = .85f;
        private SpriteRenderer view;
        private Sprite[] frames;
        private float elapsed;
        private bool complete;
        private Color tint = Color.white;

        public static EnemySpawnTelegraph Create(Vector2 position, EnemyType type)
        {
            var root = new GameObject("SpawnTelegraph_" + type); root.transform.position = position;
            var telegraph = root.AddComponent<EnemySpawnTelegraph>();
            telegraph.frames = NativeCombatEffect.Frames("Spawn" + StageFx.Theme);
            telegraph.view = root.AddComponent<SpriteRenderer>();
            telegraph.view.sortingLayerName = "WorldFloor"; telegraph.view.sortingOrder = 24;
            telegraph.view.sprite = telegraph.frames[0];
            root.transform.localScale = Vector3.one * (EnemyRoles.IsBoss(type) ? 1.5f : .72f);
            PixelFxMaterial.Apply(telegraph.view, "Spawn" + StageFx.Theme);
            telegraph.view.color = telegraph.tint;
            return telegraph;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (complete)
            {
                var color = tint; color.a = 1f - Mathf.Clamp01(elapsed / .16f); view.color = color;
                if (elapsed >= .16f) Destroy(gameObject);
                return;
            }
            // Complete the seal just before the spawn, leaving a readable final beat.
            view.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed / (Duration * .86f) * frames.Length))];
            var visible = tint; visible.a = Mathf.Clamp01(elapsed / .09f); view.color = visible;
        }

        public void Complete()
        {
            if (complete) return;
            complete = true; elapsed = 0f; view.sprite = frames[frames.Length - 1];
        }
    }
}
