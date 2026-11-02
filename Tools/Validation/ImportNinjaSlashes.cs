using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportNinjaSlashes
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before imports");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factories=new SpriteDataProviderFactories();factories.Init();var count=0;
        foreach(var file in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/External/NinjaAdventure/Slash","*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out var width,out var height);
            if(width!=height)throw new Exception("Expected fixed square effect canvas: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=width;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.isReadable=true;
            importer.wrapMode=TextureWrapMode.Clamp;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var data=factories.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot edit unavailable "+path);
            var rects=data.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected one Sprite per file "+path);
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=Vector2.one*.5f;
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();count++;
        }
        AssetDatabase.SaveAssets();if(count!=38)throw new Exception("Expected 38 individually separated slash candidates, got "+count);
        return "Imported 38 Pixel-Boy/AAA CC0 frames. Point/uncompressed/no mipmaps, fixed centered canvas per clip, unit sprite bounds.";
    }
}
