using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Newtonsoft.Json;

public static class ImportSscarySample
{
    [Serializable] public sealed class Entry {public string path;public float[] pivot;public float ppu;}
    [Serializable] public sealed class Manifest {public Entry[] images;}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before import");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var data=JsonConvert.DeserializeObject<Manifest>(File.ReadAllText("ArtDirection/ExternalCandidates/20260922/sscary-adventurer-male/sample-extraction.json"));
        // Batch both phases: importing each PNG individually repacks the entire atlas.
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var entry in data.images)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(entry.path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=entry.ppu;importer.filterMode=FilterMode.Point;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                importer.wrapMode=TextureWrapMode.Clamp;importer.isReadable=true;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                AssetDatabase.WriteImportSettingsIfDirty(entry.path);AssetDatabase.ImportAsset(entry.path);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        var factories=new SpriteDataProviderFactories();factories.Init();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var entry in data.images)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(entry.path);
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
                if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot edit unavailable "+entry.path);
                var sprites=provider.GetSpriteRects();if(sprites.Length!=1)throw new Exception("Expected one Sprite "+entry.path);
                sprites[0].alignment=SpriteAlignment.Custom;sprites[0].pivot=new Vector2(entry.pivot[0],entry.pivot[1]);
                provider.SetSpriteRects(sprites);provider.Apply();
                AssetDatabase.WriteImportSettingsIfDirty(entry.path);AssetDatabase.ImportAsset(entry.path);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();
        foreach(var entry in data.images)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(entry.path);
            if(sprite==null||sprite.pixelsPerUnit!=32||Vector2.Distance(sprite.pivot,new Vector2(24,22))>.001f)throw new Exception("Hero import mismatch "+entry.path);
        }
        return "Imported and verified "+data.images.Length+" Editor review PNGs, 48x64 canvas / 32PPU / fixed foot anchor";
    }
}

