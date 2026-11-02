using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class ImportChampiCore20261003
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static string Pixels(Color32[] pixels){var bytes=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){bytes[i*4]=pixels[i].r;bytes[i*4+1]=pixels[i].g;bytes[i*4+2]=pixels[i].b;bytes[i*4+3]=pixels[i].a;}return Hash(bytes);}
    static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before source import");
        const string source="ArtDirection/ExternalCandidates/20260922/champi-dungeon/Asset-Tileset.png";
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/ChampiDungeon";
        var bytes=File.ReadAllBytes(source);var sourceHash=Hash(bytes);
        if(sourceHash!="6fecffe24861b8984e3169177c6f86e7df32c8b35ad8f62bc8833d444ab6e295")throw new Exception("Published source identity changed");
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(bytes);
        if(sheet.width!=320||sheet.height!=320)throw new Exception("Source size changed");
        var records=new JArray();var original=sheet.GetPixels32();var factories=new SpriteDataProviderFactories();factories.Init();
        for(int i=0;i<18;i++)
        {
            int x=i<16?(i%4)*16:i==16?224:160,top=i<16?(i/4)*16:i==16?144:0,size=i<16?16:i==16?32:128;
            string name=i<16?"Paving/"+i.ToString("D2"):i==16?"Ground":"Seal";
            var pixels=new Color32[size*size];for(int y=0;y<size;y++)Array.Copy(original,(320-top-size+y)*320+x,pixels,y*size,size);
            if(pixels.Any(p=>p.a!=255))throw new Exception("Floor source must be completely opaque: "+name);
            var path=root+"/"+name+".png";Folder(Path.GetDirectoryName(path).Replace('\\','/'));
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
            if(File.Exists(path)){var old=new Texture2D(2,2,TextureFormat.RGBA32,false);old.LoadImage(File.ReadAllBytes(path));if(!old.GetPixels32().SequenceEqual(pixels))throw new Exception("Preserve different existing pixels: "+path);UnityEngine.Object.DestroyImmediate(old);}
            else File.WriteAllBytes(path,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Importer cannot edit pivot");
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One sprite per PNG required");
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=Vector2.one*.5f;provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(sprite==null||sprite.rect.size!=Vector2.one*size||sprite.pixelsPerUnit!=32||sprite.pivot!=Vector2.one*(size/2)||!imported.GetPixels32().SequenceEqual(pixels))throw new Exception("Native imported geometry or RGBA differs: "+name);
            records.Add(new JObject{{"path",path},{"sourceRectTopLeft",new JArray(x,top,size,size)},{"pixels",pixels.Length},{"rgbaSha256BottomUp",Pixels(pixels)},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true},{"ppu",32}});
        }
        UnityEngine.Object.DestroyImmediate(sheet);AssetDatabase.SaveAssets();
        File.WriteAllText("ArtDirection/Review-20261003/core-floor/native-import.json",new JObject{{"result","PASS"},{"source",source},{"sourceSha256",sourceHash},{"sourcePage","https://champi-s.itch.io/dungeon-pixel-art-x32"},{"license","CC0 / official page checked 2026-10-03"},{"method","Exact native rectangular cells; separate PNGs, Point/uncompressed/no mips/full rect, centered even-pixel pivots, 32PPU; no raster repainting or resampling"},{"images",records}}.ToString(),new UTF8Encoding(false));
        return new {images=records.Count,sourcePixelsUnchanged=true};
    }
}
