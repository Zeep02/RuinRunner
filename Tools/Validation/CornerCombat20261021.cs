using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using RuinRunner;

// Reproduce the observed stage-one wall-contact positions. Supplied state,
// stationary player, real enemy AI and colliders; never normal campaign proof.
public static class CornerCombat20261021
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (game.IsRunActive || game.BackendBusy || game.BackendHasPending)
            throw new Exception("Idle menu required");
        game.enabled = false;
        Application.runInBackground = true;
        Time.timeScale = 1;
        var folder = Path.GetFullPath("Logs/Validation/2026-10-21-goal/corner-" + DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(folder);
        game.StartCoroutine(Run(game, folder));
        return folder;
    }
    static IEnumerator Run(GameController game, string folder)
    {
        var map = DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(), _ => {}, 2026100903, 0);
        var inventory = new InventorySystem();
        inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Pistol));
        var camera = CameraFollow.Create();
        foreach (var listener in camera.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
        var player = PlayerController.Create(new Vector2(33f, -27.30f), () => true, () => false,
            () => {}, camera, inventory, new GameSettingsService());
        var enemy = EnemyController.Create(EnemyType.Charger, new Vector2(32.34f, -27.21f), player,
            () => false, _ => {}, camera);
        var center = map.GetRoomCenter(2); var room = map.GetRoom(2);
        enemy.ConfigureArena(new Rect(center - room.Size * .5f, room.Size));
        enemy.ConfigureNavigation(map.Terrain);
        var samples = new JArray();
        for (int i = 0; i < 10; i++)
        {
            var from = (Vector2)enemy.transform.position; var to = (Vector2)player.transform.position;
            var ray = Physics2D.CircleCast(from, .3f, (to-from).normalized, (to-from).magnitude, 1 << GameLayers.Ground);
            var path = StageNavigation.FindPath(map.Terrain, from, to);
            samples.Add(new JObject {
                ["second"] = i, ["enemy"] = from.ToString("F4"), ["player"] = to.ToString("F4"),
                ["lane"] = StageNavigation.HasCombatLane(from, to, .3f),
                ["wall"] = ray.collider == null ? "none" : ray.collider.name,
                ["castDistance"] = ray.distance, ["normal"] = ray.normal.ToString("F4"),
                ["route"] = path == null ? "null" : string.Join(" -> ", path),
                ["enemyFoot"] = enemy.GetComponent<BoxCollider2D>().bounds.ToString("F4"),
                ["acting"] = enemy.IsActing, ["health"] = player.Health
            });
            yield return new WaitForSeconds(1);
        }
        bool laneOpened = false, actionStarted = false;
        foreach (JObject sample in samples) { laneOpened |= (bool)sample["lane"]; actionStarted |= (bool)sample["acting"]; }
        File.WriteAllText(folder + "/result.json", new JObject {
            ["suppliedState"] = true, ["laneOpened"] = laneOpened, ["actionStarted"] = actionStarted,
            ["passed"] = laneOpened && actionStarted, ["samples"] = samples
        }.ToString(), new UTF8Encoding(false));
        UnityEngine.Object.Destroy(enemy.gameObject);
        UnityEngine.Object.Destroy(player.gameObject);
        UnityEngine.Object.Destroy(camera.gameObject);
        UnityEngine.Object.Destroy(map.Root);
        game.enabled = true;
        Debug.Log("[Corner Combat] " + folder);
    }
}
