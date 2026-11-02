using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Current production renderer, frozen review. Not a normal campaign completion.
public static class CainosEnvironmentFixture
{
    static string folder;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-cainos/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        Time.timeScale=1;Application.runInBackground=true;game.StartCoroutine(Run());return folder;
    }
    static void Layer(GameObject obj){foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=31;}
    static IEnumerator Run()
    {
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var center=map.GetRoomCenter(3);var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();var player=PlayerController.Create(center+new Vector2(-1,-1.5f),()=>true,()=>false,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);
        var boss=EnemyController.Create(EnemyType.Boss,center+new Vector2(2,1),player,()=>true,_=>{},follow);
        var grunt=EnemyController.Create(EnemyType.Grunt,center+new Vector2(-4,1),player,()=>true,_=>{},follow);
        var charger=EnemyController.Create(EnemyType.Charger,center+new Vector2(-6,1),player,()=>true,_=>{},follow);
        var chest=RewardChest.Create(center+new Vector2(3,-2),()=>{},new GameSettingsService());
        yield return null;
        foreach(var obj in new[]{map.Root,player.gameObject,boss.gameObject,grunt.gameObject,charger.gameObject,chest.gameObject})Layer(obj);
        var cameraRoot=new GameObject("Environment review camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=5.625f;camera.aspect=16f/9;camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(18/255f,15/255f,31/255f);camera.allowHDR=false;camera.allowMSAA=false;
        var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();camera.targetTexture=target;
        var report=new StringBuilder("Current production stage0. Frozen/staged actors. Not normal campaign, motion, performance or final approval.\n");
        try
        {
            Render(camera,target,center,"01-guardian-court");
            var entry=map.GetRoomCenter(0);player.Teleport(entry);Render(camera,target,entry+Vector2.up*3,"02-entry-wall");
            var r=map.GetRoomCenter(1);player.Teleport(r);Render(camera,target,r,"03-combat-clearing");
            var paths=map.Terrain.Connections;
            if(paths.Length>0){var p=paths[0].points[paths[0].points.Length/2];player.Teleport(p);Render(camera,target,p,"04-connecting-path");}
            var island=map.Root.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(s=>s.name=="Ruined plinth top");
            if(island!=null){var p=(Vector2)island.transform.position;player.Teleport(p+Vector2.down*2);Render(camera,target,p,"05-obstacle");}
            var visuals=map.Root.GetComponentsInChildren<SpriteRenderer>();
            var ground=visuals.Where(s=>s.name=="Continuous ground"||s.name=="Floor contact underlay").ToArray();
            foreach(var view in ground)
                if(view.sprite==null||!AssetDatabase.GetAssetPath(view.sprite).Contains("External/Cainos/"))throw new Exception("Old or missing production ground "+view.name);
            var props=visuals.Where(s=>s.name.StartsWith("Ruins ")).ToArray();var pairs=0;
            for(var i=0;i<props.Length;i++)for(var j=i+1;j<props.Length;j++)
            {if(props[i].bounds.Intersects(props[j].bounds))throw new Exception("Overlapping props "+props[i].name+" / "+props[j].name);pairs++;}
            foreach(var view in visuals)if(view.sprite==null)throw new Exception("Missing sprite "+view.name);
            report.AppendLine("PASS individual native32PPU source paths; ground/underlay="+ground.Length+" paving="+visuals.Count(s=>s.name=="Worn paving")+" props="+props.Length+" nonoverlap pairs="+pairs);
            report.AppendLine("No new movement/collision mask logic. Inspect five images for style, joins, relative scale and obstacle readability.");
        }
        catch(Exception e){report.AppendLine("FAIL "+e);Debug.LogException(e);}
        finally
        {
            File.WriteAllText(folder+"/result.txt",report.ToString());camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(cameraRoot);
            foreach(var obj in new[]{map.Root,player.gameObject,boss.gameObject,grunt.gameObject,charger.gameObject,chest.gameObject,follow.gameObject})UnityEngine.Object.Destroy(obj);
        }
        Debug.Log("[Cainos review] "+folder);
    }
    static void Render(Camera camera,RenderTexture target,Vector2 center,string name)
    {
        camera.transform.position=new Vector3(center.x,center.y,-10);var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
}
