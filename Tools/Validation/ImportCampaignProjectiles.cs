using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

public static class ImportCampaignProjectiles
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Import only outside Play Mode");
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var path in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Projectiles","*.png"))
        {
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit=32;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=32;
            importer.SaveAndReimport();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot capability unavailable: "+path);
            var rects=provider.GetSpriteRects();foreach(var rect in rects){rect.alignment=SpriteAlignment.Center;rect.pivot=new Vector2(.5f,.5f);}
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null || sprite.texture.width!=32 || sprite.texture.height!=32 || sprite.pixelsPerUnit!=32)throw new Exception("Import contract: "+path);
            count++;
        }
        var result="[Campaign Projectiles] Imported and verified "+count+" individual 32px / 32PPU / centered / Point sprites.";
        File.WriteAllText("ArtDirection/Generated/Projectiles-20260916/import.txt",result);
        return result;
    }
}
