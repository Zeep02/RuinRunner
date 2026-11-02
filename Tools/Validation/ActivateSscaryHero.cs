using System;
using UnityEditor;
using UnityEngine;
public static class ActivateSscaryHero
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop play mode first.");
        const string source="Assets/RuinRunner/ArtReview/SscaryMale";
        const string target="Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale";
        AssetDatabase.StartAssetEditing();
        try {
        if(!AssetDatabase.IsValidFolder(target) && !AssetDatabase.CopyAsset(source,target))throw new Exception("Copy selected native frames failed.");
        const string archive="Assets/RuinRunner/ArtReview/RetiredGeneratedHero";
        if(!AssetDatabase.IsValidFolder(archive))AssetDatabase.CreateFolder("Assets/RuinRunner/ArtReview","RetiredGeneratedHero");
        foreach(var folder in new[]{"HeroMotion","HeroWalk","Player","UserHero"})
        {
            var old="Assets/RuinRunner/Resources/Art/Pixel/Separated/"+folder;
            if(!AssetDatabase.IsValidFolder(old))continue;
            var result=AssetDatabase.MoveAsset(old,archive+"/"+folder);
            if(!string.IsNullOrEmpty(result))throw new Exception(result);
        }
        } finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        var count=AssetDatabase.FindAssets("t:Sprite",new[]{target}).Length;
        if(count!=193)throw new Exception("Native sprite count "+count);
        return "193 selected source sprites active; previous generated hero folders archived outside Resources.";
    }
}

