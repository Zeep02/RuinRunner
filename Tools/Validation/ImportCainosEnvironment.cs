using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json;

public static class ImportCainosEnvironment
{
    [Serializable] public sealed class Entry {public string path;public float[] pivot;public float ppu;}
    [Serializable] public sealed class Manifest {public Entry[] images;}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before import");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return Import("ArtDirection/ExternalCandidates/20260921/cainos-basic/environment-extraction.json")+"; "+Import("ArtDirection/ExternalCandidates/20260921/ninja-adventure/combat-extraction.json");
    }
    static string Import(string manifest)
    {
        var data=JsonConvert.DeserializeObject<Manifest>(File.ReadAllText(manifest));
        if(data==null||data.images==null)throw new Exception("Environment manifest has no images");
        var factories=new SpriteDataProviderFactories();factories.Init();
        foreach(var entry in data.images)
        {
            AssetDatabase.ImportAsset(entry.path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(entry.path);
            if(importer==null)throw new Exception("Texture importer missing: "+entry.path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=entry.ppu;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.isReadable=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot edit unavailable "+entry.path);
            var sprites=provider.GetSpriteRects();if(sprites.Length!=1)throw new Exception("Expected one Sprite "+entry.path);
            sprites[0].alignment=SpriteAlignment.Custom;sprites[0].pivot=new Vector2(entry.pivot[0],entry.pivot[1]);
            provider.SetSpriteRects(sprites);provider.Apply();importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();return "Imported "+data.images.Length+" native32PPU Sprites from "+manifest;
    }
}
