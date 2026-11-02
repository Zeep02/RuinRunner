using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using RuinRunner;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bounded cancellation checks against an already-isolated server profile.
// Stage advancement is supplied; this is lifecycle evidence, not a campaign.
public static class TransitionCancellation20261025
{
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
    static readonly JArray checks=new JArray();
    static GameController game;
    static string folder;
    static double deadline;
    public static string Main()
    {
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||!game.SaveSlots.IsValidationProfile||game.BackendBusy||game.SessionOperationPending)
            throw new InvalidOperationException("Idle isolated profile required.");
        checks.Clear();folder=Path.GetFullPath("Logs/Validation/2026-10-25-goal/cancellation-"+DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(folder);deadline=Time.realtimeSinceStartupAsDouble+120;
        var host=new GameObject("CancellationCheck_TEMP");
        host.AddComponent<CancellationCheckRunner20261025>().StartCoroutine(Guard(host,Run()));
        return folder;
    }
    static IEnumerator Guard(GameObject host,IEnumerator routine)
    {
        string error=null;var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(routine);
        while(stack.Count>0)
        {
            object current=null;bool moved=false;
            try
            {
                if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Cancellation check deadline");
                moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;
            }
            catch(Exception e){error=e.ToString();break;}
            if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
            if(current is IEnumerator nested)stack.Push(nested);else yield return current;
        }
        File.WriteAllText(folder+"/result.json",new JObject{["passed"]=error==null,["checks"]=checks,["error"]=error,
            ["isolatedProfile"]=true,["suppliedStageAdvance"]=true,["normalCampaign"]=false}.ToString(),new UTF8Encoding(false));
        if(error!=null)Debug.LogError(error);
        UnityEngine.Object.Destroy(host);
    }
    static void Check(bool ok,string name,object details=null)
    {
        checks.Add(new JObject{["check"]=name,["pass"]=ok,["details"]=details==null?null:JToken.FromObject(details)});
        if(!ok)throw new InvalidOperationException(name);
    }
    static IEnumerator WaitIdle()
    {
        while(game.BackendBusy||game.SessionOperationPending||game.EconomyOperationPending)yield return null;
    }
    static IEnumerator Town()
    {
        yield return WaitIdle();game.ReturnToMainMenu();yield return null;yield return null;
        game.OpenProfileTown(1);yield return WaitIdle();
        Check(game.IsTown,"profile enters town");
        if(game.BackendHasUnfinishedRun){game.ResolveInterruptedExpedition();yield return WaitIdle();}
        Check(!game.BackendHasUnfinishedRun&&!game.BackendHasPending,"interrupted server run resolved once");
    }
    static IEnumerator PartBuilt(int minimumObjects)
    {
        while(game.DungeonMap?.Root==null||game.DungeonMap.Root.GetComponentsInChildren<Transform>().Length<minimumObjects)
        {CheckLoading();yield return null;}
        Check(game.IsStageTransitioning,"cancellation occurs during partial generation",new{minimumObjects,objects=game.DungeonMap.Root.GetComponentsInChildren<Transform>().Length});
        Check(game.IsPaused&&Time.timeScale==0,"partial generation locks gameplay input and scaled time");
    }
    static void CheckLoading()
    {
        if(game.DungeonMap!=null&&!game.IsStageTransitioning)throw new InvalidOperationException("Missed partial-generation window");
    }
    static IEnumerator CancelToMenu(string label)
    {
        var root=game.DungeonMap.Root;var templates=root.GetComponent<StageRuntimeOwnership>().Rooms.ToArray();
        yield return WaitIdle();game.ReturnToMainMenu();
        for(int i=0;i<12;i++)yield return null;
        Check(root==null&&templates.All(r=>r==null),label+": owned root and room templates destroyed");
        Check(UnityEngine.Object.FindObjectsByType<StageRuntimeOwnership>(FindObjectsInactive.Include).Length==0,label+": no orphan stage roots");
        Check(!game.IsRunActive&&!game.IsStageTransitioning&&game.DungeonMap==null&&game.Player==null&&game.Phase==GameSessionPhase.MainMenu,
            label+": controller returns to clean menu");
        Check(Time.timeScale==0&&!StageValidationInput.Enabled,label+": expected menu pause; no synthetic input enabled");
        Check(game.BackendHasUnfinishedRun&&!game.BackendHasPending,label+": interrupted run remains recoverable, no fabricated settlement");
    }
    static IEnumerator Run()
    {
        yield return Town();game.DepartTown();yield return WaitIdle();yield return PartBuilt(100);yield return CancelToMenu("first departure");
        yield return Town();game.DepartTown();yield return WaitIdle();
        while(game.IsStageTransitioning||game.Player==null)yield return null;
        Check(game.IsExpedition&&!game.IsPaused&&Time.timeScale==1,"new expedition after first cancellation restores control");
        typeof(GameController).GetProperty("RunSeed",Flags).SetValue(game,2026100903);
        typeof(GameController).GetMethod("BeginNextStage",Flags).Invoke(game,null);
        // During the initial fade the old root still exists until deferred Destroy.
        while(game.CurrentStageIndex!=1)yield return null;
        yield return PartBuilt(1200);yield return CancelToMenu("second-region transition");
        yield return Town();game.ReturnToMainMenu();yield return null;

        // Unload a scene while its coroutine is building terrain. The menu scene remains loaded.
        var original=SceneManager.GetActiveScene();var isolated=SceneManager.CreateScene("PartialTerrainUnload_TEMP");
        SceneManager.SetActiveScene(isolated);var owner=new GameObject("PartialTerrainOwner_TEMP");
        var runner=owner.AddComponent<CancellationCheckRunner20261025>();DungeonMap partial=null;
        runner.StartCoroutine(DungeonMap.BuildStageProgressively(m=>partial=m,_=>{},2026100903,2));
        SceneManager.SetActiveScene(original);
        while(partial==null||partial.Root.GetComponentsInChildren<Transform>().Length<100)yield return null;
        var sceneRoot=partial.Root;var sceneRooms=sceneRoot.GetComponent<StageRuntimeOwnership>().Rooms.ToArray();
        Check(sceneRoot.scene==isolated,"partial terrain belongs to the scene being unloaded");
        var unload=SceneManager.UnloadSceneAsync(isolated);while(!unload.isDone)yield return null;
        for(int i=0;i<12;i++)yield return null;
        Check(owner==null&&sceneRoot==null&&sceneRooms.All(r=>r==null),"scene unload cancels builder and destroys partial resources");
        Check(UnityEngine.Object.FindObjectsByType<StageRuntimeOwnership>(FindObjectsInactive.Include).Length==0,"no new objects appear after scene cancellation");
        yield return Town();game.DepartTown();yield return WaitIdle();
        while(game.IsStageTransitioning||game.Player==null)yield return null;
        Check(game.IsExpedition&&!game.IsPaused&&Time.timeScale==1,"normal generation completes after cancellation checks");
        game.ReturnToMainMenu();yield return Town();game.ReturnToMainMenu();
    }
}
public sealed class CancellationCheckRunner20261025:MonoBehaviour{}
