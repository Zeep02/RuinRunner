using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Isolated visual/geometry check. Does not advance campaign or grant rewards.
public static class ActorAspectFixture
{
    static string folder;
    static readonly List<EnemyController> actors=new List<EnemyController>();
    static readonly StringBuilder rows=new StringBuilder("type,rootX,rootY,visualX,visualY\n");
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-actor-aspect/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var old in UnityEngine.Object.FindObjectsByType<CameraFollow>()){old.gameObject.SetActive(false);UnityEngine.Object.Destroy(old.gameObject);}
        typeof(StageValidationInput).GetField("initialized",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        typeof(StageValidationInput).GetField("enabled",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,true);
        StageValidationInput.ReleaseAll();Time.timeScale=1;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();
        var player=PlayerController.Create(new Vector2(-7,-4),()=>true,()=>true,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=null;follow.FocusRoom(Vector2.zero,new Vector2(20,11.25f),true);
        actors.Clear();rows.Clear();rows.AppendLine("type,rootX,rootY,visualX,visualY");
        var index=0;
        foreach(EnemyType type in Enum.GetValues(typeof(EnemyType)))
        {
            var actor=EnemyController.Create(type,new Vector2(-7+index%5*3.5f,index<5?1.6f:-2.6f),player,()=>true,_=>{},follow);
            var visual=actor.transform.Find("VisualRoot");
            actors.Add(actor);
            var scale=visual.lossyScale;var root=actor.transform.localScale;
            rows.AppendLine(type+","+root.x+","+root.y+","+scale.x+","+scale.y);
            if(Mathf.Abs(scale.x-scale.y)>.0001f)throw new Exception("Stretched sprite "+type);
            // SetParent's default worldPositionStays preserves world scale.
            // Root gameplay dimensions do not stretch the visual child.
            if(Mathf.Abs(scale.x-actor.PresentationScale)>.0001f)throw new Exception("Artwork display scale changed "+type);
            index++;
        }
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartCoroutine(Capture());return folder;
    }
    static IEnumerator Capture()
    {
        yield return new WaitForEndOfFrame();
        Save("actual-actor-scales.png");
        File.WriteAllText(folder+"/scales.csv",rows.ToString());
        var result="PASS "+actors.Count+" enemy types preserve sprite aspect and authored PresentationScale. Original SetParent(worldPositionStays=true) already cancels root stretch. No enemy scale correction adopted. Isolated frozen composition; not normal campaign or performance evidence.";
        File.WriteAllText(folder+"/result.txt",result);Debug.Log("[Actor Aspect] "+result+" "+folder);
    }
    static void Save(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+name,image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
}
