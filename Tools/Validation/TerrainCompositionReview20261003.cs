using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using RuinRunner;
using Newtonsoft.Json.Linq;

// Frozen comparison of production terrain. No gameplay or movement claim.
public static class TerrainCompositionReview20261003
{
    static string folder;
    static GameController game;
    static void Layer(GameObject obj){foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=31;}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-03-terrain-composition/review-"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        Application.runInBackground=true;Time.timeScale=1;game.StartCoroutine(Run());return folder;
    }
    static IEnumerator Run()
    {
        var report=new JArray();
        for(int stage=0;stage<3;stage++)
        {
            UnityEngine.Random.InitState(9100);
            var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,stage);
            var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
            var follow=CameraFollow.Create();var player=PlayerController.Create(map.GetSpawnPoint(0),()=>true,()=>false,()=>{},follow,inventory,new GameSettingsService());player.EquipWeapon(inventory.EquippedWeapon);
            yield return null;Layer(map.Root);Layer(player.gameObject);
            var cameraRoot=new GameObject("Terrain review camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=5.625f;camera.aspect=16f/9;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(18/255f,15/255f,31/255f);camera.allowHDR=false;camera.allowMSAA=false;
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();camera.targetTexture=target;
            try
            {
                for(int room=0;room<map.RoomCount;room++){var point=map.GetRoomCenter(room);player.Teleport(map.GetSpawnPoint(room));Render(camera,target,point,"stage-"+(stage+1)+"-room-"+room);}
                var path=map.Terrain.Connections[0];var middle=path.points[path.points.Length/2];player.Teleport(middle);Render(camera,target,middle,"stage-"+(stage+1)+"-connection");
                var obstacles=new List<Vector2>();for(int r=0;r<map.RoomCount;r++)foreach(var o in map.GetRoom(r).platforms)obstacles.Add(map.GetRoomCenter(r)+o.position);
                if(obstacles.Count>0){player.Teleport(obstacles[0]+Vector2.down*3);Render(camera,target,obstacles[0],"stage-"+(stage+1)+"-obstacle");}
                var visuals=map.Root.GetComponentsInChildren<SpriteRenderer>();
                if(visuals.Any(v=>v.sprite==null))throw new Exception("Missing production sprite");
                var bytes=map.Terrain.Walkable.Select(b=>b?(byte)1:(byte)0).ToArray();string mask;
                using(var sha=SHA256.Create())mask=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                report.Add(new JObject{{"stage",stage},{"walkMaskSha256",mask},{"walkableFineCells",bytes.Count(b=>b!=0)},{"colliders",map.Root.GetComponentsInChildren<Collider2D>().Length},{"ground",visuals.Count(v=>v.name=="Continuous ground")},{"paving",visuals.Count(v=>v.name=="Worn paving")},{"sprites",visuals.Length}});
            }
            catch(Exception e){File.WriteAllText(folder+"/failure.txt",e.ToString(),new UTF8Encoding(false));Debug.LogException(e);}
            finally
            {
                camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(cameraRoot);
                foreach(var obj in new[]{map.Root,player.gameObject,follow.gameObject})UnityEngine.Object.Destroy(obj);
            }
            yield return null;
        }
        File.WriteAllText(folder+"/terrain.json",new JObject{{"scope","Frozen production map and supplied actor at same seed/camera; visual comparison and geometry identity only"},{"seed",9100},{"stages",report}}.ToString(),new UTF8Encoding(false));
        game.enabled=true;Debug.Log("[Terrain Composition] Captured "+folder);
    }
    static void Render(Camera camera,RenderTexture target,Vector2 center,string name)
    {
        camera.transform.position=new Vector3(center.x,center.y,-10);var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
}
