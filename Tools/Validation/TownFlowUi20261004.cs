using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using RuinRunner;
using UnityEditor;
using UnityEngine;

// Production GameController + API. New isolated guest only. Panels/positions and death are supplied.
// This proves controller integration and records real UI; it is not manual walking, combat or campaign proof.
public static class TownFlowUi20261004
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;
    static GameController game; static SaveSlotService oldSaves; static object oldBackend;
    static GameObject host; static EditorWindow view; static object sizes; static int oldSize,oldCustomCount;
    static Type autoTick; static bool oldTick,oldBackground,running; static long oldInterval; static float oldTime;
    static string folder; static double deadline; static readonly List<int> addedSizes=new List<int>();
    static readonly JArray checks=new JArray(),captures=new JArray(); static Dictionary<string,string> savedHashes;
    static object Read(object obj,string name)=>obj.GetType().GetField(name,Flags).GetValue(obj);
    static void Write(object obj,string name,object value)=>obj.GetType().GetField(name,Flags).SetValue(obj,value);
    static void Set(object obj,string name,object value)=>obj.GetType().GetProperty(name,Flags).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
    static void Require(bool value,string label){if(!value)throw new InvalidOperationException(label);checks.Add(label);}
    static string Hash(string file){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-","");}
    static Dictionary<string,string> SaveHashes()
    {
        var result=new Dictionary<string,string>();
        foreach(var dir in new[]{Path.Combine(Application.persistentDataPath,"RuinRunner"),oldSaves.SaveDirectory}.Distinct())
            if(Directory.Exists(dir))foreach(var file in Directory.GetFiles(dir,"*",SearchOption.AllDirectories))result[file]=Hash(file);
        return result;
    }
    public static string Main()
    {
        if(running)throw new InvalidOperationException("Town review already active.");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||game.IsRunActive||game.BackendBusy||game.BackendHasPending)
            throw new InvalidOperationException("Play Mode at an idle main menu required; no active or pending session may be interrupted.");
        checks.Clear();captures.Clear();addedSizes.Clear();
        folder=Path.GetFullPath("Logs/Validation/2026-10-04-town-growth/ui-"+DateTime.Now.ToString("HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(folder);
        oldSaves=game.SaveSlots;oldBackend=Read(game,"backendFlow");savedHashes=SaveHashes();oldTime=Time.timeScale;oldBackground=Application.runInBackground;
        var temporary=Path.GetFullPath("Backend/.local/private/town-ui-"+Guid.NewGuid().ToString("N")+"/Saves");
        Set(game,"SaveSlots",new SaveSlotService(temporary));Write(game,"backendFlow",null);
        running=true;deadline=EditorApplication.timeSinceStartup+150;
        EditorApplication.update+=Watchdog;EditorApplication.playModeStateChanged+=PlayChanged;
        try
        {
            SetupView();Application.runInBackground=true;
            host=new GameObject("TownFlowUi20261004_TEMP"){hideFlags=HideFlags.DontSave};host.AddComponent<TownFlowUiRunner20261004>().StartCoroutine(Guard(Run()));
            return folder+" (running: fresh guest + production API; supplied panel poses/death)";
        }
        catch(Exception e){Finish("FAIL",e.ToString());throw;}
    }
    static void SetupView()
    {
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Focus();oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(view);
        var type=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");var singleton=type.GetProperty("instance",Flags).GetValue(null);
        sizes=type.GetMethod("GetGroup",Flags).Invoke(singleton,new[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"),"Standalone")});oldCustomCount=(int)Call(sizes,"GetCustomCount");
        autoTick=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand")).FirstOrDefault(t=>t!=null);
        if(autoTick!=null){oldTick=(bool)autoTick.GetProperty("IsEnabled",Flags).GetValue(null);oldInterval=(long)autoTick.GetProperty("CurrentIntervalMs",Flags).GetValue(null);autoTick.GetMethod("SetAutoTick",Flags).Invoke(null,new object[]{true,0,false});}
    }
    static void Resolution(int width,int height)
    {
        var type=Type.GetType("UnityEditor.GameViewSize,UnityEditor");var size=Activator.CreateInstance(type,new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),width,height,"Town review "+width+"x"+height});
        int index=(int)Call(sizes,"GetTotalCount");Call(sizes,"AddCustomSize",size);addedSizes.Add(index);view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,index);view.Repaint();
    }
    static void OpenPanel(FieldServiceKind kind)
    {
        game.CloseService();var obj=UnityEngine.Object.FindObjectsByType<FieldServiceObject>().Single(o=>o.Kind==kind);
        game.Player.Teleport((Vector2)obj.transform.position+Vector2.down*1.3f);
        Set(game,"OpenService",obj);Call(game,"RefreshTimeScale");
    }
    static void Capture(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());captures.Add(new JObject{["file"]=name+".png",["width"]=image.width,["height"]=image.height,["phase"]=game.Phase.ToString(),["suppliedPanelOrPosition"]=true});}finally{UnityEngine.Object.Destroy(image);}
    }
    static IEnumerator Run()
    {
        game.OpenProfileTown(1);while(game.SessionOperationPending)yield return null;
        Require(game.IsTown&&game.BackendProfile!=null,"fresh isolated profile enters real walkable Town through production GameController");
        Require(game.SaveSlots.ServerBound&&game.BackendProfile.state.totalRuns==0,"new guest has server profile and no supplied progression");
        Require(WeaponLootTable.PinnedCatalogVersion==game.BackendCatalog.version&&AccessoryLootTable.PinnedCatalogVersion==game.BackendCatalog.version,"town catalogs are server bound");
        Require(UnityEngine.Object.FindObjectsByType<FieldServiceObject>().Length==3&&UnityEngine.Object.FindAnyObjectByType<TownTrainingTarget>()!=null,"three actual town services and actual training target exist");
        OpenPanel(FieldServiceKind.StartingGear);game.ChooseStartingWeapon("rust_sword");while(game.SessionOperationPending)yield return null;
        Require(game.BackendProfile.state.startingWeaponId=="rust_sword"&&game.Inventory.EquippedWeapon.Id=="rust_sword","starting gear selection confirms on server and practice inventory");
        OpenPanel(FieldServiceKind.Talents);var before=game.ActiveTalents.Ids;game.ToggleTalent("survey_next");while(game.SessionOperationPending)yield return null;
        Require(!game.ActiveTalents.Has(TalentId.SurveyNext),"unlocked talent can be deselected by actual configuration API");
        game.ToggleTalent("survey_next");while(game.SessionOperationPending)yield return null;
        Require(game.ActiveTalents.Has(TalentId.SurveyNext)&&game.BackendProfile.state.activeTalentIds.Length==before.Length,"free town configuration restores selected talent through server");
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            Require(Screen.width==resolution.x&&Screen.height==resolution.y,"native screenshot resolution "+resolution);
            string prefix=resolution.x+"x"+resolution.y;
            game.CloseService();game.Player.Teleport(TownWorld.Arrival);yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(prefix+"-town-arrival");
            foreach(var kind in new[]{FieldServiceKind.StartingGear,FieldServiceKind.Talents,FieldServiceKind.Expedition})
            {OpenPanel(kind);yield return new WaitForEndOfFrame();Capture(prefix+"-"+kind);}
            game.CloseService();Set(game,"IsInventoryOpen",true);game.FocusInventoryQuickSlot(0);Call(game,"RefreshTimeScale");yield return new WaitForEndOfFrame();Capture(prefix+"-town-inventory");Set(game,"IsInventoryOpen",false);Call(game,"RefreshTimeScale");
        }
        OpenPanel(FieldServiceKind.Expedition);game.DepartTown();while(game.SessionOperationPending)yield return null;
        Require(game.IsExpedition&&game.BackendRun?.status=="active","actual town departure starts a server-pinned expedition");
        while(game.Player==null||game.DungeonMap==null||game.BackendBusy)yield return null;
        Require(game.RunSeed==game.BackendRun.seed&&game.DungeonMap.Graph!=null,"real generated world uses server run seed");
        Require(game.Inventory.ActiveSlot.Item.ServerInstanceId!="","free starting copy receives authoritative server instance identity");
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Capture("1920x1080-expedition-start");
        // Explicit supplied terminal event: this is not a combat defeat or normal completion claim.
        Call(game,"PlayerDied");while(game.BackendBusy)yield return null;
        Require(game.IsGameOver&&game.BackendRun.status=="defeat","supplied death invokes production once-only server settlement");
        game.ReturnToTown();while(game.SessionOperationPending)yield return null;
        Require(game.IsTown&&game.BackendProfile.state.totalRuns==1,"settled result returns to actual town");
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Capture("1920x1080-returned-town");
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true){bool next;object current=null;try{next=run.MoveNext();if(next)current=run.Current;}catch(Exception e){Finish("FAIL",e.ToString());yield break;}if(!next)break;yield return current;}Finish("CAPTURED",null);
    }
    public static string Cancel(){Finish("CANCELLED","Explicit stop; inspect fixture guest for any unsettled run.");return folder;}
    static void Watchdog(){if(running&&EditorApplication.timeSinceStartup>deadline)Finish("FAIL","150-second timeout; fixture guest preserved for diagnosis.");}
    static void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED","Play ended during review.");}
    static void Finish(string result,string error)
    {
        if(!running)return;running=false;EditorApplication.update-=Watchdog;EditorApplication.playModeStateChanged-=PlayChanged;
        var failures=new JArray();void Restore(Action action){try{action();}catch(Exception e){failures.Add(e.Message);}}
        Restore(()=>{if(game!=null){game.CloseService();game.ReturnToMainMenu();if(game.IsRunActive)throw new InvalidOperationException("Fixture still has a pending server operation; did not tear down active session.");game.DisposeBackend();Set(game,"SaveSlots",oldSaves);Write(game,"backendFlow",oldBackend);}});
        Restore(()=>{if(view!=null)view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,oldSize);if(sizes!=null){for(int i=addedSizes.Count-1;i>=0;i--)Call(sizes,"RemoveCustomSize",addedSizes[i]);if((int)Call(sizes,"GetCustomCount")!=oldCustomCount)throw new Exception("GameView custom presets changed.");}});
        Restore(()=>{if(autoTick!=null)autoTick.GetMethod("SetAutoTick",Flags).Invoke(null,new object[]{oldTick,(int)oldInterval,false});Time.timeScale=oldTime;Application.runInBackground=oldBackground;});
        Restore(()=>{if(host!=null)UnityEngine.Object.Destroy(host);});
        bool unchanged=false;Restore(()=>{var after=SaveHashes();unchanged=savedHashes.Count==after.Count&&savedHashes.All(p=>after.TryGetValue(p.Key,out var h)&&h==p.Value);if(!unchanged)throw new Exception("Original user profile files changed; never automatically restore over them.");});
        File.WriteAllText(folder+"/result.json",new JObject{["result"]=failures.Count>0?"FAIL":result,["checks"]=checks,["captures"]=captures,["realProductionControllerAndApi"]=true,["suppliedPanelPositionsAndDeath"]=true,["manualWalkingValidated"]=false,["normalCombatOrCampaignValidated"]=false,["visualVerdict"]="pending direct PNG review",["originalSavesUnchanged"]=unchanged,["stateRestored"]=failures.Count==0,["restoreErrors"]=failures,["error"]=error}.ToString(),new UTF8Encoding(false));
        Debug.Log("[Town UI review] "+result+" "+folder);
    }
}
public sealed class TownFlowUiRunner20261004:MonoBehaviour{}
