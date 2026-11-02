using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using RuinRunner;

// One-time authoring bake of the existing weapon silhouettes; no new artwork.
public static class BakeHeldWeapons
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before baking");
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/HeldWeapons";
        Directory.CreateDirectory(root); var factory=new SpriteDataProviderFactories();factory.Init();
        var report=new StringBuilder("id,width,height,ppu,pivotX,pivotY\n");var count=0;
        foreach(var weapon in WeaponLootTable.All)
        {
            var source=SourceSprite(weapon);var rect=source.rect;
            var texture=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGBA32,false);
            texture.SetPixels(source.texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height));texture.Apply();
            var path=root+"/"+weapon.Id+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=source.pixelsPerUnit;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.maxTextureSize=512;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot capability "+path);
            var pivot=new Vector2(source.pivot.x/rect.width,source.pivot.y/rect.height);
            if(RangedWeaponArt.TryGet(weapon.Id,out var art))pivot=new Vector2(art.Grip.x/rect.width,1-art.Grip.y/rect.height);
            var sprites=data.GetSpriteRects();foreach(var s in sprites){s.alignment=SpriteAlignment.Custom;s.pivot=pivot;}
            data.SetSpriteRects(sprites);data.Apply();importer.SaveAndReimport();
            report.AppendLine(weapon.Id+","+rect.width+","+rect.height+","+source.pixelsPerUnit+","+pivot.x+","+pivot.y);count++;
            UnityEngine.Object.DestroyImmediate(source);
        }
        File.WriteAllText("ArtDirection/Generated/HeroMotion-20260916/held-weapon-bake.csv",report.ToString());
        return "Baked "+count+" existing held weapons into individual PNG/Sprite assets with authored grips.";
    }
    private static Sprite SourceSprite(WeaponDefinition weapon)
    {
        var icon=PixelArtAtlas.GetInventoryIcon(weapon.Id,InventoryIconKind.Weapon);
        var bounds=ItemIconLibrary.ContentBounds(icon);
        var pivot=weapon.Id=="rust_sword"?new Vector2(.5f,.15f):weapon.Id=="hunter_bow"?new Vector2(.85f,.5f):Grip(weapon.Family);
        return Sprite.Create(icon,new Rect(bounds.x,bounds.y,bounds.width,bounds.height),pivot,Mathf.Max(bounds.width,bounds.height),0,SpriteMeshType.FullRect);
    }
    private static Vector2 Grip(WeaponFamily family)
    {
        switch(family)
        {
            case WeaponFamily.Bow:return new Vector2(.5f,.5f);
            case WeaponFamily.Crossbow:return new Vector2(.32f,.34f);
            case WeaponFamily.Pistol:return new Vector2(.28f,.27f);
            case WeaponFamily.Rifle:case WeaponFamily.Shotgun:case WeaponFamily.Cannon:return new Vector2(.34f,.32f);
            default:return new Vector2(.14f,.14f);
        }
    }
}
