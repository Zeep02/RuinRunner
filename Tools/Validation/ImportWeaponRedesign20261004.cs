using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Imports selected licensed originals byte-for-byte; never resamples their pixels.
public static class ImportWeaponRedesign20261004
{
    const string Review="ArtDirection/Review-20261004/weapon-redesign/";
    const string Target="Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon/";
    static string Hash(string path) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before importing weapons.");
        var entries=JArray.Parse(File.ReadAllText(Review+"accepted-selection.json",Encoding.UTF8));
        var originals=new JObject();
        Directory.CreateDirectory(Review+"before");
        // Validate the entire manifest before changing any project asset.
        foreach(JObject row in entries)
        {
            var id=(string)row["id"];var from=(string)row["source"];
            if(string.IsNullOrEmpty(id)||id.IndexOfAny(new[]{'/','\\','.'})>=0)throw new Exception("Invalid weapon ID");
            if(Hash(from)!=(string)row["sourceSha256"])throw new Exception("Changed source: "+from);
            var size=(JArray)row["size"];var grip=(JArray)row["grip"];
            if((float)grip[0]<0||(float)grip[0]>=(int)size[0]||(float)grip[1]<0||(float)grip[1]>=(int)size[1])throw new Exception("Grip out of bounds: "+id);
            var path=Target+id+".png";originals[id]=AssetDatabase.AssetPathToGUID(path);
            foreach(var suffix in new[]{".png",".png.meta"})
                if(File.Exists(Target+id+suffix)&&!File.Exists(Review+"before/"+id+suffix))File.Copy(Target+id+suffix,Review+"before/"+id+suffix);
        }
        AssetDatabase.StartAssetEditing();
        try {foreach(JObject row in entries){var path=Target+(string)row["id"]+".png";File.Copy((string)row["source"],path,true);AssetDatabase.ImportAsset(path);}}
        finally {AssetDatabase.StopAssetEditing();}
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(JObject row in entries)
            {
                var path=Target+(string)row["id"]+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.textureCompression=TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;imp.alphaIsTransparency=true;
                imp.wrapMode=TextureWrapMode.Clamp;imp.isReadable=true;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);
                AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);
            }
        }
        finally {AssetDatabase.StopAssetEditing();}
        var factories=new SpriteDataProviderFactories();factories.Init();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(JObject row in entries)
            {
                var path=Target+(string)row["id"]+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                var data=factories.GetSpriteEditorDataProviderFromObject(imp);data.InitSpriteEditorDataProvider();
                var capability=data.GetDataProvider<ISpriteFrameEditCapability>();
                if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot capability unavailable: "+path);
                var rects=data.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected one sprite: "+path);
                var size=(JArray)row["size"];var grip=(JArray)row["grip"];
                rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2((float)grip[0]/(int)size[0],1-(float)grip[1]/(int)size[1]);
                data.SetSpriteRects(rects);data.Apply();AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);
            }
        }
        finally {AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();var records=new JArray();
        foreach(JObject row in entries)
        {
            var id=(string)row["id"];var path=Target+id+".png";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var size=(JArray)row["size"];var grip=(JArray)row["grip"];var guid=AssetDatabase.AssetPathToGUID(path);
            if(sprite==null||sprite.pixelsPerUnit!=32||sprite.rect.width!=(int)size[0]||sprite.rect.height!=(int)size[1]||
                Vector2.Distance(sprite.pivot,new Vector2((float)grip[0],(int)size[1]-(float)grip[1]))>.001f)throw new Exception("Imported sprite geometry mismatch: "+id);
            if(!string.IsNullOrEmpty((string)originals[id])&&(string)originals[id]!=guid)throw new Exception("Existing GUID changed: "+id);
            if(Hash(path)!=(string)row["sourceSha256"])throw new Exception("Source pixels were modified: "+id);
            records.Add(new JObject{{"id",id},{"path",path},{"guid",guid},{"source",row["source"]},{"sha256",Hash(path)},{"size",size.DeepClone()},{"grip",grip.DeepClone()},{"sourcePixelsUnchanged",true}});
        }
        File.WriteAllText(Review+"import-result.json",new JObject{{"result","PASS"},{"utc",DateTime.UtcNow.ToString("O")},{"weapons",records}}.ToString(),new UTF8Encoding(false));
        return "Imported "+entries.Count+" original native weapon sprites; pixels, dimensions, grips and existing GUIDs verified.";
    }
}
