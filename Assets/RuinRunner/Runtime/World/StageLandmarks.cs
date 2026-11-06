using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The structure standing on a solid island, chosen by footprint so the picture covers what blocks you.
    /// Sprites live in Campaign/Landmark/&lt;Theme&gt;/ as &lt;width&gt;x&lt;height&gt;-&lt;n&gt;, pivoted at the bottom centre.</summary>
    public static class StageLandmarks
    {
        static readonly Dictionary<string, int> Variants = new Dictionary<string, int> { { "2x2", 3 }, { "3x3", 2 }, { "5x2", 1 }, { "2x4", 1 }, { "4x4", 2 } };

        // Evenly spaced pillars must not all land on the same variant.
        static uint Mix(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                h *= 0x846ca68bu;
                return h ^ (h >> 16);
            }
        }

        public static void Place(Transform parent, int stage, Vector2 center, Vector2 size, List<Rect> occupied)
        {
            var key = Mathf.RoundToInt(size.x) + "x" + Mathf.RoundToInt(size.y);
            if (!Variants.TryGetValue(key, out var variants))
                throw new System.InvalidOperationException("No landmark drawn for footprint " + key);
            // The room's own position picks the variant, so a seed always builds the same room.
            var variant = (int)(Mix(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y)) % (uint)variants);
            var sprite = IndividualArt.Sprite("Campaign/Landmark/" + StageTerrainArt.Theme(stage) + "/" + key + "-" + variant);
            if (sprite == null)
                throw new System.InvalidOperationException("Missing landmark sprite " + key + "-" + variant);
            // A tall structure stands on the footprint's front edge and rises past it; a low one sits in the middle of it.
            var height = sprite.bounds.size.y;
            var foot = new Vector2(center.x, center.y - Mathf.Min(size.y, height) * .5f);
            var view = new GameObject("Landmark " + key).AddComponent<SpriteRenderer>();
            view.transform.SetParent(parent, false);
            view.transform.position = new Vector2(Mathf.Round(foot.x * 32f) / 32f, Mathf.Round(foot.y * 32f) / 32f);
            view.sprite = sprite;
            view.sortingOrder = 900 - Mathf.RoundToInt(foot.y * 100f);
            occupied.Add(new Rect((Vector2)view.transform.position + (Vector2)sprite.bounds.min, sprite.bounds.size));
        }
    }
}
