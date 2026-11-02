using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Frozen, explicitly positioned visual inspection. Never normal completion evidence.
public static class EnvironmentVisualFixture
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string folder;
    static void Set(GameController game,string name,object value)=>typeof(GameController).GetProperty(name).SetValue(game,value);
    static void Clear()=>typeof(GameController).Assembly.GetType("RuinRunner.RuntimeSceneObjects").GetMethod("ClearSession").Invoke(null,null);
    static void Capture(string name)=>Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule").GetMethod("CaptureScreenshot",new[]{typeof(string),typeof(int)}).Invoke(null,new object[]{folder+"/"+name+".png",1});
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-environment/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);Application.runInBackground=true;
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(game.IsRunActive)game.ReturnToMainMenu();
        game.SaveSlots.OpenTransientForTests();
        typeof(GameController).GetMethod("BeginRunForActiveSlot",Private).Invoke(game,null);
        game.CameraFollow.StartCoroutine(Run(game));
        return "Frozen visual fixture started at "+folder+"; current Game View="+Screen.width+"x"+Screen.height;
    }
    static IEnumerator Run(GameController game)
    {
        yield return new WaitForSecondsRealtime(1);
        game.StopAllCoroutines();Clear();
        var report=new System.Text.StringBuilder("Frozen art fixture, NOT normal completion.\n");
        var types=new[]{EnemyType.Boss,EnemyType.Tidekeeper,EnemyType.SealHeart};
        for(var stage=0;stage<3;stage++)
        {
            Clear();var old=game.DungeonMap;
            var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,stage);
            Set(game,"DungeonMap",map);Set(game,"CurrentStageIndex",stage);Set(game,"CurrentRoom",3);
            Set(game,"StageIntroUntil",0f);Set(game,"WaveBannerUntil",0f);
            Set(game,"Boss",null);Set(game,"EnemiesAlive",0);Set(game,"ActiveChest",null);
            if(old?.Root!=null){old.Root.SetActive(false);UnityEngine.Object.Destroy(old.Root);}
            var center=map.GetRoomCenter(3);var room=map.GetRoom(3);
            game.Player.Teleport(center+new Vector2(-1,-1.5f));game.Player.SetStageClearSafety(true);
            var boss=EnemyController.Create(types[stage],center+new Vector2(2,1),game.Player,()=>true,_=>{},game.CameraFollow);
            boss.ConfigureArena(new Rect(center+room.LocalBounds.position,room.LocalBounds.size));
            Set(game,"Boss",boss);Set(game,"EnemiesAlive",1);
            EnemyController.Create(stage==0?EnemyType.Grunt:EnemyType.Tidecaller,center+new Vector2(-4,1),game.Player,()=>true,_=>{},game.CameraFollow);
            EnemyController.Create(stage==0?EnemyType.Shooter:EnemyType.Sentinel,center+new Vector2(-6,1),game.Player,()=>true,_=>{},game.CameraFollow);
            RewardChest.Create(center+new Vector2(3,-2),()=>{},game.Settings);
            var weapon=WeaponLootTable.Find("rust_sword");
            FieldLootPickup.Create(center+new Vector2(4,-2),weapon.Name,weapon.Color,game.Player,game.Settings,()=>false,0,PixelArtAtlas.GetHeldWeaponSprite(weapon));
            var landmarks=map.Root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Campaign environment landmarks").Single();
            var visuals=landmarks.GetComponentsInChildren<SpriteRenderer>();
            var mosaics=visuals.Count(s=>s.name.EndsWith("Mosaic"));
            var monuments=visuals.Where(s=>s.name.EndsWith("Monument")).ToArray();
            var solids=visuals.Where(s=>s.name.EndsWith("Cluster") || s.name.EndsWith("Cistern")).ToArray();
            var expectedSolids=map.Terrain.Rooms.Sum(r=>r.platforms.Length);
            if(mosaics!=1 || monuments.Length!=2 || solids.Length!=expectedSolids || visuals.Any(s=>s.sprite==null) || landmarks.GetComponentsInChildren<Collider2D>().Length!=0)
                throw new Exception("Landmark contract stage="+stage+" mosaics="+mosaics+" monuments="+monuments.Length+" solids="+solids.Length+" expected="+expectedSolids);
            var pairs=0;var wallSupports=0;
            for(var i=0;i<visuals.Length;i++)
            {
                var a=visuals[i];var ab=a.bounds;
                var ar=new Rect((Vector2)ab.min-Vector2.one*.17f,(Vector2)ab.size+Vector2.one*.34f);
                for(var j=i+1;j<visuals.Length;j++)
                {
                    var b=visuals[j].bounds;var br=new Rect((Vector2)b.min-Vector2.one*.17f,(Vector2)b.size+Vector2.one*.34f);
                    if(ar.Overlaps(br))throw new Exception("Overlapping environment: "+a.name+" / "+visuals[j].name);
                    pairs++;
                }
                if(a.name.EndsWith("Monument") || a.name.EndsWith("WallLamp") || a.name.EndsWith("WallRelic"))
                {
                    var p=(Vector2)a.transform.position;
                    if(a.name.EndsWith("Monument"))p+=Vector2.up*CampaignEnvironmentArt.MonumentWallInset;
                    var half=a.name.EndsWith("Monument")?1.55f:.55f;
                    for(var dx=-half;dx<=half+.01f;dx+=.25f)
                        if(map.Terrain.IsOpen(p+new Vector2(dx,0)) || !map.Terrain.IsOpen(p+new Vector2(dx,-1)))
                            throw new Exception("Unsupported wall attachment: "+a.name+" at "+p);
                    wallSupports++;
                }
            }
            foreach(var solid in solids)
                if(map.Terrain.IsOpen(solid.transform.position))throw new Exception("Solid decoration must cover its obstacle: "+solid.name);
            report.AppendLine("stage="+(stage+1)+" arenaMosaics="+mosaics+" entryAndBossMonuments="+monuments.Length+" completeObstacles="+solids.Length+" nonOverlappingPairs="+pairs+" supportedWallAttachments="+wallSupports+" extraColliders=0");
            var pools=map.Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name.StartsWith("Pool/")).ToArray();
            foreach(var pool in pools)
            {
                var p=(Vector2)pool.transform.position;
                if(map.Terrain.IsOpen(p) || Mathf.Abs(p.x-Mathf.Floor(p.x)-.5f)>.001f || Mathf.Abs(p.y-Mathf.Floor(p.y)-.5f)>.001f)
                    throw new Exception("Pool must match a closed one-unit terrain cell: "+p);
            }
            if(pools.Length!=0)throw new Exception("Disconnected pool tiles must be replaced by the complete cistern");
            if(stage==1 && (solids.Length!=1 || solids[0].name!="FloodedCistern"))throw new Exception("Flooded obstacle must be one complete cistern");
            game.CameraFollow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);Time.timeScale=0;
            yield return new WaitForSecondsRealtime(.4f);Capture("stage-"+(stage+1)+"-composition");
            yield return new WaitForSecondsRealtime(.3f);
            Set(game,"Boss",null);Set(game,"EnemiesAlive",0);
            var monument=monuments.OrderBy(s=>Vector2.Distance(s.transform.position,center)).First();
            game.Player.Teleport((Vector2)monument.transform.position+Vector2.down*2.3f);
            game.CameraFollow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
            yield return new WaitForSecondsRealtime(.4f);Capture("stage-"+(stage+1)+"-north-landmark");
            yield return new WaitForSecondsRealtime(.3f);
            var cluster=solids.First();
            game.Player.Teleport((Vector2)cluster.transform.position+Vector2.down*3f);
            game.CameraFollow.FocusRoom(map.Terrain.Bounds.center,map.Terrain.Bounds.size,true);
            yield return new WaitForSecondsRealtime(.4f);Capture("stage-"+(stage+1)+"-obstacle");
            yield return new WaitForSecondsRealtime(.3f);
        }
        report.AppendLine("PASS placement/resources/no-extra-collider checks. Visual acceptance requires inspection of all nine screenshots.");
        File.WriteAllText(folder+"/result.txt",report.ToString());
        Debug.Log("[Environment Visual] DONE "+folder+";9 frozen screenshots, inspect before adoption.");
    }
}
