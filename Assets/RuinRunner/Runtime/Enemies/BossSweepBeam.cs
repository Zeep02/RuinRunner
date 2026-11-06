using UnityEngine;

namespace RuinRunner
{
    /// <summary>A beam from a fixed point that holds still while it warns, then turns. It stops at walls, and it hurts along exactly what is drawn.</summary>
    public sealed class BossSweepBeam : MonoBehaviour
    {
        const float MaxLength = 22f, NativeLength = 2f, RehitSeconds = .6f, FramesPerSecond = 12f;
        EnemyController owner;
        PlayerController player;
        SpriteRenderer view;
        Sprite[] frames;
        Vector2 origin;
        float angle, turn, warning, active, width, damage, age, nextHit;

        public static BossSweepBeam Create(EnemyController owner, PlayerController player, Vector2 origin, float angle, float turnPerSecond, float warning, float active, float width, float damage)
        {
            var root = new GameObject("BossSweepBeam");
            root.transform.position = origin;
            var beam = root.AddComponent<BossSweepBeam>();
            beam.owner = owner; beam.player = player; beam.origin = origin; beam.angle = angle; beam.turn = turnPerSecond;
            beam.warning = warning; beam.active = active; beam.width = width; beam.damage = damage;
            beam.frames = NativeCombatEffect.Frames("ESealBeamLoop");
            beam.view = root.AddComponent<SpriteRenderer>();
            beam.view.sprite = beam.frames[0];
            beam.view.sortingOrder = 1990;
            PixelFxMaterial.Apply(beam.view, "ESealBeamLoop");
            beam.Pose();
            return beam;
        }

        float Reach(Vector2 direction)
        {
            var wall = Physics2D.Raycast(origin, direction, MaxLength, 1 << GameLayers.Ground);
            return wall.collider == null ? MaxLength : Mathf.Max(.5f, wall.distance);
        }

        float Pose()
        {
            var radians = angle * Mathf.Deg2Rad;
            var reach = Reach(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));
            var firing = age >= warning;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.localScale = new Vector3(reach / NativeLength, firing ? 1f : .22f + .3f * Mathf.Clamp01(age / warning), 1f);
            view.color = new Color(1f, 1f, 1f, firing ? 1f : .45f);
            view.sprite = frames[Mathf.FloorToInt(age * FramesPerSecond) % frames.Length];
            return reach;
        }

        void Update()
        {
            if (owner == null || owner.IsDead || player == null) { Destroy(gameObject); return; }
            if (owner.AnimationPaused) return;
            age += Time.deltaTime;
            if (age >= warning + active) { Destroy(gameObject); return; }
            if (age >= warning) angle += turn * Time.deltaTime;
            var reach = Pose();
            if (age < warning || age < nextHit) return;
            var radians = angle * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var feet = player.FeetPoint - origin;
            var closest = along * Mathf.Clamp(Vector2.Dot(feet, along), 0f, reach);
            if ((feet - closest).sqrMagnitude > width * width * .25f) return;
            nextHit = age + RehitSeconds;
            player.TakeDamage(damage, feet - closest);
        }
    }
}
