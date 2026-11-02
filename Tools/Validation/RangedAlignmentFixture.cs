using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Isolated authored geometry and obstruction checks; not a normal campaign run.
public static class RangedAlignmentFixture
{
    static string folder;
    static PlayerController player;
    static InventorySystem inventory;
    static readonly List<string> checks=new List<string>();
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly MethodInfo Fire=typeof(PlayerController).GetMethod("RangedAttack",Private);
    static readonly MethodInfo Special=typeof(PlayerController).GetMethod("TryRangedWeaponAction",Private);
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-ranged-motion/alignment-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.captureFramerate=0;Time.timeScale=1;Application.runInBackground=true;Application.targetFrameRate=60;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();var follow=game.CameraFollow;
        player=PlayerController.Create(Vector2.zero,()=>false,()=>true,()=>{},follow,inventory,new GameSettingsService());
        follow.Target=player.transform;follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartCoroutine(Guard(Run()));return folder;
    }
    static IEnumerator Guard(IEnumerator run)
    {
        while(true)
        {
            bool next;object current=null;
            try{next=run.MoveNext();if(next)current=run.Current;}
            catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL "+e+"\n"+string.Join("\n",checks));Debug.LogException(e);yield break;}
            if(!next)break;yield return current;
        }
        StageValidationInput.ReleaseAll();File.WriteAllText(folder+"/result.txt","PASS "+checks.Count+" isolated geometry/obstruction checks\n"+string.Join("\n",checks));
        Debug.Log("[Ranged Alignment] PASS "+checks.Count+" "+folder);
    }
    static void Require(bool ok,string label){if(!ok)throw new Exception(label);checks.Add("PASS "+label);}
    static PlayerProjectile[] Projectiles()=>UnityEngine.Object.FindObjectsByType<PlayerProjectile>();
    static void ClearShots(){foreach(var p in Projectiles()){p.gameObject.SetActive(false);UnityEngine.Object.Destroy(p.gameObject);}}
    static void Equip(WeaponDefinition weapon){inventory.Reset();inventory.AddWeapon(weapon);player.EquipWeapon(inventory.EquippedWeapon);}
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.4f);
        var animation=player.GetComponent<PlayerAnimationController>();
        var renderer=player.transform.Find("HeldWeaponPivot/HeldWeaponSprite").GetComponent<SpriteRenderer>();
        var velocity=typeof(PlayerProjectile).GetField("velocity",Private);
        foreach(var weapon in WeaponLootTable.All)
        {
            if(weapon.AttackType!=WeaponAttackType.Ranged)continue;
            RangedWeaponArt.TryGet(weapon.Id,out var art);
            for(var direction=0;direction<8;direction++)
            {
                ClearShots();Equip(weapon);player.Teleport(Vector2.zero);
                var aim=new Vector2(Mathf.Sin(direction*Mathf.PI/4),Mathf.Cos(direction*Mathf.PI/4));
                StageValidationInput.SetAim(aim);yield return null;
                Fire.Invoke(player,null);
                var bullets=Projectiles();Require(bullets.Length==(weapon.Family==WeaponFamily.Shotgun?3:1),"expected primary pellets "+weapon.Id+"/"+direction);
                var emission=(Vector2)bullets[0].transform.position;
                var travel=((Vector2)velocity.GetValue(bullets[0])).normalized;
                yield return new WaitForEndOfFrame();
                Require(animation.PoseName=="shoot" && animation.FrameIndex==1,"emission body "+weapon.Id+"/"+direction);
                var sprite=renderer.sprite;
                var localMuzzle=(new Vector2(art.Muzzle.x,sprite.rect.height-art.Muzzle.y)-sprite.pivot)/sprite.pixelsPerUnit;
                if(renderer.flipY)localMuzzle.y=-localMuzzle.y;
                var renderedMuzzle=(Vector2)renderer.transform.TransformPoint(localMuzzle);
                Require(Vector2.Distance(renderedMuzzle,emission)<.002f,"visible muzzle equals actual emission "+weapon.Id+"/"+direction);
                var sourceAxis=new Vector2(Mathf.Cos(art.ForwardAngle*Mathf.Deg2Rad),Mathf.Sin(art.ForwardAngle*Mathf.Deg2Rad));
                if(renderer.flipY)sourceAxis.y=-sourceAxis.y;
                var barrel=((Vector2)renderer.transform.TransformDirection(sourceAxis)).normalized;
                Require(Vector2.Dot(barrel,travel)>(weapon.Family==WeaponFamily.Shotgun?.991f:.9999f),"barrel axis follows projectile travel/spread "+weapon.Id+"/"+direction);
                Require(Vector2.Distance(renderer.transform.TransformPoint(Vector3.zero),player.transform.TransformPoint(animation.HandLocalPosition))<.001f,"grip attaches to glove "+weapon.Id+"/"+direction);
                var ammoBounds=player.transform.Find("AmmoGauge").GetComponentInChildren<SpriteRenderer>().bounds;
                var dash=player.transform.Find("DashCharges").GetComponent<MeshRenderer>().bounds;
                var body=player.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().bounds;
                Require(!Overlap(ammoBounds,renderer.bounds) && !Overlap(dash,renderer.bounds),"resource row clears aimed weapon "+weapon.Id+"/"+direction);
                Require(!Overlap(ammoBounds,body) && !Overlap(dash,body) && !Overlap(ammoBounds,dash),"resource row clears body and itself "+weapon.Id+"/"+direction);
                if(direction==2)Capture(weapon.Id);
                yield return null;
            }
        }
        ClearShots();Equip(WeaponLootTable.FindFirst(WeaponFamily.Shotgun));StageValidationInput.SetAim(Vector2.right);yield return null;
        Special.Invoke(player,null);var fan=Projectiles();Require(fan.Length==7,"shotgun fan contains seven pellets");
        foreach(var pellet in fan)Require(Vector2.Distance(pellet.transform.position,fan[0].transform.position)<.0001f,"fan shares one muzzle origin");
        ClearShots();Equip(WeaponLootTable.FindFirst(WeaponFamily.Pistol));StageValidationInput.SetAim(Vector2.right);yield return null;
        var wall=new GameObject("Fixture wall between hand and muzzle");wall.layer=GameLayers.Ground;
        wall.transform.position=new Vector3(.6f,0,0);var collider=wall.AddComponent<BoxCollider2D>();collider.size=new Vector2(.1f,3);
        Physics2D.SyncTransforms();var ammo=inventory.ActiveSlot.Ammo;Fire.Invoke(player,null);
        Require(Projectiles().Length==0,"muzzle beyond wall cannot shoot through it");Require(inventory.ActiveSlot.Ammo==ammo-1,"blocked shot still consumes one round");
        collider.enabled=false;Physics2D.SyncTransforms();Fire.Invoke(player,null);
        Require(Projectiles().Length==1,"same shot emits after obstruction is removed");
        UnityEngine.Object.Destroy(wall);ClearShots();
    }
    static void Capture(string weapon)
    {
        Type.GetType("UnityEngine.ScreenCapture,UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{folder+"/"+weapon+".png",1});
    }
    static bool Overlap(Bounds a,Bounds b)=>a.min.x<b.max.x && a.max.x>b.min.x && a.min.y<b.max.y && a.max.y>b.min.y;
}
