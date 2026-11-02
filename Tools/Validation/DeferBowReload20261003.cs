using System;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

public static class DeferBowReload20261003
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        string from="Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV2.asset";
        string to="Assets/RuinRunner/ArtSources/Archive/WeaponReloadV2.catalog.asset";
        if(AssetDatabase.LoadMainAssetAtPath(from)!=null)
        {
            if(AssetDatabase.LoadMainAssetAtPath(to)!=null)throw new Exception("Preserve existing deferred catalog");
            string error=AssetDatabase.MoveAsset(from,to);if(error.Length>0)throw new Exception(error);
        }
        var importer=(SpriteAtlasImporter)AssetImporter.GetAtPath("Assets/RuinRunner/ArtAtlases/NativeHeroWeaponReloadV2.spriteatlasv2");
        if(importer!=null){importer.includeInBuild=false;importer.SaveAndReimport();}
        AssetDatabase.SaveAssets();return "Deferred unfinished six-family reload authoring; source PNGs/GUIDs preserved, catalog outside Resources and atlas excluded from builds.";
    }
}
