using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

public static class ImportGuardianTown20261004
{
    [Serializable] public class Row { public string path;public float ppu,pivotX,pivotY;public int width,height;public string filter; }
    [Serializable] public class Report { public string status="PASS";public List<Row> rows=new List<Row>(); }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before importing source art.");
        AssetDatabase.Refresh();var report=new Report();
        var roots=new[]{"Assets/RuinRunner/Resources/Art/Pixel/Separated/External/10k/SealGuardian",
            "Assets/RuinRunner/Resources/Art/Pixel/Separated/External/TownQuesters"};
        foreach(var root in roots)foreach(var path in Directory.GetFiles(root,"*.png",SearchOption.AllDirectories))
        {
            var asset=path.Replace('\\','/');
            var importer=(TextureImporter)AssetImporter.GetAtPath(asset);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;
            importer.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);
            if(provider==null)throw new Exception("Sprite provider unavailable: "+asset);
            provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                throw new Exception("Importer cannot edit pivot; aborted: "+asset);
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected one native cell: "+asset);
            var pivot=asset.Contains("SealGuardian")?new Vector2(.5f,7f/64):
                asset.Contains("/smith/")?new Vector2(.5f,19f/64):new Vector2(.5f,.25f);
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivot;
            provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(asset);
            if(sprite==null||sprite.pixelsPerUnit!=32||Vector2.Distance(sprite.pivot,pivot*64)>.001f)
                throw new Exception("Native sprite import verification failed: "+asset);
            report.rows.Add(new Row{path=asset,ppu=sprite.pixelsPerUnit,pivotX=sprite.pivot.x,pivotY=sprite.pivot.y,
                width=(int)sprite.rect.width,height=(int)sprite.rect.height,filter=importer.filterMode.ToString()});
        }
        const string output="ArtDirection/Review-20261004/seal-guardian/import-result.json";
        File.WriteAllText(output,JsonUtility.ToJson(report,true),new UTF8Encoding(false));
        return "PASS "+report.rows.Count+" native sprites: PPU32, Point, FullRect, authored ground pivots.";
    }
}
