using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportHeroMotion
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before importer edits");
        AssetDatabase.Refresh();
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var file in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion","*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/'); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32; importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=64;
            importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot capability: "+path);
            var rects=data.GetSpriteRects();
            foreach(var rect in rects){rect.alignment=SpriteAlignment.Custom;rect.pivot=new Vector2(.5f,10f/48f);}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();
            if(AssetDatabase.LoadAssetAtPath<Sprite>(path)==null)throw new Exception("No Sprite: "+path);
            count++;
        }
        return "Imported "+count+" individual 48x48 hero sprites, 32PPU, common foot pivot 24/10, Point, uncompressed.";
    }
}
