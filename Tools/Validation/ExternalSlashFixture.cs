using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Actual GameAction attack inputs on an isolated stage with staged equipment.
// No enemies or damage manipulation; not a campaign/performance validation.
public static class ExternalSlashFixture
{
    static string folder,phase;static GameController game;static PlayerController player;
    static InventorySystem inventory;static bool recording;static int frame,observedEffects;
    static readonly StringBuilder rows=new StringBuilder();
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-external-slash/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();player=PlayerController.Create(map.GetRoomCenter(3),()=>false,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        rows.Clear();rows.AppendLine("frame,realtime,phase,effects,sprite,rotation,scaleX,scaleY,reverse");frame=0;observedEffects=0;phase="settle";
        game.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        string result="PASS actual-input effect capture; visual inspection required";
        while(true)
        {
            bool next;object current=null;
            try{next=run.MoveNext();if(next)current=run.Current;}
            catch(Exception e){result="FAIL "+e;Debug.LogException(e);break;}
            if(!next)break;yield return current;
        }
        recording=false;StageValidationInput.ReleaseAll();Time.timeScale=1;
        File.WriteAllText(folder+"/result.txt",result+"\nObserved effect samples="+observedEffects);
        File.WriteAllText(folder+"/scope.txt","Isolated stage1 arena, staged weapons, actual GameAction attacks, no enemies. Direction changes and weapons are sequenced. Unscaled timestamps preserve recording duration; no motion interpolation or forced captureFramerate. Frame samples do not prove all authored frames were displayed or stable60fps. Technical checks: pause freezes effect, resume removes it, session cleanup removes an active effect. Not normal campaign evidence or complete art approval.");
        Debug.Log("[External Slash] "+result+" "+folder);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);recording=true;game.StartCoroutine(Record());
        foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Dagger,WeaponFamily.Katana,WeaponFamily.Greatsword,WeaponFamily.Mace})
        {
            StageValidationInput.ReleaseAll();inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            phase=family+"-equip";yield return new WaitForSeconds(.5f);
            for(var dir=0;dir<8;dir++)
            {
                phase=family+"-attack-"+dir;StageValidationInput.SetAim(new Vector2(Mathf.Sin(dir*Mathf.PI/4),Mathf.Cos(dir*Mathf.PI/4)));
                StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);yield return new WaitForSeconds(.6f);
                StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);yield return new WaitForSeconds(.2f);
            }
        }
        StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.5f);
        if(observedEffects==0)throw new Exception("No actual input slash effects observed");
        phase="isolated-pause-lifetime";
        MeleeSlashEffect.Create(player.transform.position+Vector3.right*2,0,new Vector2(2,1),WeaponFamily.Longsword,true,false);
        var effects=UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>();if(effects.Length!=1)throw new Exception("Expected one explicit lifetime probe");
        var effect=effects[0];var view=effect.GetComponent<SpriteRenderer>();var sprite=view.sprite;Time.timeScale=0;
        yield return new WaitForSecondsRealtime(.25f);
        if(effect==null||view.sprite!=sprite)throw new Exception("Pause changed effect");
        Time.timeScale=1;yield return new WaitForSeconds(.4f);
        if(effect!=null)throw new Exception("Effect did not expire after resume");
        // This fixture constructs its actors independently; GameController has
        // no active run and ReturnToMainMenu correctly returns immediately.
        // Exercise the shared session cleanup directly, without claiming this
        // isolated scene is a real menu transition.
        phase="isolated-session-cleanup";MeleeSlashEffect.Create(player.transform.position,0,Vector2.one,WeaponFamily.Longsword,false,false);
        typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        yield return null;yield return null;
        if(UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>().Length!=0)throw new Exception("Session cleanup leaked slash");
        recording=false;yield return new WaitForSecondsRealtime(.15f);
    }
    static IEnumerator Record()
    {
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            var effects=UnityEngine.Object.FindObjectsByType<MeleeSlashEffect>();var effect=effects.Length>0?effects[0]:null;
            if(effect!=null)observedEffects++;
            var view=effect==null?null:effect.GetComponent<SpriteRenderer>();
            rows.AppendLine(string.Join(",",frame,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,effects.Length,
                view==null?"none":AssetDatabase.GetAssetPath(view.sprite),effect==null?0:effect.transform.eulerAngles.z,
                effect==null?0:effect.transform.localScale.x,effect==null?0:effect.transform.localScale.y,view!=null&&view.flipX));
            ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+(frame++).ToString("D6")+".png");
            yield return new WaitForSecondsRealtime(.025f);
        }
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
    }
}
