using System;
using UnityEditor;
using UnityEngine;
public static class RetireChargedWedge20261001
{
    public static object Main()
    {
        const string source="Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/ChargedCut";
        const string parent="Assets/RuinRunner/ArtReview";
        const string destination=parent+"/RejectedChargedWedge20261001";
        if(AssetDatabase.IsValidFolder(source))
        {
            if(AssetDatabase.IsValidFolder(destination))throw new Exception("Reject destination already exists; preserve both sources.");
            var error=AssetDatabase.MoveAsset(source,destination);
            if(!string.IsNullOrEmpty(error))throw new Exception(error);
            AssetDatabase.SaveAssets();
        }
        if(AssetDatabase.IsValidFolder(source)||!AssetDatabase.IsValidFolder(destination))throw new Exception("Rejected source remains active or backup missing.");
        var poses=AssetDatabase.FindAssets("t:Sprite",new[]{destination});
        if(poses.Length!=7)throw new Exception("Rejected seven source poses were not preserved.");
        return new {retired=destination,preservedSpriteCount=poses.Length,activeSource=source,active=false};
    }
}
