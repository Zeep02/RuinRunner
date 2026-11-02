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

// Staged actors, normal enemy AI/physics/attack clocks, player healed between
// actors. This is visual validation, NOT a normal campaign or performance test.
public static class CreatureGameplayFixture
{
    static string folder;
    static GameController game;
    static PlayerController player;
    static CameraFollow follow;
    static readonly StringBuilder rows=new StringBuilder();
    static readonly List<string> results=new List<string>();
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-creature-gameplay/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var cam in UnityEngine.Object.FindObjectsByType<CameraFollow>()){cam.gameObject.SetActive(false);UnityEngine.Object.Destroy(cam.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        follow=CameraFollow.Create();player=PlayerController.Create(Vector2.zero,()=>Time.timeScale==0,()=>false,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=null;follow.FocusRoom(Vector2.zero,new Vector2(20,11.25f),true);
        rows.Clear();rows.AppendLine("frame,realtime,phase,pose,acting,attackPhase,velocity,flip,health,playerX,playerY,enemyX,enemyY");results.Clear();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartCoroutine(Run());return folder;
    }
    static IEnumerator Run()
    {
        yield return new WaitForSeconds(.25f);var frame=0;
        foreach(var type in new[]{EnemyType.Charger,EnemyType.Grunt,EnemyType.Shooter,EnemyType.Leaper})
        {
            player.Stats.Heal(player.MaxHealth);
            var playerBody=player.GetComponent<Rigidbody2D>();playerBody.position=Vector2.zero;playerBody.linearVelocity=Vector2.zero;player.transform.position=Vector3.zero;
            yield return new WaitForSeconds(.2f);
            var spawn=(Vector2)player.transform.position+new Vector2(type==EnemyType.Shooter?-5:4,0);
            if(Physics2D.Linecast(spawn,player.transform.position,1<<GameLayers.Ground))throw new Exception("Staged spawn lacks clear sight: "+type);
            var actor=EnemyController.Create(type,spawn,player,()=>Time.timeScale==0,_=>{},follow);
            var renderer=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
            var phases=new HashSet<int>();var sprites=new HashSet<string>();var end=Time.time+5.2f;
            while(Time.time<end&&!player.IsDead)
            {
                yield return new WaitForEndOfFrame();
                if(actor.IsActing)phases.Add(actor.AttackVisualPhase);
                sprites.Add(renderer.sprite.name);
                if(renderer.sprite.pixelsPerUnit!=32||renderer.sprite.pivot!=new Vector2(32,8))throw new Exception("Wrong live Sprite metadata: "+type);
                ScreenCapture.CaptureScreenshot(folder+"/frames/frame-"+frame.ToString("D6")+".png");
                rows.AppendLine(string.Join(",",frame++,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),type,renderer.sprite.name,actor.IsActing,actor.AttackVisualPhase,actor.Velocity.magnitude.ToString("F4",CultureInfo.InvariantCulture),renderer.flipX,player.Health.ToString("F2",CultureInfo.InvariantCulture),player.transform.position.x,player.transform.position.y,actor.transform.position.x,actor.transform.position.y));
                yield return new WaitForSecondsRealtime(.025f);
            }
            results.Add(type+": attack phases="+string.Join("/",phases)+", Sprite poses="+string.Join("/",sprites)+", player hp="+player.Health);
            if(phases.Count!=3)results.Add("INCOMPLETE attack phase coverage: "+type);
            var pausedPose=renderer.sprite;Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);
            if(renderer.sprite!=pausedPose)throw new Exception("Live AI pose changed while paused: "+type);
            Time.timeScale=1;
            UnityEngine.Object.Destroy(actor.gameObject);
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<EnemyProjectile>())UnityEngine.Object.Destroy(projectile.gameObject);
            if(player.IsDead){results.Add("FAIL player died before sequence finished");break;}
            yield return new WaitForSeconds(.15f);
        }
        yield return new WaitForSecondsRealtime(.5f);
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
        File.WriteAllLines(folder+"/observations.txt",results);
        File.WriteAllText(folder+"/scope.txt","Staged one enemy at a time. Actual EnemyController AI, physics and attack clocks; player receives damage and is healed/repositioned between actors to keep each encounter in view. No attack phase or pose injection. HUD hidden, isolated stage. Live AI pose freeze checked with timeScale=0 between segments. Not normal campaign completion or performance evidence. Screenshot timestamps preserve actual playback; source sample rate is reported separately. Previous 034335 had cumulative knockback off-camera and missed Shooter attacks; retained as incomplete evidence.");
        Debug.Log("[Creature Gameplay] DONE "+folder);
    }
}
