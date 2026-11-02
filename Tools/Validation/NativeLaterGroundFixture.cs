using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Production terrain and source sprites, staged actors. Not campaign or performance evidence.
public static class NativeLaterGroundFixture
{
    static string folder;
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-native-floors/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        Time.timeScale=1;Application.runInBackground=true;game.StartCoroutine(Run());return folder;
    }
    static void Layer(GameObject obj){foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=31;}
    static IEnumerator Run()
    {
        var report=new StringBuilder("Production stages 1/2, staged actors, 3x native pixel review. Not normal campaign or performance.\n");
        for(var stage=1;stage<3;stage++)
        {
            var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,stage);
            var center=map.GetRoomCenter(3);var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Bow));
            var follow=CameraFollow.Create();
            var player=PlayerController.Create(center+new Vector2(-1,-1.5f),()=>true,()=>false,()=>{},follow,inventory,new GameSettingsService());
            player.EquipWeapon(inventory.EquippedWeapon);
            var boss=EnemyController.Create(CampaignStage.Get(stage).BossType,center+new Vector2(2,1),player,()=>true,_=>{},follow);
            var grunt=EnemyController.Create(stage==1?EnemyType.Tidecaller:EnemyType.Sentinel,center+new Vector2(-4,1),player,()=>true,_=>{},follow);
            var chest=RewardChest.Create(center+new Vector2(3,-2),()=>{},new GameSettingsService());
            yield return null;
            var owned=new[]{map.Root,player.gameObject,boss.gameObject,grunt.gameObject,chest.gameObject,follow.gameObject};
            foreach(var obj in owned)Layer(obj);
            var cameraRoot=new GameObject("Native terrain review camera");var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=5.625f;camera.aspect=16f/9;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(18/255f,15/255f,31/255f);
            camera.allowHDR=false;camera.allowMSAA=false;
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){filterMode=FilterMode.Point,antiAliasing=1};target.Create();camera.targetTexture=target;
            try
            {
                var prefix="stage-"+(stage+1)+"-";
                Render(camera,target,center,prefix+"01-guardian-court");
                var entry=map.GetRoomCenter(0);player.Teleport(entry);Render(camera,target,entry+Vector2.up*3,prefix+"02-entry-wall");
                var room=map.GetRoomCenter(1);player.Teleport(room);Render(camera,target,room,prefix+"03-combat-clearing");
                var paths=map.Terrain.Connections;
                if(paths.Length>0){var p=paths[0].points[paths[0].points.Length/2];player.Teleport(p);Render(camera,target,p,prefix+"04-connecting-path");}
                var obstacle=map.Root.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(s=>s.name=="FloodedCistern"||s.name=="CoreCluster");
                if(obstacle!=null){var p=(Vector2)obstacle.transform.position;player.Teleport(p+Vector2.down*2);Render(camera,target,p,prefix+"05-obstacle");}
                var visuals=map.Root.GetComponentsInChildren<SpriteRenderer>();
                var ground=visuals.Where(s=>s.name=="Continuous ground"||s.name=="Floor contact underlay").ToArray();
                if(ground.Length==0)throw new Exception("Missing terrain");
                var source=stage==1?"External/RafaelRuins/":"External/FoozleDungeon/";
                foreach(var view in ground)
                {
                    if(view.sprite==null||!AssetDatabase.GetAssetPath(view.sprite).Contains(source))throw new Exception("Old or missing ground "+view.name);
                    if(view.sprite.pixelsPerUnit!=32||view.sprite.rect.size!=new Vector2(32,32)||view.sprite.texture.filterMode!=FilterMode.Point)
                        throw new Exception("Incorrect native tile import "+view.sprite.name);
                    if(view.color!=NativeStageGround.Tint(stage))throw new Exception("Underlay tint mismatch");
                }
                var grout=map.Root.GetComponentsInChildren<MeshRenderer>().Where(m=>m.name=="Native stone grout").ToArray();
                if(grout.Length!=0)throw new Exception("Retired grout mesh remains");
                foreach(var sprite in ground.Select(s=>s.sprite).Distinct())
                {
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(sprite));
                    if(texture.GetPixels32().Any(pixel=>pixel.a!=255))throw new Exception("Floor has holes into scene void");
                }
                foreach(var view in visuals)if(view.sprite==null)throw new Exception("Missing sprite "+view.name);
                report.AppendLine("PASS stage "+(stage+1)+": ground/underlay="+ground.Length+", distinct native floor cells="+ground.Select(s=>s.sprite).Distinct().Count()+", grout meshes="+grout.Length);
            }
            catch(Exception e){report.AppendLine("FAIL stage "+(stage+1)+": "+e);Debug.LogException(e);}
            finally
            {
                camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(cameraRoot);
                foreach(var obj in owned)UnityEngine.Object.Destroy(obj);
            }
            yield return null;
        }
        File.WriteAllText(folder+"/result.txt",report.ToString(),Encoding.UTF8);Debug.Log("[Native terrain review] "+folder);
    }
    static void Render(Camera camera,RenderTexture target,Vector2 center,string name)
    {
        camera.transform.position=new Vector3(center.x,center.y,-10);var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
}
