using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using Newtonsoft.Json.Linq;

// Game import of image_gen designs: a documented regular-grid representation,
// not a claim that the generated large image was an exact integer enlargement.
public static class PrepareGeneratedKatanas20261004
{
    const string Root="ArtDirection/Generated/Weapons-20261004/";
    const string Selection="ArtDirection/Review-20261004/weapon-redesign/accepted-selection.json";
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        var palette=new List<Color32>();var keys=new HashSet<int>();
        foreach(var id in new[]{"scrap_pistol","scrap_shotgun","flintlock","scattergun","storm_rifle","starbreaker","dragon_cannon","twin_pistols","void_revolver"})
        {
            var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
            tex.LoadImage(File.ReadAllBytes("Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon/"+id+".png"));
            foreach(var c in tex.GetPixels32())if(c.a==255&&keys.Add(c.r<<16|c.g<<8|c.b))palette.Add(c);
            UnityEngine.Object.DestroyImmediate(tex);
        }
        Directory.CreateDirectory(Root+"native");
        var rows=JArray.Parse(File.ReadAllText(Selection,Encoding.UTF8));var audit=new JArray();
        var ids=new[]{"training_katana","moon_blade","blood_moon_katana"};
        var versions=new[]{"v1","v1","v2"};
        for(int n=0;n<ids.Length;n++)
        {
            var id=ids[n];var sourcePath=Root+id+"-source-"+versions[n]+".png";
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(File.ReadAllBytes(sourcePath));
            var input=source.GetPixels32();var pixels=new Color32[48*48];var used=new HashSet<int>();
            for(int y=0;y<48;y++)for(int x=0;x<48;x++)
            {
                int sx=Mathf.Min(source.width-1,(int)((x+.5f)*source.width/48f));
                int sy=Mathf.Min(source.height-1,(int)((y+.5f)*source.height/48f));
                var original=input[sy*source.width+sx];if(original.a<128)continue;
                int best=int.MaxValue;Color32 selected=default;
                foreach(var c in palette)
                {
                    int dr=c.r-original.r,dg=c.g-original.g,db=c.b-original.b,d=dr*dr+dg*dg+db*db;
                    if(d<best){best=d;selected=c;}
                }
                pixels[y*48+x]=selected;used.Add(selected.r<<16|selected.g<<8|selected.b);
            }
            var native=new Texture2D(48,48,TextureFormat.RGBA32,false);native.SetPixels32(pixels);native.Apply();
            string path=Root+"native/"+id+".png";File.WriteAllBytes(path,native.EncodeToPNG());
            for(int i=rows.Count-1;i>=0;i--)if((string)rows[i]["id"]==id)rows.RemoveAt(i);
            rows.Add(new JObject{{"id",id},{"source",path},{"family","Katana"},{"size",new JArray(48,48)},
                {"sourceSha256",Hash(path)},{"grip",new JArray(14f,39f)},{"muzzle",new JArray(45f,3f)},
                {"forwardAngle",49f},{"license","Generated using built-in image_gen; CC0 reference source recorded"}});
            audit.Add(new JObject{{"id",id},{"generatedSource",sourcePath},{"generatedSourceSha256",Hash(sourcePath)},
                {"generatedSize",new JArray(source.width,source.height)},{"nativeSize",new JArray(48,48)},
                {"nativeSource",path},{"nativeSha256",Hash(path)},{"opaqueColors",used.Count},
                {"conversion","48x48 center-nearest sampling, existing firearm color palette, alpha threshold128. No blurred resampling; not lossless grid extraction."}});
            UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(native);
        }
        File.WriteAllText(Selection,rows.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Root+"native-import-receipt.json",new JObject{{"result","Prepared for scene review"},{"paletteColorCount",palette.Count},{"images",audit}}.ToString(),new UTF8Encoding(false));
        return "Prepared three generated 48x48 katana candidates for import and actual-scale review.";
    }
}
