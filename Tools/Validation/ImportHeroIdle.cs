using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportHeroIdle
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before importing idle poses");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var file in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/idle","*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot capability: "+path);
            var rects=data.GetSpriteRects();
            foreach(var rect in rects){rect.alignment=SpriteAlignment.Custom;rect.pivot=new Vector2(.5f,10f/48f);}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null || sprite.rect.size!=new Vector2(48,48) || sprite.pivot!=new Vector2(24,10))throw new Exception("Invalid idle sprite: "+path);
            count++;
        }
        if(count!=48)throw new Exception("Expected 48 idle poses; got "+count);
        return "Imported 48 idle poses, 48x48, fixed 24/10 foot pivot, 32 PPU, Point, no compression";
    }
}
