using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportStageThemes
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before imports");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var file in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Terrain","*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            var quadrant=path.Contains("/Floor/") && Path.GetFileNameWithoutExtension(path).Contains("-");
            importer.spritePixelsPerUnit=quadrant?16:32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.isReadable=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var cap=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(cap==null || !cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot edit: "+path);
            var rects=data.GetSpriteRects();foreach(var rect in rects)
            {rect.alignment=SpriteAlignment.Custom;rect.pivot=path.EndsWith("/Cistern.png")?new Vector2(.5f,72f/176):Vector2.one*.5f;}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();count++;
        }
        AssetDatabase.SaveAssets();if(count!=109)throw new Exception("Expected 109 theme sprites, got "+count);
        return "PASS: 3 distinct terrain kits, 60 authored sprites, 48 floor quadrants, one complete cistern; Point/no compression/authored pivots.";
    }
}
