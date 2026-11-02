using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using RuinRunner;

// Replays the actual stage-three masonry corner with real colliders and enemy AI.
// The staged player is stationary; this is a focused regression, not a full run.
public static class CoverNavigationFixture
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first");
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu();
        game.enabled = false;
        Time.timeScale = 1f;
        Application.runInBackground = true;
        var folder = "F:/codex/LoglikeCap/Logs/Validation/" + DateTime.Now.ToString("yyyy-MM-dd") +
                     "-cover-navigation/" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);
        game.StartCoroutine(Run(folder));
        return folder;
    }

    private static IEnumerator Run(string folder)
    {
        // These coordinates belong to the pre-generator authored masonry map.
        var map = DungeonMap.BuildAuthoredStageForValidation(RoomTemplateCatalog.CreateRandomRun(), _ => { }, 824942335, 2);
        var inventory = new InventorySystem();
        inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var camera = CameraFollow.Create();
        foreach (var listener in camera.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
        var player = PlayerController.Create(new Vector2(39.06f, 17.02f), () => true, () => false,
            () => { }, camera, inventory, new GameSettingsService());
        var enemy = EnemyController.Create(EnemyType.Sentinel, new Vector2(39.06f, 11.96f), player,
            () => false, _ => { }, camera);
        var center = map.GetRoomCenter(1);
        var room = map.GetRoom(1);
        enemy.ConfigureArena(new Rect(center - room.Size * .5f, room.Size));
        enemy.ConfigureNavigation(map.Terrain);
        var report = new StringBuilder();
        var origin = (Vector2)enemy.transform.position;
        var destination = (Vector2)player.transform.position;
        var path = StageNavigation.FindPath(map.Terrain, origin, destination);
        report.AppendLine("stage=3 room=2 seed=824942335");
        report.AppendLine("initialLane=" + StageNavigation.HasCombatLane(origin, destination, .3f));
        report.AppendLine("route=" + (path == null ? "null" : string.Join(" -> ", path)));
        var validSetup = !StageNavigation.HasCombatLane(origin, destination, .3f) && path != null && path.Count >= 2;
        if (!validSetup)
            report.AppendLine("FAIL: cover corridor was not detected or has no route");
        var elapsed = 0f;
        var lastLoggedSecond = -1;
        var reached = false;
        while (elapsed < 20f && !reached)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
            var second = Mathf.FloorToInt(elapsed);
            if (second != lastLoggedSecond)
            {
                lastLoggedSecond = second;
                report.AppendLine("t=" + elapsed.ToString("0.00") + " enemy=" + enemy.transform.position +
                                  " player=" + player.transform.position);
            }
            reached = Vector2.Distance(enemy.transform.position, player.transform.position) < 4.2f &&
                      StageNavigation.HasCombatLane(enemy.transform.position, player.transform.position, .3f);
        }
        report.AppendLine(validSetup && reached ? "PASS: Sentinel physically rounded cover and approached within attack range" :
            "FAIL: Sentinel did not round cover and approach within 20 simulated seconds");
        File.WriteAllText(Path.Combine(folder, "result.txt"), report.ToString(), Encoding.UTF8);
        Debug.Log("[Cover Navigation] " + folder + " " + (validSetup && reached ? "PASS" : "FAIL"));
        UnityEngine.Object.Destroy(enemy.gameObject);
        UnityEngine.Object.Destroy(player.gameObject);
        UnityEngine.Object.Destroy(camera.gameObject);
        UnityEngine.Object.Destroy(map.Root);
    }
}
