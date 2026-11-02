using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Run through Unity Pipeline run_script in Play Mode. Deliberately staged art fixture,
// not gameplay evidence. Uses a transient profile and never writes a user save.
public static class CaptureReference
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        Application.runInBackground = true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        if (game.IsRunActive) game.ReturnToMainMenu();
        game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        game.Player.SetStageClearSafety(true);
        game.StartCoroutine(Compose(game));
        return "Staged visual reference started; no progression/completion evidence.";
    }

    private static IEnumerator Compose(GameController game)
    {
        yield return new WaitForSeconds(2.2f);
        game.Player.Teleport(new Vector2(-4, 0));
        var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>();
        for (var i = 0; i < enemies.Length; i++)
        {
            enemies[i].transform.position = new Vector2(-.5f + i * 2, 1.5f);
            enemies[i].GetComponent<Rigidbody2D>().position = enemies[i].transform.position;
        }
        RewardChest.Create(new Vector2(-2, -2), () => {}, game.Settings);
        game.CameraFollow.FocusRoom(game.DungeonMap.Terrain.Bounds.center, game.DungeonMap.Terrain.Bounds.size, true);
        Time.timeScale = 0;
        Debug.Log("[Reference] READY: player, held weapon, live enemies, user chest, terrain, HUD. Transient slot / frozen fixture.");
    }
}
