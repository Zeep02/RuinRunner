using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ImportContinuousTerrain
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode first");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var count=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var theme in new[]{"Outer","Flooded","Core"})
            foreach(var kind in new[]{"Field","Ribbon"})
            foreach(var path in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Terrain/"+theme+"/"+kind,"*.png"))
            {
                var asset=path.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(asset);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
                importer.isReadable=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;
                importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;
                settings.spritePivot=Vector2.one*.5f;importer.SetTextureSettings(settings);
                importer.SaveAndReimport();count++;
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();
        if(count!=3096)throw new Exception("Expected 3072 floor cells and 24 ribbon strips; got "+count);
        return "PASS imported "+count+" individual PNGs, Point, uncompressed, full rectangle, center pivots.";
    }
}
