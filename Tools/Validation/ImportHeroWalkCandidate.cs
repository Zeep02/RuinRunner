using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportHeroWalkCandidate
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        var files=new List<string>(Directory.GetFiles("Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth","*.png",SearchOption.AllDirectories));
        var runtime="Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroWalk";
        if(Directory.Exists(runtime))files.AddRange(Directory.GetFiles(runtime,"*.png",SearchOption.AllDirectories));
        foreach(var file in files)
        {
            var path=file.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;importer.isReadable=true;importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var cap=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot edit");
            var rig=path.Contains("/rig/")||Path.GetFileName(path).StartsWith("torso-");var scarf=path.EndsWith("/scarf.png");
            var pivot=rig?(scarf?new Vector2(.75f,.75f):new Vector2(.5f,.875f)):new Vector2(.5f,10f/48);
            var rects=data.GetSpriteRects();foreach(var rect in rects){rect.alignment=SpriteAlignment.Custom;rect.pivot=pivot;}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();count++;
        }
        return "Imported "+count+" authoring/candidate sprites with fixed pivots and 32 PPU.";
    }
}
