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

// Real-time isolated input/visual regression. Equipment/positions are fixtures,
// never normal campaign completion or performance evidence.
public static class RangedMotionFixture
{
    static string folder,phase;
    static PlayerController player;
    static PlayerAnimationController animation;
    static InventorySystem inventory;
    static CameraFollow follow;
    static bool paused,recording;
    static int captured;
    static readonly List<string> checks=new List<string>();
    static readonly StringBuilder frames=new StringBuilder("frame,realtime,phase,weapon,pose,direction,bodyFrame,progress,aimX,aimY,handX,handY,ammo\n");
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-20-ranged-motion/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder+"/frames");
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();
        Time.captureFramerate=0;Time.timeScale=1;Application.runInBackground=true;Application.targetFrameRate=60;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Pistol));
        follow=CameraFollow.Create();
        player=PlayerController.Create(Vector2.zero,()=>paused,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        animation=player.GetComponent<PlayerAnimationController>();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        recording=true;phase="ready";game.StartCoroutine(Record());game.StartCoroutine(Guard(Run()));
        return folder+"; real-time fixture started, no campaign completion evidence";
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool next;object current=null;
            try {next=run.MoveNext();if(next)current=run.Current;}
            catch(Exception error){File.WriteAllText(folder+"/result.txt","FAIL "+error+"\n"+string.Join("\n",checks));Debug.LogException(error);recording=false;yield break;}
            if(!next)break;yield return current;
        }
        recording=false;StageValidationInput.ReleaseAll();Time.timeScale=1;paused=false;
        File.WriteAllText(folder+"/result.txt","PASS "+checks.Count+" assertions. Isolated positions/equipment, not normal completion.\n"+string.Join("\n",checks));
        Debug.Log("[Ranged Motion] PASS "+checks.Count+" "+folder);
    }
    static void Require(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static void Equip(WeaponFamily family)
    {
        StageValidationInput.ReleaseAll();inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
    }
    static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.4f);
        var body=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        var hand=player.transform.Find("HeldWeaponPivot");
        for(var dir=0;dir<8;dir++)
        {
            Equip(WeaponFamily.Pistol);phase="direction-"+dir;player.Teleport(Vector2.zero);
            var aim=new Vector2(Mathf.Sin(dir*Mathf.PI/4),Mathf.Cos(dir*Mathf.PI/4));
            StageValidationInput.SetAim(aim);yield return new WaitForSeconds(.25f);
            Require(animation.PoseName=="idle" && animation.FrameIndex>=0 && animation.FrameIndex<6 && animation.FacingDirection==dir,"breathing ready pose "+dir);
            var target=EnemyController.Create(EnemyType.Boss,aim*2.8f,player,()=>true,_=>{},follow);target.enabled=false;
            var health=target.Health;var ammo=inventory.ActiveSlot.Ammo;var sequence=player.AttackAnimationSequence;
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);
            yield return null;yield return new WaitForEndOfFrame();
            Require(inventory.ActiveSlot.Ammo==ammo-1 && player.AttackAnimationSequence==sequence+1,"one immediate shot/ammo "+dir);
            Require(player.IsRangedShotActive && animation.PoseName=="shoot" && animation.FrameIndex==1,"authored emission "+dir);
            Require(Vector2.Distance(hand.localPosition,animation.HandLocalPosition)<.0001f,"weapon attached to authored glove "+dir);
            Require(body.sprite.pivot==new Vector2(24,10) && player.transform.Find("VisualRoot").localScale==Vector3.one,"fixed canvas anchor/scale "+dir);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);StageValidationInput.SetAim(-aim);
            yield return null;yield return new WaitForEndOfFrame();
            Require(Vector2.Dot(player.WeaponAimDirection,aim)>.999f && animation.FacingDirection==dir,"shot direction committed while mouse changes "+dir);
            yield return new WaitForSeconds(.30f);
            Require(target.Health<health,"projectile deals damage "+dir);
            Require(animation.PoseName=="idle","recovers to breathing ready without guard "+dir);
            UnityEngine.Object.Destroy(target.gameObject);
        }
        foreach(var family in new[]{WeaponFamily.Bow,WeaponFamily.Crossbow,WeaponFamily.Pistol,WeaponFamily.Rifle,WeaponFamily.Shotgun,WeaponFamily.Cannon})
        {
            Equip(family);phase="special-"+family;player.Teleport(Vector2.zero);StageValidationInput.SetAim(Vector2.right);
            yield return new WaitForSeconds(.85f);
            var mana=player.Mana;var ammo=inventory.ActiveSlot.Ammo;
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,true);
            yield return null;yield return new WaitForEndOfFrame();
            Require(player.IsRangedShotActive && animation.PoseName=="shoot","special uses shot body "+family);
            Require(player.Mana<mana && inventory.ActiveSlot.Ammo==ammo,"special mana rule unchanged "+family);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Guard,false);
            var until=Time.time+.4f;
            while(Time.time<until){yield return null;yield return new WaitForEndOfFrame();Require(animation.PoseName!="guard","no special-to-melee guard flash "+family);}
        }
        Equip(WeaponFamily.Pistol);phase="moving-shots";StageValidationInput.SetAim(Vector2.right);
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.MoveUp,true);StageValidationInput.SetHeld(GameAction.Attack,true);
        yield return new WaitForSeconds(.7f);
        Require(player.GetComponent<Rigidbody2D>().linearVelocity.y>1,"moving shot retains responsive movement");
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);StageValidationInput.SetHeld(GameAction.MoveUp,false);
        yield return new WaitForSeconds(.5f);
        phase="pause-shot";StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);
        yield return null;yield return new WaitForEndOfFrame();
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);paused=true;Time.timeScale=0;
        var progress=player.AttackAnimationProgress;var sprite=body.sprite;var socket=hand.localPosition;
        yield return new WaitForSecondsRealtime(.3f);
        Require(player.AttackAnimationProgress==progress && body.sprite==sprite && hand.localPosition==socket,"pause freezes body/weapon/clock together");
        paused=false;Time.timeScale=1;yield return new WaitForSeconds(.5f);
        phase="dash-cancel";StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);
        yield return null;yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);StageValidationInput.SetHeld(GameAction.Dash,true);
        yield return null;yield return new WaitForEndOfFrame();
        Require(!player.IsRangedShotActive && animation.PoseName=="dash","dash cancels shot body immediately");
        StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Dash,false);yield return new WaitForSeconds(.55f);
        phase="weapon-switch";StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);
        yield return null;yield return new WaitForEndOfFrame();StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);
        inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));player.EquipWeapon(inventory.SelectQuickSlot(1));
        yield return null;yield return new WaitForEndOfFrame();Require(!player.IsRangedShotActive && animation.PoseName!="shoot","weapon switch clears shot pose");
        Equip(WeaponFamily.Pistol);phase="hurt";yield return new WaitForSeconds(1.1f);player.TakeDamage(1,Vector2.zero);
        yield return null;yield return new WaitForEndOfFrame();Require(animation.PoseName=="hurt","ranged readiness yields to hurt");
        yield return new WaitForSeconds(1.1f);phase="death";player.TakeDamage(10000,Vector2.zero);
        yield return new WaitForSecondsRealtime(.95f);Require(animation.PoseName=="death" && animation.FrameIndex==4,"ranged death reaches final authored pose");
    }
    static IEnumerator Record()
    {
        var capture=Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)});
        while(recording)
        {
            yield return new WaitForEndOfFrame();
            var frame=captured++;capture.Invoke(null,new object[]{folder+"/frames/frame-"+frame.ToString("D6")+".png",1});
            var aim=player.WeaponAimDirection;var hand=animation.HandLocalPosition;
            frames.AppendLine(string.Join(",",frame,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,player.EquippedWeapon?.Id,animation.PoseName,animation.FacingDirection,animation.FrameIndex,player.AttackAnimationProgress.ToString("F4",CultureInfo.InvariantCulture),aim.x,aim.y,hand.x,hand.y,inventory.ActiveSlot.Ammo));
            yield return new WaitForSecondsRealtime(.035f);
        }
        File.WriteAllText(folder+"/recording.csv",frames.ToString());
    }
}
