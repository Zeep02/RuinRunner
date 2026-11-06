using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Plays each boss's set pieces in the sandbox and writes frames for review: --combat-sandbox --boss-move-tour folder</summary>
    public sealed class BossMoveTour : MonoBehaviour
    {
        public const string Argument = "--boss-move-tour";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), Argument) < 0)
                return;
            DontDestroyOnLoad(new GameObject("BossMoveTour").AddComponent<BossMoveTour>());
        }

        private IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            var folder = args[System.Array.IndexOf(args, Argument) + 1];
            GameController game = null;
            while (game == null || game.Player == null || !game.IsRunActive)
            {
                game = FindFirstObjectByType<GameController>();
                yield return null;
            }
            Application.runInBackground = true;
            Time.captureFramerate = 30;
            yield return new WaitForSeconds(.5f);
            game.Player.SandboxInvulnerable = true;
            var home = (Vector2)game.Player.transform.position;
            if (System.Array.IndexOf(args, "--contact-only") < 0)
            {
                yield return Boss(game, EnemyType.Boss, home, folder, "WardenTripleLeap", "WardenRifts", "WardenCage", "WardenRockFan");
                yield return Boss(game, EnemyType.Tidekeeper, home, folder, "TideMaelstrom", "TideWaveWall", "TideDive", "TideGeyserChase");
                yield return Boss(game, EnemyType.SealHeart, home, folder, "SealBeams", "SealBlinkCuts", "SealJudgment", "SealBloom");
            }
            yield return BodyContactCheck(game, home);
            yield return PickupCheck(game, home, folder);
            Debug.Log("[Boss Move Tour] Finished");
            Application.Quit(0);
        }

        // Running and dashing into an enemy leaves it where it stood, and one that somehow got outside the floor comes back.
        private static IEnumerator BodyContactCheck(GameController game, Vector2 home)
        {
            var player = game.Player;
            player.SandboxInvulnerable = true;
            player.Teleport(home);
            var enemy = EnemyController.Create(EnemyType.Grunt, home + Vector2.right * 2.5f, player, () => true, _ => { }, null);
            var body = enemy.GetComponent<Rigidbody2D>();
            yield return new WaitForFixedUpdate();
            var spot = body.position;
            var moved = 0f;
            for (var step = 0; step < 18; step++)
            {
                player.ApplyPush(Vector2.right * 3f);
                yield return new WaitForFixedUpdate();
                moved = Mathf.Max(moved, Vector2.Distance(body.position, spot));
            }
            var stopped = ((Vector2)player.transform.position - spot).magnitude;
            Debug.Log("[Body Contact] " + (moved < .01f && stopped > .3f ? "PASS" : "FAIL") + " run-in: enemy moved " + moved.ToString("F3") + ", hero stopped " + stopped.ToString("F2") + " from it");
            yield return new WaitForSeconds(.6f);
            player.Teleport(spot + Vector2.left * 2f);
            yield return new WaitForFixedUpdate();
            player.StartCoroutine((IEnumerator)typeof(PlayerController).GetMethod("Dash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null));
            var rushed = 0f;
            for (var step = 0; step < 24; step++)
            {
                yield return new WaitForFixedUpdate();
                rushed = Mathf.Max(rushed, Vector2.Distance(body.position, spot));
            }
            Debug.Log("[Body Contact] " + (rushed < .01f ? "PASS" : "FAIL") + " dash: enemy moved " + rushed.ToString("F3") + ", hero now " + ((Vector2)player.transform.position - spot));
            if (game.DungeonMap != null && game.DungeonMap.Terrain != null)
            {
                enemy.ConfigureNavigation(game.DungeonMap.Terrain);
                yield return new WaitForFixedUpdate();
                var open = game.DungeonMap.Terrain.IsOpen(enemy.GetComponent<BoxCollider2D>().bounds.center);
                body.position = spot + Vector2.one * 500f;
                yield return new WaitForSeconds(.4f);
                Debug.Log("[Body Contact] " + (!open || Vector2.Distance(body.position, spot) < 1f ? "PASS" : "FAIL") + " off the floor: back at distance " + Vector2.Distance(body.position, spot).ToString("F2") + " (started on floor " + open + ")");
            }
            Destroy(enemy.gameObject);
            yield return null;
        }

        // The small prompt, the corner note, and a potion that really heals.
        private static IEnumerator PickupCheck(GameController game, Vector2 home, string folder)
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var player = game.Player;
            var legacy = typeof(GameController).GetField("legacyFixture", Private);
            legacy.SetValue(game, false);
            player.Teleport(home);
            typeof(GameController).GetMethod("DropHealthPotion", Private).Invoke(game, new object[] { home + Vector2.right * 1.5f, 0 });
            legacy.SetValue(game, true);
            FieldLootPickup potion = null;
            foreach (var item in RuntimeSceneObjects.FindAll<FieldLootPickup>())
                potion = item;
            yield return new WaitForSeconds(1.5f);
            player.Teleport(potion.LandingPosition + Vector2.left * .3f);
            yield return new WaitForSeconds(.3f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "prompt-potion.png"));
            yield return null;
            player.SandboxInvulnerable = false;
            player.TakeDamage(player.MaxHealth * .5f, Vector2.zero);
            player.SandboxInvulnerable = true;
            var before = player.Health;
            var healed = ((System.Func<bool>)typeof(FieldLootPickup).GetField("collect", Private).GetValue(potion))();
            Debug.Log("[Pickup Check] " + (healed && player.Health > before ? "PASS" : "FAIL") + " potion " + before + " -> " + player.Health + " of " + player.MaxHealth);
            typeof(GameController).GetMethod("CompletePickup", Private).Invoke(game, new object[] { "장신구 획득  금 간 주사위" });
            yield return new WaitForSeconds(.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "toast-and-heal.png"));
            yield return null;
        }

        private static IEnumerator Boss(GameController game, EnemyType type, Vector2 home, string folder, params string[] moves)
        {
            var player = game.Player;
            foreach (var halved in new[] { false, true })
                foreach (var move in moves)
                {
                    player.Teleport(home);
                    var boss = EnemyController.Create(type, home + Vector2.left * 4.5f, player, () => game.IsPaused, _ => { }, game.CameraFollow);
                    boss.ConfigureArena(new Rect(home - new Vector2(9f, 6f), new Vector2(18f, 12f)));
                    boss.enabled = true;
                    yield return null;
                    if (halved)
                    {
                        boss.TakeDamage(boss.Health - boss.MaxHealth * .45f, Vector2.zero);
                        typeof(EnemyController).GetField("phaseShifted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boss, true);
                    }
                    var path = Path.Combine(folder, type + "-" + move + (halved ? "-2" : "-1"));
                    Directory.CreateDirectory(path);
                    Debug.Log("[Boss Move Tour] " + type + " " + move + " phase " + (halved ? 2 : 1));
                    boss.StartCoroutine((IEnumerator)typeof(EnemyController).GetMethod(move, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boss, null));
                    var frame = 0;
                    yield return new WaitForEndOfFrame();
                    while (boss != null && boss.IsActing && frame < 300)
                    {
                        // The hero keeps moving around it, so everything aimed at them has something to aim at.
                        var drift = home + new Vector2(Mathf.Sin(frame * .05f) * 3.5f, Mathf.Cos(frame * .037f) * 2.5f);
                        player.Teleport(drift);
                        if (frame % 5 == 0)
                            ScreenCapture.CaptureScreenshot(Path.Combine(path, frame.ToString("D3") + ".png"));
                        frame++;
                        yield return new WaitForEndOfFrame();
                    }
                    Debug.Log("[Boss Move Tour] done in " + frame + " frames");
                    if (boss != null)
                        Destroy(boss.gameObject);
                    yield return null;
                    RuntimeSceneObjects.ClearProjectiles();
                    yield return new WaitForSeconds(.4f);
                }
        }
    }
}
