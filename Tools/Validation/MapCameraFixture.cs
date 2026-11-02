using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using RuinRunner;

// Runs the standalone MapValidation sequence inside Editor without Application.Quit.
public static class MapCameraFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Main()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();
        var directory="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-map-camera/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(directory);
        var fixture=game.gameObject.AddComponent<MapValidation>();fixture.enabled=false;
        typeof(MapValidation).GetField("game",Private).SetValue(fixture,game);
        typeof(MapValidation).GetField("directory",Private).SetValue(fixture,directory);
        typeof(MapValidation).GetField("screenshot",Private).SetValue(fixture,Type.GetType("UnityEngine.ScreenCapture,UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}));
        typeof(StageValidationInput).GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        typeof(StageValidationInput).GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        StageValidationInput.ReleaseAll();Application.runInBackground=true;
        UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var sequence=(IEnumerator)typeof(MapValidation).GetMethod("Validate",Private).Invoke(fixture,null);
        game.CameraFollow.StartCoroutine(Run(sequence,directory));return directory;
    }
    static IEnumerator Run(IEnumerator sequence,string directory)
    {
        while(true)
        {
            bool more;
            try{more=sequence.MoveNext();}
            catch(Exception e){File.WriteAllText(directory+"/result.txt","FAIL "+e);Debug.LogException(e);yield break;}
            if(!more)break;
            yield return null;StageValidationInput.BeginFrame();
        }
        File.WriteAllText(directory+"/result.txt","PASS fixed pixel camera, room tracking, paused map, Escape resume and geometry; isolated fixture\n");
        Debug.Log("[Map Camera] PASS "+directory);
    }
}
