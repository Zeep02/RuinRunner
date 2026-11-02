using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RuinRunner;

// Isolated render/geometry inspection, not normal gameplay or completion evidence.
public static class WallFacingFixture
{
    public static string Main()
    {
        if(!Application.isPlaying) throw new Exception("Enter Play Mode first");
        var folder="F:/codex/LoglikeCap/Logs/Validation/"+DateTime.Now.ToString("yyyy-MM-dd")+"-wall-facing/"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);
        var report=new StringBuilder();
        var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
        for(var stage=0;stage<3;stage++)
        {
            var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,stage);
            try
            {
                foreach(var t in map.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                var terrain=map.Terrain;
                var floor=new HashSet<Vector2Int>();
                for(int y=Mathf.FloorToInt(terrain.Bounds.yMin);y<terrain.Bounds.yMax;y++)
                for(int x=Mathf.FloorToInt(terrain.Bounds.xMin);x<terrain.Bounds.xMax;x++)
                    if(terrain.IsOpen(new Vector2(x+.5f,y+.5f)))floor.Add(new Vector2Int(x,y));
                var geometry=new StringBuilder();
                foreach(var c in floor.OrderBy(c=>c.y).ThenBy(c=>c.x))geometry.AppendLine("open,"+c.x+","+c.y);
                foreach(var c in map.Root.GetComponentsInChildren<BoxCollider2D>().OrderBy(c=>c.transform.position.y).ThenBy(c=>c.transform.position.x).ThenBy(c=>c.name))
                    geometry.AppendLine("box,"+c.name+","+c.transform.position+","+c.size+","+c.offset+","+c.isTrigger);
                File.WriteAllText(folder+"/stage-"+(stage+1)+"-geometry.txt",geometry.ToString());
                var walls=map.Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name.StartsWith("Masonry/")).ToArray();
                var rows=new StringBuilder("x,y,sprite,openN,openE,openS,openW\n");
                foreach(var s in walls.OrderBy(s=>s.transform.position.y).ThenBy(s=>s.transform.position.x))
                {
                    var c=Vector2Int.FloorToInt(s.transform.position);
                    rows.AppendLine(c.x+","+c.y+","+s.name+","+string.Join(",",directions.Select(d=>floor.Contains(c+d)?"1":"0")));
                }
                File.WriteAllText(folder+"/stage-"+(stage+1)+"-walls.csv",rows.ToString());
                var neighbors=new[]{new Vector2Int(-1,1),Vector2Int.up,new Vector2Int(1,1),Vector2Int.left,Vector2Int.right,new Vector2Int(-1,-1),Vector2Int.down,new Vector2Int(1,-1)};
                var masks=new Dictionary<string,int>();
                foreach(var s in walls)
                {
                    var c=Vector2Int.FloorToInt(s.transform.position);
                    var mask=string.Concat(neighbors.Select(d=>floor.Contains(c+d)?"1":"0"));
                    if(!masks.ContainsKey(mask))masks[mask]=0;masks[mask]++;
                }
                File.WriteAllLines(folder+"/stage-"+(stage+1)+"-masks.txt",masks.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value));
                var b=terrain.Bounds;b.xMin-=2;b.yMin-=2;b.xMax+=2;b.yMax+=2;
                File.WriteAllText(folder+"/stage-"+(stage+1)+"-render-bounds.json",JsonUtility.ToJson(new RenderBounds{center=b.center,width=Mathf.CeilToInt(b.width)*16,height=Mathf.CeilToInt(b.height)*16,ppu=16}));
                Render(folder+"/stage-"+(stage+1)+"-overview.png",b.center,Mathf.CeilToInt(b.width)*16,Mathf.CeilToInt(b.height)*16,16);
                for(int room=0;room<4;room++)
                {
                    var rb=map.GetRoom(room).LocalBounds;
                    Render(folder+"/stage-"+(stage+1)+"-room-"+(room+1)+".png",map.GetRoomCenter(room)+rb.center,Mathf.CeilToInt(rb.width+4)*32,Mathf.CeilToInt(rb.height+4)*32,32);
                }
                Render(folder+"/stage-"+(stage+1)+"-corner.png",map.GetRoomCenter(0)+new Vector2(-4,-5),640,480,64);
                var ribbon=map.Root.GetComponentInChildren<ContinuousWallRibbon>();
                var meshCount=ribbon.GetComponentsInChildren<MeshRenderer>().Length;
                var ground=map.Root.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name=="Continuous ground").ToArray();
                if(ribbon.CapCells==0 || ribbon.FaceCells==0 || ground.Any(s=>s.sprite==null))throw new Exception("Missing continuous terrain");
                report.AppendLine("stage="+(stage+1)+" floorCells="+floor.Count+" distinctGroundSprites="+ground.Select(s=>s.sprite).Distinct().Count()+" capCells="+ribbon.CapCells+" faceCells="+ribbon.FaceCells+" wallMeshBatches="+meshCount+" colliders="+map.Root.GetComponentsInChildren<Collider2D>().Length);
            }
            finally {map.Root.SetActive(false);UnityEngine.Object.Destroy(map.Root);}
        }
        File.WriteAllText(folder+"/result.txt",report.ToString());
        return folder+"\n"+report;
    }
    [Serializable] public sealed class RenderBounds {public Vector2 center;public int width,height,ppu;}
    static void Render(string path,Vector2 center,int width,int height,int pixelsPerUnit)
    {
        var obj=new GameObject("Wall inspection camera");var camera=obj.AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=height/(pixelsPerUnit*2f);
        camera.transform.position=new Vector3(center.x,center.y,-10);camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(20/255f,17/255f,31/255f,1);
        camera.allowHDR=false;camera.allowMSAA=false;
        var target=new RenderTexture(width,height,24);target.filterMode=FilterMode.Point;
        var previous=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(obj);}
    }
}
