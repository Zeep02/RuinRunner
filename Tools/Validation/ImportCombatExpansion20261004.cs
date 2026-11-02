using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;

public static class ImportCombatExpansion20261004
{
    const string Review="ArtDirection/Review-20261004/combat-expansion/";
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before import");
        var rows=JArray.Parse(File.ReadAllText(Review+"native-selection.json",Encoding.UTF8));
        foreach(JObject row in rows)if(Hash((string)row["source"])!=(string)row["sourceSha256"]||Hash((string)row["target"])!=(string)row["sourceSha256"])throw new Exception("Source changed: "+row["id"]);
        var factories=new SpriteDataProviderFactories();factories.Init();
        AssetDatabase.Refresh();AssetDatabase.StartAssetEditing();
        try{foreach(JObject row in rows)
        {
            var path=(string)row["target"];
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
            imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;imp.alphaIsTransparency=true;imp.wrapMode=TextureWrapMode.Clamp;imp.isReadable=true;
            var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
            AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }}finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.StartAssetEditing();
        try{foreach(JObject row in rows)
        {
            var path=(string)row["target"];var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            var data=factories.GetSpriteEditorDataProviderFromObject(imp);data.InitSpriteEditorDataProvider();var rects=data.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected single sprite");
            var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot capability unavailable: "+path);
            var grip=(JArray)row["grip"];rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2((float)grip[0]/(int)row["width"],1-(float)grip[1]/(int)row["height"]);
            data.SetSpriteRects(rects);data.Apply();AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        }}finally{AssetDatabase.StopAssetEditing();}
        foreach(JObject row in rows)
        {
            var path=(string)row["target"];
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.rect.width!=(int)row["width"]||sprite.rect.height!=(int)row["height"]||sprite.pixelsPerUnit!=32||Hash(path)!=(string)row["sourceSha256"])throw new Exception("Import mismatch "+row["id"]);
            row["guid"]=AssetDatabase.AssetPathToGUID(path);row["originalPixelsUnchanged"]=true;
        }
        AssetDatabase.SaveAssets();File.WriteAllText(Review+"native-import-result.json",rows.ToString(),new UTF8Encoding(false));return "PASS 10 native originals, no pixel edits";
    }
}
