using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Opt-in visible animation fixture, not gameplay/progression completion evidence.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class BossSpriteValidation : MonoBehaviour
    {
        private EnemyController boss;
        private BossSpriteAnimator animator;
        private PlayerController player;
        private Vector2 center;
        private IEnumerator sequence;
        private int checks;
        private bool paused;
        private string directory;
        private string label = "BOSS SPRITE / ISOLATED VALIDATION";
        private MethodInfo screenshot;
        private readonly HashSet<string> captured = new HashSet<string>();
        private readonly HashSet<int> shown = new HashSet<int>();

        private void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            // PNG capture can stall a real-time frame. Keep fixture time deterministic
            // so a slow disk/GPU cannot skip an otherwise valid animation pose.
            Time.captureFramerate = 60;
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "--validation-capture-dir");
            if (index >= 0 && index + 1 < args.Length)
            {
                directory = args[index + 1]; Directory.CreateDirectory(directory);
                screenshot = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?.GetMethod("CaptureScreenshot", new[] { typeof(string), typeof(int) });
            }
            sequence = Validate();
        }

        private void Update()
        {
            if (sequence == null) return;
            try
            {
                if (sequence.MoveNext()) return;
                Debug.Log("[Boss Sprite Validation] PASS " + checks + " checks, all 64 poses rendered; isolated fixture, not a stage completion test.");
                sequence = null; Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("[Boss Sprite Validation] FAIL " + error); sequence = null; Application.Quit(3); }
        }

        private IEnumerator Validate()
        {
            var unique = new HashSet<Sprite>();
            for (var row = 0; row < 8; row++) for (var frame = 0; frame < 8; frame++)
            {
                var sprite = PixelArtAtlas.GetBossFrames(row)[frame]; var texture = sprite.texture;
                Require(unique.Add(sprite) && texture.width == 112 && texture.height == 112 && sprite.pixelsPerUnit == 32 &&
                    sprite.pivot == new Vector2(64, 8) && texture.filterMode == FilterMode.Point, "individual import " + row + "/" + frame);
                var b = SpriteVisibleBounds.Local(sprite);
                Require(b.size.x > 1.5f && b.size.x < 3 && b.size.y > (row==7?.75f:1.2f) && b.size.y < 2.1f, "consistent unscaled body " + row + "/" + frame);
                var pixels = SpriteVisibleBounds.ReadPixels(texture); var clear = true;
                for (var x = 0; x < texture.width; x++) clear &= pixels[x].a == 0 && pixels[(texture.height-1) * texture.width + x].a == 0;
                for (var y = 0; y < texture.height; y++) clear &= pixels[y * texture.width].a == 0 && pixels[y * texture.width + texture.width-1].a == 0;
                Require(clear, "unclipped alpha margins " + row + "/" + frame);
            }
            var map = DungeonMap.BuildStage(RoomTemplateCatalog.CreateDemoRun(), _ => { });
            center = map.GetRoomCenter(3);
            var follow = CameraFollow.Create(); follow.enabled = false;
            follow.transform.position = new Vector3(center.x + 1, center.y + 1, -10);
            var inventory = new InventorySystem(); inventory.AddWeapon(DemoContent.Weapon("rust_sword"));
            player = PlayerController.Create(center + new Vector2(7, -2), () => true, () => true, () => { }, follow, inventory, new GameSettingsService());
            boss = EnemyController.Create(EnemyType.Boss, center, player, () => paused, _ => { }, null);
            boss.enabled = false;
            animator = boss.GetComponent<BossSpriteAnimator>();
            Require(animator != null && boss.GetComponent<EnemyPixelAnimator>() == null, "boss has its own event-driven animator");
            var foot = boss.GetComponent<BoxCollider2D>(); var footSize = foot.size; var footOffset = foot.offset;
            var body = boss.GetComponent<Rigidbody2D>();
            for (var i = 0; i < 100; i++) yield return null;
            paused = true; var frozen = animator.FrameIndex;
            for (var i = 0; i < 12; i++) yield return null;
            Require(animator.FrameIndex == frozen, "pause freezes frame progression"); paused = false;
            body.linearVelocity = Vector2.right * .25f;
            for (var i = 0; i < 60; i++) yield return null;
            body.linearVelocity = Vector2.zero;
            boss.TakeDamage(1, Vector2.zero);
            for (var i = 0; i < 24; i++) yield return null;
            Require(shown.Contains((int)BossPose.Hit * 8 + 7), "hit reaction reaches its recovery pose");

            foreach (var lowHealth in new[] { false, true })
            {
                if (lowHealth)
                {
                    boss.TakeDamage(boss.MaxHealth * .7f, Vector2.zero);
                    for (var i = 0; i < 24; i++) yield return null;
                    Require(boss.Health < boss.MaxHealth * .5f && !boss.IsDead, "enraged fixture below half health");
                }
                foreach (var pose in new[] { BossPose.Radial, BossPose.Volley, BossPose.Charge, BossPose.Leap })
                {
                    foreach (var projectile in RuntimeSceneObjects.FindAll<EnemyProjectile>()) Destroy(projectile.gameObject);
                    yield return null;
                    body.position = center; boss.transform.position = center;
                    player.Teleport(center + new Vector2(lowHealth ? -7 : 7, -2)); player.Stats.Heal(1000);
                    label = pose + (lowHealth ? " / BELOW HALF HEALTH" : " / NORMAL");
                    var name = pose == BossPose.Radial ? "BossRadialBurst" : pose == BossPose.Volley ? "BossAimedVolley" : pose == BossPose.Charge ? "ChargeAttack" : "LeapSlam";
                    var method = typeof(EnemyController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    var direction = ((Vector2)player.transform.position - center).normalized;
                    var parameters = pose == BossPose.Charge || pose == BossPose.Leap ? new object[] { direction, true } : null;
                    boss.StartCoroutine((IEnumerator)method.Invoke(boss, parameters));
                    Require(animator.Pose == pose && animator.FrameIndex == 0, "correct tell starts " + label);
                    boss.TakeDamage(1, Vector2.zero);
                    Require(animator.Pose == pose, "hit cannot obscure active tell " + pose);
                    var frames = new HashSet<int>(); var phases = new HashSet<int>(); var projectiles = new HashSet<EnemyProjectile>();
                    var ticks = 0;
                    while (boss.IsActing && ticks++ < 240)
                    {
                        Require(animator.Pose == pose, "correct attack row " + pose);
                        frames.Add(animator.FrameIndex); phases.Add(boss.AttackVisualPhase);
                        foreach (var projectile in RuntimeSceneObjects.FindAll<EnemyProjectile>())
                        {
                            if (!projectiles.Add(projectile)) continue;
                            Require(projectile.Art == (pose == BossPose.Radial ? EnemyProjectileArt.BossRadial : EnemyProjectileArt.BossAimed),
                                "correct supplied projectile row " + pose);
                            if (pose == BossPose.Volley)
                                Require(Vector2.Dot(projectile.transform.right, projectile.TravelDirection) > .999f, "aimed projectile points along travel");
                        }
                        if (boss.AttackVisualPhase == 0) Require(projectiles.Count == 0, "no damage projectile before tell " + pose);
                        Require(foot.size == footSize && foot.offset == footOffset, "animation does not resize movement collider");
                        yield return null;
                    }
                    Require(!boss.IsActing && frames.Count == 8 && phases.Count == 3, "8 frames and three timed phases " + label);
                    var expected = pose == BossPose.Radial ? (lowHealth ? 24 : 9) : pose == BossPose.Volley ? (lowHealth ? 12 : 9) : 0;
                    Require(projectiles.Count == expected, "authored projectile count (slam=0) " + label + "=" + projectiles.Count);
                    Require(boss.transform.Find("VisualRoot").localPosition == new Vector3(0, -.35f, 0), "jump restores body anchor");
                    var renderer = boss.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                    if (pose == BossPose.Charge || pose == BossPose.Leap || pose == BossPose.Volley)
                        Require(renderer.flipX == lowHealth, "committed left/right facing " + label);
                    for (var i = 0; i < 12; i++) yield return null;
                }
            }
            foreach (var projectile in RuntimeSceneObjects.FindAll<EnemyProjectile>()) Destroy(projectile.gameObject);
            body.position = center; boss.transform.position = center; label = "DEATH / NON-LOOPING";
            boss.TakeDamage(10000, Vector2.zero);
            Require(boss.IsDead && !body.simulated, "death disables movement and attacks");
            for (var i = 0; i < 90; i++) yield return null;
            Require(boss == null, "death completes and destroys boss");
            Require(shown.Count == 64, "all 64 supplied frames rendered at runtime: " + shown.Count);
            Require(Screen.width == 1280 && Screen.height == 720, "fixed 1280x720 viewport");
        }

        private void LateUpdate()
        {
            if (animator == null) return;
            shown.Add((int)animator.Pose * 8 + animator.FrameIndex);
            var key = ((int)animator.Pose).ToString("D2") + "-" + animator.FrameIndex.ToString("D2");
            if (screenshot != null && captured.Add(key)) screenshot.Invoke(null, new object[] { Path.Combine(directory, key + ".png"), 1 });
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(16, 16, 440, 50), "BOSS SPRITE / ISOLATED VALIDATION\n" + label);
        }
        private void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
            if (!message.StartsWith("correct attack row") && !message.StartsWith("animation does not") && !message.StartsWith("no damage projectile"))
                Debug.Log("[Boss Sprite Validation] OK " + message);
        }
    }
}
