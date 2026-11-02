using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Supplied-state integration and visual review. Not a normal campaign or balance test.
public static class SealGuardianReview20261004
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.FlattenHierarchy;
    static GameController game; static EnemyController boss; static EditorWindow view;
    static string folder; static Vector2 center; static Type autoTick; static object sizes;
    static bool oldTick,oldBackground,oldInput,oldInitialized; static long oldInterval; static int oldSize,newSize,oldFrameRate;
    static readonly JArray cases=new JArray(); static readonly List<string> checks=new List<string>();
    static Dictionary<string,string> saves; static int previousSlot; static SaveSlotData previousData; static bool previousServer;
    static string Hash(string p){using(var s=SHA256.Create())return Convert.ToBase64String(s.ComputeHash(File.ReadAllBytes(p)));}
    static Dictionary<string,string> SaveHashes()
    {
        var result=new Dictionary<string,string>();
        if(Directory.Exists(game.SaveSlots.SaveDirectory))foreach(var p in Directory.GetFiles(game.SaveSlots.SaveDirectory,"*",SearchOption.AllDirectories))result[p]=Hash(p);
        return result;
    }
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(game==null||game.IsRunActive||game.BackendBusy)throw new Exception("Idle main menu required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-04-seal-guardian/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        cases.Clear();checks.Clear();saves=SaveHashes();previousSlot=game.SaveSlots.ActiveSlot;previousData=game.SaveSlots.ActiveData;previousServer=game.SaveSlots.ServerBound;
        oldBackground=Application.runInBackground;oldFrameRate=Application.targetFrameRate;
        oldInput=(bool)typeof(StageValidationInput).GetField("enabled",All).GetValue(null);
        oldInitialized=(bool)typeof(StageValidationInput).GetField("initialized",All).GetValue(null);
        game.SaveSlots.OpenTransientForTests();game.enabled=true;typeof(GameController).GetMethod("BeginRunForActiveSlot",All).Invoke(game,null);
        typeof(StageValidationInput).GetField("enabled",All).SetValue(null,true);typeof(StageValidationInput).GetField("initialized",All).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Application.runInBackground=true;Application.targetFrameRate=60;
        view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Focus();oldSize=(int)view.GetType().GetProperty("selectedSizeIndex",All).GetValue(view);
        var t=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");var instance=t.GetProperty("instance",All).GetValue(null);
        sizes=t.GetMethod("GetGroup",All).Invoke(instance,new[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor"),"Standalone")});
        var size=Activator.CreateInstance(Type.GetType("UnityEditor.GameViewSize,UnityEditor"),new object[]{Enum.Parse(Type.GetType("UnityEditor.GameViewSizeType,UnityEditor"),"FixedResolution"),1280,720,"Guardian review"});
        newSize=(int)sizes.GetType().GetMethod("GetTotalCount",All).Invoke(sizes,null);sizes.GetType().GetMethod("AddCustomSize",All).Invoke(sizes,new[]{size});view.GetType().GetProperty("selectedSizeIndex",All).SetValue(view,newSize);
        foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()){autoTick=asm.GetType("Unity.Pipeline.Editor.Commands.AutoTickCommand");if(autoTick!=null)break;}
        oldTick=(bool)autoTick.GetProperty("IsEnabled",All).GetValue(null);oldInterval=(long)autoTick.GetProperty("CurrentIntervalMs",All).GetValue(null);
        autoTick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{true,0,false});game.CameraFollow.StartCoroutine(Guard());return folder;
    }
    static IEnumerator Guard()
    {
        var stack=new Stack<IEnumerator>();stack.Push(Run());string status="PASS";
        while(stack.Count>0)
        {
            object value=null;bool next;
            try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}
            catch(Exception e){status="FAIL "+e;break;}
            if(!next){stack.Pop();continue;}
            if(value is IEnumerator nested){stack.Push(nested);continue;}yield return value;
        }
        try{Restore();}catch(Exception e){status="FAIL restore "+e;}
        File.WriteAllText(folder+"/result.txt",status+"\nSupplied transient profile, generated stage2, forced boss actions and protected hero. Not normal gameplay, balance or smoothness certification.\n"+string.Join("\n",checks),new UTF8Encoding(false));
        File.WriteAllText(folder+"/cases.json",cases.ToString(),new UTF8Encoding(false));Debug.Log("[Guardian Review] "+status+" "+folder);
    }
    static void Restore()
    {
        StageValidationInput.ReleaseAll();BossEntrancePresentation.Cancel(game);game.ResumeGame();game.enabled=true;game.ReturnToMainMenu();Time.timeScale=1;
        typeof(SaveSlotService).GetProperty("ActiveSlot",All).SetValue(game.SaveSlots,previousSlot);
        typeof(SaveSlotService).GetProperty("ActiveData",All).SetValue(game.SaveSlots,previousData);
        typeof(SaveSlotService).GetProperty("ServerBound",All).SetValue(game.SaveSlots,previousServer);
        typeof(StageValidationInput).GetField("enabled",All).SetValue(null,oldInput);typeof(StageValidationInput).GetField("initialized",All).SetValue(null,oldInitialized);
        view.GetType().GetProperty("selectedSizeIndex",All).SetValue(view,oldSize);sizes.GetType().GetMethod("RemoveCustomSize",All).Invoke(sizes,new object[]{newSize});
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldFrameRate;autoTick.GetMethod("SetAutoTick",All).Invoke(null,new object[]{oldTick,(int)oldInterval,false});
        var after=SaveHashes();Check(after.Count==saves.Count,"user save count preserved");foreach(var p in saves)Check(after.TryGetValue(p.Key,out var hash)&&hash==p.Value,"user save bytes preserved "+Path.GetFileName(p.Key));
        Check(game.Phase==GameSessionPhase.MainMenu&&!game.CameraFollow.HasPresentationFocus,"main menu camera/input state restored");
    }
    static void ClearAttacks()
    {
        foreach(var h in UnityEngine.Object.FindObjectsByType<ArenaHazard>())UnityEngine.Object.Destroy(h.gameObject);
        foreach(var p in UnityEngine.Object.FindObjectsByType<BossRuneProjectile>())UnityEngine.Object.Destroy(p.gameObject);
        foreach(var p in UnityEngine.Object.FindObjectsByType<BossNativeEffect>())UnityEngine.Object.Destroy(p.gameObject);
    }
    static IEnumerator Capture(string id)
    {
        var bodyRenderer=(SpriteRenderer)typeof(EnemyController).GetField("spriteRenderer",All).GetValue(boss);
        bodyRenderer.sortingOrder=900-Mathf.RoundToInt(boss.transform.position.y*100);
        typeof(EnemyController).GetMethod("UpdateHealthBarDepth",All).Invoke(boss,null);
        view.Repaint();EditorApplication.QueuePlayerLoopUpdate();yield return new WaitForEndOfFrame();
        Check(Screen.width==1280&&Screen.height==720,"capture resolution "+id);
        var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+id+".png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
        var renderer=(SpriteRenderer)typeof(EnemyController).GetField("spriteRenderer",All).GetValue(boss);
        cases.Add(new JObject{{"id",id},{"source",AssetDatabase.GetAssetPath(renderer.sprite)},{"ppu",renderer.sprite.pixelsPerUnit},
            {"bossScale",new JArray(boss.transform.lossyScale.x,boss.transform.lossyScale.y)},{"action",boss.CurrentBossAction.ToString()},
            {"phase",boss.AttackVisualPhase},{"introLocked",boss.IntroLocked},{"session",game.Phase.ToString()},
            {"hazards",UnityEngine.Object.FindObjectsByType<ArenaHazard>().Length},{"shots",UnityEngine.Object.FindObjectsByType<BossRuneProjectile>().Length}});
        Check(renderer.sprite.pixelsPerUnit==32&&AssetDatabase.GetAssetPath(renderer.sprite).Contains("/10k/SealGuardian/"),"native guardian source "+id);
    }
    static IEnumerator Run()
    {
        yield return null;game.StopAllCoroutines();game.enabled=false;typeof(GameController).GetProperty("StageIntroUntil",All).SetValue(game,0f);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemySpawnTelegraph>())UnityEngine.Object.Destroy(e.gameObject);
        foreach(var e in UnityEngine.Object.FindObjectsByType<EnemyController>()){e.gameObject.SetActive(false);UnityEngine.Object.Destroy(e.gameObject);}
        UnityEngine.Object.Destroy(game.DungeonMap.Root);
        var map=DungeonMap.BuildStage(_=>{},20261004,2);typeof(GameController).GetProperty("DungeonMap",All).SetValue(game,map);typeof(GameController).GetProperty("CurrentStageIndex",All).SetValue(game,2);
        center=map.GetRoomCenter(map.BossRoomIndex);var roomSize=map.GetRoom(map.BossRoomIndex).Size;
        typeof(GameController).GetProperty("CurrentRoom",All).SetValue(game,map.BossRoomIndex);map.MarkVisited(map.BossRoomIndex);map.BeginEncounter(map.BossRoomIndex);
        game.Player.Teleport(center+Vector2.down*3);typeof(PlayerController).GetField("invincibilityTicks",All).SetValue(game.Player,1000000);
        game.CameraFollow.FocusRoom(center,roomSize,true);boss=EnemyController.Create(EnemyType.SealHeart,center,game.Player,()=>game.IsPaused,_=>{},game.CameraFollow);
        boss.ConfigureArena(new Rect(center-roomSize*.5f,roomSize));boss.ConfigureNavigation(map.Terrain);boss.enabled=false;
        typeof(GameController).GetProperty("Boss",All).SetValue(game,boss);yield return new WaitForSeconds(.2f);
        for(int d=0;d<4;d++)
        {game.Player.Teleport(center+new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right}[d]*3);yield return new WaitForSeconds(.25f);yield return Capture("idle-"+d);}
        game.Player.Teleport(center+Vector2.down*3);
        int callbacks=0;BossEntrancePresentation.Begin(game,boss,"봉인의 수호체","깨어난 봉인",()=>callbacks++);
        yield return new WaitForSecondsRealtime(.65f);Check(boss.IntroLocked&&game.Phase==GameSessionPhase.BossIntro,"intro freezes boss and session");
        float health=boss.Health;boss.TakeDamage(999,Vector2.zero);Check(boss.Health==health,"intro rejects damage");yield return Capture("intro");
        var presentation=UnityEngine.Object.FindAnyObjectByType<BossEntrancePresentation>();var elapsedField=typeof(BossEntrancePresentation).GetField("elapsed",All);float frozen=(float)elapsedField.GetValue(presentation);
        typeof(GameController).GetMethod("SetManualPause",All).Invoke(game,new object[]{true});yield return new WaitForSecondsRealtime(.25f);
        Check(Mathf.Abs((float)elapsedField.GetValue(presentation)-frozen)<.001f,"pause suspends intro clock");game.ResumeGame();
        BossEntrancePresentation.Skip(game);BossEntrancePresentation.Skip(game);yield return null;
        Check(callbacks==1&&!boss.IntroLocked&&!game.CameraFollow.HasPresentationFocus&&game.Phase==GameSessionPhase.Expedition,"skip restores state and invokes once");
        BossEntrancePresentation.Begin(game,boss,"봉인의 수호체","깨어난 봉인",()=>callbacks++);BossEntrancePresentation.Cancel(game);yield return null;
        Check(callbacks==1&&!boss.IntroLocked&&!game.CameraFollow.HasPresentationFocus,"cancel restores without completion");
        BrainChecks();HazardChecks();
        foreach(int phase in new[]{1,2})foreach(string action in new[]{"SealSweep","SealCharge","SealCross","SealVolley"})
        {
            boss.StopAllCoroutines();ClearAttacks();typeof(EnemyController).GetProperty("Health",All).SetValue(boss,phase==1?boss.MaxHealth:boss.MaxHealth*.4f);
            boss.transform.position=center;boss.GetComponent<Rigidbody2D>().position=center;game.Player.Teleport(center+Vector2.right*3);
            var method=typeof(EnemyController).GetMethod(action,All);var routine=(IEnumerator)method.Invoke(boss,action=="SealCross"?null:new object[]{Vector2.right});boss.StartCoroutine(routine);
            yield return new WaitForSeconds(.15f);yield return Capture("p"+phase+"-"+action+"-warning");
            float warning=action=="SealSweep"?.7f:action=="SealCharge"?.85f:action=="SealCross"?1.05f:.9f;
            yield return new WaitForSeconds(warning-.10f);yield return Capture("p"+phase+"-"+action+"-active");
            Check(boss.IsActing&&boss.AttackVisualPhase==1,"active action phase "+phase+action);
            yield return new WaitForSeconds(.7f);yield return Capture("p"+phase+"-"+action+"-recover");
            yield return new WaitForSeconds(1.5f);Check(!boss.IsActing,"action ends "+phase+action);
        }
        ClearAttacks();boss.enabled=true;boss.TakeDamage(100000,Vector2.zero);yield return new WaitForSeconds(.55f);yield return Capture("death-settle");
        yield return new WaitForSeconds(.8f);Check(boss==null,"death cleanup");
    }
    static void BrainChecks()
    {
        foreach(bool tide in new[]{false,true})foreach(float distance in new[]{2f,5f,9f})foreach(bool phase in new[]{false,true})
        {
            var brain=new CampaignBossBrain();int last=-1;var seen=new HashSet<int>();
            for(int i=0;i<32;i++){int n=brain.Select(distance,phase,tide);Check(n!=last,"no adjacent repeat "+tide+distance+phase+i);seen.Add(n);last=n;}
            Check(seen.Count==4,"four actions reachable "+tide+distance+phase);
        }
    }
    static void HazardChecks()
    {
        var h=ArenaHazard.Create(boss,game.Player,center,Vector2.one*4.8f,0,true,.7f,.3f,1,Color.red,ArenaHazardStyle.SealSweep,false,135);
        Check(h.Contains(center+Vector2.right*2)&&!h.Contains(center+Vector2.left)&&!h.Contains(center+Vector2.right*3),"sweep sector bounds");
        var elapsed=typeof(ArenaHazard).GetField("elapsed",All);var contains=typeof(ArenaHazard).GetMethod("ContainsActive",All);
        elapsed.SetValue(h,.7f);Check((bool)contains.Invoke(h,new object[]{center+Vector2.right*.65f})&&!(bool)contains.Invoke(h,new object[]{center+Vector2.right*2}),"sweep damage starts at crest");
        elapsed.SetValue(h,.995f);Check((bool)contains.Invoke(h,new object[]{center+Vector2.right*2.2f})&&!(bool)contains.Invoke(h,new object[]{center+Vector2.right*.6f}),"sweep damage follows crest");
        UnityEngine.Object.Destroy(h.gameObject);
    }
}
