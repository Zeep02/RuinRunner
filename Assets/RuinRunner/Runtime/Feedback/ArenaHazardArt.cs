using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum ArenaHazardStyle { Contour, TidePool, SealRift, TideLane, StoneSlam, SealSweep }

    /// <summary>Native ground eruptions follow the hazard clock; they never deal damage.</summary>
    public sealed class ArenaHazardArt
    {
        sealed class Part
        {
            public SpriteRenderer View;
            public Sprite[] Frames;
            public int ReleaseLast;
            public float ReleaseLead;
            public float Delay;
        }
        readonly List<Part> parts = new List<Part>();
        readonly SealGroundAttack sealGround;
        public const float TailSeconds = .24f;

        public ArenaHazardArt(Transform root, ArenaHazardStyle style, Vector2 size, bool omitCenter)
        {
            if (style == ArenaHazardStyle.TidePool)
            {
                Add(root, "TidePillar", 9, 5, Vector2.zero);
                // Small splashes belong to the same well. Preserve their native
                // scale and upright projection, rather than stretching a column.
                float offset = Mathf.Min(.625f, size.x * .17f);
                Add(root, "TidePillar", 9, 5, new Vector2(-offset, -.375f), .05f, true);
                Add(root, "TidePillar", 9, 5, new Vector2(offset, -.375f), .10f);
                Add(root, "TideSplash", 11, 4, new Vector2(0, -.625f), .04f);
            }
            else if (style == ArenaHazardStyle.TideLane)
            {
                bool vertical = size.y > size.x;
                float length = vertical ? size.y : size.x;
                float breadth = vertical ? size.x : size.y;
                int columns = Mathf.Max(1, Mathf.CeilToInt(length / 1.25f));
                int rows = Mathf.Max(1, Mathf.CeilToInt(breadth / 1.15f));
                for (int i = 0; i < columns; i++)
                    for (int j = 0; j < rows; j++)
                    {
                        float along = -length * .5f + (i + .5f) * length / columns;
                        float across = -breadth * .5f + (j + .5f) * breadth / rows;
                        var offset = vertical ? new Vector2(across, along) : new Vector2(along, across);
                        // A progressing crest, followed by low water. Each authored
                        // cell keeps its native size; no stretched wall of droplets.
                        Add(root, "TideSplash", 11, 4, offset, 0, (i + j) % 2 != 0, i * .012f);
                        if (j == rows / 2)
                            Add(root, "TidePillar", 9, 5, offset, 0, i % 2 != 0, i * .012f);
                    }
            }
            else if (style == ArenaHazardStyle.StoneSlam)
            {
                Add(root, "SlamDust", NativeCombatEffect.Frames("SlamDust").Length, 7, Vector2.zero);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3;
                    Add(root, "StoneChip", NativeCombatEffect.Frames("StoneChip").Length, 3,
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size.x * .28f, .03f, i % 2 != 0);
                }
            }
            else if (style == ArenaHazardStyle.SealSweep)
            {
                sealGround = SealGroundAttack.Create(root, size, true);
            }
            else if (style == ArenaHazardStyle.SealRift)
            {
                sealGround = SealGroundAttack.Create(root, size, false);
                // One native dust impact grounds both crossing fissures. Tall
                // orange spikes and duplicated center eruptions hid the hero.
                if (!omitCenter)
                {
                    Add(root, "SlamDust", NativeCombatEffect.Frames("SlamDust").Length, 7, Vector2.zero);
                    parts[parts.Count - 1].View.color = new Color(.46f, .74f, .77f, .45f);
                }
            }
        }

        void Add(Transform root, string clip, int count, int releaseLast, Vector2 offset, float releaseLead = 0, bool flip = false, float delay = 0)
        {
            var child = new GameObject("HazardFX_" + clip);
            child.transform.SetParent(root, false);
            child.transform.position = root.TransformPoint(offset);
            child.transform.rotation = Quaternion.identity;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "WorldFloor";
            renderer.sortingOrder = 9;
            renderer.enabled = false;
            renderer.flipX = flip;
            var frames = IndividualArt.Frames("CombatFX/" + clip, count);
            renderer.sprite = frames[0];
            parts.Add(new Part { View = renderer, Frames = frames, ReleaseLast = Mathf.Min(count - 2, releaseLast), ReleaseLead = releaseLead, Delay = delay });
        }

        public void Sample(float elapsed, float warning, float active)
        {
            if (sealGround != null)
                sealGround.Sample(elapsed, warning, active);
            foreach (var part in parts)
            {
                float release = elapsed - warning - part.Delay;
                part.View.enabled = release >= -.12f;
                if (!part.View.enabled)
                    continue;
                int frame;
                if (release < 0)
                    frame = 0;
                else if (release < active)
                    frame = 1 + Mathf.Min(part.ReleaseLast - 1, (int)((release + part.ReleaseLead) / active * part.ReleaseLast));
                else
                {
                    int remaining = part.Frames.Length - part.ReleaseLast - 1;
                    frame = part.ReleaseLast + 1 + Mathf.Min(remaining - 1, (int)((release - active) / TailSeconds * remaining));
                }
                part.View.sprite = part.Frames[Mathf.Clamp(frame, 0, part.Frames.Length - 1)];
            }
        }
    }
}
