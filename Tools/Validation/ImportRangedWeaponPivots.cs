using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using RuinRunner;

public static class ImportRangedWeaponPivots
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before editing importers");
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/HeldWeapons/";
        const string backup="Backups/2026-09-20-ranged-alignment/importers/";
        Directory.CreateDirectory(backup);
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        var report=new List<string>{"weapon,width,height,ppu,gripX,gripY,muzzleX,muzzleY,sourceAngle"};
        foreach(var weapon in WeaponLootTable.All)
        {
            if(weapon.AttackType!=WeaponAttackType.Ranged)continue;
            if(!RangedWeaponArt.TryGet(weapon.Id,out var art))throw new Exception("Missing authoring: "+weapon.Id);
            var path=root+weapon.Id+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(importer==null || sprite==null)throw new Exception("Missing individual sprite: "+path);
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                throw new Exception("Pivot editing unsupported: "+path);
            if(!File.Exists(backup+weapon.Id+".png.meta"))File.Copy(path+".meta",backup+weapon.Id+".png.meta");
            var size=sprite.rect.size;var ppu=sprite.pixelsPerUnit;
            var rects=data.GetSpriteRects();
            if(rects.Length!=1)throw new Exception("Expected single sprite: "+path);
            rects[0].alignment=SpriteAlignment.Custom;
            rects[0].pivot=new Vector2(art.Grip.x/size.x,1-art.Grip.y/size.y);
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();
            sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(Vector2.Distance(sprite.pivot,new Vector2(art.Grip.x,size.y-art.Grip.y))>.001f || sprite.pixelsPerUnit!=ppu || sprite.rect.size!=size)
                throw new Exception("Unexpected imported geometry: "+path);
            report.Add(string.Join(",",weapon.Id,size.x,size.y,ppu,art.Grip.x,art.Grip.y,art.Muzzle.x,art.Muzzle.y,art.ForwardAngle));count++;
        }
        if(count!=16)throw new Exception("Expected 16 ranged weapons, got "+count);
        File.WriteAllLines("Logs/Validation/2026-09-20-ranged-motion/weapon-landmarks.csv",report);
        AssetDatabase.SaveAssets();
        return "PASS: 16 ranged grip pivots, unchanged canvas/PPU/source pixels. Source axes/muzzles authored separately.";
    }
}
