using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using RuinRunner;

// Only enables the normal GameAction driver and creates a fresh isolated profile.
// No health/damage/position/inventory/progression injection, no time acceleration.
public static class StartNormalCampaignPilot
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(game.IsRunActive)game.ReturnToMainMenu();
        var folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-16-normal-campaign/editor-"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);
        typeof(GameController).GetProperty("SaveSlots").SetValue(game,new SaveSlotService(folder+"/Saves"));
        var input=typeof(GameController).Assembly.GetType("RuinRunner.StageValidationInput");
        input.GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetField("initialized",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
        input.GetMethod("ReleaseAll").Invoke(null,null);
        Time.timeScale=1;Time.captureFramerate=0;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartSlot(1);
        game.gameObject.AddComponent<AutomatedStageValidation>().Initialize(game,folder,true,true);
        Debug.Log("[Normal Campaign] Isolated new slot, no upgrades/cheats; output="+folder);
        return folder;
    }
}
