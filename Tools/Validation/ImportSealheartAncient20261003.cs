using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class ImportSealheartAncient20261003
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static void Folder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before native asset import");
        var records=new JArray();var factories=new SpriteDataProviderFactories();factories.Init();
        const string sourceRoot="ArtDirection/ExternalCandidates/20261003/foozle-ancient/original/Foozle_2DC0020_Lucifer_Skeleton_Ancient_Pixel_Art/";
        var clips=new[]{new {name="Idle",count=7},new {name="Walk",count=8},new {name="Hurt",count=4},new {name="Death",count=9},new {name="Attack02",count=13},new {name="Attack03",count=7}};
        var sources=(from direction in new[]{"Down","Up","Left","Right"} from clip in clips
            select new {id=clip.name.ToLowerInvariant()+"/"+direction.ToLowerInvariant(),
                source=sourceRoot+direction+"/Png/AncientSkeleton"+direction+(direction=="Left"&&clip.name=="Idle"?"t":"")+clip.name+".png",
                count=clip.count,width=80,height=80,pivot=new Vector2(40,24),page="https://foozlecc.itch.io/lucifer-skeleton-ancient-boss"}).ToArray();
        foreach(var source in sources)
        {
            string folder="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Foozle/Ancient/"+source.id;Folder(folder);
            var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source.source));
            if(sheet.width!=source.width*source.count||sheet.height!=source.height)throw new Exception("Unexpected native geometry "+source.source);
            var original=sheet.GetPixels32();var uniqueVisible=new System.Collections.Generic.HashSet<string>();
            for(int frame=0;frame<source.count;frame++)
            {
                var pixels=new Color32[source.width*source.height];for(int y=0;y<source.height;y++)Array.Copy(original,y*sheet.width+frame*source.width,pixels,y*source.width,source.width);
                var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
                string rgba=Hash(texture.GetRawTextureData<byte>().ToArray());bool visible=pixels.Any(p=>p.a!=0);if(visible)uniqueVisible.Add(rgba);
                string path=folder+"/"+frame.ToString("D2")+".png";
                if(File.Exists(path))
                {var previous=new Texture2D(2,2,TextureFormat.RGBA32,false);previous.LoadImage(File.ReadAllBytes(path));if(previous.width!=source.width||previous.height!=source.height||!previous.GetPixels32().SequenceEqual(pixels))throw new Exception("Preserve unexpected existing pixels "+path);UnityEngine.Object.DestroyImmediate(previous);}
                else File.WriteAllBytes(path,texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=false;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("EditPivot capability required");
                var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One exact full native cell required");
                rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2(source.pivot.x/source.width,source.pivot.y/source.height);provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(imported==null||imported.rect.size!=new Vector2(source.width,source.height)||imported.pivot!=source.pivot||imported.pixelsPerUnit!=32)throw new Exception("Native import geometry differs");
                var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);decoded.LoadImage(File.ReadAllBytes(path));if(!decoded.GetPixels32().SequenceEqual(pixels))throw new Exception("Native RGBA changed");
                records.Add(new JObject{{"clip",source.id},{"frame",frame},{"source",source.source},{"sourcePage",source.page},{"sourceSha256",Hash(File.ReadAllBytes(source.source))},{"sourceRect",new JArray(frame*source.width,0,source.width,source.height)},{"path",path},{"pivot",new JArray(source.pivot.x,source.pivot.y)},{"ppu",32},{"visible",visible},{"rgbaSha256",rgba},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true}});
                UnityEngine.Object.DestroyImmediate(decoded);UnityEngine.Object.DestroyImmediate(texture);
            }
            // Record authored holds honestly; clip file count is not its unique pose count.
            if(AssetDatabase.FindAssets("t:Sprite",new[]{folder}).Length!=source.count)throw new Exception("Unexpected extra native sprites");
            UnityEngine.Object.DestroyImmediate(sheet);
        }
        AssetDatabase.SaveAssets();Directory.CreateDirectory("ArtDirection/Review-20261003/sealheart-ancient");
        File.WriteAllText("ArtDirection/Review-20261003/sealheart-ancient/native-import.json",new JObject{{"method","Exact original RGBA32 native cells, no resizing, repainting, interpolation or fake new poses. Four-direction Ancient guardian idle, walk, recoil, two attacks and collapse."},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {nativeCells=records.Count,sourcePixelsUnchanged=true};
    }
}
