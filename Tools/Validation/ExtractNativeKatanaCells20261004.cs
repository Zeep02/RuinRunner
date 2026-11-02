using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Sprite-sheet extraction only. Every output texel equals its original atlas texel.
public static class ExtractNativeKatanaCells20261004
{
    const string Root="ArtDirection/ExternalCandidates/20261004/tetrabyte-swords/";
    static string Hash(string path) {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    public static string Main()
    {
        var atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(atlas,File.ReadAllBytes(Root+"original/64 Swords.png")))throw new Exception("Source unavailable");
        if(atlas.width!=256||atlas.height!=256)throw new Exception("Unexpected original atlas dimensions");
        Directory.CreateDirectory(Root+"native-cells");
        var ids=new[]{"training_katana","moon_blade","blood_moon_katana"};
        var cells=new[]{new Vector2Int(4,0),new Vector2Int(1,3),new Vector2Int(5,6)};
        var rows=new JArray();
        for(var i=0;i<ids.Length;i++)
        {
            int x=cells[i].x*32,y=atlas.height-(cells[i].y+1)*32;
            var original=atlas.GetPixels(x,y,32,32);
            var cell=new Texture2D(32,32,TextureFormat.RGBA32,false);cell.SetPixels(original);cell.Apply();
            var path=Root+"native-cells/"+ids[i]+".png";File.WriteAllBytes(path,cell.EncodeToPNG());
            var verify=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(verify,File.ReadAllBytes(path));
            var read=verify.GetPixels32();var expected=cell.GetPixels32();
            if(read.Length!=1024)throw new Exception("Changed native cell size");
            for(var p=0;p<read.Length;p++)if(!read[p].Equals(expected[p]))throw new Exception("Changed source texel");
            rows.Add(new JObject{{"id",ids[i]},{"source",path},{"family","Katana"},{"size",new JArray(32,32)},
                {"sourceSha256",Hash(path)},{"originalAtlas",Root+"original/64 Swords.png"},
                {"originalAtlasSha256",Hash(Root+"original/64 Swords.png")},{"cellTopLeft",new JArray(cells[i].x,cells[i].y)},
                {"pixelsUnchanged",true},{"resampled",false},{"license","TETRABYTE custom free-use license"},
                {"sourceUrl","https://tetrabyte.itch.io/fantasy-64-sword-pack"}});
            UnityEngine.Object.DestroyImmediate(cell);UnityEngine.Object.DestroyImmediate(verify);
        }
        UnityEngine.Object.DestroyImmediate(atlas);
        File.WriteAllText(Root+"native-cells/extraction.json",rows.ToString(),new UTF8Encoding(false));
        return "Three original 32x32 cells extracted and all 3072 RGBA texels verified; no scaling, filtering, palette changes or generated pixels.";
    }
}
