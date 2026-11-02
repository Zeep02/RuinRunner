using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

public static class ImportCampaignEnvironment
{
    public static string Main()
    {
        if(Application.isPlaying) throw new Exception("Import outside Play Mode");
        var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if(pipeline!=null) throw new Exception("Expected the existing Built-in pixel pipeline, found "+pipeline.GetType().FullName);
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var path in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Environment","*.png"))
        {
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.isReadable=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit=32;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=128;
            importer.SaveAndReimport();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot)) throw new Exception("Pivot capability unavailable: "+path);
            var pivot=new Vector2(.5f,path.Contains("Monument")?12f/128f:.5f);
            var rects=provider.GetSpriteRects();
            foreach(var rect in rects){rect.alignment=SpriteAlignment.Custom;rect.pivot=pivot;}
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null || sprite.texture.width!=128 || sprite.texture.height!=128 || sprite.pixelsPerUnit!=32 || sprite.pivot!=pivot*128)
                throw new Exception("Import contract: "+path);
            foreach(var color in sprite.texture.GetPixels32()) if(color.a!=0 && color.a!=255)throw new Exception("Nonbinary alpha: "+path);
            count++;
        }
        if(count!=9)throw new Exception("Expected nine separate environment sprites");
        var result="[Environment] Built-in pipeline,9 individual 128px/32PPU/Point/uncompressed sprites; binary alpha and authored pivots verified.";
        File.WriteAllText("ArtDirection/Generated/Environment-20260920/import.txt",result);
        return result;
    }
}
