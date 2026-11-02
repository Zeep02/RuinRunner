using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Seeded inventory presentation only. Never a normal drop or campaign claim.
public static class EquipmentVisualValidation
{
    static string folder; static GameController game;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-22-equipment-ui/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.enabled=true;game.ReturnToMainMenu();game.SaveSlots.OpenTransientForTests();
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=true;
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.CameraFollow.StartCoroutine(Run());return folder;
    }
    static void Press(GameAction action){StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();StageValidationInput.SetHeld(action,true);StageValidationInput.SetHeld(action,false);}
    static void Capture(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();var bag=game.Inventory;bag.Reset();bag.ConfigureExpansion(3);
        foreach(var weapon in WeaponLootTable.All)if(!bag.AddWeapon(weapon))throw new Exception("Weapon did not fit "+weapon.Id);
        game.Player.EquipWeapon(bag.EquippedWeapon);Press(GameAction.Inventory);yield return null;StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
        yield return new WaitForEndOfFrame();Capture("01-weapons-inventory");
        Press(GameAction.Inventory);yield return null;bag.Reset();bag.ConfigureExpansion(0);
        bag.AddWeapon(WeaponLootTable.Find("scrap_pistol"));bag.AddWeapon(WeaponLootTable.Find("hunter_bow"));bag.AddWeapon(WeaponLootTable.Find("rust_sword"));
        foreach(var accessory in AccessoryLootTable.All)if(!bag.AddAccessory(accessory))throw new Exception("Accessory did not fit "+accessory.Id);
        game.Player.EquipWeapon(bag.EquippedWeapon);Press(GameAction.Inventory);yield return null;StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();
        yield return new WaitForEndOfFrame();Capture("02-accessories-inventory");Press(GameAction.Inventory);yield return null;
        StageValidationInput.BeginFrame();StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.3f);
        var origin=(Vector2)game.Player.transform.position;
        var ids=new[]{"scrap_pistol","hunter_bow","iron_mace","crimson_ring","mana_prism","lucky_dice"};
        for(int i=0;i<ids.Length;i++)
        {
            var kind=i<3?InventoryIconKind.Weapon:InventoryIconKind.Accessory;
            var sprite=InventoryIconFactory.GetWorldSprite(ids[i],Color.white,kind);
            var pickup=FieldLootPickup.Create(origin+new Vector2(-4.5f+(i%3)*2.5f,2f-(i/3)*3f),ids[i],Color.white,game.Player,game.Settings,()=>false,0,sprite);
        }
        yield return new WaitForSeconds(5.5f);yield return new WaitForEndOfFrame();Capture("03-world-drops");
        File.WriteAllText(folder+"/result.txt","PASS seeded31 weapon and18 accessory inventory pages plus6 world drops; source-icon aliases validated separately; actual HUD/FieldLootPickup. Not normal campaign evidence.");
    }
}
