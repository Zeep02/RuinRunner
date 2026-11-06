using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Spawns each enemy in the running sandbox, walks the hero around it, hits it and kills it,
    /// writing screenshots and a per-frame record of which authored clip is on screen.
    /// --combat-sandbox --enemy-art-tour folder [Type ...]</summary>
    public sealed class EnemyArtTour : MonoBehaviour
    {
        public const string Argument = "--enemy-art-tour";
        private static readonly string[] Directions = { "down", "up", "left", "right" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), Argument) < 0)
                return;
            DontDestroyOnLoad(new GameObject("EnemyArtTour").AddComponent<EnemyArtTour>());
        }

        private IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, Argument);
            if (at + 1 >= args.Length)
            {
                Debug.LogError(Argument + " needs a folder");
                Application.Quit(2);
                yield break;
            }
            var folder = args[at + 1];
            var types = new List<EnemyType>();
            for (var i = at + 2; i < args.Length && System.Enum.TryParse<EnemyType>(args[i], out var named); i++)
                types.Add(named);
            if (types.Count == 0)
                types.AddRange((EnemyType[])System.Enum.GetValues(typeof(EnemyType)));

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
            foreach (var type in types)
            {
                Debug.Log("[Enemy Art Tour] " + type);
                yield return Visit(game, type, home, Path.Combine(folder, type.ToString()));
            }
            yield return Accessories(game, home, folder);
            Debug.Log("[Enemy Art Tour] Finished");
            Application.Quit(0);
        }

        // Every accessory on the floor and in the real bag screen.
        private IEnumerator Accessories(GameController game, Vector2 home, string folder)
        {
            game.Player.Teleport(home);
            var all = AccessoryLootTable.All;
            for (var i = 0; i < all.Count; i++)
            {
                var spot = home + new Vector2(-4.5f + (i % 10) * 1.05f, -2f + (i / 10) * 1.2f);
                FieldLootPickup.Create(spot, all[i].Name, all[i].Color, game.Player, game.Settings, () => false, 0, IndividualArt.Sprite("Equipment/Accessory/" + all[i].Id), all[i].Rarity);
            }
            yield return new WaitForSeconds(1.2f);
            yield return Shot(Path.Combine(folder, "accessories-field.png"));
            for (var i = 0; i < all.Count && game.Inventory.StoredItemCount < game.Inventory.UnlockedCapacity; i++)
                game.Inventory.AddAccessory(all[i]);
            typeof(GameController).GetProperty(nameof(GameController.IsInventoryOpen)).SetValue(game, true);
            yield return new WaitForSeconds(.4f);
            yield return Shot(Path.Combine(folder, "accessories-bag.png"));
            typeof(GameController).GetProperty(nameof(GameController.IsInventoryOpen)).SetValue(game, false);
        }

        private static IEnumerator Shot(string path)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
        }

        // Real physics rays at the heights the art occupies: top, middle, fists, feet.
        private static string Probe(EnemyController enemy)
        {
            Physics2D.SyncTransforms();
            var text = new StringBuilder("hurtbox " + enemy.DamageCollider.bounds.size + " centre-above-root " + (enemy.DamageCollider.bounds.center.y - enemy.transform.position.y) + "\n");
            foreach (var height in new[] { 0f, .3f, 1f, 2f, 2.5f })
            {
                var hit = false;
                var from = (Vector2)enemy.transform.position + new Vector2(-4f, height);
                foreach (var cast in Physics2D.RaycastAll(from, Vector2.right, 8f, 1 << GameLayers.Enemy))
                    if (EnemyController.DamageTarget(cast.collider) == enemy)
                        hit = true;
                text.Append("ray y+").Append(height).Append(hit ? " hit\n" : " miss\n");
            }
            return text.ToString();
        }

        private IEnumerator Visit(GameController game, EnemyType type, Vector2 home, string folder)
        {
            Directory.CreateDirectory(folder);
            var player = game.Player;
            player.Teleport(home);
            var boss = EnemyRoles.IsBoss(type);
            var enemy = EnemyController.Create(type, home + Vector2.left * (boss ? 5f : 4f), player, () => game.IsPaused, _ => { }, game.CameraFollow);
            var names = Catalog(type);
            yield return null;
            File.WriteAllText(Path.Combine(folder, "hurtbox.txt"), Probe(enemy));
            var seen = new Dictionary<string, int>();
            var log = new StringBuilder("frame,clip,acting,phase,x,y,sx,sy\n");
            var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
            if (boss)
            {
                // The entrance owns the sprite while the camera holds on the boss.
                BossEntrancePresentation.Begin(game, enemy, type.ToString(), "entrance", null);
                var shown = 0f;
                var entranceFrame = 0;
                while (BossEntrancePresentation.IsActive(game) && shown < 3f)
                {
                    yield return new WaitForEndOfFrame();
                    shown += Time.unscaledDeltaTime;
                    if (entranceFrame++ % 6 == 0)
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "entrance-" + entranceFrame.ToString("D3") + ".png"));
                }
            }
            var duration = boss ? 62f : 30f;
            var elapsed = 0f;
            var frame = 0;
            var nextHit = 6f;
            var moves = 0;
            var halved = false;
            var lastPhase = 0;
            var strikeFrames = 0;
            // Close, middle and far, so every range of the move set comes up. The sandbox panel covers the right of the window.
            var offsets = new[] { Vector2.up * 1.8f, Vector2.left * 3.2f, Vector2.down * 6f, Vector2.left * 1.8f, Vector2.up * 3.2f, Vector2.left * 6f };
            var moveEvery = duration / 12f;
            var nextMove = moveEvery;
            while (elapsed < duration + 2.4f && enemy != null)
            {
                yield return new WaitForEndOfFrame();
                elapsed += Time.deltaTime;
                if (enemy == null)
                    break;
                if (renderer.sprite != null)
                {
                    var name = names.TryGetValue(renderer.sprite, out var known) ? known : "UNKNOWN:" + renderer.sprite.name;
                    seen.TryGetValue(name, out var count);
                    seen[name] = count + 1;
                    log.Append(frame).Append(',').Append(name).Append(',').Append(enemy.IsActing).Append(',').Append(enemy.AttackVisualPhase).Append(',')
                        .Append(enemy.transform.position.x.ToString("F2")).Append(',').Append(enemy.transform.position.y.ToString("F2")).Append(',');
                    var screen = Camera.main == null ? Vector3.zero : Camera.main.WorldToScreenPoint(enemy.transform.position);
                    log.Append(Mathf.RoundToInt(screen.x)).Append(',').Append(Mathf.RoundToInt(screen.y)).Append('\n');
                }
                // Every drawn frame around a strike, a sparse record the rest of the time.
                if (enemy.AttackVisualPhase == 1 && lastPhase != 1)
                    strikeFrames = 14;
                lastPhase = enemy.AttackVisualPhase;
                if (strikeFrames > 0 || frame % 6 == 0)
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, frame.ToString("D4") + ".png"));
                strikeFrames--;
                frame++;
                if (enemy.IsDead)
                    continue;
                // Keep the hero clear of enemies that chase, and make them turn to face all four ways.
                if (elapsed >= nextMove && !enemy.IsActing)
                {
                    player.Teleport((Vector2)enemy.transform.position + offsets[moves++ % offsets.Length]);
                    nextMove = elapsed + moveEvery;
                }
                if (elapsed >= nextHit && !enemy.IsActing)
                {
                    enemy.TakeDamage(1f, Vector2.zero);
                    nextHit = elapsed + 6f;
                }
                if (boss && !halved && elapsed >= duration * .6f && !enemy.IsActing)
                {
                    // Into the second phase, for its roar and its doubled moves.
                    halved = true;
                    enemy.TakeDamage(enemy.Health - enemy.MaxHealth * .45f, Vector2.zero);
                }
                if (elapsed >= duration)
                {
                    player.Teleport((Vector2)enemy.transform.position + Vector2.right * 4f);
                    enemy.TakeDamage(enemy.MaxHealth * 3f, Vector2.zero);
                }
            }
            File.WriteAllText(Path.Combine(folder, "frames.csv"), log.ToString());
            var summary = new StringBuilder();
            var keys = new List<string>(seen.Keys);
            keys.Sort();
            foreach (var key in keys)
                summary.Append(key).Append(' ').Append(seen[key]).Append('\n');
            File.WriteAllText(Path.Combine(folder, "clips.txt"), summary.ToString());
            if (enemy != null)
                Destroy(enemy.gameObject);
            yield return null;
        }

        private static Dictionary<Sprite, string> Catalog(EnemyType type)
        {
            var names = new Dictionary<Sprite, string>();
            void Add(Sprite[] frames, string label)
            {
                if (frames == null)
                    return;
                for (var i = 0; i < frames.Length; i++)
                    if (frames[i] != null && !names.ContainsKey(frames[i]))
                        names[frames[i]] = label + "/" + i.ToString("D2");
            }
            if (type == EnemyType.Boss)
                for (var row = 0; row < 8; row++)
                    Add(IndividualArt.Frames(BossSpriteAnimator.RowId(row), BossSpriteAnimator.FrameCount), "row" + row);
            else if (type == EnemyType.Tidekeeper)
            {
                Add(TidekeeperArt.Frames(0), "idle");
                Add(IndividualArt.Frames(TidekeeperArt.Root + "lanes", 24), "lanes");
                Add(IndividualArt.Frames(TidekeeperArt.Root + "pools", 24), "pools");
                Add(IndividualArt.Frames(TidekeeperArt.Root + "hurt", 8), "hurt");
                Add(TidekeeperArt.Frames(3), "death");
            }
            else if (type == EnemyType.SealHeart)
                foreach (var clip in new[] { "idle", "move", "cast", "sweep", "charge", "hurt", "death" })
                    Add(SealHeartArt.Clip(clip), clip);
            else
                for (var d = 0; d < 4; d++)
                {
                    for (var state = 0; state < 4; state++)
                        Add(ExternalCreatureArt.Frames(type, state, d), new[] { "idle", "walk", "attack", "death" }[state] + "/" + Directions[d]);
                    Add(ExternalCreatureArt.HurtFrames(type, d), "hurt/" + Directions[d]);
                    Add(ExternalCreatureArt.AttackFrames(type, d, 1), "attack1/" + Directions[d]);
                }
            return names;
        }
    }
}
