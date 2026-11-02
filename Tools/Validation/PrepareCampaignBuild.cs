using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class PrepareCampaignBuild
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode before building");
        const string scene="Assets/RuinRunner/Scenes/Demo.unity";
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene)==null)throw new Exception("Game entry scene missing");
        // Keep company/product identity so existing persistentDataPath profiles remain visible.
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene,true)};
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Directory.CreateDirectory("Logs/Validation/2026-09-16-campaign-build");
        return "Demo scene is the normal menu entry. Resizable Windows build prepared; existing persistent-data identity retained.";
    }
}
