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
public static class ExternalHeroFixture
{
    static string folder,phase="settle";static bool recording,paused;static int frame;
    static GameController game;static PlayerController player;static PlayerAnimationController animation;static InventorySystem inventory;
    static readonly StringBuilder rows=new StringBuilder();static readonly HashSet<int> southFrames=new HashSet<int>();
    static readonly HashSet<int> northFrames=new HashSet<int>();
    static readonly HashSet<int> eastFrames=new HashSet<int>();
    static readonly HashSet<int>[] seen=new HashSet<int>[8];static Vector2 origin;
    static readonly List<string> checks=new List<string>();static bool badSocket,badFrame;static float distance,scaledRunningSeconds;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-22-external-hero/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));var follow=CameraFollow.Create();
        origin=map.GetRoomCenter(3);
        player=PlayerController.Create(origin,()=>paused,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        rows.Clear();rows.AppendLine("frame,realtime,phase,pose,direction,bodyFrame,handX,handY,playerX,playerY,velocity,scaledTime,sprite,flipX");
        frame=0;southFrames.Clear();northFrames.Clear();eastFrames.Clear();checks.Clear();badSocket=false;badFrame=false;distance=0;scaledRunningSeconds=0;paused=false;
        for(var i=0;i<8;i++)seen[i]=new HashSet<int>();
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
        var summary=new StringBuilder(status);for(var i=0;i<8;i++)summary.AppendLine("\nDirection "+i+": "+string.Join("/",seen[i]));
        summary.AppendLine("Running distance="+distance+"; scaled seconds="+scaledRunningSeconds+"; eight input directions, six original run poses, four world units per cycle. Source art has two mirrored facings, not eight separate art views.");File.WriteAllText(folder+"/result.txt",summary.ToString());
        File.WriteAllText(folder+"/scope.txt","Isolated boss arena, staged sword/pistol, no enemies, HUD hidden. Player repositioned between individual-direction segments for coverage. Continuous clockwise segments use uninterrupted actual GameAction input with no repositioning. Native source animation, 64PPU; no generated body fallback. Dedicated dash/hurt/death source art is absent; dash uses running poses and death fades the last pose. Not normal campaign completion/performance evidence or whole-art approval.");
        Debug.Log("[External Hero] "+status+" "+folder);
    }
    static void Move(GameAction action,bool held){StageValidationInput.BeginFrame();StageValidationInput.SetHeld(action,held);}
    static void Direction(int direction)
    {
        var v=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));StageValidationInput.BeginFrame();
        StageValidationInput.SetHeld(GameAction.MoveRight,v.x>.1f);StageValidationInput.SetHeld(GameAction.MoveLeft,v.x<-.1f);
        StageValidationInput.SetHeld(GameAction.MoveUp,v.y>.1f);StageValidationInput.SetHeld(GameAction.MoveDown,v.y<-.1f);StageValidationInput.SetAim(v);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);recording=true;game.StartCoroutine(Record());game.StartCoroutine(Observe());
        foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Pistol})
        {
            StageValidationInput.ReleaseAll();inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            foreach(var aim in new[]{Vector2.right,Vector2.left})
            {
                phase=family+"-idle-"+aim.x;StageValidationInput.SetAim(aim);yield return new WaitForSeconds(.1f);
                var idleSeen=new HashSet<Sprite>();var until=Time.time+1.8f;
                var position=player.transform.position;
                while(Time.time<until)
                {
                    yield return new WaitForEndOfFrame();
                    Require(animation.PoseName=="idle","native idle state");
                    var sr=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();idleSeen.Add(sr.sprite);
                    Require(sr.flipX==(aim.x<0),"idle facing");
                    Require(player.transform.position==position,"idle does not slide feet");
                }
                Require(idleSeen.Count==8,"eight original idle poses "+family+"/"+aim.x);
            }
            for(var dir=0;dir<8;dir++)
            {
                phase=family+"-reset-"+dir;StageValidationInput.ReleaseAll();player.GetComponent<Rigidbody2D>().position=origin;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                yield return new WaitForSeconds(.2f);phase=family+"-direction-"+dir;Direction(dir);yield return new WaitForSeconds(1.15f);
                phase=family+"-stop-"+dir;StageValidationInput.ReleaseAll();yield return new WaitForSeconds(.2f);
            }
            phase=family+"-circle-reset";player.GetComponent<Rigidbody2D>().position=origin;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;yield return new WaitForSeconds(.3f);
            for(var dir=0;dir<8;dir++){phase=family+"-continuous-turn-"+dir;Direction(dir);yield return new WaitForSeconds(.35f);}
            phase=family+"-moving-attack";Move(GameAction.Attack,true);yield return new WaitForSeconds(.3f);StageValidationInput.ReleaseAll();
            yield return new WaitForSeconds(.3f);phase=family+"-dash";Move(GameAction.Dash,true);yield return null;Move(GameAction.Dash,false);yield return new WaitForSeconds(.6f);
            phase=family+"-pause";paused=true;Time.timeScale=0;
            var sprite=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;var hand=animation.HandLocalPosition;
            yield return new WaitForSecondsRealtime(.3f);
            Require(sprite==player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite&&hand==animation.HandLocalPosition,"pause body/hand "+family);
            paused=false;Time.timeScale=1;phase=family+"-settle";yield return new WaitForSeconds(.6f);
        }
        for(var dir=0;dir<8;dir++)Require(seen[dir].Count==6,"all six actual-input poses direction "+dir);
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
                seen[animation.FacingDirection].Add(animation.FrameIndex);
                if(animation.FacingDirection==4)southFrames.Add(animation.FrameIndex);
                if(animation.FacingDirection==0)northFrames.Add(animation.FrameIndex);
                if(animation.FacingDirection==2)eastFrames.Add(animation.FrameIndex);
                badFrame|=animation.FrameIndex<0||animation.FrameIndex>=PlayerAnimationController.MoveFrameCount(animation.FacingDirection);
                distance+=body.linearVelocity.magnitude*Time.deltaTime;scaledRunningSeconds+=Time.deltaTime;
            }
            badFrame|=Vector2.Distance(renderer.sprite.pivot,ExternalHeroArt.Pivot)>.001f||renderer.sprite.pixelsPerUnit!=64||!AssetDatabase.GetAssetPath(renderer.sprite).Contains("/External/PixelFrog/Pawn/");
            badSocket|=Vector2.Distance(hand.localPosition,animation.HandLocalPosition)>.0001f;
        }
    }
    static IEnumerator Record()
    {
        while(recording)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+frame.ToString("D6")+".png");
            rows.AppendLine(string.Join(",",frame++,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,animation.PoseName,animation.FacingDirection,animation.FrameIndex,animation.HandLocalPosition.x,animation.HandLocalPosition.y,player.transform.position.x,player.transform.position.y,player.GetComponent<Rigidbody2D>().linearVelocity.magnitude,Time.time,AssetDatabase.GetAssetPath(player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite),player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().flipX));
            yield return new WaitForSecondsRealtime(.025f);
        }
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
    }
}
