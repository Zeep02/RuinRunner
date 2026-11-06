using UnityEngine;

namespace RuinRunner
{
    public enum BossPose { Idle, Walk, Radial, Volley, Charge, Leap, Hit, Death }

    /// <summary>Actual attack events drive row/phase changes; the attack loop is never guessed from IsActing.
    /// Clips are PixelLab animations of one character: 16 drawings each, first and last on one shared stance,
    /// so any clip can follow any other without a jump. Callers address a clip in eight equal slices.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class BossSpriteAnimator : MonoBehaviour
    {
        public const string FrameRoot = "Campaign/WardenV3/";
        public const int FrameCount = 16;
        private const int FramesPerSlice = FrameCount / 8;
        public const float EntranceDuration = 1.6f;
        private static readonly string[] Rows = { "idle", "walk", "pound", "punch", "charge", "leap", "hit", "death" };
        public const string Root = "Pilot/Warden/";
        public const string EntranceId = Root + "entrance";
        public static string RowId(int row) => Root + Rows[row];
        public BossPose Pose { get; private set; }
        public int FrameIndex { get; private set; }
        private readonly Sprite[][] frames = new Sprite[8][];
        private EnemyController owner;
        private SpriteRenderer target;
        private BossPose action;
        private int first, last;
        private float duration, elapsed, hitElapsed = 1f, loopTime;
        private BossPose lastLoop;

        public void Initialize(EnemyController enemy, SpriteRenderer renderer)
        {
            owner = enemy;
            target = renderer;
            for (var row = 0; row < frames.Length; row++)
                frames[row] = IndividualArt.Frames(RowId(row), FrameCount);
            Show(BossPose.Idle, 0);
        }

        public void Play(BossPose pose, int fromSlice, int toSlice, float seconds)
        {
            action = pose;
            first = fromSlice * FramesPerSlice;
            last = toSlice * FramesPerSlice + FramesPerSlice - 1;
            duration = Mathf.Max(.001f, seconds);
            elapsed = 0;
            hitElapsed = 1;
            Show(action, first);
        }

        public void Hit()
        {
            // Never cover up a live attack tell/contact with an unrelated hit pose.
            if (!owner.IsActing && !owner.IsDead)
            {
                hitElapsed = 0;
                Show(BossPose.Hit, 0);
            }
        }

        public void Death(float progress) => Show(BossPose.Death, Mathf.Min(FrameCount - 1, Mathf.FloorToInt(progress * FrameCount)));

        public void Entrance(float time)
        {
            // The entrance owner samples this only while IntroLocked suspends regular animation.
            if (time >= EntranceDuration)
            {
                Show(BossPose.Idle, 0);
                return;
            }
            var entrance = IndividualArt.Frames(EntranceId, FrameCount);
            target.sprite = entrance[Mathf.Clamp(Mathf.FloorToInt(time / EntranceDuration * FrameCount), 0, FrameCount - 1)];
            target.flipX = owner.FacingDirection < 0;
        }

        private void LateUpdate()
        {
            if (owner == null || target == null || owner.IsDead || owner.AnimationPaused)
                return;
            if (owner.IsActing)
            {
                elapsed += Time.deltaTime;
                Show(action, Mathf.Min(last, first + Mathf.FloorToInt(elapsed / duration * (last - first + 1))));
            }
            else if (hitElapsed < .32f)
            {
                hitElapsed += Time.deltaTime;
                // Only the opening recoil, out and back: it stays small so chip damage never locks
                // the body in a stagger, and it lands on the shared stance instead of cutting to it.
                var step = Mathf.Min(15, Mathf.FloorToInt(hitElapsed / .32f * 16));
                Show(BossPose.Hit, step < 8 ? step : 15 - step);
            }
            else
            {
                var loop = owner.Velocity.sqrMagnitude > .0225f ? BossPose.Walk : BossPose.Idle;
                if (loop != lastLoop)
                {
                    loopTime = 0;
                    lastLoop = loop;
                }
                else
                    loopTime += Time.deltaTime;
                Show(loop, Mathf.FloorToInt(loopTime * 15f) % FrameCount);
            }
        }

        private void Show(BossPose pose, int frame)
        {
            Pose = pose;
            FrameIndex = Mathf.Clamp(frame, 0, FrameCount - 1);
            target.sprite = frames[(int)pose][FrameIndex];
            target.flipX = owner.FacingDirection < 0;
        }
    }
}
