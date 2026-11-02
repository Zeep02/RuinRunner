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

// Staged visual comparison, not normal campaign or performance evidence.
public static class GruntMotionFixture
{
    static string folder;
    static GameController game;
    static readonly List<EnemyController> actors=new List<EnemyController>();
    static readonly List<SpriteRenderer> old=new List<SpriteRenderer>();
    static readonly StringBuilder rows=new StringBuilder();
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-creature-consistency/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder+"/frames");
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        foreach(var hud in UnityEngine.Object.FindObjectsByType<PrototypeHud>())hud.enabled=false;
        foreach(var cam in UnityEngine.Object.FindObjectsByType<CameraFollow>()){cam.gameObject.SetActive(false);UnityEngine.Object.Destroy(cam.gameObject);}
        Time.timeScale=1;Time.captureFramerate=0;Application.runInBackground=true;
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var camera=CameraFollow.Create();
        var player=PlayerController.Create(new Vector2(-8,-4),()=>true,()=>true,()=>{},camera,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);camera.Target=null;camera.FocusRoom(Vector2.zero,new Vector2(20,11.25f),true);
        actors.Clear();old.Clear();rows.Clear();rows.AppendLine("frame,realtime,phase,body,head,flip,anchorX,anchorY");
        for(var i=0;i<6;i++)
        {
            var actor=EnemyController.Create(EnemyType.Grunt,new Vector2(-7.5f+i*3,-.8f),player,()=>true,_=>{},camera);
            actor.enabled=false;actor.GetComponent<EnemyPixelAnimator>().enabled=false;actors.Add(actor);
            var before=new GameObject("PreviousGrunt-"+i);before.transform.position=new Vector3(-7.5f+i*3,2f,0);before.transform.localScale=Vector3.one*.75f;
            var renderer=before.AddComponent<SpriteRenderer>();renderer.sprite=IndividualArt.Sprite("Meadow/Creatures/Grunt/"+i.ToString("D2"));renderer.sortingOrder=1000;old.Add(renderer);
            Pose(actor,i,false);
        }
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        game.StartCoroutine(Run());return folder;
    }
    static void Pose(EnemyController actor,int pose,bool flip)
    {
        var body=actor.transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
        body.sprite=IndividualArt.Sprite("CreatureMotion/Grunt/frames/"+pose.ToString("D2"));body.flipX=flip;
    }
    static IEnumerator Run()
    {
        yield return new WaitForEndOfFrame();Save("comparison-six-poses.png");
        for(var i=0;i<6;i++)
        {
            var sprite=actors[i].transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;
            if(sprite.pixelsPerUnit!=32||sprite.pivot!=new Vector2(32,8)||sprite.rect.size!=new Vector2(64,64))throw new Exception("Pose canvas/pivot changes");
        }
        var timing=new[]{.38f,.26f,.125f,.125f,.125f,.125f,.22f,.27f,.15f};
        var poses=new[]{0,1,0,1,2,3,4,5,3};
        var sample=0;
        for(var cycle=0;cycle<4;cycle++)for(var p=0;p<poses.Length;p++)
        {
            var flip=cycle%2==1;var phase=p<2?"idle":p<6?"move":p==6?"anticipate":p==7?"charge":"recover";
            foreach(var actor in actors)Pose(actor,poses[p],flip);
            var end=Time.realtimeSinceStartup+timing[p];
            while(Time.realtimeSinceStartup<end)
            {
                yield return new WaitForEndOfFrame();
                var body=actors[0].transform.Find("VisualRoot").GetComponent<SpriteRenderer>();
                if(body.flipX!=flip||Mathf.Abs(body.transform.lossyScale.x-body.transform.lossyScale.y)>.0001f)throw new Exception("Pose aspect/flip failed");
                Save("frames/frame-"+sample.ToString("D6")+".png");
                rows.AppendLine(string.Join(",",sample++,Time.realtimeSinceStartupAsDouble.ToString("F6",CultureInfo.InvariantCulture),phase,poses[p],"baked",flip,body.sprite.pivot.x,body.sprite.pivot.y));
                yield return new WaitForSecondsRealtime(.045f);
            }
        }
        // Real animator on paused, stationary AI: verifies frame clock/pause behavior.
        var chosen=actors[0];chosen.GetComponent<EnemyPixelAnimator>().enabled=true;
        yield return new WaitForSeconds(.75f);
        var selected=chosen.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite;
        Time.timeScale=0;yield return new WaitForSecondsRealtime(.5f);
        if(chosen.transform.Find("VisualRoot").GetComponent<SpriteRenderer>().sprite!=selected)throw new Exception("Pose moved during pause");
        Time.timeScale=1;
        File.WriteAllText(folder+"/recording.csv",rows.ToString());
        File.WriteAllText(folder+"/result.txt","PASS six complete baked Sprite poses, fixed 64px canvas, 32/8 pivot, 32 PPU, aspect, mirrored poses, pause. Head pixel identity checked separately against bake source. Staged animation/pose comparison; visual quality requires captured-frame review. Top row previous art, bottom row adopted candidate. NOT campaign completion or performance evidence.");
        Debug.Log("[Creature Motion] DONE "+folder);
    }
    static void Save(string relative)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(folder+"/"+relative,image.EncodeToPNG());UnityEngine.Object.Destroy(image);
    }
}

