using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportCreatureParts
{
    public static string Main()
    {
        if(Application.isPlaying) throw new Exception("Stop Play Mode before sprite import");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var factory=new SpriteDataProviderFactories();factory.Init();var count=0;
        foreach(var file in Directory.GetFiles("Assets/RuinRunner/Resources/Art/Pixel/Separated/CreatureMotion","*.png",SearchOption.AllDirectories))
        {
            var path=file.Replace('\\','/');var head=Path.GetFileNameWithoutExtension(path)=="head";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;importer.isReadable=true;importer.SaveAndReimport();
            var data=factory.GetSpriteEditorDataProviderFromObject(importer);data.InitSpriteEditorDataProvider();
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("No pivot editing: "+path);
            var rects=data.GetSpriteRects();
            foreach(var rect in rects){rect.alignment=SpriteAlignment.Custom;rect.pivot=head?new Vector2(.5f,.5f):new Vector2(.5f,.125f);}
            data.SetSpriteRects(rects);data.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.pixelsPerUnit!=32||sprite.rect.width!=(head?32:64))throw new Exception("Invalid rig sprite: "+path);
            count++;
        }
        return "Imported "+count+" creature sprites with common 32 PPU, fixed pivots, Point/uncompressed";
    }
}
