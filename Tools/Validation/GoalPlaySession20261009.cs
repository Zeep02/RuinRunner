using System;
using System.IO;
using System.Reflection;
using RuinRunner;
using UnityEditor;
using UnityEngine;

// Only isolates persistence/settings/API. No actors, equipment, health, money,
// combat positions or time scaling are supplied by this setup.
public static class GoalPlaySession20261009
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    public static string Town()=>Start(false);
    public static string Campaign()=>Start(true);
    static string Start(bool campaign)
    {
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||game.IsRunActive||game.BackendBusy||game.BackendHasPending)
            throw new InvalidOperationException("Idle main menu in Play required; active work is never replaced.");
        string id=DateTime.Now.ToString("HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
        string output=Path.GetFullPath("Logs/Validation/2026-10-09-goal/play-"+id);
        string privateRoot=Path.GetFullPath("Backend/.local/private/goal-play-"+id);
        Directory.CreateDirectory(output);
        game.DisposeBackend();
        Environment.SetEnvironmentVariable("RUINRUNNER_API_URL","http://127.0.0.1:5196/");
        typeof(GameController).GetProperty("SaveSlots",Flags).SetValue(game,new SaveSlotService(Path.Combine(privateRoot,"Saves")));
        typeof(GameController).GetProperty("Settings",Flags).SetValue(game,new GameSettingsService(Path.Combine(privateRoot,"settings.json")));
        PrototypeAudio.Configure(game.Settings);
        Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        if(campaign)
        {
            typeof(StageValidationInput).GetField("initialized",Flags).SetValue(null,true);
            typeof(StageValidationInput).GetField("enabled",Flags).SetValue(null,true);
            StageValidationInput.ReleaseAll();
            game.gameObject.AddComponent<AutomatedStageValidation>().Initialize(game,output,false,false);
        }
        else game.OpenProfileTown(1);
        return output;
    }
}
