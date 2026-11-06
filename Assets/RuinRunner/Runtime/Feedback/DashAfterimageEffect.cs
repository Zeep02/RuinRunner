using UnityEngine;

namespace RuinRunner
{
    // Capture after both native body animation and held-weapon sockets have settled.
    // Five reusable pairs bound the cost even when dashes are chained.
    [DefaultExecutionOrder(300)]
    public sealed class DashAfterimageEffect : MonoBehaviour
    {
        private const int Capacity = 5;
        private const float Lifetime = .2f, Opacity = .6f;
        private const float Interval = .04f;
        private readonly SpriteRenderer[,] images = new SpriteRenderer[Capacity, 2];
        private readonly float[] remaining = new float[Capacity];
        private PlayerController player;
        private SpriteRenderer body;
        private HeldWeaponView weapon;
        private GameObject root;
        private bool wasDashing;
        private float nextSample;
        private int next;
        private Vector3 lastPosition;

        public void Initialize(PlayerController owner, SpriteRenderer source)
        {
            player = owner;
            body = source;
            weapon = owner.GetComponent<HeldWeaponView>();
            root = new GameObject("DashAfterimages");
            for (var i = 0; i < Capacity; i++)
                for (var part = 0; part < 2; part++)
                {
                    var visual = new GameObject(part == 0 ? "BodyEcho" : "WeaponEcho");
                    visual.transform.SetParent(root.transform, false);
                    var renderer = visual.AddComponent<SpriteRenderer>();
                    renderer.enabled = false;
                    images[i, part] = renderer;
                }
        }

        private void LateUpdate()
        {
            if (player == null || body == null || root == null)
                return;
            if (player.IsDead || (player.ItemEffectsSuspended && !player.AnimationPaused))
            {
                Clear();
                return;
            }
            if (player.AnimationPaused)
                return;

            for (var i = 0; i < Capacity; i++)
            {
                remaining[i] = Mathf.Max(0f, remaining[i] - Time.deltaTime);
                var fade = remaining[i] / Lifetime;
                for (var part = 0; part < 2; part++)
                {
                    var image = images[i, part];
                    image.enabled = remaining[i] > 0f && image.sprite != null;
                    image.color = new Color(.55f, .85f, 1f, Opacity * fade);
                    // Re-evaluate against the current actor, including northward dashes.
                    image.sortingOrder = body.sortingOrder - 8 + part;
                }
            }

            if (player.IsDashing)
            {
                if (!wasDashing)
                {
                    nextSample = 0f;
                    lastPosition = body.transform.position;
                }
                nextSample -= Time.deltaTime;
                if (nextSample <= 0f && (!wasDashing || Vector3.Distance(lastPosition, body.transform.position) >= .2f))
                {
                    Stamp();
                    nextSample = Interval;
                }
            }
            wasDashing = player.IsDashing;
        }

        // Leaves one afterimage of the current pose. Weapon skills that move the hero without a dash call this themselves.
        internal void Stamp()
        {
            Snapshot(images[next, 0], body);
            Snapshot(images[next, 1], weapon == null ? null : weapon.VisualRenderer);
            // Keep the captured hand/weapon occlusion within each silhouette.
            if (weapon != null && weapon.VisualRenderer != null && weapon.VisualRenderer.sortingOrder < body.sortingOrder)
            {
                var swap = images[next, 0];
                images[next, 0] = images[next, 1];
                images[next, 1] = swap;
            }
            remaining[next] = Lifetime;
            lastPosition = body.transform.position;
            next = (next + 1) % Capacity;
        }

        private static void Snapshot(SpriteRenderer target, SpriteRenderer source)
        {
            target.sprite = source != null && source.enabled ? source.sprite : null;
            target.enabled = target.sprite != null;
            if (!target.enabled)
                return;
            target.sharedMaterial = source.sharedMaterial;
            target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            target.transform.localScale = source.transform.lossyScale;
            target.flipX = source.flipX;
            target.flipY = source.flipY;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder - 8;
            target.color = new Color(.55f, .85f, 1f, Opacity);
        }

        private void Clear()
        {
            wasDashing = false;
            for (var i = 0; i < Capacity; i++)
            {
                remaining[i] = 0f;
                for (var part = 0; part < 2; part++)
                    if (images[i, part] != null)
                    {
                        images[i, part].enabled = false;
                        images[i, part].sprite = null;
                    }
            }
        }
        internal void CancelTransient() => Clear();
        private void OnDisable() => Clear();
        private void OnDestroy()
        {
            if (root != null)
                Destroy(root);
        }
    }
}
