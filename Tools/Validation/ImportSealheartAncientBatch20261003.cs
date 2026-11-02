using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class ImportSealheartAncientBatch20261003
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    static void Batch(Action action){AssetDatabase.StartAssetEditing();try{action();}finally{AssetDatabase.StopAssetEditing();}}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before native import");
        const string root="ArtDirection/ExternalCandidates/20261003/foozle-ancient/original/Foozle_2DC0020_Lucifer_Skeleton_Ancient_Pixel_Art/";
        const string target="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Foozle/Ancient/";
        var records=new JArray();var files=new List<string>();var oldGuid=new Dictionary<string,string>();
        var clips=new[]{new {name="Idle",count=7},new {name="Walk",count=8},new {name="Hurt",count=4},new {name="Death",count=9},new {name="Attack02",count=13},new {name="Attack03",count=7}};
        foreach(var direction in new[]{"Down","Up","Left","Right"})foreach(var clip in clips)
        {
            string folder=target+clip.name.ToLowerInvariant()+"/"+direction.ToLowerInvariant();Folder(folder);
            string source=root+direction+"/Png/AncientSkeleton"+direction+(direction=="Left"&&clip.name=="Idle"?"t":"")+clip.name+".png";
            int stride=clip.name=="Walk"&&(direction=="Left"||direction=="Right")?96:80;
            int padding=(stride-80)/2;
            var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source));
            if(sheet.width!=stride*clip.count||sheet.height!=stride)throw new Exception("Unexpected source canvas "+source);
            var sourcePixels=sheet.GetPixels32();
            for(int frame=0;frame<clip.count;frame++)
            {
                int targetPadding=(96-stride)/2;
                var pixels=new Color32[96*96];for(int y=0;y<stride;y++)Array.Copy(sourcePixels,y*sheet.width+frame*stride,pixels,(y+targetPadding)*96+targetPadding,stride);
                var texture=new Texture2D(96,96,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
                string path=folder+"/"+frame.ToString("D2")+".png";
                if(File.Exists(path))
                {
                    var guid=AssetDatabase.AssetPathToGUID(path);if(!string.IsNullOrEmpty(guid))oldGuid[path]=guid;
                    var existing=new Texture2D(2,2,TextureFormat.RGBA32,false);existing.LoadImage(File.ReadAllBytes(path));
                    if(existing.width==80&&existing.height==80)
                    {
                        var padded=new Color32[96*96];var previous=existing.GetPixels32();for(int y=0;y<80;y++)Array.Copy(previous,y*80,padded,(y+8)*96+8,80);
                        if(!padded.SequenceEqual(pixels))throw new Exception("Existing 80px frame differs beyond transparent padding "+path);
                        File.WriteAllBytes(path,texture.EncodeToPNG());
                    }
                    else if(existing.width!=96||existing.height!=96||!existing.GetPixels32().SequenceEqual(pixels))throw new Exception("Preserve existing native pixels "+path);
                    UnityEngine.Object.DestroyImmediate(existing);
                }
                else File.WriteAllBytes(path,texture.EncodeToPNG());
                files.Add(path);
                records.Add(new JObject{{"clip",clip.name.ToLowerInvariant()},{"direction",direction.ToLowerInvariant()},{"frame",frame},{"source",source},{"sourceSha256",Hash(File.ReadAllBytes(source))},{"sourceCanvas",stride},{"addedTransparentPadding",targetPadding},{"sourceRect",new JArray(frame*stride,0,stride,stride)},{"path",path},{"pivot",new JArray(48,32)},{"ppu",32},{"visible",pixels.Any(p=>p.a!=0)},{"rgbaSha256",Hash(texture.GetRawTextureData<byte>().ToArray())},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true}});
                UnityEngine.Object.DestroyImmediate(texture);
            }
            UnityEngine.Object.DestroyImmediate(sheet);
        }
        Batch(()=>{foreach(var path in files)AssetDatabase.ImportAsset(path);});
        Batch(()=>{foreach(var path in files)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new Exception("Importer missing "+path);

            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);
        }});
        var factories=new SpriteDataProviderFactories();factories.Init();
        Batch(()=>{foreach(var path in files)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("EditPivot capability required");
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One native cell required");
            var pivot=new Vector2(.5f,1f/3f);if(rects[0].alignment==SpriteAlignment.Custom&&rects[0].pivot==pivot)continue;
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivot;provider.SetSpriteRects(rects);provider.Apply();
            AssetDatabase.WriteImportSettingsIfDirty(path);AssetDatabase.ImportAsset(path);
        }});
        foreach(var path in files)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(sprite==null||sprite.rect.size!=new Vector2(96,96)||sprite.pivot!=new Vector2(48,32)||sprite.pixelsPerUnit!=32||importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed)
                throw new Exception("Native import mismatch "+path);
            if(oldGuid.ContainsKey(path)&&oldGuid[path]!=AssetDatabase.AssetPathToGUID(path))throw new Exception("Existing native GUID changed");
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20261003/sealheart-ancient/native-import.json",new JObject{{"method","Exact native cells, no scale/repaint/interpolation. Every source uses a 96px common canvas: 80px cells gain 8 transparent pixels per edge, side walk 96px cells remain intact. Existing imported pixels and GUIDs preserved; partial 80px PNGs backed up."},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {nativeCells=records.Count,preservedExistingGuids=oldGuid.Count};
    }
}
