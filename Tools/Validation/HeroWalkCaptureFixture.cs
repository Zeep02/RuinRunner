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

// Isolated stage/equipment. Only GameAction inputs during each sequence;
// positions reset between weapons, no enemies. Not campaign/performance proof.
public static class HeroWalkCaptureFixture
{
    static string folder,phase="settle";static bool recording,paused;static int frame;
    static GameController game;static PlayerController player;static PlayerAnimationController animation;static InventorySystem inventory;
    static readonly StringBuilder rows=new StringBuilder();static readonly HashSet<int> southFrames=new HashSet<int>();
    static readonly HashSet<int> northFrames=new HashSet<int>();
    static readonly HashSet<int> eastFrames=new HashSet<int>();
    static readonly List<string> checks=new List<string>();static bool badSocket,badFrame;static float distance,scaledRunningSeconds;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-hero-walk/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));var follow=CameraFollow.Create();
        player=PlayerController.Create(new Vector2(-8,3),()=>paused,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        rows.Clear();rows.AppendLine("frame,realtime,phase,pose,direction,bodyFrame,handX,handY,playerX,playerY,velocity,scaledTime");
        frame=0;southFrames.Clear();northFrames.Clear();eastFrames.Clear();checks.Clear();badSocket=false;badFrame=false;distance=0;scaledRunningSeconds=0;paused=false;
        if(!DemoContractValidation.Validate(map))throw new Exception("Current art/content contract failed");
        game.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool next;object current=null;
            try{next=run.MoveNext();if(next)current=run.Current;}
            catch(Exception e){Finish("FAIL "+e);Debug.LogException(e);yield break;}
            if(!next)break;yield return current;
        }
        Finish("PASS "+string.Join("; ",checks));
    }
    static void Require(bool test,string label){if(!test)throw new Exception(label);checks.Add(label);}
    static void Finish(string status)
    {
        StageValidationInput.ReleaseAll();Time.timeScale=1;paused=false;recording=false;
        File.WriteAllText(folder+"/result.txt",status+"\n12-frame south states="+string.Join("/",southFrames)+"\n12-frame north states="+string.Join("/",northFrames)+"\n12-frame east states="+string.Join("/",eastFrames)+"\nRunning distance="+distance+"; scaled seconds="+scaledRunningSeconds+"; walk cycle remains 2 world units. Other 5 directions remain 4 frames.");
        File.WriteAllText(folder+"/scope.txt","Isolated stage and staged equipment, no enemies, HUD hidden, player repositioned between weapons. Actual GameAction inputs/physics/animation. Whole-head invariance separately checked. Not normal campaign completion or performance evidence. Frame count is not a visual quality verdict.");
        Debug.Log("[Hero Walk] "+status+" "+folder);
    }
    static void Move(GameAction action,bool held){StageValidationInput.BeginFrame();StageValidationInput.SetHeld(action,held);}
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);recording=true;game.StartCoroutine(Record());game.StartCoroutine(Observe());
        foreach(var axis in new[]{4,2})foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Pistol})
        {
            var positive=axis==4?GameAction.MoveDown:GameAction.MoveRight;var negative=axis==4?GameAction.MoveUp:GameAction.MoveLeft;
            var forward=axis==4?"south":"east";var backward=axis==4?"north":"west";
            StageValidationInput.ReleaseAll();inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            player.GetComponent<Rigidbody2D>().position=new Vector2(-8,3);player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
            StageValidationInput.SetAim(axis==4?Vector2.down:Vector2.right);phase=family+"-stand-"+forward;yield return new WaitForSeconds(.8f);
            phase=family+"-"+forward;Move(positive,true);yield return new WaitForSeconds(1.25f);
            phase=family+"-stop-"+forward;Move(positive,false);yield return new WaitForSeconds(.45f);
            phase=family+"-"+backward;Move(negative,true);yield return new WaitForSeconds(1.25f);Move(negative,false);
            phase=family+"-"+forward+"-again";Move(positive,true);yield return new WaitForSeconds(1.0f);
            phase=family+"-moving-attack-"+forward;Move(GameAction.Attack,true);yield return new WaitForSeconds(.3f);Move(GameAction.Attack,false);Move(positive,false);
            yield return new WaitForSeconds(.3f);phase=family+"-dash";Move(GameAction.Dash,true);yield return null;Move(GameAction.Dash,false);yield return new WaitForSeconds(.6f);
            phase=family+"-pause";paused=true;Time.timeScale=0;
            var sprite=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;var hand=animation.HandLocalPosition;
            yield return new WaitForSecondsRealtime(.3f);
            Require(sprite==player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite&&hand==animation.HandLocalPosition,"pause body/hand "+family);
            paused=false;Time.timeScale=1;phase=family+"-settle";yield return new WaitForSeconds(.6f);
        }
        Require(southFrames.Count==12,"all twelve south poses observed from actual inputs");
        Require(northFrames.Count==12,"all twelve north poses observed from actual inputs");
        Require(eastFrames.Count==12,"all twelve east poses observed from actual inputs");
        Require(!badSocket,"weapon pivot follows authored hand on every observed pose");
        Require(!badFrame,"direction changes keep valid frame index/canvas/pivot");
        Require(distance>15,"actual physics travel recorded");recording=false;yield return new WaitForSecondsRealtime(.3f);
    }
    static IEnumerator Observe()
    {
        var hand=player.transform.Find("HeldWeaponPivot");var body=player.GetComponent<Rigidbody2D>();
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            var renderer=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            if(animation.PoseName=="move")
            {
                if(animation.FacingDirection==4)southFrames.Add(animation.FrameIndex);
                if(animation.FacingDirection==0)northFrames.Add(animation.FrameIndex);
                if(animation.FacingDirection==2)eastFrames.Add(animation.FrameIndex);
                badFrame|=animation.FrameIndex<0||animation.FrameIndex>=PlayerAnimationController.MoveFrameCount(animation.FacingDirection);
                distance+=body.linearVelocity.magnitude*Time.deltaTime;scaledRunningSeconds+=Time.deltaTime;
            }
            badFrame|=renderer.sprite.pivot!=new Vector2(24,10)||renderer.sprite.pixelsPerUnit!=32;
            badSocket|=Vector2.Distance(hand.localPosition,animation.HandLocalPosition)>.0001f;
        }
    }
    static IEnumerator Record()
    {
        while(recording)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+frame.ToString("D6")+".png");
            rows.AppendLine(string.Join(",",frame++,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,animation.PoseName,animation.FacingDirection,animation.FrameIndex,animation.HandLocalPosition.x,animation.HandLocalPosition.y,player.transform.position.x,player.transform.position.y,player.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Time.time));
            yield return new WaitForSecondsRealtime(.025f);
        }
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
    }
}
