using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

public static class RangedEffectSizeFixture
{
    static string folder;
    static InventorySystem inventory;
    static PlayerController player;
    static DungeonMap map;
    static StringBuilder report=new StringBuilder();
    public static string Main()
    {
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-ranged-fx/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        if(game.CameraFollow!=null){game.CameraFollow.enabled=false;game.CameraFollow.GetComponent<Camera>().enabled=false;game.CameraFollow.GetComponent<AudioListener>().enabled=false;}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;
        map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        inventory=new InventorySystem();var follow=CameraFollow.Create();
        player=PlayerController.Create(map.GetRoomCenter(3),()=>false,()=>true,()=>{},follow,inventory,new GameSettingsService());follow.Target=player.transform;
        follow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        follow.StartCoroutine(Run());return folder;
    }
    static IEnumerator Run()
    {
        foreach(var family in new[]{WeaponFamily.Bow,WeaponFamily.Crossbow,WeaponFamily.Pistol,WeaponFamily.Shotgun,WeaponFamily.Rifle,WeaponFamily.Cannon})
        {
            inventory.Reset();inventory.AddWeapon(WeaponLootTable.FindFirst(family));player.EquipWeapon(inventory.EquippedWeapon);
            player.Teleport(map.GetRoomCenter(3)+Vector2.left*3);StageValidationInput.SetAim(Vector2.right);
            yield return new WaitForSeconds(.4f);var ammo=inventory.ActiveSlot.Ammo;
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,true);yield return null;
            report.AppendLine("TRACE "+family+" after input t="+Time.time+" dt="+Time.deltaTime+" shots="+UnityEngine.Object.FindObjectsByType<PlayerProjectile>().Length);
            StageValidationInput.BeginFrame();StageValidationInput.SetHeld(GameAction.Attack,false);
            yield return new WaitForSeconds(.07f);yield return new WaitForEndOfFrame();
            report.AppendLine("TRACE "+family+" capture t="+Time.time+" dt="+Time.deltaTime);
            var shots=UnityEngine.Object.FindObjectsByType<PlayerProjectile>();
            if(shots.Length!=(family==WeaponFamily.Shotgun?3:1)||inventory.ActiveSlot.Ammo!=ammo-1){Finish("FAIL "+family+" one-round primary pellets: shots="+shots.Length+" ammo="+inventory.ActiveSlot.Ammo+" before="+ammo);yield break;}
            var trail=shots[0].GetComponent<TrailRenderer>();
            var collider=shots[0].GetComponent<BoxCollider2D>();
            if(Vector2.Distance(collider.bounds.center,shots[0].transform.position)>.01f){Finish("FAIL "+family+" collision detached from projectile away from world origin");yield break;}
            if(trail==null||trail.positionCount<2){Finish("FAIL "+family+" visible travel trail");yield break;}
            report.AppendLine("PASS "+family+" single projectile/ammo; trail="+trail.positionCount+" width="+trail.startWidth+" visual="+shots[0].GetComponentInChildren<SpriteRenderer>().bounds.size);
            ScreenCapture.CaptureScreenshot(folder+"/"+family+".png");
            yield return new WaitForSeconds(.4f);foreach(var shot in UnityEngine.Object.FindObjectsByType<PlayerProjectile>())UnityEngine.Object.Destroy(shot.gameObject);
        }
        Finish("PASS");
    }
    static void Finish(string result){StageValidationInput.ReleaseAll();File.WriteAllText(folder+"/result.txt",result+"\n"+report+"\nIsolated staged equipment; real single-click input. Screenshots are for visual review, not performance or normal campaign evidence.");Debug.Log("[Ranged effect size] "+result);}
}
