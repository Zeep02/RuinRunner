using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RetireUnusedGround
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before moving assets");
        const string resource="Assets/RuinRunner/Resources/Art/Pixel/Separated/";
        const string archive="Assets/RuinRunner/ArtReview/RetiredGeneratedTerrain";
        Directory.CreateDirectory(archive);AssetDatabase.Refresh();
        var result=new StringBuilder();
        foreach(var theme in new[]{"Outer","Flooded","Core"})
            Move(resource+"Campaign/Terrain/"+theme+"/Field",archive+"/"+theme+"Field",1024,result);
        Move(resource+"External/ChampiDungeon",archive+"/RejectedChampiDungeon",6,result);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/Validation/2026-09-30-native-floors");
        File.WriteAllText("Logs/Validation/2026-09-30-native-floors/retirement.txt",result.ToString(),Encoding.UTF8);
        return result.ToString();
    }
    static void Move(string source,string destination,int expected,StringBuilder result)
    {
        if(!AssetDatabase.IsValidFolder(source))
        {
            if(AssetDatabase.IsValidFolder(destination)){result.AppendLine("Already retired: "+destination);return;}
            throw new Exception("Missing source and archive: "+source);
        }
        if(AssetDatabase.IsValidFolder(destination))throw new Exception("Archive collision: "+destination);
        var assets=AssetDatabase.FindAssets("t:Texture2D",new[]{source});
        if(assets.Length!=expected)throw new Exception("Unexpected source count "+source+": "+assets.Length);
        var error=AssetDatabase.MoveAsset(source,destination);
        if(!string.IsNullOrEmpty(error))throw new Exception(error);
        foreach(var guid in assets)
            if(!AssetDatabase.GUIDToAssetPath(guid).StartsWith(destination+"/",StringComparison.Ordinal))
                throw new Exception("GUID was not preserved: "+guid);
        result.AppendLine("Preserved "+assets.Length+" PNGs and GUIDs outside Resources: "+destination);
    }
}
