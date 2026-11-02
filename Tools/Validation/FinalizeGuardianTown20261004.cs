using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class FinalizeGuardianTown20261004
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Must import out of Play.");
        const string town="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/TownQuesters";
        var paths=Directory.GetFiles(town,"*.png",SearchOption.AllDirectories);
        AssetDatabase.Refresh();AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var raw in paths)
            {
                string path=raw.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
                importer.alphaIsTransparency=true;importer.isReadable=true;importer.npotScale=TextureImporterNPOTScale.None;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var raw in paths)
            {
                string path=raw.Replace('\\','/');var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                var factory=new SpriteDataProviderFactories();factory.Init();
                var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
                if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                    throw new Exception("Pivot capability unavailable: "+path);
                var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected native cell "+path);
                rects[0].alignment=SpriteAlignment.Custom;
                rects[0].pivot=path.EndsWith("/anvil.png")?new Vector2(.5f,0):
                    path.Contains("/smith/")?new Vector2(.5f,19f/64):new Vector2(.5f,.25f);
                provider.SetSpriteRects(rects);provider.Apply();AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        var rows=new JArray();
        foreach(var root in new[]{town,"Assets/RuinRunner/Resources/Art/Pixel/Separated/External/10k/SealGuardian"})
        foreach(var raw in Directory.GetFiles(root,"*.png",SearchOption.AllDirectories))
        {
            var path=raw.Replace('\\','/');var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var pivot=path.Contains("SealGuardian")?new Vector2(.5f,7f/64):path.EndsWith("/anvil.png")?new Vector2(.5f,0):
                path.Contains("/smith/")?new Vector2(.5f,19f/64):new Vector2(.5f,.25f);
            if(sprite==null||sprite.pixelsPerUnit!=32||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||
                Vector2.Distance(sprite.pivot,Vector2.Scale(pivot,sprite.rect.size))>.001f)
                throw new Exception("Import verification failed: "+path+" sprite="+sprite+" pivot="+(sprite==null?Vector2.zero:sprite.pivot));
            rows.Add(new JObject{{"path",path},{"ppu",sprite.pixelsPerUnit},{"width",sprite.rect.width},{"height",sprite.rect.height},
                {"pivot",new JArray(sprite.pivot.x,sprite.pivot.y)}});
        }
        File.WriteAllText("ArtDirection/Review-20261004/seal-guardian/import-result.json",
            new JObject{{"status","PASS"},{"method","Importer/data provider; batched Town imports; existing guardian verified"},{"rows",rows}}.ToString(),new UTF8Encoding(false));
        return "PASS "+rows.Count+" native guardian/town sprites.";
    }
}
