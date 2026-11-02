using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

// The four full source cells were previously cut into six 38px fragments.
public static class CorrectKatanaSlash20261001
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before source correction");
        const string source="ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/FX/Slash/SpriteSheetSlash03.png";
        const string active="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/NinjaAdventure/Slash/Slash03";
        const string retired="Assets/RuinRunner/ArtReview/IncorrectKatanaSlices20261001";
        var sheet=new Texture2D(2,2,TextureFormat.RGBA32,false);sheet.LoadImage(File.ReadAllBytes(source));
        if(sheet.width!=228||sheet.height!=42)throw new Exception("Original four-cell source geometry changed");
        var sourcePixels=sheet.GetPixels32();const int width=57,height=42,count=4;
        if(!AssetDatabase.IsValidFolder(retired))
        {
            if(AssetDatabase.FindAssets("t:Sprite",new[]{active}).Length!=6)throw new Exception("Preserve and inspect unexpected current source");
            var error=AssetDatabase.MoveAsset(active,retired);if(!string.IsNullOrEmpty(error))throw new Exception(error);
            AssetDatabase.CreateFolder(Path.GetDirectoryName(active).Replace('\\','/'),"Slash03");
        }
        if(AssetDatabase.FindAssets("t:Sprite",new[]{retired}).Length!=6)throw new Exception("Six rejected fragments must remain with their GUIDs");
        var records=new JArray();var unique=new System.Collections.Generic.HashSet<string>();
        var factories=new SpriteDataProviderFactories();factories.Init();
        for(int frame=0;frame<count;frame++)
        {
            var pixels=new Color32[width*height];for(int y=0;y<height;y++)Array.Copy(sourcePixels,y*sheet.width+frame*width,pixels,y*width,width);
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
            string rgba=Hash(texture.GetRawTextureData<byte>().ToArray());if(!unique.Add(rgba))throw new Exception("Repeated original source cell");
            var path=active+"/"+frame.ToString("D2")+".png";
            if(File.Exists(path))
            {
                var previous=new Texture2D(2,2,TextureFormat.RGBA32,false);previous.LoadImage(File.ReadAllBytes(path));
                if(previous.width!=width||previous.height!=height||!previous.GetPixels32().SequenceEqual(pixels))throw new Exception("Existing native cell differs; preserve before another change");
                UnityEngine.Object.DestroyImmediate(previous);
            }
            else File.WriteAllBytes(path,texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=width;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;importer.isReadable=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
            if(capability==null||!capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Sprite pivot edit capability required");
            var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One complete native sprite required");
            rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=new Vector2(.5f,.5f);provider.SetSpriteRects(rects);provider.Apply();importer.SaveAndReimport();
            var imported=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(imported==null||imported.rect.size!=new Vector2(width,height)||imported.pivot!=new Vector2(width*.5f,height*.5f)||imported.pixelsPerUnit!=width)throw new Exception("Native sprite geometry mismatch");
            var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false);decoded.LoadImage(File.ReadAllBytes(path));
            if(!decoded.GetPixels32().SequenceEqual(pixels))throw new Exception("Exported source pixels changed");
            records.Add(new JObject{{"frame",frame},{"path",path},{"sourceRect",new JArray(frame*width,0,width,height)},{"nativeSize",new JArray(width,height)},{"pivot",new JArray(width*.5f,height*.5f)},{"ppu",width},{"rgbaSha256",rgba},{"fileSha256",Hash(File.ReadAllBytes(path))},{"unchangedSourcePixels",true}});
            UnityEngine.Object.DestroyImmediate(decoded);UnityEngine.Object.DestroyImmediate(texture);
        }
        UnityEngine.Object.DestroyImmediate(sheet);AssetDatabase.SaveAssets();
        if(AssetDatabase.FindAssets("t:Sprite",new[]{active}).Length!=count)throw new Exception("Only four complete cells may be active");
        var manifest=new JObject{{"source",source},{"sourceSha256",Hash(File.ReadAllBytes(source))},{"sourcePage","https://pixel-boy.itch.io/ninja-adventure-asset-pack"},{"license","CC0-1.0"},{"method","Four exact 57x42 source cells. Unity lossless RGBA crop only; no resize, repaint, generated poses or interpolation."},{"retiredSixFragments",retired},{"images",records}};
        File.WriteAllText("ArtDirection/Review-20261001/melee-fx-binding/katana-native-import.json",manifest.ToString(),new UTF8Encoding(false));
        return new {activeSource=active,completeNativeCells=count,retiredSixFragments=retired,sourcePixelsUnchanged=true};
    }
}
