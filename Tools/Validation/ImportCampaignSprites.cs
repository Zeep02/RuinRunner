using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportCampaignSprites
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before importer edits");
        AssetDatabase.Refresh();
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var file in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign","*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/');
            if(path.Contains("/Hazards/"))continue; // These use their own geometric contour importer.
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var edit=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(edit==null || !edit.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot editing unsupported: "+path);
            var rects=data.GetSpriteRects();
            foreach(var rect in rects){rect.alignment=SpriteAlignment.Custom;rect.pivot=new Vector2(.5f,14f/96f);}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();
            if(AssetDatabase.LoadAssetAtPath<Sprite>(path)==null)throw new Exception("No Sprite: "+path);
            count++;
        }
        return "Imported "+count+" individual sprites: bosses 96x96, minor enemies 48x48, 32PPU, common foot pivot (0.5,14/96), Point, no compression/mips.";
    }
}
