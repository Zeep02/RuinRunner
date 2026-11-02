using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RuinRunner;

// Frozen candidate comparison on the game's real stage. No runtime asset adoption.
public static class ExternalArtComparisonFixture
{
    const string Sources="F:/codex/LoglikeCap/ArtDirection/ExternalCandidates/20260921/";
    static string folder;
    static readonly List<UnityEngine.Object> temporary=new List<UnityEngine.Object>();
    static Texture2D Load(string path)
    {
        var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(Sources+path));
        t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;temporary.Add(t);return t;
    }
    static Sprite Tile(Texture2D t,int x,int y,int w,int h,float ppu=32)
    {
        var s=Sprite.Create(t,new Rect(x,t.height-y-h,w,h),new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect);
        temporary.Add(s);return s;
    }
    static void Layer(GameObject obj){foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=31;}
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode");
        folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-external-art/"+DateTime.Now.ToString("HHmmss");Directory.CreateDirectory(folder);
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();game.enabled=false;
        Time.timeScale=1;Application.runInBackground=true;temporary.Clear();
        game.StartCoroutine(Run());return folder;
    }
    static IEnumerator Run()
    {
        var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,0);
        var center=map.GetRoomCenter(3);var inventory=new InventorySystem();inventory.AddWeapon(WeaponLootTable.FindFirst(WeaponFamily.Longsword));
        var follow=CameraFollow.Create();var player=PlayerController.Create(center+new Vector2(-1,-1.5f),()=>true,()=>false,()=>{},follow,inventory,new GameSettingsService());
        player.EquipWeapon(inventory.EquippedWeapon);follow.Target=player.transform;
        var boss=EnemyController.Create(EnemyType.Boss,center+new Vector2(2,1),player,()=>true,_=>{},follow);
        var grunt=EnemyController.Create(EnemyType.Grunt,center+new Vector2(-4,1),player,()=>true,_=>{},follow);
        var charger=EnemyController.Create(EnemyType.Charger,center+new Vector2(-6,1),player,()=>true,_=>{},follow);
        var chest=RewardChest.Create(center+new Vector2(3,-2),()=>{},new GameSettingsService());
        yield return null;
        foreach(var obj in new[]{map.Root,player.gameObject,boss.gameObject,grunt.gameObject,charger.gameObject,chest.gameObject})Layer(obj);
        var camObj=new GameObject("Candidate comparison camera");var cam=camObj.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;
        cam.orthographicSize=5.625f;cam.aspect=16f/9;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(18/255f,15/255f,31/255f);
        cam.allowHDR=false;cam.allowMSAA=false;cam.transform.position=new Vector3(center.x,center.y,-10);
        var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=1,filterMode=FilterMode.Point};rt.Create();cam.targetTexture=rt;
        Render(cam,rt,"01-current-floor");
        var source=Load("cainos-basic/original/Texture/TX Tileset Grass.png");var tiles=new Sprite[8,8];
        for(var y=0;y<8;y++)for(var x=0;x<8;x++)tiles[x,y]=Tile(source,x*32,y*32,32,32);
        var ground=map.Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Continuous ground").ToArray();
        var lookup=new Dictionary<Vector2Int,Sprite>();var stones=0;
        foreach(var sr in ground)
        {
            var p=(Vector2)sr.transform.position;var cell=Vector2Int.FloorToInt(p);var hash=Hash(cell.x,cell.y);
            var path=map.Terrain.Connections.Concat(map.Terrain.Trails).Concat(map.Terrain.Exploration).Any(t=>StageTerrainLayout.PathDistance(p,t)<1.4f);
            // The guardian approach and courtyard are an architectural continuation of the paths.
            path|=Mathf.Abs(p.x-center.x)<2.6f&&p.y<center.y+2.4f;
            var sprite=path?tiles[(int)(hash%2),4+(int)(hash/7%4)]:tiles[(int)(hash%4),(int)(hash/7%4)];
            if(!path&&hash%29==0)sprite=tiles[4+(int)(hash%4),(int)(hash/13%4)];
            sr.sprite=sprite;lookup[cell]=sprite;if(path)stones++;
        }
        foreach(var sr in map.Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Floor contact underlay"))
            if(lookup.TryGetValue(Vector2Int.FloorToInt(sr.transform.position),out var sprite))sr.sprite=sprite;
        Render(cam,rt,"02-cainos-native-floor");
        var candidateRoot=new GameObject("External actor candidates");
        var orc=Load("zerie-tiny-rpg/original/Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc/Characters(100x100 split)/Orc/Orc/Orc_Idle.png");
        var soldier=Load("zerie-tiny-rpg/original/Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc/Characters(100x100 split)/Soldier/Soldier/Soldier_Idle.png");
        var knight=Load("tiny-swords/original/Tiny Swords (Free Pack)/Units/Blue Units/Warrior/Warrior_Idle.png");
        Actor(candidateRoot,"Zerie Orc, native 32PPU",Tile(orc,0,0,100,100),center+new Vector2(-5,-2));
        Actor(candidateRoot,"Zerie Soldier, native 32PPU",Tile(soldier,0,0,100,100),center+new Vector2(-3,-2));
        Actor(candidateRoot,"Tiny Swords Warrior, native 64PPU",Tile(knight,0,0,192,192,64),center+new Vector2(5,-2));
        Render(cam,rt,"03-native-actor-candidates");
        candidateRoot.SetActive(false);
        var paving=NaturalSurface(map,source,tiles,ground,center);
        Render(cam,rt,"04-cainos-worn-paths");
        // The earlier centered arena view did not expose an outside wall.
        // Include the actual entrance and its north boundary in the next review.
        var entry=map.GetRoomCenter(0);
        cam.transform.position=new Vector3(entry.x,entry.y+3,-10);
        Render(cam,rt,"05-cainos-entrance-boundary");
        File.WriteAllText(folder+"/scope.txt","Frozen comparison rendered by Unity from real stage 1 and current player/Warden/Grunt/Charger/chest. Same 1920x1080 camera, 5.625 orthographic size. Cainos floor uses unchanged native 32px tiles and 16px paving pieces at32PPU; no resizing/repainting. Actors: Zerie native32PPU, Tiny Swords native64PPU. Images04/05 are a separate floor-layout proposal with sparse vegetation, worn path edges and a paved guardian court; existing masonry remains for comparison. No collision/AI/animation replacement. Source archives under ArtDirection/ExternalCandidates/20260921. This is visual comparison, not normal gameplay, animation acceptance or adoption. floor="+ground.Length+" first-path="+stones+" revised-paving="+paving);
        cam.targetTexture=null;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(camObj);
        UnityEngine.Object.Destroy(candidateRoot);
        foreach(var obj in new[]{map.Root,player.gameObject,boss.gameObject,grunt.gameObject,charger.gameObject,chest.gameObject,follow.gameObject})UnityEngine.Object.Destroy(obj);
        foreach(var o in temporary)UnityEngine.Object.Destroy(o);temporary.Clear();
        Debug.Log("[External Art] Comparison written "+folder);
    }
    static int NaturalSurface(DungeonMap map,Texture2D source,Sprite[,] tiles,SpriteRenderer[] ground,Vector2 court)
    {
        var lookup=new Dictionary<Vector2Int,Sprite>();
        foreach(var sr in ground)
        {
            var cell=Vector2Int.FloorToInt(sr.transform.position);var hash=Hash(cell.x,cell.y);
            // Quiet open turf carries the action. Tufts occupy broad irregular
            // patches, rather than selecting one busy tile for every grid cell.
            var growth=Mathf.PerlinNoise(cell.x*.13f+71,cell.y*.13f+37);
            var sprite=growth>.57f&&hash%4!=0?tiles[(int)(hash%4),(int)(hash/7%4)]:tiles[0,0];
            if(growth>.64f&&hash%19==0)sprite=tiles[4+(int)(hash%4),(int)(hash/13%4)];
            sr.sprite=sprite;lookup[cell]=sprite;
        }
        foreach(var sr in map.Root.GetComponentsInChildren<SpriteRenderer>())
        {
            if(sr.name=="Floor contact underlay"&&lookup.TryGetValue(Vector2Int.FloorToInt(sr.transform.position),out var sprite))sr.sprite=sprite;
            if(sr.name=="OuterMosaic")sr.enabled=false;
        }
        var stone=new Sprite[32];
        for(var y=0;y<8;y++)for(var x=0;x<4;x++)stone[y*4+x]=Tile(source,x*16,128+y*16,16,16);
        var count=0;
        foreach(var sr in ground)
        {
            var cell=Vector2Int.FloorToInt(sr.transform.position);
            for(var y=0;y<2;y++)for(var x=0;x<2;x++)
            {
                var p=(Vector2)cell+new Vector2(.25f+x*.5f,.25f+y*.5f);
                if(!map.Terrain.IsOpen(p))continue;
                var clearance=float.NegativeInfinity;
                foreach(var paths in new[]{map.Terrain.Connections,map.Terrain.Trails,map.Terrain.Exploration})
                    foreach(var path in paths)clearance=Mathf.Max(clearance,Mathf.Clamp(path.width*.23f,.7f,1.35f)-StageTerrainLayout.PathDistance(p,path));
                var n=p-court;
                var courtDistance=(1-new Vector2(n.x/4.2f,n.y/3.5f).magnitude)*3.5f;
                clearance=Mathf.Max(clearance,courtDistance);
                var edge=(Mathf.PerlinNoise(p.x*.28f+19,p.y*.28f+43)-.5f)*.7f;
                if(clearance<edge)continue;
                var hash=Hash(cell.x*2+x,cell.y*2+y);
                // Wear clusters vary on a larger scale than each paving stone.
                var erosion=Mathf.PerlinNoise(p.x*.4f+83,p.y*.4f+17);
                if((clearance<.5f&&hash%5==0)||(erosion>.7f&&hash%3==0))continue;
                var obj=new GameObject("Candidate paving stone"){layer=31};obj.transform.SetParent(map.Root.transform,false);obj.transform.position=p;
                var view=obj.AddComponent<SpriteRenderer>();view.sprite=stone[hash%32];view.sortingLayerName="WorldFloor";view.sortingOrder=1;count++;
            }
        }
        return count;
    }
    static uint Hash(int x,int y){unchecked{return (uint)(x*73856093^y*19349663)*2654435761u;}}
    static void Actor(GameObject parent,string name,Sprite sprite,Vector2 point)
    {
        var obj=new GameObject(name){layer=31};obj.transform.SetParent(parent.transform);obj.transform.position=point;
        var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=1200;
    }
    static void Render(Camera camera,RenderTexture target,string name)
    {
        var previous=RenderTexture.active;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
    }
}
