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
public static class GoalSession20261009
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;
    static GameController game; static SaveSlotService oldSaves; static object oldBackend;
    static GameObject host; static EditorWindow view; static object sizes; static int oldSize,oldCustomCount;
    static Type autoTick; static bool oldTick,oldBackground,running,oldMaximized; static long oldInterval; static float oldTime;
    static string folder; static double deadline; static readonly List<int> addedSizes=new List<int>();
    static string previousEndpoint;
    static bool slotsOnly, talentsOnly, interactive, interactiveDone, textReview;
    static string apiUrl="http://127.0.0.1:5196/";
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
        slotsOnly=false;talentsOnly=false;interactive=false;
        return Start();
    }
    public static string MainSlots()
    {
        slotsOnly=true;talentsOnly=false;interactive=false;
        return Start();
    }
    public static string MainTalents(string endpoint)
    {
        var uri=new Uri(endpoint);
        if(!uri.IsLoopback)throw new ArgumentException("Isolated loopback backend required.");
        apiUrl=uri.AbsoluteUri;slotsOnly=false;talentsOnly=true;interactive=false;
        return Start();
    }
    public static string MainInteractive(string endpoint)
    {
        var uri=new Uri(endpoint);
        if(!uri.IsLoopback)throw new ArgumentException("Isolated loopback backend required.");
        apiUrl=uri.AbsoluteUri;slotsOnly=false;talentsOnly=false;interactive=true;interactiveDone=false;
        return Start();
    }
    public static string MainTextReview(string endpoint)
    {
        var uri=new Uri(endpoint);if(!uri.IsLoopback)throw new ArgumentException("Isolated loopback backend required.");
        apiUrl=uri.AbsoluteUri;slotsOnly=false;talentsOnly=false;interactive=false;textReview=true;
        return Start();
    }
    public static string PrepareInteractive(string service,int width,int height)
    {
        if(!running||!interactive)throw new InvalidOperationException("Interactive fixture not active.");
        Resolution(width,height);
        if(!string.IsNullOrEmpty(service))
        {
            game.CloseService();
            var kind=(FieldServiceKind)Enum.Parse(typeof(FieldServiceKind),service);
            var obj=UnityEngine.Object.FindObjectsByType<FieldServiceObject>().Single(o=>o.Kind==kind);
            game.Player.Teleport((Vector2)obj.transform.position+Vector2.down*1.3f);
        }
        deadline=EditorApplication.timeSinceStartup+900;
        return "Supplied resolution/position only. Open and operate the panel using native input.";
    }
    public static string InteractiveState()
    {
        var hud=UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
        return new JObject{["phase"]=game.Phase.ToString(),["service"]=game.OpenService?.Kind.ToString(),
            ["talentTab"]=hud==null?-1:(int)Read(hud,"talentFamilyTab"),["paused"]=game.IsPaused,
            ["inventory"]=game.IsInventoryOpen,["map"]=game.IsMapOpen,["backendBusy"]=game.BackendBusy,
            ["width"]=Screen.width,["height"]=Screen.height}.ToString();
    }
    public static void FinishInteractive(){interactiveDone=true;}
    public static void CaptureInteractive(string name)
    {
        if(!running||!interactive||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
            throw new InvalidOperationException("Invalid interactive capture.");
        Capture(name);
        checks.Add("Observed after native input: "+name+" "+InteractiveState());
    }
    public static void RevealInteractiveMap()
    {
        if(!interactive||!running||game.DungeonMap==null)throw new InvalidOperationException("Interactive world required.");
        for(int i=0;i<game.DungeonMap.RoomCount;i++)game.DungeonMap.MarkVisited(i);
        checks.Add("SUPPLIED discovery for whole-map visual review, stage "+(game.CurrentStageIndex+1));
    }
    public static void SupplyNextStage()
    {
        if(!interactive||!running||!game.IsExpedition||game.CurrentStageIndex>=2||game.IsStageTransitioning)
            throw new InvalidOperationException("Idle interactive expedition stage required.");
        game.CloseService();Call(game,"BeginNextStage");
        checks.Add("SUPPLIED stage advance for region visual review; not combat completion.");
    }
    public static void SupplyResult()
    {
        if(!interactive||!running||!game.IsExpedition||game.BackendBusy)throw new InvalidOperationException("Idle interactive expedition required.");
        game.CloseService();Call(game,"PlayerDied");
        checks.Add("SUPPLIED terminal event for native result UI checks; not a combat defeat.");
    }
    static string Start()
    {
        if(running)throw new InvalidOperationException("Town review already active.");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||game.IsRunActive||game.BackendBusy||game.BackendHasPending)
            throw new InvalidOperationException("Play Mode at an idle main menu required; no active or pending session may be interrupted.");
        checks.Clear();captures.Clear();addedSizes.Clear();
        folder=Path.GetFullPath("Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-goal/session-"+DateTime.Now.ToString("HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(folder);
        oldSaves=game.SaveSlots;oldBackend=Read(game,"backendFlow");savedHashes=SaveHashes();oldTime=Time.timeScale;oldBackground=Application.runInBackground;
        previousEndpoint=Environment.GetEnvironmentVariable("RUINRUNNER_API_URL");
        Environment.SetEnvironmentVariable("RUINRUNNER_API_URL",apiUrl);
        var temporary=Path.GetFullPath("Backend/.local/private/goal-session-"+Guid.NewGuid().ToString("N")+"/Saves");
        Set(game,"SaveSlots",new SaveSlotService(temporary));Write(game,"backendFlow",null);
        running=true;deadline=EditorApplication.timeSinceStartup+(interactive?900:300);
        EditorApplication.update+=Watchdog;EditorApplication.playModeStateChanged+=PlayChanged;
        try
        {
            SetupView();Application.runInBackground=true;
            host=new GameObject("GoalSession20261009_TEMP"){hideFlags=HideFlags.DontSave};host.AddComponent<GoalSessionRunner20261009>().StartCoroutine(Guard(Run()));
            return folder+" (running: fresh guest + production API; supplied panel poses/death)";
        }
        catch(Exception e){Finish("FAIL",e.ToString());throw;}
    }
    static void SetupView()
    {
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Focus();oldMaximized=view.maximized;if(interactive)view.maximized=true;oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",Flags).GetValue(view);
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
        if(textReview){yield return ReviewChangedText();textReview=false;yield break;}
        if(interactive)
        {
            while(!interactiveDone)yield return null;
            yield break;
        }
        if(talentsOnly)
        {
            game.OpenProfileTown(1);while(game.SessionOperationPending)yield return null;
            Require(game.IsTown&&game.BackendProfile!=null,"fresh isolated profile enters real Town");
            var talentHud=UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
            foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
            {
                Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
                Require(Screen.width==resolution.x&&Screen.height==resolution.y,"native screenshot resolution "+resolution);
                OpenPanel(FieldServiceKind.Talents);
                for(int tab=0;tab<4;tab++)
                {
                    Write(talentHud,"talentFamilyTab",tab);
                    yield return new WaitForEndOfFrame();
                    Capture(resolution.x+"x"+resolution.y+"-talents-"+tab);
                }
            }
            yield break;
        }
        if(slotsOnly)
        {
            game.OpenSaveSlots();while(game.BackendBusy)yield return null;
            Require(!game.SaveSlots.GetSummary(1).Exists&&!game.SaveSlots.GetSummary(2).Exists&&!game.SaveSlots.GetSummary(3).Exists,
                "all three slots are empty in a new isolated guest; original saves are untouched");
            foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
            {
                Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
                yield return new WaitForEndOfFrame();Capture(resolution.x+"x"+resolution.y+"-empty-slots");
            }
            yield break;
        }
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            yield return new WaitForEndOfFrame();Capture(resolution.x+"x"+resolution.y+"-menu");
        }
        game.OpenProfileTown(1);while(game.SessionOperationPending)yield return null;
        Require(game.IsTown&&game.BackendProfile!=null,"fresh isolated profile enters real walkable Town through production GameController");
        string isolatedProfileId=game.BackendProfile.profileId;
        Require(game.SaveSlots.ServerBound&&game.BackendProfile.state.totalRuns==0,"new guest has server profile and no supplied progression");
        Require(WeaponLootTable.PinnedCatalogVersion==game.BackendCatalog.version&&AccessoryLootTable.PinnedCatalogVersion==game.BackendCatalog.version,"town catalogs are server bound");
        Require(UnityEngine.Object.FindObjectsByType<FieldServiceObject>().Length==3&&UnityEngine.Object.FindAnyObjectByType<TownTrainingTarget>()!=null,"three actual town services and actual training target exist");
        OpenPanel(FieldServiceKind.StartingGear);game.ChooseStartingWeapon("rust_sword");while(game.SessionOperationPending)yield return null;
        Require(game.BackendProfile.state.startingWeaponId=="rust_sword"&&game.Inventory.EquippedWeapon.Id=="rust_sword","starting gear selection confirms on server and practice inventory");
        OpenPanel(FieldServiceKind.Talents);var before=game.ActiveTalents.Ids;game.ToggleTalent("survey_next");while(game.SessionOperationPending)yield return null;
        Require(!game.ActiveTalents.Has(TalentId.SurveyNext),"unlocked talent can be deselected by actual configuration API");
        game.ToggleTalent("survey_next");while(game.SessionOperationPending)yield return null;
        Require(game.ActiveTalents.Has(TalentId.SurveyNext)&&game.BackendProfile.state.activeTalentIds.Length==before.Length,"free town configuration restores selected talent through server");
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            Require(Screen.width==resolution.x&&Screen.height==resolution.y,"native screenshot resolution "+resolution);
            string prefix=resolution.x+"x"+resolution.y;
            game.CloseService();game.Player.Teleport(TownWorld.Arrival);yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(prefix+"-town-arrival");
            foreach(var kind in new[]{FieldServiceKind.StartingGear,FieldServiceKind.Talents,FieldServiceKind.Expedition})
            {OpenPanel(kind);yield return new WaitForEndOfFrame();Capture(prefix+"-"+kind);}
            game.CloseService();Set(game,"IsInventoryOpen",true);game.FocusInventoryQuickSlot(0);Call(game,"RefreshTimeScale");yield return new WaitForEndOfFrame();Capture(prefix+"-town-inventory");Set(game,"IsInventoryOpen",false);Call(game,"RefreshTimeScale");
        }
        OpenPanel(FieldServiceKind.Expedition);game.DepartTown();while(game.SessionOperationPending||game.IsStageTransitioning)yield return null;
        Require(game.IsExpedition&&game.BackendRun?.status=="active","actual town departure starts a server-pinned expedition");
        while(game.Player==null||game.DungeonMap==null||game.BackendBusy)yield return null;
        Require(game.RunSeed==game.BackendRun.seed&&game.DungeonMap.Graph!=null,"real generated world uses server run seed");
        Require(game.Inventory.ActiveSlot.Item.ServerInstanceId!="","free starting copy receives authoritative server instance identity");
        Require(game.ExchangeCatalogVersion==game.BackendRun.contentVersion,"exchange definitions use the server-pinned expedition version");
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Capture("expedition-start");
        // Supplied positions/panels only, using real server stock and descriptions.
        // This is resolution/layout evidence, not earned facilities or normal travel.
        var hud=UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
        var arrival=game.Player.transform.position;
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            string prefix=resolution.x+"x"+resolution.y;
            OpenPanel(FieldServiceKind.Merchant);yield return new WaitForEndOfFrame();Capture(prefix+"-shop");
            Require(game.CurrentShop!=null,"actual pinned server stock displayed "+prefix);
            foreach(var offer in game.CurrentShop.offers)
            {
                Write(hud,"merchantDetailOffer",offer.offerId);Write(hud,"merchantDetailScroll",Vector2.zero);
                yield return new WaitForEndOfFrame();Capture(prefix+"-shop-detail-"+offer.kind+"-"+offer.contentId);
            }
            Write(hud,"merchantDetailOffer",null);
            foreach(var kind in new[]{FieldServiceKind.ShapeAnvil,FieldServiceKind.OathStone,FieldServiceKind.ExchangeAltar})
            {
                if(!UnityEngine.Object.FindObjectsByType<FieldServiceObject>().Any(o=>o.Kind==kind))continue;
                OpenPanel(kind);game.ServiceSelectedEquipment=game.Inventory.ActiveSlot.Item.Identity;
                yield return new WaitForEndOfFrame();Capture(prefix+"-"+kind);
            }
            game.CloseService();game.Player.Teleport(arrival);
            Set(game,"IsMapOpen",true);Call(game,"RefreshTimeScale");yield return new WaitForEndOfFrame();Capture(prefix+"-map");
            Set(game,"IsMapOpen",false);Set(game,"IsInventoryOpen",true);game.FocusInventoryQuickSlot(0);
            Call(game,"RefreshTimeScale");yield return new WaitForEndOfFrame();Capture(prefix+"-expedition-inventory");
            Set(game,"IsInventoryOpen",false);Set(game,"IsStatsOpen",true);Call(game,"RefreshTimeScale");
            yield return new WaitForEndOfFrame();Capture(prefix+"-stats");Set(game,"IsStatsOpen",false);Call(game,"RefreshTimeScale");
        }
        // Supplied local receipt regression; this is not an earned reward or normal play.
        int cancelBefore=(int)Read(game.Player,"actionCancelSequence");
        var projection=new RuinRunner.Backend.EconomyReceipt{economy=new RuinRunner.Backend.EconomyView{runId=game.BackendRun.runId,revision=long.MaxValue-1}};
        Call(game,"ApplyEconomyReceipt",projection);
        Require((int)Read(game.Player,"actionCancelSequence")==cancelBefore,"supplied gold-only projection does not cancel combat or simulate a weapon swap");
        float healthBefore=game.Player.Stats.Health;
        game.Player.Stats.ReceiveDamage(12);
        float damaged=game.Player.Stats.Health;
        projection.economy.revision=long.MaxValue;projection.healAmount=3;
        Call(game,"ApplyEconomyReceipt",projection);Call(game,"ApplyEconomyReceipt",projection);
        Require(Mathf.Abs(game.Player.Stats.Health-Mathf.Min(game.Player.Stats.MaxHealth,damaged+3))<.001f,"supplied duplicate receipt heals once");
        game.Player.Stats.Heal(healthBefore-game.Player.Stats.Health);
        // Explicit supplied terminal event: this is not a combat defeat or normal completion claim.
        Call(game,"PlayerDied");
        Require(!game.ResultSettlementConfirmed,"result remains unconfirmed while settlement response is pending");
        while(game.BackendBusy)yield return null;
        yield return null;
        Require(game.IsGameOver&&game.BackendRun.status=="defeat","supplied death invokes production once-only server settlement");
        Require(game.ResultSettlementConfirmed&&game.MetaProgression.LegacyShards==game.BackendProfile.legacyShards,
            "result confirmation and displayed owned legacy match the authoritative profile");
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            yield return new WaitForEndOfFrame();Capture(resolution.x+"x"+resolution.y+"-settlement-confirmed");
        }
        game.ReturnToTown();while(game.SessionOperationPending)yield return null;
        Require(game.IsTown&&game.BackendProfile.state.totalRuns==1,"settled result returns to actual town");
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Capture("returned-town");
        game.ReturnToMainMenu();game.OpenSaveSlots();while(game.BackendBusy)yield return null;
        game.RequestProfileReset(1);yield return new WaitForEndOfFrame();Capture("reset-step1");
        Require(game.ResetProfileSlot==1&&!game.ResetProfileChallengeReady,"first reset step requires separate owner/revision confirmation");
        game.PrepareProfileReset();while(game.SessionOperationPending)yield return null;
        Require(game.ResetProfileChallengeReady&&game.BackendProfile.state.totalRuns==1,"server challenge leaves existing test progress untouched");
        yield return new WaitForEndOfFrame();Capture("reset-step2");
        game.ConfirmProfileReset();while(game.SessionOperationPending)yield return null;
        Require(game.ResetProfileSlot==0&&game.BackendProfile.profileId==isolatedProfileId&&game.BackendProfile.state.totalRuns==0,
            "explicit second confirmation resets only the new test profile and preserves its stable identity");
        Require(game.BackendProfile.state.legacyImportBlocked,"reset permanently blocks automatic legacy reimport");
        game.OpenProfileTown(1);while(game.SessionOperationPending)yield return null;
        Require(game.IsTown&&game.BackendProfile.state.totalRuns==0,"same reset test profile opens the real town with fresh progress");
    }
    static IEnumerator ReviewChangedText()
    {
        checks.Add("SUPPLIED layout review only: handlers, positions and stage advance; no native input or normal combat claim.");
        game.OpenProfileTown(1);while(game.SessionOperationPending)yield return null;
        Require(game.IsTown,"isolated profile enters town");
        OpenPanel(FieldServiceKind.Expedition);game.DepartTown();
        while(game.SessionOperationPending||game.IsStageTransitioning)yield return null;
        Require(game.IsExpedition&&game.Player!=null,"progressive departure completed");
        var hud=UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            game.CloseService();Set(game,"IsInventoryOpen",true);game.FocusInventoryQuickSlot(0);game.ShowItemDetails=true;Call(game,"RefreshTimeScale");
            yield return new WaitForEndOfFrame();Capture(resolution.x+"x"+resolution.y+"-weapon-details");
            Set(game,"IsInventoryOpen",false);Call(game,"RefreshTimeScale");OpenPanel(FieldServiceKind.Merchant);
            var offer=game.CurrentShop.offers.First(o=>o.kind=="weapon");Write(hud,"merchantDetailOffer",offer.offerId);
            yield return new WaitForEndOfFrame();Capture(resolution.x+"x"+resolution.y+"-merchant-details");
        }
        game.CloseService();Call(game,"BeginNextStage");while(game.IsStageTransitioning)yield return null;
        Require(game.CurrentStageIndex==1,"progressive next region completed");
        foreach(var resolution in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1600,1000)})
        {
            Resolution(resolution.x,resolution.y);for(int f=0;f<4;f++)yield return null;
            OpenPanel(FieldServiceKind.ExchangeAltar);game.PreviewExchange(game.ExchangeRecipes[0].Id);
            yield return new WaitForEndOfFrame();Capture(resolution.x+"x"+resolution.y+"-exchange-details");
        }
        game.CloseService();game.ReturnToMainMenu();
        Require(!game.IsRunActive,"supplied layout session returned to menu");
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true){bool next;object current=null;try{next=run.MoveNext();if(next)current=run.Current;}catch(Exception e){Finish("FAIL",e.ToString());yield break;}if(!next)break;yield return current;}Finish("CAPTURED",null);
    }
    public static string Cancel(){Finish("CANCELLED","Explicit stop; inspect fixture guest for any unsettled run.");return folder;}
    static void Watchdog(){if(running&&EditorApplication.timeSinceStartup>deadline)Finish("FAIL","300-second timeout; fixture guest preserved for diagnosis.");}
    static void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish("CANCELLED","Play ended during review.");}
    static void Finish(string result,string error)
    {
        if(!running)return;running=false;EditorApplication.update-=Watchdog;EditorApplication.playModeStateChanged-=PlayChanged;
        var failures=new JArray();void Restore(Action action){try{action();}catch(Exception e){failures.Add(e.Message);}}
        Restore(()=>{if(game!=null){game.CloseService();game.ReturnToMainMenu();if(game.IsRunActive)throw new InvalidOperationException("Fixture still has a pending server operation; did not tear down active session.");game.DisposeBackend();Set(game,"SaveSlots",oldSaves);Write(game,"backendFlow",oldBackend);}});
        Restore(()=>Environment.SetEnvironmentVariable("RUINRUNNER_API_URL",previousEndpoint));
        Restore(()=>{if(view!=null){view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,oldSize);view.maximized=oldMaximized;}if(sizes!=null){for(int i=addedSizes.Count-1;i>=0;i--)Call(sizes,"RemoveCustomSize",addedSizes[i]);if((int)Call(sizes,"GetCustomCount")!=oldCustomCount)throw new Exception("GameView custom presets changed.");}});
        Restore(()=>{if(autoTick!=null)autoTick.GetMethod("SetAutoTick",Flags).Invoke(null,new object[]{oldTick,(int)oldInterval,false});Time.timeScale=oldTime;Application.runInBackground=oldBackground;});
        Restore(()=>{if(host!=null)UnityEngine.Object.Destroy(host);});
        bool unchanged=false;Restore(()=>{var after=SaveHashes();unchanged=savedHashes.Count==after.Count&&savedHashes.All(p=>after.TryGetValue(p.Key,out var h)&&h==p.Value);if(!unchanged)throw new Exception("Original user profile files changed; never automatically restore over them.");});
        File.WriteAllText(folder+"/result.json",new JObject{["result"]=failures.Count>0?"FAIL":result,["checks"]=checks,["captures"]=captures,["realProductionControllerAndApi"]=true,["suppliedPanelPositionsAndDeath"]=!talentsOnly&&!slotsOnly&&!interactive,["suppliedPositions"]=!slotsOnly,["interactiveNativeInputSession"]=interactive,["manualWalkingValidated"]=false,["normalCombatOrCampaignValidated"]=false,["visualVerdict"]="pending direct PNG review",["originalSavesUnchanged"]=unchanged,["stateRestored"]=failures.Count==0,["restoreErrors"]=failures,["error"]=error}.ToString(),new UTF8Encoding(false));
        Debug.Log("[Town UI review] "+result+" "+folder);
    }
}
public sealed class GoalSessionRunner20261009:MonoBehaviour{}
