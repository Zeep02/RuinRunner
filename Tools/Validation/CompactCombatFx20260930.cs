using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class CompactCombatFx20260930
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        const string root="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX";
        const string archive="Assets/RuinRunner/ArtReview/CombatFXGridPadding20260930";
        if(Directory.Exists(archive))throw new Exception("Compaction already performed");
        var manifestPath="ArtDirection/Review-20260930/combat-presentation-import.json";
        var data=JObject.Parse(File.ReadAllText(manifestPath,Encoding.UTF8));var output=new JArray();var report=new JObject();
        File.Copy(manifestPath,"ArtDirection/Review-20260930/combat-presentation-grid-original.json",false);
        var error=AssetDatabase.MoveAsset(root,archive);if(!string.IsNullOrEmpty(error))throw new Exception(error);
        foreach(var directory in Directory.GetDirectories(archive))
        {
            var key=Path.GetFileName(directory);var seen=new HashSet<string>();var index=0;
            Directory.CreateDirectory(root+"/"+key);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var ordered=Directory.GetFiles(directory,"*.png");Array.Sort(ordered,StringComparer.Ordinal);
            foreach(var old in ordered)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(old));
                var pixels=texture.GetPixels32();var nonempty=false;
                foreach(var pixel in pixels)if(pixel.a!=0){nonempty=true;break;}
                var bytes=texture.GetRawTextureData<byte>().ToArray();string hash;
                using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes));
                UnityEngine.Object.DestroyImmediate(texture);
                if(!nonempty||!seen.Add(hash))continue;
                var dest=root+"/"+key+"/"+index.ToString("D2")+".png";index++;
                var previous=old.Replace('\\','/').Replace(archive,root);
                foreach(JObject entry in data["imagesAndSounds"])
                    if((string)entry["path"]==previous){var row=(JObject)entry.DeepClone();row["path"]=dest;output.Add(row);break;}
                error=AssetDatabase.MoveAsset(old,dest);if(!string.IsNullOrEmpty(error))throw new Exception(error);
            }
            report[key]=index;
        }
        foreach(JObject entry in data["imagesAndSounds"])if(((string)entry["path"]).EndsWith(".wav"))output.Add(entry.DeepClone());
        data["imagesAndSounds"]=output;data["validFrameCounts"]=report;
        File.WriteAllText(manifestPath,data.ToString(),new UTF8Encoding(false));AssetDatabase.SaveAssets();
        return report.ToString();
    }
}
