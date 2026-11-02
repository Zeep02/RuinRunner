using System;
using UnityEngine;
using RuinRunner;
public static class TideLaneFixture
{
    public static string Main()
    {
        var stage=StageLayoutDefinition.Load(1);var room=stage.regions[3];
        var arena=new Rect(room.center-room.size*.5f,room.size);
        var cases=0;
        foreach(var vertical in new[]{false,true})foreach(var positive in new[]{false,true})
        for(var x=0;x<11;x++)for(var y=0;y<11;y++)
        {
            var player=new Vector2(Mathf.Lerp(arena.xMin+.5f,arena.xMax-.5f,x/10f),Mathf.Lerp(arena.yMin+.5f,arena.yMax-.5f,y/10f));
            var pattern=new TideLanePattern(arena,player,vertical,positive);
            var area=pattern.First.width*pattern.First.height+pattern.Safe.width*pattern.Safe.height+pattern.Second.width*pattern.Second.height;
            if(!Mathf.Approximately(area,arena.width*arena.height))throw new Exception("Floods and safe corridor must partition whole arena");
            var low=vertical?pattern.Safe.xMin:pattern.Safe.yMin;
            var high=vertical?pattern.Safe.xMax:pattern.Safe.yMax;
            var axis=vertical?player.x:player.y;
            var travel=Mathf.Abs(Mathf.Clamp(axis,low+.4f,high-.4f)-axis);
            // Allow .25s reaction and .4-unit body clearance; no dash/upgrade assumed.
            if(travel>(TideLanePattern.WarningSeconds-.25f)*7)throw new Exception("Refuge unreachable with base speed and body clearance");
            for(var a=0;a<17;a++)for(var b=0;b<17;b++)
            {
                var p=new Vector2(Mathf.Lerp(arena.xMin+.01f,arena.xMax-.01f,(a+.5f)/17),Mathf.Lerp(arena.yMin+.01f,arena.yMax-.01f,(b+.5f)/17));
                var count=(pattern.First.Contains(p)?1:0)+(pattern.Safe.Contains(p)?1:0)+(pattern.Second.Contains(p)?1:0);
                if(count!=1)throw new Exception("Unmarked refuge or overlap in flooded partition");
            }
            cases++;
        }
        var template=ScriptableObject.CreateInstance<RoomTemplate>();
        template.roomSize=room.size;template.walkableOutline=room.outline;template.platforms=room.land;
        var terrain=new StageTerrainLayout(new[]{template},new[]{room.center});terrain.SnapToDungeonTiles();
        var physicalCases=0;var longest=0f;
        try
        {
            foreach(var vertical in new[]{false,true})foreach(var positive in new[]{false,true})
            for(var x=0;x<15;x++)for(var y=0;y<15;y++)
            {
                var p=new Vector2(Mathf.Lerp(arena.xMin+.6f,arena.xMax-.6f,x/14f),Mathf.Lerp(arena.yMin+.6f,arena.yMax-.6f,y/14f));
                if(!StageNavigation.IsClear(terrain,p))continue;
                var pattern=new TideLanePattern(arena,p,vertical,positive);
                var best=float.PositiveInfinity;
                // Test actual snapped walk cells, including the arena's stepped corners.
                // A directly walkable segment is stricter than a path with turns.
                for(var ray=0;ray<64;ray++)
                {
                    var angle=ray*Mathf.PI/32;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    for(var length=.125f;length<=8f;length+=.125f)
                    {
                        var point=p+direction*length;
                        if(!StageNavigation.IsClear(terrain,point))break;
                        var inset=pattern.Safe;inset.min+=Vector2.one*.4f;inset.max-=Vector2.one*.4f;
                        if(!inset.Contains(point))continue;
                        best=Mathf.Min(best,length);break;
                    }
                }
                if(float.IsPositiveInfinity(best))
                {
                    var queue=new System.Collections.Generic.Queue<Vector2Int>();
                    var lengths=new System.Collections.Generic.Dictionary<Vector2Int,int>();
                    queue.Enqueue(Vector2Int.zero);lengths.Add(Vector2Int.zero,0);
                    var inset=pattern.Safe;inset.min+=Vector2.one*.4f;inset.max-=Vector2.one*.4f;
                    while(queue.Count>0)
                    {
                        var cell=queue.Dequeue();var length=lengths[cell];var point=p+(Vector2)cell*.25f;
                        if(inset.Contains(point)){best=length*.25f;break;}
                        if(length>=32)continue;
                        foreach(var offset in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                        {
                            var next=cell+offset;
                            if(lengths.ContainsKey(next) || !StageNavigation.IsClear(terrain,p+(Vector2)next*.25f) ||
                                !StageNavigation.IsClear(terrain,point+(Vector2)offset*.125f))continue;
                            lengths.Add(next,length+1);queue.Enqueue(next);
                        }
                    }
                }
                if(float.IsPositiveInfinity(best))throw new Exception("Physical refuge unreachable within 8 units from "+p+" vertical="+vertical+" positive="+positive);
                longest=Mathf.Max(longest,best);physicalCases++;
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(template);}
        var result="[Tide Lanes] PASS "+cases+" partition plans + "+physicalCases+" actual snapped-terrain/body routes. Longest refuge walk="+longest+" / allowed 8.05 units including .25s reaction. Geometry test; live boss play required.";
        System.IO.Directory.CreateDirectory("F:/codex/LoglikeCap/Logs/Validation/2026-09-16-tide");
        System.IO.File.WriteAllText("F:/codex/LoglikeCap/Logs/Validation/2026-09-16-tide/geometry.txt",result);
        Debug.Log(result);return result;
    }
}
