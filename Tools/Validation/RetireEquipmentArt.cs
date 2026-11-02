using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RuinRunner;
public static class RetireEquipmentArt
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        foreach(var weapon in WeaponLootTable.All)
            if(!NativeWeaponArt.Supports(weapon.Id)||PixelArtAtlas.GetHeldWeaponSprite(weapon)==null)throw new Exception("Replacement missing "+weapon.Id);
        foreach(var item in AccessoryLootTable.All)
            if(InventoryIconFactory.Get(item.Id,Color.white,InventoryIconKind.Accessory)==null)throw new Exception("Replacement missing "+item.Id);
        const string dest="Assets/RuinRunner/ArtReview/RetiredGeneratedEquipment";
        if(!AssetDatabase.IsValidFolder(dest))AssetDatabase.CreateFolder("Assets/RuinRunner/ArtReview","RetiredGeneratedEquipment");
        var moved=0;
        foreach(var pair in new[]{new[]{"Assets/RuinRunner/Resources/Art/Pixel/Items",dest+"/Items"},new[]{"Assets/RuinRunner/Resources/Art/Pixel/Separated/HeldWeapons",dest+"/HeldWeapons"}})
        {
            if(!AssetDatabase.IsValidFolder(pair[0]))continue;
            var count=Directory.GetFiles(pair[0],"*.png",SearchOption.AllDirectories).Length;
            var error=AssetDatabase.MoveAsset(pair[0],pair[1]);if(error!="")throw new Exception(error);moved+=count;
        }
        AssetDatabase.SaveAssets();return "Preserved and retired "+moved+" old generated equipment PNGs outside Resources; GUIDs retained";
    }
}
