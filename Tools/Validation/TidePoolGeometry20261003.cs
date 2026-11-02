using System;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using RuinRunner;
using Newtonsoft.Json.Linq;

public static class TidePoolGeometry20261003
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        string folder="F:/codex/LoglikeCap/Logs/Validation/2026-10-03-tide-pool-placement/geometry-"+DateTime.Now.ToString("HHmmss");
        Directory.CreateDirectory(folder);var report=new JArray();var clock=Stopwatch.StartNew();
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();game.ReturnToMainMenu();
        try
        {
            for(int stage=0;stage<3;stage++)
            {
                var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100,stage);
                try
                {
                    Physics2D.SyncTransforms();
                    for(int room=0;room<map.RoomCount;room++)
                    {
                        bool boss=room==map.RoomCount-1;
                        var size=map.GetRoom(room).Size;var center=map.GetRoomCenter(room);
                        var bounds=new Rect(center-size*.5f,size);int samples=0,circles=0;
                        float minimumGap=float.PositiveInfinity,maxShift=0;
                        for(float y=bounds.yMin+.5f;y<bounds.yMax;y+=boss?.5f:1f)
                        for(float x=bounds.xMin+.5f;x<bounds.xMax;x+=boss?.5f:1f)
                        {
                            var target=new Vector2(x,y);
                            if(!StageNavigation.IsClear(map.Terrain,target))continue;
                            var pattern=new TidePoolPattern(bounds,target,boss,map.Terrain);
                            int expected=boss?3:1;
                            if(pattern.Centers.Length!=expected)throw new Exception("Missing well stage="+stage+" room="+room+" target="+target+" count="+pattern.Centers.Length);
                            maxShift=Mathf.Max(maxShift,Vector2.Distance(pattern.Centers[0],target));
                            foreach(var point in pattern.Centers)
                            {
                                // Independent query against actual collider geometry, not Fits().
                                var hit=Physics2D.OverlapCircle(point,pattern.Radius,1<<GameLayers.Ground);
                                if(hit!=null)throw new Exception("Well intersects actual wall/cover stage="+stage+" room="+room+" target="+target+" point="+point+" object="+hit.name);
                                if(point.x-pattern.Radius<bounds.xMin||point.x+pattern.Radius>bounds.xMax||point.y-pattern.Radius<bounds.yMin||point.y+pattern.Radius>bounds.yMax)
                                    throw new Exception("Well crosses the arena");
                                circles++;
                            }
                            for(int a=0;a<pattern.Centers.Length;a++)for(int b=a+1;b<pattern.Centers.Length;b++)
                            {
                                float gap=Vector2.Distance(pattern.Centers[a],pattern.Centers[b])-pattern.Radius*2;
                                if(gap<.75f)throw new Exception("No readable escape gap: "+gap);
                                minimumGap=Mathf.Min(minimumGap,gap);
                            }
                            samples++;
                        }
                        report.Add(new JObject{{"stage",stage},{"room",room},{"bossLayout",boss},{"targets",samples},{"circles",circles},{"minimumGap",boss?minimumGap:0},{"maxPrimaryShift",maxShift}});
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(map.Root);}
            }
            File.WriteAllText(folder+"/result.json",new JObject{{"result","PASS"},{"milliseconds",clock.ElapsedMilliseconds},{"seed",9100},{"rooms",report},{"scope","Actual production masks and ground colliders; hypothetical pattern positions over reachable targets. Not normal gameplay, rendering or performance."}}.ToString());
        }
        catch(Exception error){File.WriteAllText(folder+"/failure.txt",error+"\n"+report);throw;}
        return folder;
    }
}
