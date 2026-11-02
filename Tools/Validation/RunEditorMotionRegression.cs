using System;
using System.Collections;
using System.Reflection;
using System.IO;
using UnityEngine;
using RuinRunner;

public static class RunEditorMotionRegression
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-16-motion/four-weapons-01";
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        if(Directory.Exists(Folder))throw new Exception("Preserve previous evidence; choose a new folder");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var camera in UnityEngine.Object.FindObjectsByType<CameraFollow>())UnityEngine.Object.Destroy(camera.gameObject);
        Time.timeScale=1;
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        var test=new GameObject("IsolatedMotionRegression").AddComponent<MotionValidation>();
        game.StartCoroutine(Configure(test));
        return "Isolated motion regression started; injected fixture equipment/positions, NOT normal campaign evidence.";
    }
    static IEnumerator Configure(MotionValidation test)
    {
        yield return null;yield return null;
        Directory.CreateDirectory(Folder);
        typeof(MotionValidation).GetField("fourWeapons",P).SetValue(test,true);
        typeof(MotionValidation).GetField("captureDirectory",P).SetValue(test,Folder);
        test.StartCoroutine((IEnumerator)typeof(MotionValidation).GetMethod("RecordWorldFrames",P).Invoke(test,null));
    }
}
