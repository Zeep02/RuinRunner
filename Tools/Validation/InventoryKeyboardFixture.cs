using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Isolated inventory/input regression; seeded equipment is not campaign evidence.
public static class InventoryKeyboardFixture
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string folder;
    static int checks;
    static void Check(bool ok, string label)
    {
        File.AppendAllText(folder + "/result.txt", (ok ? "PASS " : "FAIL ") + label + "\n");
        if (!ok) throw new Exception(label);
        checks++;
    }
    static void Press(GameAction action)
    {
        StageValidationInput.BeginFrame();
        StageValidationInput.ReleaseAll();
        StageValidationInput.SetHeld(action, true);
        StageValidationInput.SetHeld(action, false);
    }
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        folder = "F:/codex/LoglikeCap/Logs/Validation/" + DateTime.Now.ToString("yyyy-MM-dd") + "-inventory-keys/" + DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder); checks = 0;
        var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu(); game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot", Private).Invoke(game, null);
        typeof(StageValidationInput).GetField("enabled", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, true);
        typeof(StageValidationInput).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, true);
        StageValidationInput.ReleaseAll(); Application.runInBackground = true;
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.CameraFollow.StartCoroutine(Run(game));
        return folder;
    }
    static IEnumerator Run(GameController game)
    {
        yield return null;
        game.StopAllCoroutines();
        var bag = game.Inventory;
        var bow = WeaponLootTable.Find("hunter_bow");
        var pistol = WeaponLootTable.Find("scrap_pistol");
        var dagger = WeaponLootTable.Find("wind_dagger");
        bag.AddWeapon(bow); bag.AddWeapon(pistol);
        bag.AddWeapon(dagger); bag.AddWeapon(bow);
        var carriedWeapons = bag.StoredWeapons.Count + 3;
        Press(GameAction.Inventory); yield return null;
        Check(game.IsInventoryOpen && game.IsPaused, "inventory opens and owns simulation input");
        var position = game.Player.transform.position;
        var contacts = game.Player.MeleeContactsResolved;
        bag.QuickSlots[1].TryConsumeAmmo(); bag.QuickSlots[1].TryConsumeAmmo();
        bag.QuickSlots[1].StartReload();
        var ammo = bag.QuickSlots[1].Ammo;
        var reload = bag.QuickSlots[1].ReloadRemaining;
        Press(GameAction.Weapon2); yield return null;
        Check(bag.QuickSlots[1].Weapon == dagger && game.Player.EquippedWeapon == dagger, "selected bag weapon equips with slot key");
        Press(GameAction.Weapon2); yield return null;
        Check(bag.QuickSlots[1].Weapon == bow && bag.QuickSlots[1].Ammo == ammo && bag.QuickSlots[1].ReloadRemaining == reload, "round trip preserves partial ammo and pending reload");
        Check(carriedWeapons == bag.StoredWeapons.Count + 3, "exchange never duplicates or removes equipment");
        foreach (var rank in new[] { 0, 1, 2, 3 })
        {
            bag.ConfigureExpansion(rank);
            foreach (var cell in new[] { 0, 4, 5, 19, bag.UnlockedCapacity - 1 })
                Check(bag.BagCell(InventorySystem.BagColumn(cell), InventorySystem.BagRow(cell)) == cell, "physical grid mapping rank=" + rank + " cell=" + cell);
            Check(bag.BagCell(5 + rank, 0) == -1 && bag.BagCell(-1, 0) == -1, "locked/outside columns rejected rank=" + rank);
        }
        for (var i = 0; i < 7; i++) { Press(GameAction.MoveRight); yield return null; }
        Check(game.InventoryFocusCell == 28, "right input reaches third expansion column, top row");
        Press(GameAction.MoveDown); yield return null;
        Check(game.InventoryFocusCell == 29, "down input follows physical expansion rows");
        Press(GameAction.Weapon1); yield return null;
        Check(bag.QuickSlots[0].Weapon.Id == "rust_sword", "empty selected cell cannot replace equipped weapon");
        Press(GameAction.Attack); yield return null;
        Check(game.Player.MeleeContactsResolved == contacts && Vector3.Distance(position, game.Player.transform.position) < .05f, "inventory navigation and attack never reach world movement/combat");
        game.FocusInventoryCell(0);
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(folder + "/inventory.png");
        }
        else File.AppendAllText(folder + "/result.txt", "NO VISUAL CAPTURE: headless logic validation only\n");
        Press(GameAction.Inventory);
        StageValidationInput.SetHeld(GameAction.Attack, true);
        StageValidationInput.SetHeld(GameAction.Attack, false);
        yield return null;
        Check(!game.IsInventoryOpen && game.Player.MeleeContactsResolved == contacts, "closing frame consumes simultaneous attack");
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll();
        yield return new WaitForSecondsRealtime(.4f);
        Check(!game.IsPaused && Time.timeScale == 1f, "closing bag resumes simulation on next frame");
        Check(game.Player.MeleeContactsResolved == contacts, "closing frame leaves no queued melee hit");
        Press(GameAction.Inventory); yield return null;
        Press(GameAction.Pause); yield return null;
        Check(!game.IsInventoryOpen && !game.IsManuallyPaused, "Escape closes bag without opening pause menu");
        StageValidationInput.BeginFrame(); StageValidationInput.ReleaseAll();
        yield return null;
        Check(!game.IsPaused && Time.timeScale == 1f, "Escape close resumes simulation on next frame");
        File.AppendAllText(folder + "/result.txt", "COMPLETE " + checks + " isolated assertions\n");
        Debug.Log("[Inventory Keys] PASS " + checks + " assertions " + folder);
    }
}
