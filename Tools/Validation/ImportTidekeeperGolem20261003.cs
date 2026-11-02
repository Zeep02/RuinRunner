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

// Exact published PNG cells; preserve the full source canvas and common ground anchor.
public static class ImportTidekeeperGolem20261003
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static string PixelHash(Color32[] pixels){var bytes=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){bytes[i*4]=pixels[i].r;bytes[i*4+1]=pixels[i].g;bytes[i*4+2]=pixels[i].b;bytes[i*4+3]=pixels[i].a;}return Hash(bytes);}
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before native import");
        const string source="ArtDirection/ExternalCandidates/20261003/kronovi-golem/original/Mecha-stone Golem 0.1/PNG sheet/Character_sheet.png";
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Kronovi/Golem";
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source));
        if(sheet.width!=1000||sheet.height!=1000)throw new Exception("Unexpected published source size");
        var original=sheet.GetPixels32();var sourceSha=Hash(File.ReadAllBytes(source));
        string[] clips={"idle","glow","immune","laser_cast","shield_cast","death"};int[] rows={0,1,3,5,6,7},counts={4,8,8,7,10,14};
        var records=new JArray();var audit=new JArray();var pivot=new Vector2(49,6);
        for(int c=0;c<clips.Length;c++)
        {
            var folder=root+"/"+clips[c];Folder(folder);var unique=new HashSet<string>();
            for(int f=0;f<counts[c];f++)
            {
                int x=f%10*100,top=(rows[c]+f/10)*100,bottom=1000-top-100;
                var pixels=new Color32[10000];for(int y=0;y<100;y++)Array.Copy(original,(bottom+y)*1000+x,pixels,y*100,100);
                if(!pixels.Any(p=>p.a==255))throw new Exception("Blank native frame "+clips[c]+"/"+f);
                var texture=new Texture2D(100,100,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
                var rgba=PixelHash(pixels);unique.Add(rgba);var path=folder+"/"+f.ToString("D2")+".png";
                if(File.Exists(path))
                {var old=new Texture2D(2,2,TextureFormat.RGBA32,false);old.LoadImage(File.ReadAllBytes(path));if(!old.GetPixels32().SequenceEqual(pixels))throw new Exception("Preserve different existing asset "+path);UnityEngine.Object.DestroyImmediate(old);}
                else File.WriteAllBytes(path,texture.EncodeToPNG());
                records.Add(new JObject{{"clip",clips[c]},{"frame",f},{"source",source},{"sourceSha256",sourceSha},{"sourceRectTopLeft",new JArray(x,top,100,100)},{"path",path},{"pivot",new JArray(49,6)},{"ppu",32},{"rgbaSha256BottomUp",rgba},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true}});
                UnityEngine.Object.DestroyImmediate(texture);
            }
            audit.Add(new JObject{{"clip",clips[c]},{"files",counts[c]},{"uniqueRgba",unique.Count}});
        }
        UnityEngine.Object.DestroyImmediate(sheet);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        // Batch metadata imports; the new textures already exist before providers are queried.
        var factories=new SpriteDataProviderFactories();factories.Init();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var record in records)
            {
                var path=(string)record["path"];var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(existing!=null && existing.rect.size==new Vector2(100,100) && Vector2.Distance(existing.pivot,pivot)<.0001f && existing.pixelsPerUnit==32 && importer.filterMode==FilterMode.Point && !importer.mipmapEnabled && importer.isReadable && importer.textureCompression==TextureImporterCompression.Uncompressed)continue;
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=true;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
                var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot editing unsupported");
                var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("Expected one source cell per PNG");
                rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivot/100;provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            }
        }
        finally {AssetDatabase.StopAssetEditing();}
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var record in records)
        {
            var path=(string)record["path"];var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.rect.size!=new Vector2(100,100)||Vector2.Distance(sprite.pivot,pivot)>.0001f||sprite.pixelsPerUnit!=32)throw new Exception("Native geometry not preserved: "+path);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var png=new Texture2D(2,2,TextureFormat.RGBA32,false);png.LoadImage(File.ReadAllBytes(path));
            if(PixelHash(png.GetPixels32())!=(string)record["rgbaSha256BottomUp"])throw new Exception("Saved PNG differs from source: "+path);
            var expected=png.GetPixels32();var actual=texture.GetPixels32();int transparentRgbChanges=0;
            for(int i=0;i<expected.Length;i++)if(!expected[i].Equals(actual[i]))
            {
                // Unity Alpha Is Transparency dilates invisible RGB. Every alpha
                // and every visible source pixel must still agree exactly.
                if(expected[i].a!=0||actual[i].a!=0)throw new Exception("Visible imported source pixels differ: "+path);
                transparentRgbChanges++;
            }
            record["transparentImporterRgbChanges"]=transparentRgbChanges;
            record["visibleImportedPixelsUnchanged"]=true;UnityEngine.Object.DestroyImmediate(png);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20261003/tidekeeper-motion/native-import.json",new JObject{{"sourcePage","https://darkpixel-kronovi.itch.io/mecha-golem-free"},{"method","Exact published source cells; original holds preserved and reported separately; common 100px canvas/ground pivot; no raster editing or resizing"},{"clips",audit},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {frames=records.Count,clips=audit.Count,sourcePixelsUnchanged=true};
    }
}
