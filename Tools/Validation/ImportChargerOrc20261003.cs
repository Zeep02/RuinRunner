using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

// Exact artist cells only; never redraw, resize, interpolate or synthesize poses.
public static class ImportChargerOrc20261003
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before native import");
        var records=new JArray();var audit=new JArray();var factories=new SpriteDataProviderFactories();factories.Init();
        string[] clips={"idle","run","attack","run_attack","hurt","death"};int[] counts={4,8,8,8,6,8};
        string[] directions={"down","up","left","right"};var pivot=new Vector2(32,24);
        for(int clipIndex=0;clipIndex<clips.Length;clipIndex++)
        {
            string clip=clips[clipIndex],source="ArtDirection/ExternalCandidates/20261003/craftpix-orcs/original/PNG/Orc3/orc3_"+clip+"/orc3_"+clip+"_full.png";
            var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source));int count=counts[clipIndex];
            if(sheet.width!=count*64||sheet.height!=256)throw new Exception("Unexpected source geometry: "+source);
            var original=sheet.GetPixels32();string sourceSha=Hash(File.ReadAllBytes(source));
            for(int row=0;row<4;row++)
            {
                var unique=new HashSet<string>();string folder="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/CraftPix/Orc3/"+clip+"/"+directions[row];Folder(folder);
                for(int frame=0;frame<count;frame++)
                {
                    var pixels=new Color32[64*64];int bottom=(3-row)*64;
                    for(int y=0;y<64;y++)Array.Copy(original,(bottom+y)*sheet.width+frame*64,pixels,y*64,64);
                    var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
                    string rgba=Hash(texture.GetRawTextureData<byte>().ToArray());unique.Add(rgba);if(!pixels.Any(p=>p.a==255))throw new Exception("Blank authored pose");
                    string path=folder+"/"+frame.ToString("D2")+".png";
                    if(File.Exists(path))
                    {var old=new Texture2D(2,2,TextureFormat.RGBA32,false);old.LoadImage(File.ReadAllBytes(path));if(!old.GetPixels32().SequenceEqual(pixels))throw new Exception("Preserve different existing asset: "+path);UnityEngine.Object.DestroyImmediate(old);}
                    else File.WriteAllBytes(path,texture.EncodeToPNG());
                    AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=true;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                    var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                    var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("EditPivot unsupported");
                    var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One native cell required");
                    rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivot/64;provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
                    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite==null||sprite.rect.size!=new Vector2(64,64)||sprite.pivot!=pivot||sprite.pixelsPerUnit!=32)throw new Exception("Native import geometry differs");
                    records.Add(new JObject{{"clip",clip},{"direction",directions[row]},{"frame",frame},{"source",source},{"sourceSha256",sourceSha},{"sourceRect",new JArray(frame*64,row*64,64,64)},{"path",path},{"pivot",new JArray(32,24)},{"ppu",32},{"rgbaSha256BottomUp",rgba},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true}});
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                if(unique.Count!=count)throw new Exception("Duplicated source frames in "+clip+"/"+directions[row]);
                audit.Add(new JObject{{"clip",clip},{"direction",directions[row]},{"files",count},{"uniqueVisible",unique.Count}});
            }
            UnityEngine.Object.DestroyImmediate(sheet);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20261003/boar-motion/native-orc-import.json",new JObject{{"sourcePage","https://free-game-assets.itch.io/free-top-down-orc-game-character-pixel-art"},{"method","Exact source RGBA cells, common canvas/pivot/PPU, no resizing or generated poses"},{"clips",audit},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {nativeCells=records.Count,clips=audit.Count,sourcePixelsUnchanged=true};
    }
}
