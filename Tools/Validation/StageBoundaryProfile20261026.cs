using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using RuinRunner;
using UnityEngine;

// Isolated supplied stage/UI boundary, with no navigation pilot. Not a normal campaign.
public static class StageBoundaryProfile20261026
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static GameController game; static Type profiler; static string folder; static double deadline;
    static readonly JArray events=new JArray();
    static object Call(string name,params object[] args)=>game.GetType().GetMethod(name,Flags).Invoke(game,args);
    static void Set(string name,object value)=>game.GetType().GetProperty(name,Flags).SetValue(game,value);
    static void Stamp(string label)=>events.Add(new JObject{["event"]=label,["frame"]=Time.frameCount,["seconds"]=Time.realtimeSinceStartupAsDouble,["fixedTime"]=Time.fixedTime,["scale"]=Time.timeScale,["map"]=game.IsMapOpen,["inventory"]=game.IsInventoryOpen,["stage"]=game.CurrentStageIndex+1});
    public static string Main()
    {
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||!game.SaveSlots.IsValidationProfile||game.IsRunActive)throw new Exception("Fresh isolated menu required");
        profiler=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UiProfile20261021")).First(t=>t!=null);
        folder=Path.GetFullPath("Logs/Validation/2026-10-26-goal/boundary-"+DateTime.Now.ToString("HHmmss"));Directory.CreateDirectory(folder);
        events.Clear();deadline=Time.realtimeSinceStartupAsDouble+90;
        var host=new GameObject("StageBoundary_TEMP");host.AddComponent<StageBoundaryRunner20261026>().StartCoroutine(Guard(host,Run()));
        return folder;
    }
    static IEnumerator Guard(GameObject host,IEnumerator routine)
    {
        string error=null;var stack=new Stack<IEnumerator>();stack.Push(routine);
        while(stack.Count>0)
        {
            bool moved=false;object value=null;
            try{if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException();moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}
            catch(Exception e){error=e.ToString();break;}
            if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
            if(value is IEnumerator nested)stack.Push(nested);else yield return value;
        }
        var profile=(string)profiler.GetMethod("Finish").Invoke(null,null);
        File.WriteAllText(folder+"/boundary.json",new JObject{["error"]=error,["events"]=events,["profile"]=profile,["suppliedStageAndPanels"]=true,["navigationPilot"]=false}.ToString(),new UTF8Encoding(false));
        if(game!=null){Set("IsMapOpen",false);Set("IsInventoryOpen",false);Call("SetManualPause",false);game.ReturnToMainMenu();}
        UnityEngine.Object.Destroy(host);
    }
    static IEnumerator Idle(){while(game.BackendBusy||game.SessionOperationPending)yield return null;}
    static IEnumerator Run()
    {
        yield return Idle();game.OpenProfileTown(1);yield return Idle();
        if(!game.IsTown)throw new Exception("Town not ready");
        game.DepartTown();yield return Idle();while(game.IsStageTransitioning||game.Player==null)yield return null;
        Set("RunSeed",2026100903);
        var profile=profiler.GetMethod("Main").Invoke(null,null);Stamp("profile start; supplied stage advance follows");
        // Hold scaled simulation through generation, as the automatically opened map does.
        Call("SetManualPause",true);Call("BeginNextStage");
        while(game.IsStageTransitioning)yield return null;
        Stamp("stage 2 built; simulation remains paused");
        Set("IsMapOpen",true);Call("SetManualPause",false);Stamp("map open");
        yield return new WaitForSecondsRealtime(.8f);
        Set("IsMapOpen",false);game.GetType().GetField("modalCloseFrame",Flags).SetValue(game,Time.frameCount);Call("RefreshTimeScale");Stamp("map close; modal-close frame");
        yield return null;Stamp("one frame after close");
        yield return null;Stamp("two frames after close");
        Set("IsInventoryOpen",true);Call("RefreshTimeScale");Stamp("inventory open");
        yield return new WaitForSecondsRealtime(.8f);
        Set("IsInventoryOpen",false);Call("RefreshTimeScale");Stamp("inventory close; simulation resumes");
        yield return new WaitForSecondsRealtime(1.5f);Stamp("settled after resume");
    }
}
public sealed class StageBoundaryRunner20261026:MonoBehaviour{}
