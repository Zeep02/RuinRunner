using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using RuinRunner;

public static class StopSweepCatalogReview20261001
{
    public static object Main()
    {
        const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("SweepFxCatalog20261001")).FirstOrDefault(t=>t!=null);
        if(type==null)throw new Exception("Only the active source catalog recorder may be stopped");
        var folder=(string)type.GetField("folder",flags).GetValue(null);
        if(folder!="F:/codex/LoglikeCap/Logs/Validation/2026-10-01-melee-fx-binding/catalog-181532"||File.Exists(folder+"/result.txt"))throw new Exception("Recorder identity changed or already finished");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.CameraFollow.StopAllCoroutines();
        AsyncGPUReadback.WaitAllRequests();
        foreach(var name in new[]{"recording","telemetry"})
            File.WriteAllText(folder+(name=="recording"?"/partial-recording.csv":"/partial-body-telemetry.csv"),type.GetField(name,flags).GetValue(null).ToString(),new UTF8Encoding(false));
        type.GetMethod("Flush",flags).Invoke(null,null);type.GetMethod("RestoreView",flags).Invoke(null,null);
        StageValidationInput.ReleaseAll();Time.timeScale=1;game.ReturnToMainMenu();
        File.WriteAllText(folder+"/result.txt","FAIL stopped after independent base screen QA found obsolete yellow RuntimeSquare hit particles crossing the original head. Partial real capture and checks retained; not accepted as complete catalog.\n"+string.Join("\n",(System.Collections.Generic.List<string>)type.GetField("checks",flags).GetValue(null)),new UTF8Encoding(false));
        return new {interrupted=folder,preservedSamples=type.GetField("sample",flags).GetValue(null),restoredView=true,accepted=false};
    }
}
