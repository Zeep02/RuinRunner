using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Production PlayerController and GameAction inputs in an isolated arena.
// This verifies replacement and control continuity, not finished combat art or campaign completion.
public static class SscaryPlayerValidation
{
    static GameController game;
    static PlayerController player;
    static PlayerAnimationController animation;
    static string folder,phase;
    static bool paused,observe;
    static int observed,oldFrames;
    static readonly HashSet<string> poses=new HashSet<string>();
    static readonly HashSet<int> idleFrames=new HashSet<int>(),dashFrames=new HashSet<int>();
    static readonly StringBuilder rows=new StringBuilder();
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-22-active-hero/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var camera in UnityEngine.Object.FindObjectsByType<CameraFollow>()){camera.gameObject.SetActive(false);UnityEngine.Object.Destroy(camera.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();
        player=PlayerController.Create(map.GetRoomCenter(3),()=>paused,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        if(!DemoContractValidation.Validate(map))throw new Exception("Active art/content contract failed");
        rows.Clear();rows.AppendLine("time,phase,pose,frame,source,speed,combo,thrust");
        observed=oldFrames=0;poses.Clear();idleFrames.Clear();dashFrames.Clear();paused=false;observe=true;
        game.StartCoroutine(Observe());game.StartCoroutine(Guard());return folder;
    }
    static IEnumerator Guard()
    {
        var run=Run();string status="PASS";
        while(true)
        {
            bool more;object current=null;
            try{more=run.MoveNext();if(more)current=run.Current;}
            catch(Exception e){status="FAIL "+e;break;}
            if(!more)break;yield return current;
        }
        observe=false;StageValidationInput.ReleaseAll();paused=false;Time.timeScale=1;
        File.WriteAllText(folder+"/timeline.csv",rows.ToString());
        File.WriteAllText(folder+"/result.txt",status+"\nObserved="+observed+"; retired/foreign body frames="+oldFrames+"; idle="+idleFrames.Count+"; dash sampled="+dashFrames.Count+"\nReal PlayerController/GameAction movement, sword combo, dash, pause and death on production map; isolated encounter, no campaign/performance claim. Combat body art is explicitly pending; same-source idle/walk stance only. Source six views map to eight input directions.");
        Debug.Log("[Active Sscary] "+status+" "+folder);
    }
    static void Require(bool value,string label){if(!value)throw new Exception(label);}
    static void Input(Vector2 direction,bool attack=false,bool dash=false)
    {
        StageValidationInput.BeginFrame();StageValidationInput.SetAim(direction);
        StageValidationInput.SetHeld(GameAction.MoveRight,direction.x>.1f);StageValidationInput.SetHeld(GameAction.MoveLeft,direction.x<-.1f);
        StageValidationInput.SetHeld(GameAction.MoveUp,direction.y>.1f);StageValidationInput.SetHeld(GameAction.MoveDown,direction.y<-.1f);
        StageValidationInput.SetHeld(GameAction.Attack,attack);StageValidationInput.SetHeld(GameAction.Dash,dash);
    }
    static void Capture(string name){ScreenCapture.CaptureScreenshot(folder+"/"+name+".png");}
    static IEnumerator Run()
    {
        phase="idle";StageValidationInput.SetAim(Vector2.down);yield return new WaitForSeconds(1.7f);Capture("01-native-idle");
        Require(idleFrames.Count==8,"all eight source idle frames");
        var origin=player.transform.position;
        for(var dir=0;dir<8;dir++)
        {
            phase="move-"+dir;Input(new Vector2(Mathf.Sin(dir*Mathf.PI/4),Mathf.Cos(dir*Mathf.PI/4)));
            yield return new WaitForSeconds(.65f);
        }
        Require(Vector2.Distance(origin,player.transform.position) < 8,"closed input loop stays in arena");
        phase="moving-combo";var sequence=player.AttackAnimationSequence;var travel=player.transform.position;
        Input(Vector2.right,true);yield return new WaitForSeconds(.9f);Capture("02-native-moving-combo");
        Require(player.AttackAnimationSequence>=sequence+2,"actual consecutive attack inputs");
        Require(Vector2.Distance(travel,player.transform.position)>1,"attack permits travel");
        StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.25f);
        phase="dash";Input(Vector2.left,false,true);yield return null;
        Input(Vector2.left,false,false);yield return new WaitForSeconds(.07f);Capture("03-native-dash");
        yield return new WaitForSeconds(.25f);StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.3f);
        Require(dashFrames.Count>0,"source dash observed");
        phase="pause";paused=true;Time.timeScale=0;var sprite=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;
        yield return new WaitForSecondsRealtime(.3f);
        Require(sprite==player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite,"pause holds sprite");
        paused=false;Time.timeScale=1;
        phase="death";player.TakeDamage(99999,Vector2.zero);yield return new WaitForSeconds(1);Capture("04-native-death");
        Require(player.IsDead&&animation.PoseName=="death"&&animation.FrameIndex==7,"native death reaches final frame");
        Require(observed>30&&oldFrames==0,"only selected source hero on every observed frame");
        yield return new WaitForSeconds(.2f);
    }
    static IEnumerator Observe()
    {
        while(observe)
        {
            yield return new WaitForEndOfFrame();
            var sr=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();var source=AssetDatabase.GetAssetPath(sr.sprite);
            observed++;if(!source.StartsWith("Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/"))oldFrames++;
            poses.Add(animation.PoseName);
            if(phase=="idle")idleFrames.Add(animation.FrameIndex);
            if(animation.PoseName=="dash")dashFrames.Add(animation.FrameIndex);
            rows.AppendLine(string.Join(",",Time.time.ToString("F4",CultureInfo.InvariantCulture),phase,animation.PoseName,animation.FrameIndex,source,player.GetComponent<Rigidbody2D>().linearVelocity.magnitude,player.ActiveComboAnimationStep,player.IsThrustActive));
        }
    }
}

