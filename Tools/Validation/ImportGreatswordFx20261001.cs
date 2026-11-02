using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Newtonsoft.Json.Linq;

public static class ImportGreatswordFx20261001
{
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before import");
        const string source="ArtDirection/ExternalCandidates/20261001/frostwindz-slashes/original/128x128/";
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/";
        // Slash2/color4 was rejected by the user. Never reintroduce that wedge
        // through a future source import; its original metadata remains in ArtReview.
        var clips=new[]{"GreatswordCut"};var slashes=new[]{3};var colors=new[]{5};var counts=new[]{9};
        var pivots=new[]{new Vector2(64,64)};var records=new JArray();
        var factories=new SpriteDataProviderFactories();factories.Init();
        for(int c=0;c<clips.Length;c++)
        {
            var dest=root+clips[c]+"/";
            if(Directory.Exists(dest))throw new Exception("Preserve existing effect before another import: "+dest);
            var paths=Directory.GetFiles(source+"Slash "+slashes[c]+"/color"+colors[c],"*.png",SearchOption.AllDirectories)
                .Where(p=>Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains("frame"))
                .OrderBy(p=>int.Parse(Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Split(new[]{"frame"},StringSplitOptions.None).Last())).ToArray();
            if(paths.Length!=counts[c])throw new Exception("Source frame count "+clips[c]);
            var unique=new System.Collections.Generic.HashSet<string>();Directory.CreateDirectory(dest);
            for(int f=0;f<paths.Length;f++)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(paths[f]));
                if(texture.width!=128||texture.height!=128)throw new Exception("Native 128px source required");
                var pixels=texture.GetPixels32();int opaque=0,minX=128,minY=128,maxX=-1,maxY=-1;
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)if(pixels[y*128+x].a>0){opaque++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                string rgba=Hash(texture.GetRawTextureData<byte>().ToArray());
                if(opaque==0||!unique.Add(rgba))throw new Exception("Empty/repeated source pose "+paths[f]);
                UnityEngine.Object.DestroyImmediate(texture);
                var path=dest+f.ToString("D2")+".png";File.Copy(paths[f],path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.spritePixelsPerUnit=32;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;
                imp.npotScale=TextureImporterNPOTScale.None;imp.wrapMode=TextureWrapMode.Clamp;imp.alphaIsTransparency=true;imp.isReadable=false;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);imp.SaveAndReimport();
                var provider=factories.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();
                var cap=provider.GetDataProvider<ISpriteFrameEditCapability>();
                if(cap==null||!cap.GetEditCapability().HasCapability(EEditCapability.EditPivot))throw new Exception("Pivot edit capability required "+path);
                var rects=provider.GetSpriteRects();if(rects.Length!=1)throw new Exception("One native sprite required "+path);
                rects[0].alignment=SpriteAlignment.Custom;rects[0].pivot=pivots[c]/128;provider.SetSpriteRects(rects);provider.Apply();imp.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(sprite==null||sprite.rect.size!=new Vector2(128,128)||sprite.pivot!=pivots[c]||sprite.pixelsPerUnit!=32)throw new Exception("Imported sprite geometry "+path);
                string sourceHash=Hash(File.ReadAllBytes(paths[f])),runtimeHash=Hash(File.ReadAllBytes(path));
                if(sourceHash!=runtimeHash)throw new Exception("Copied source differs "+path);
                records.Add(new JObject{{"clip",clips[c]},{"frame",f},{"source",paths[f].Replace('\\','/')},{"path",path},{"fileSha256",sourceHash},{"rgbaSha256",rgba},{"nativeSize",new JArray(128,128)},{"ppu",32},{"pivot",new JArray(pivots[c].x,pivots[c].y)},{"visibleBounds",new JArray(minX,minY,maxX+1,maxY+1)},{"visiblePixels",opaque}});
            }
        }
        File.WriteAllText("ArtDirection/Review-20261001/weapon-identity-more/native-import.json",new JObject{{"source","https://frostwindz.itch.io/pixel-art-slashes"},{"archive","Free official download; bundled license permits commercial games/modification, prohibits standalone redistribution/resale/AI training."},{"method","Exact copies of original 128x128 separate PNGs, not our resized 64px version. No repaint/resampling/recolor/generated raster. SpriteEditor pivot only."},{"images",records}}.ToString(),new UTF8Encoding(false));
        AssetDatabase.SaveAssets();return "Nine distinct overhead source poses imported; rejected branching wedge excluded; original PNG hashes preserved.";
    }
}
