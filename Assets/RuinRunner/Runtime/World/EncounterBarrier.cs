using UnityEngine;

namespace RuinRunner
{
    /// <summary>When a room locks, a row of stage-themed stone posts bursts out of the floor from the walls inward and runes link them.
    /// When it clears they sink back. The collider is the seal; the posts only show it.</summary>
    public sealed class EncounterBarrier : MonoBehaviour
    {
        private const float PostSpacing = .82f, RiseSeconds = .24f, StaggerSeconds = .03f, RuneHeight = .55f;
        private const int RiseFrames = 8;
        private const float SettledClock = 10f;
        private BoxCollider2D blocker;
        private SpriteRenderer[] posts, links;
        private float[] delays;
        private Sprite[] postFrames, linkFrames;
        private float clock;
        private bool closed;
        public bool IsClosed => closed;

        public static EncounterBarrier Create(Transform parent, Vector2 position, bool verticalTravel, bool closed, float width = StageTerrainLayout.CorridorWidth, string theme = "Outer")
        {
            var root = new GameObject("EncounterSeal"); root.transform.SetParent(parent, false); root.transform.position = position;
            root.layer = GameLayers.Ground;
            var barrier = root.AddComponent<EncounterBarrier>();
            barrier.closed = closed;
            barrier.clock = SettledClock;
            barrier.blocker = root.AddComponent<BoxCollider2D>();
            barrier.blocker.size = verticalTravel ? new Vector2(width + .4f, .3f) : new Vector2(.3f, width + .4f);
            barrier.blocker.enabled = closed;
            barrier.postFrames = IndividualArt.Frames("Campaign/Seal/" + theme, RiseFrames);
            barrier.linkFrames = NativeCombatEffect.Frames("ESealLink" + theme);
            barrier.BuildRow(verticalTravel ? Vector2.right : Vector2.up, width);
            barrier.Show();
            return barrier;
        }

        private void BuildRow(Vector2 tangent, float width)
        {
            var count = Mathf.Max(3, Mathf.RoundToInt(width / PostSpacing) + 1);
            var step = width / (count - 1);
            posts = new SpriteRenderer[count];
            links = new SpriteRenderer[count - 1];
            delays = new float[count];
            for (var i = 0; i < count; i++)
            {
                var offset = tangent * (i * step - width * .5f);
                var world = (Vector2)transform.position + offset;
                posts[i] = NewSprite("Seal post", offset, 900 - Mathf.RoundToInt(world.y * 100f));
                // From both walls toward the middle, so the gap closes in front of the player.
                delays[i] = Mathf.Min(i, count - 1 - i) * StaggerSeconds;
                if (i == count - 1) break;
                links[i] = NewSprite("Seal rune link", offset + Vector2.up * RuneHeight, 900 - Mathf.RoundToInt(world.y * 100f) + 1);
                links[i].sharedMaterial = PixelFxMaterial.Shared;
                links[i].transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                // The strip is drawn two units long.
                links[i].transform.localScale = new Vector3(step / 2f, 1f, 1f);
            }
        }

        private SpriteRenderer NewSprite(string name, Vector2 point, int order)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false); obj.transform.localPosition = point;
            var view = obj.AddComponent<SpriteRenderer>(); view.sortingOrder = order;
            return view;
        }

        public void SetClosed(bool value)
        {
            if (closed == value) return;
            closed = value;
            blocker.enabled = value;
            clock = 0f;
            // Closing and opening are both events: the stone has to move.
            foreach (var post in posts) BossNativeEffect.CreateOnFloor("ERushDust", post.transform.position, .34f);
            var view = FindAnyObjectByType<CameraFollow>();
            if (view != null) view.Shake(value ? .22f : .12f, value ? .2f : .1f);
            if (!value) { CombatAudio.Native("block", AudioChannel.Monster, .16f, .7f); return; }
            CombatAudio.Native("heavy-impact", AudioChannel.Monster, .3f, .8f);
        }

        private void Update()
        {
            if (!closed && clock >= SettledClock) return;
            clock += Time.deltaTime;
            if (!closed && clock > RiseSeconds + delays[delays.Length / 2]) clock = SettledClock;
            Show();
        }

        private void Show()
        {
            var runeFrame = Mathf.FloorToInt(Time.time * 12f);
            for (var i = 0; i < posts.Length; i++)
            {
                var raised = Mathf.Clamp01((clock - delays[i]) / RiseSeconds);
                var height = closed ? raised : 1f - raised;
                posts[i].enabled = height > 0f;
                posts[i].sprite = postFrames[Mathf.Clamp(Mathf.FloorToInt(height * RiseFrames), 0, RiseFrames - 1)];
                if (i == posts.Length - 1) continue;
                var nextRaised = Mathf.Clamp01((clock - delays[i + 1]) / RiseSeconds);
                var linked = closed && raised >= 1f && nextRaised >= 1f;
                links[i].enabled = linked;
                if (linked) links[i].sprite = linkFrames[(runeFrame + i) % linkFrames.Length];
            }
        }
    }
}
