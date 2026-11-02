using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using RuinRunner;
using RuinRunner.EditorTools;
using Newtonsoft.Json.Linq;

public static class ImportFirearmReload20261003
{
    const string Review="ArtDirection/Review-20261003/firearm-reload/";
    const string Root="Assets/RuinRunner/ArtSources/Hero/WeaponReloadV3";
    static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before import");
        if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Expected existing Built-in renderer");
        if(Directory.Exists(Root))throw new Exception("Preserve existing V3; inspect running operation/progress before any retry");
        var data=JObject.Parse(File.ReadAllText(Review+"source-map.json",Encoding.UTF8));
        NativeHeroAtlasPrebuild.EnsureFolder(Root);int count=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(JObject row in data["sources"])
            {
                string source=row.Value<string>("source"),destination=row.Value<string>("destination");
                if(Hash(source)!=row.Value<string>("pngSha256")||Hash(source+".meta")!=row.Value<string>("sourceMetaSha256"))throw new Exception("Source changed before adoption "+source);
                if(!AssetDatabase.CopyAsset(source,destination))throw new Exception("Unity copy failed "+destination);
                if(++count%250==0)File.WriteAllText(Review+"import-progress.json",new JObject{{"phase","copying"},{"copied",count},{"total",data["sources"].Count()}}.ToString(),new UTF8Encoding(false));
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        var factories=new SpriteDataProviderFactories();factories.Init();var guids=new JArray();
        foreach(JObject row in data["sources"])
        {
            string source=row.Value<string>("source"),path=row.Value<string>("destination");
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var rects=provider.GetSpriteRects();
            if(sprite==null||sprite.rect!=new Rect(0,0,48,64)||sprite.pivot!=new Vector2(24,22)||sprite.pixelsPerUnit!=32||
                importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed||
                settings.spriteMeshType!=SpriteMeshType.FullRect||rects.Length!=1||rects[0].pivot!=new Vector2(.5f,22/64f))throw new Exception("Copied importer geometry/settings invalid "+path);
            if(Hash(path)!=row.Value<string>("pngSha256")||Hash(source)!=row.Value<string>("pngSha256")||Hash(source+".meta")!=row.Value<string>("sourceMetaSha256"))throw new Exception("Source/native pixels changed "+path);
            string guid=AssetDatabase.AssetPathToGUID(path);if(guid==AssetDatabase.AssetPathToGUID(source))throw new Exception("Copy did not receive separate GUID");
            guids.Add(new JObject{{"path",path},{"guid",guid},{"source",source},{"sourceGuid",AssetDatabase.AssetPathToGUID(source)}});
        }
        AssetDatabase.SaveAssets();
        var proof=new JObject{{"result","PASS"},{"sources",count},{"sampleSlots",data["frames"].Count()},{"bowSamples",0},{"pixelsModified",0},{"sourceMetadataModified",0},{"guids",guids}};
        File.WriteAllText(Review+"import.json",proof.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Review+"import-progress.json",new JObject{{"phase","complete"},{"copied",count}}.ToString(),new UTF8Encoding(false));
        return count+" native V2 images copied with separate GUIDs and exact original PNG/source metadata; five firearm families only.";
    }
}
