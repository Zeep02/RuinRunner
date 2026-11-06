using System.Collections;
using System.IO;
using RuinRunner.Generation;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Builds each stage with no combat and photographs every region from the game camera.
    /// --stage-art-tour folder [seed]</summary>
    public sealed class StageArtTour : MonoBehaviour
    {
        public const string Argument = "--stage-art-tour";
        public const string RouteCheckArgument = "--route-check";
        public const string LivingPropsArgument = "--living-props";

        // Generation alone, over many seeds: every room must validate, seals included, without falling back.
        private static void CheckSeeds()
        {
            const int seeds = 200;
            int fallbacks = 0, retries = 0, errors = 0, rooms = 0, obstacles = 0, fewest = int.MaxValue, most = 0;
            var layouts = new System.Collections.Generic.HashSet<string>();
            for (var seed = 0; seed < seeds; seed++)
                for (var stage = 0; stage < CampaignStage.Count; stage++)
                {
                    var graph = ProceduralStageGenerator.Generate(50000 + seed, stage);
                    if (graph.Diagnostics.UsedFallback)
                        fallbacks++;
                    retries += graph.Diagnostics.Attempts - 1;
                    var report = GeneratedStageGeometry.Validate(graph, true);
                    errors += report.Errors.Count;
                    foreach (var error in report.Errors)
                        Debug.LogError("[Stage Art Tour] seed " + seed + " stage " + stage + ": " + error);
                    foreach (var region in graph.Regions)
                    {
                        if (region.Role != RegionRole.Combat)
                            continue;
                        var count = region.Module.Obstacles.Length;
                        rooms++;
                        obstacles += count;
                        fewest = Mathf.Min(fewest, count);
                        most = Mathf.Max(most, count);
                        layouts.Add(region.Module.Id.Substring(region.Module.Id.IndexOf('-') + 1));
                    }
                }
            Debug.Log("[Stage Art Tour] seeds " + seeds + ": fallbacks " + fallbacks + ", retries " + retries + ", errors " + errors + ", combat rooms " + rooms + " (" + layouts.Count + " different), obstacles " + fewest + "-" + most + " avg " + (obstacles / (float)rooms).ToString("F1"));
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
            var seed = at + 2 < args.Length && int.TryParse(args[at + 2], out var given) ? given : 4242;
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            var follow = CameraFollow.Create();
            var view = follow.GetComponent<Camera>();
            // The follow script would pull the camera back to the map's start; this tour points it by hand.
            follow.enabled = false;
            CheckSeeds();
            yield return null;
            for (var stage = 0; stage < CampaignStage.Count; stage++)
            {
                var map = DungeonMap.BuildStage(_ => { }, seed, stage);
                yield return null;
                var dressing = 0;
                // Nothing placed loose in a room may lie on a wall, a wall's face or a solid island.
                var onWalls = 0;
                foreach (var placed in map.Root.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (!placed.name.Contains("dressing") && placed.name != "Floor detail")
                        continue;
                    // A moving piece's frames carry empty room to move in; it was placed by its still picture, which this check already covered.
                    var living = placed.GetComponent<EnvironmentProp>();
                    if (living != null && living.LivingClip != null && living.LivingClip.StartsWith("Prop"))
                        continue;
                    var picture = new Rect(placed.bounds.min, placed.bounds.size);
                    if (StageDressing.OnBareFloor(map.Terrain, picture))
                        continue;
                    onWalls++;
                    Debug.LogError("[Stage Art Tour] on a wall: " + placed.name + " " + placed.sprite.name + " at " + picture);
                }
                Debug.Log("[Stage Art Tour] stage " + stage + ": " + onWalls + " pictures on walls");
                if (System.Array.IndexOf(args, RouteCheckArgument) >= 0)
                    yield return CheckEnemyRoutes(map, follow, stage);
                if (System.Array.IndexOf(args, LivingPropsArgument) >= 0)
                    yield return PhotographLivingProps(follow, view, folder);
                WritePlan(map, Path.Combine(folder, "plan" + stage + ".png"));
                foreach (var region in map.Graph.Regions)
                {
                    var center = GeneratedStageUnityAdapter.Vector(region.Center);
                    view.transform.position = new Vector3(center.x, center.y, view.transform.position.z);
                    yield return new WaitForEndOfFrame();
                    Capture(view, Path.Combine(folder, "stage" + stage + "-" + region.Index.ToString("D2") + "-" + region.Module.Id + ".png"));
                }
                // A room locking and clearing, in steps.
                var seal = FindObjectsByType<EncounterBarrier>()[0];
                view.transform.position = new Vector3(seal.transform.position.x, seal.transform.position.y, view.transform.position.z);
                yield return null;
                seal.SetClosed(true);
                foreach (var after in new[] { .06f, .12f, .2f, .5f })
                {
                    yield return new WaitForSeconds(after);
                    yield return new WaitForEndOfFrame();
                    Capture(view, Path.Combine(folder, "seal" + stage + "-close-" + after.ToString("0.00") + ".png"));
                }
                seal.SetClosed(false);
                foreach (var after in new[] { .1f, .2f })
                {
                    yield return new WaitForSeconds(after);
                    yield return new WaitForEndOfFrame();
                    Capture(view, Path.Combine(folder, "seal" + stage + "-open-" + after.ToString("0.00") + ".png"));
                }
                foreach (var prop in FindObjectsByType<EnvironmentProp>())
                    dressing++;
                Debug.Log("[Stage Art Tour] stage " + stage + ": " + map.Graph.Regions.Length + " regions, " + dressing + " props");
                Destroy(map.Root);
                yield return null;
            }
            yield return Town(follow, view, folder);
            Debug.Log("[Stage Art Tour] Finished");
            Application.Quit(0);
        }

        // One of each moving piece, in steps, with the hero beside it: a piece only moves while the hero is near.
        private static IEnumerator PhotographLivingProps(CameraFollow follow, Camera view, string folder)
        {
            var hero = PlayerController.Create(Vector2.zero, () => false, () => true, () => { }, follow, new InventorySystem(), new GameSettingsService());
            hero.SandboxInvulnerable = true;
            follow.enabled = false;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var prop in FindObjectsByType<EnvironmentProp>())
            {
                if (prop.LivingClip == null || !(prop.LivingClip.StartsWith("Prop") || prop.LivingClip.StartsWith("FxProp")) || !seen.Add(prop.LivingClip))
                    continue;
                var at = (Vector2)prop.transform.position;
                hero.Teleport(at + new Vector2(1.1f, -.2f));
                view.transform.position = new Vector3(at.x, at.y + .4f, view.transform.position.z);
                for (var step = 0; step < 12; step++)
                {
                    yield return new WaitForSeconds(.1f);
                    yield return new WaitForEndOfFrame();
                    Capture(view, Path.Combine(folder, "living-" + prop.LivingClip + "-" + step.ToString("D2") + ".png"));
                }
                Debug.Log("[Stage Art Tour] living prop " + prop.LivingClip + " at " + at);
            }
            Destroy(hero.gameObject);
            yield return null;
        }

        // What is left of the sound after every other filter on the listener.
        private sealed class OutputProbe : MonoBehaviour
        {
            public float Peak;
            public int Buffers;
            private void OnAudioFilterRead(float[] data, int channels)
            {
                Buffers++;
                foreach (var sample in data)
                    Peak = Mathf.Max(Peak, Mathf.Abs(sample));
            }
        }

        // Every room's real enemies, against a hero standing still at each end of it. Whatever can walk must get a clear
        // line to the hero: an enemy that can't is stuck behind the room's cover. It also times what a route search costs.
        private static IEnumerator CheckEnemyRoutes(DungeonMap map, CameraFollow follow, int stage)
        {
            const float Limit = 8f;
            var inventory = new InventorySystem();
            inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Katana));
            var hero = PlayerController.Create(map.GetSpawnPoint(0), () => false, () => true, () => { }, follow, inventory, new GameSettingsService());
            hero.EquipWeapon(inventory.EquippedWeapon);
            hero.SandboxInvulnerable = true;
            var speakers = follow.gameObject.AddComponent<OutputProbe>();
            Time.timeScale = 4f;
            int checkedEnemies = 0, stuck = 0, searches = 0;
            var slowest = 0f;
            var clock = new System.Diagnostics.Stopwatch();
            foreach (var region in map.Graph.Regions)
            {
                if (!map.RequiresCombat(region.Index))
                    continue;
                var room = map.GetRoom(region.Index);
                var center = map.GetRoomCenter(region.Index);
                map.BeginEncounter(region.Index);
                foreach (var stand in new[] { map.GetSpawnPoint(region.Index), GeneratedStageUnityAdapter.Vector(region.RewardPosition) })
                {
                    hero.Teleport(stand);
                    yield return new WaitForFixedUpdate();
                    clock.Restart();
                    StageNavigation.FindPath(map.Terrain, center, stand);
                    clock.Stop();
                    searches++;
                    slowest = Mathf.Max(slowest, (float)clock.Elapsed.TotalMilliseconds);
                    for (var wave = 0; wave < room.WaveCount; wave++)
                        WaveDirector.SpawnWave(room, wave, hero, () => false, _ => { }, follow, center, map.Terrain);
                    var enemies = FindObjectsByType<EnemyController>();
                    var reached = new System.Collections.Generic.HashSet<EnemyController>();
                    for (var age = 0f; age < Limit; age += Time.deltaTime)
                    {
                        foreach (var enemy in enemies)
                            if (enemy != null && !reached.Contains(enemy) && enemy.HasLaneToPlayer)
                                reached.Add(enemy);
                        yield return null;
                    }
                    foreach (var enemy in enemies)
                    {
                        if (enemy == null || enemy.Type == EnemyType.RuneTotem)
                            continue;
                        checkedEnemies++;
                        if (reached.Contains(enemy))
                            continue;
                        stuck++;
                        Debug.LogError("[Stage Art Tour] stuck: " + enemy.Type + " in " + region.Id + " at " + enemy.transform.position + ", hero at " + stand + ": " + enemy.RouteReport());
                    }
                    RuntimeSceneObjects.ClearSession();
                    yield return null;
                }
                map.UnlockExit(region.Index);
            }
            Time.timeScale = 1f;
            Debug.Log("[Stage Art Tour] stage " + stage + ": loudest sample reaching the speakers " + speakers.Peak + " over " + speakers.Buffers + " buffers, silent run " + MasterLimiter.SilentRun);
            Destroy(speakers);
            Destroy(hero.gameObject);
            map.ResetExits();
            yield return null;
            Debug.Log("[Stage Art Tour] stage " + stage + ": " + checkedEnemies + " enemies routed, " + stuck + " stuck; slowest route search " + slowest.ToString("0.00") + " ms of " + searches);
        }

        // The floor plan: where you can walk, four pixels to the unit, with one screen marked for scale.
        private static void WritePlan(DungeonMap map, string path)
        {
            const int Scale = 4;
            var bounds = map.Terrain.Bounds;
            var width = Mathf.CeilToInt(bounds.width) * Scale;
            var height = Mathf.CeilToInt(bounds.height) * Scale;
            var plan = new Texture2D(width, height, TextureFormat.RGB24, false);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    plan.SetPixel(x, y, map.Terrain.IsOpen(bounds.min + new Vector2((x + .5f) / Scale, (y + .5f) / Scale)) ? new Color(.82f, .78f, .62f) : new Color(.12f, .12f, .16f));
            var screenWidth = Mathf.RoundToInt(CameraFollow.ViewHalfHeight * 2f * 16f / 9f) * Scale;
            var screenHeight = Mathf.RoundToInt(CameraFollow.ViewHalfHeight * 2f) * Scale;
            for (var x = 0; x < screenWidth && x < width; x++)
            {
                plan.SetPixel(x, 0, Color.red);
                plan.SetPixel(x, Mathf.Min(screenHeight, height - 1), Color.red);
            }
            for (var y = 0; y < screenHeight && y < height; y++)
            {
                plan.SetPixel(0, y, Color.red);
                plan.SetPixel(Mathf.Min(screenWidth, width - 1), y, Color.red);
            }
            plan.Apply();
            File.WriteAllBytes(path, plan.EncodeToPNG());
            Destroy(plan);
        }

        // The expedition door: walking up to it, standing at it, walking away.
        private IEnumerator Town(CameraFollow follow, Camera view, string folder)
        {
            var town = TownWorld.Create();
            var inventory = new InventorySystem();
            var player = PlayerController.Create(TownWorld.Arrival, () => false, () => true, () => { }, follow, inventory, new GameSettingsService());
            yield return null;
            var gate = FindAnyObjectByType<ProximityGate>();
            view.transform.position = new Vector3(gate.transform.position.x - 2f, gate.transform.position.y + 1f, view.transform.position.z);
            var feet = (Vector2)gate.transform.position;
            var away = feet + Vector2.left * 4f;
            var near = feet + Vector2.down * 1.4f;
            player.Teleport(away);
            yield return new WaitForSeconds(.6f);
            var index = 0;
            foreach (var step in new[] { (near, "open"), (away, "close") })
            {
                player.Teleport(step.Item1);
                for (var i = 0; i < 6; i++)
                {
                    yield return new WaitForSeconds(.1f);
                    yield return new WaitForEndOfFrame();
                    Debug.Log("[Stage Art Tour] gate " + step.Item2 + " " + i + " open=" + gate.OpenAmount.ToString("0.00"));
                    Capture(view, Path.Combine(folder, "gate-" + (index++).ToString("D2") + "-" + step.Item2 + ".png"));
                }
            }
            Destroy(town.gameObject);
        }

        private static void Capture(Camera view, string path)
        {
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            view.targetTexture = target;
            view.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            view.targetTexture = null;
            RenderTexture.active = previous;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            target.Release();
            Destroy(target);
            Destroy(pixels);
        }
    }
}
