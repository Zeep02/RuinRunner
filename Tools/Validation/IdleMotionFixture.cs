using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Isolated visual/input fixture. Equipment and positions are staged, never
// represented as campaign completion. Real frame time, no captureFramerate.
public static class IdleMotionFixture
{
    static string folder, phase;
    static PlayerController player;
    static PlayerAnimationController animation;
    static InventorySystem inventory;
    static bool paused;
    static readonly List<string> checks=new List<string>();
    static readonly StringBuilder samples=new StringBuilder("realtime,scaledTime,phase,pose,direction,frame,handX,handY\n");
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-idle/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder+"/frames");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();
        player=PlayerController.Create(Vector2.zero,()=>paused,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;
        follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
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
        Finish("PASS "+checks.Count+" assertions; isolated visual/input fixture, not campaign completion");
    }
    static void Finish(string result)
    {
        StageValidationInput.ReleaseAll();Time.timeScale=1;paused=false;
        File.WriteAllText(folder+"/samples.csv",samples.ToString());
        File.WriteAllText(folder+"/result.txt",result+"\n"+string.Join("\n",checks));Debug.Log("[Idle Motion] "+result+" "+folder);
    }
    static void Require(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
    static void Sample()
    {
        samples.AppendLine(string.Join(",",Time.realtimeSinceStartup,Time.time,phase,animation.PoseName,
            animation.FacingDirection,animation.FrameIndex,animation.HandLocalPosition.x,animation.HandLocalPosition.y));
    }
    static void Capture(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(folder+"/frames/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);
        var visual=player.transform.Find("VisualRoot");var body=visual.GetComponent<SpriteRenderer>();
        var hand=player.transform.Find("HeldWeaponPivot");var initialOffset=visual.localPosition;
        foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Pistol})
        {
            inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            for(var dir=0;dir<8;dir++)
            {
                phase=family+"-idle-"+dir;
                StageValidationInput.SetAim(new Vector2(Mathf.Sin(dir*Mathf.PI/4),Mathf.Cos(dir*Mathf.PI/4)));
                yield return null;yield return new WaitForEndOfFrame();
                var seen=new HashSet<int>();var unique=new HashSet<Sprite>();var until=Time.time+PlayerAnimationController.IdleCycleSeconds+.2f;
                var position=player.transform.position;var badSocket=false;var badTransform=false;
                while(Time.time<until)
                {
                    yield return null;yield return new WaitForEndOfFrame();Sample();
                    Require(animation.PoseName=="idle" && animation.FacingDirection==dir,"idle direction/state "+phase);
                    unique.Add(body.sprite);
                    if(seen.Add(animation.FrameIndex))Capture(phase+"-"+animation.FrameIndex);
                    badSocket|=Vector2.Distance(hand.localPosition,animation.HandLocalPosition)>.0001f;
                    badTransform|=visual.localScale!=Vector3.one || visual.localPosition!=initialOffset || player.transform.position!=position;
                }
                Require(seen.Count==6 && unique.Count==6,"six actual sprite poses "+phase);
                Require(!badSocket && !badTransform,"fixed feet/scale and attached weapon "+phase);
                Require(body.sprite.pivot==new Vector2(24,10) && body.sprite.rect.size==new Vector2(48,48),"canvas/pivot "+phase);
            }
        }
        phase="pause";paused=true;var frozen=body.sprite;var frozenHand=hand.localPosition;
        yield return new WaitForSecondsRealtime(.7f);
        Require(body.sprite==frozen && hand.localPosition==frozenHand,"pause freezes both body and weapon");paused=false;
        phase="scaled-clock";Time.timeScale=.25f;
        var scaledStart=Time.time;var frameSet=new HashSet<int>();var realUntil=Time.realtimeSinceStartup+1.6f;
        while(Time.realtimeSinceStartup<realUntil){yield return null;yield return new WaitForEndOfFrame();frameSet.Add(animation.FrameIndex);Sample();}
        Require(Time.time-scaledStart<.65f && frameSet.Count<=3,"breathing follows scaled simulation clock");Time.timeScale=1;
        foreach(var family in new[]{WeaponFamily.Longsword,WeaponFamily.Dagger,WeaponFamily.Katana,WeaponFamily.Greatsword,WeaponFamily.Pistol,WeaponFamily.Bow})
        {
            inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            StageValidationInput.ReleaseAll();yield return new WaitForSeconds(1f);phase=family+"-interrupt";
            var sequence=player.AttackAnimationSequence;
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);
            yield return null;yield return new WaitForEndOfFrame();Sample();
            Require(player.AttackAnimationSequence>sequence && animation.PoseName!="idle","attack immediately interrupts idle "+family);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);
            yield return new WaitForSeconds(1f);
            Require(animation.PoseName=="idle","attack returns to breathing "+family);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.MoveRight,true);
            yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();Sample();
            Require(animation.PoseName=="move","movement interrupts idle "+family);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.MoveRight,false);
            yield return new WaitForSeconds(.25f);
            Require(animation.PoseName=="idle","brake returns to breathing "+family);
        }
        phase="dash";yield return new WaitForSeconds(.5f);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,true);
        yield return null;yield return new WaitForEndOfFrame();Sample();
        Require(animation.PoseName=="dash","dash immediately interrupts idle");
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,false);
        yield return new WaitForSeconds(.8f);Require(animation.PoseName=="idle","dash returns to idle");
        yield return new WaitForEndOfFrame();Capture("final");
    }
}
