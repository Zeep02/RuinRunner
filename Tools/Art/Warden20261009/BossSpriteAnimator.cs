using UnityEngine;

namespace RuinRunner
{
    public enum BossPose { Idle, Walk, Radial, Volley, Charge, Leap, Hit, Death }

    /// <summary>Actual attack events drive row/phase changes; the attack loop is never guessed from IsActing.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class BossSpriteAnimator : MonoBehaviour
    {
        public const string FrameRoot = "Campaign/WardenV3/";
        public const float EntranceDuration = 1.05f;
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
            owner = enemy; target = renderer;
            for (var row = 0; row < frames.Length; row++) frames[row] = IndividualArt.Frames(FrameRoot + row.ToString("D2"), 8);
            Show(BossPose.Idle, 0);
        }

        public void Play(BossPose pose, int from, int to, float seconds)
        {
            action = pose; first = from; last = to; duration = Mathf.Max(.001f, seconds); elapsed = 0;
            hitElapsed = 1; Show(action, first);
        }

        public void Hit()
        {
            // Never cover up a live attack tell/contact with an unrelated hit pose.
            if (!owner.IsActing && !owner.IsDead) { hitElapsed = 0; Show(BossPose.Hit, 0); }
        }

        public void Death(float progress) => Show(BossPose.Death, Mathf.Min(7, Mathf.FloorToInt(progress * 8)));

        public void Entrance(float time)
        {
            // The entrance owner samples this only while IntroLocked suspends
            // regular animation; the separate kneel-to-awake poses share feet.
            if (time >= EntranceDuration) { Show(BossPose.Idle, 0); return; }
            var entrance = IndividualArt.Frames(FrameRoot + "entrance", 8);
            target.sprite = entrance[Mathf.Clamp(Mathf.FloorToInt(time / EntranceDuration * 8), 0, 7)];
            target.flipX = owner.FacingDirection < 0;
        }

        private void LateUpdate()
        {
            if (owner == null || target == null || owner.IsDead || owner.AnimationPaused) return;
            if (owner.IsActing)
            {
                elapsed += Time.deltaTime;
                Show(action, Mathf.Min(last, first + Mathf.FloorToInt(elapsed / duration * (last - first + 1))));
            }
            else if (hitElapsed < .28f)
            {
                hitElapsed += Time.deltaTime;
                Show(BossPose.Hit, Mathf.Min(7, Mathf.FloorToInt(hitElapsed / .28f * 8)));
            }
            else
            {
                var loop = owner.Velocity.sqrMagnitude > .0225f ? BossPose.Walk : BossPose.Idle;
                if (loop != lastLoop) { loopTime = 0; lastLoop = loop; } else loopTime += Time.deltaTime;
                Show(loop, Mathf.FloorToInt(loopTime * (loop == BossPose.Walk ? 10 : 6)) % 8);
            }
        }

        private void Show(BossPose pose, int frame)
        {
            Pose = pose; FrameIndex = Mathf.Clamp(frame, 0, 7);
            target.sprite = frames[(int)pose][FrameIndex];
            target.flipX = owner.FacingDirection < 0;
        }
    }
}
