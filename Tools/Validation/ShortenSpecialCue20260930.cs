using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
public static class ShortenSpecialCue20260930
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode for sound adoption");
        var source="ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/Audio/Sounds/Magic & Skill/Magic2.wav";
        var path="Assets/RuinRunner/Resources/Audio/Combat/magic.wav";
        var backup="ArtDirection/Review-20260930/before/Audio/magic.wav";Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!File.Exists(backup)){File.Copy(path,backup);File.Copy(path+".meta",backup+".meta");}
        File.Copy(source,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var manifest="ArtDirection/Review-20260930/combat-presentation-import.json";var data=JObject.Parse(File.ReadAllText(manifest,Encoding.UTF8));
        foreach(JObject row in data["imagesAndSounds"])if((string)row["path"]==path)
        {
            row["source"]=source;
            using(var hash=SHA256.Create())row["sourceSha256"]=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(source))).Replace("-","").ToLowerInvariant();
        }
        File.WriteAllText(manifest,data.ToString(),new UTF8Encoding(false));
        return "Special layer shortened from2.67s to0.307s using native Magic2, original and GUID retained.";
    }
}
