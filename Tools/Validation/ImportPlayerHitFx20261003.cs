using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class ImportPlayerHitFx20261003
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before native asset import");
        var records=new JArray();var factories=new SpriteDataProviderFactories();factories.Init();
        var sources=new[]{new {id="PlayerHit",source="ArtDirection/ExternalCandidates/20261001/icemaan-vfx/original/Spritesheets/Hit Sparks/Hit Spark.png",count=5,pivot=new Vector2(16,16),page="https://icemaan.itch.io/pixel-vfx-essentials"}};
        foreach(var source in sources)
        {
            string folder="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/"+source.id;Folder(folder);
            var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source.source));
            if(sheet.width!=32*source.count||sheet.height!=32)throw new Exception("Unexpected native geometry "+source.source);
            var original=sheet.GetPixels32();var uniqueVisible=new System.Collections.Generic.HashSet<string>();
            for(int frame=0;frame<source.count;frame++)
            {
                var pixels=new Color32[1024];for(int y=0;y<32;y++)Array.Copy(original,y*sheet.width+frame*32,pixels,y*32,32);
                var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
                string rgba=Hash(texture.GetRawTextureData<byte>().ToArray());bool visible=pixels.Any(p=>p.a!=0);if(visible)uniqueVisible.Add(rgba);
                string path=folder+"/"+frame.ToString("D2")+".png";
                if(File.Exists(path))
                {var previous=new Texture2D(2,2,TextureFormat.RGBA32,false);previous.LoadImage(File.ReadAllBytes(path));if(previous.width!=32||previous.height!=32||!previous.GetPixels32().SequenceEqual(pixels))throw new Exception("Preserve unexpected existing pixels "+path);UnityEngine.Object.DestroyImmediate(previous);}
                else File.WriteAllBytes(path,texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=false;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("EditPivot capability required");
                var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One exact full native cell required");
                rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=source.pivot/32f;provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(imported==null||imported.rect.size!=new Vector2(32,32)||imported.pivot!=source.pivot||imported.pixelsPerUnit!=32)throw new Exception("Native import geometry differs");
                var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);decoded.LoadImage(File.ReadAllBytes(path));if(!decoded.GetPixels32().SequenceEqual(pixels))throw new Exception("Native RGBA changed");
                records.Add(new JObject{{"clip",source.id},{"frame",frame},{"source",source.source},{"sourcePage",source.page},{"sourceSha256",Hash(File.ReadAllBytes(source.source))},{"sourceRect",new JArray(frame*32,0,32,32)},{"path",path},{"pivot",new JArray(source.pivot.x,source.pivot.y)},{"ppu",32},{"visible",visible},{"rgbaSha256",rgba},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true}});
                UnityEngine.Object.DestroyImmediate(decoded);UnityEngine.Object.DestroyImmediate(texture);
            }
            int expected=5;if(uniqueVisible.Count!=expected)throw new Exception("Unexpected authored visible poses "+source.id);
            if(AssetDatabase.FindAssets("t:Sprite",new[]{folder}).Length!=source.count)throw new Exception("Unexpected extra native sprites");
            UnityEngine.Object.DestroyImmediate(sheet);
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory("ArtDirection/Review-20261003/hero-hurt");
        File.WriteAllText("ArtDirection/Review-20261003/hero-hurt/native-import.json",new JObject{{"method","Exact original RGBA32 native cells, no resizing, repainting, interpolation or fake new poses. Player contact spark has five genuinely distinct source frames."},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {nativeCells=5,uniqueVisiblePoses=5,sourcePixelsUnchanged=true};
    }
}
