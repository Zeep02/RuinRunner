using System;
using System.Collections.Generic;

namespace RuinRunner.Generation
{
    public sealed class GeneratedStageValidation
    {
        public readonly List<string> Errors=new List<string>();
        public int ReachableRegions,ReachableReservations,SealedConnections,WalkableTiles;
        public string Summary=>"regions="+ReachableRegions+" reservations="+ReachableReservations+" sealedConnections="+SealedConnections+" walkableTiles="+WalkableTiles;
    }
    public sealed class GeneratedWalkMask
    {
        public readonly int Left,Bottom,Width,Height;
        public readonly bool[] Tiles;
        public GeneratedWalkMask(GeneratedStage stage)
        {
            float left=float.MaxValue,bottom=float.MaxValue,right=float.MinValue,top=float.MinValue;
            foreach(var region in stage.Regions)
            {var b=region.Module.Bounds.Translated(region.Center);left=Math.Min(left,b.X);bottom=Math.Min(bottom,b.Y);right=Math.Max(right,b.Right);top=Math.Max(top,b.Top);}
            Left=(int)Math.Floor(left)-7;Bottom=(int)Math.Floor(bottom)-7;Width=(int)Math.Ceiling(right)+7-Left;Height=(int)Math.Ceiling(top)+7-Bottom;
            Tiles=new bool[Width*Height];
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {
                var point=new MapPoint(Left+x+.5f,Bottom+y+.5f);bool open=false;
                foreach(var region in stage.Regions)if(Contains(region.Module.Outline,point-region.Center)){open=true;break;}
                if(!open)foreach(var edge in stage.Connections)if(PathDistanceSquared(point,edge.Points)<edge.Width*edge.Width*.25f){open=true;break;}
                if(open)foreach(var region in stage.Regions)
                {
                    foreach(var obstacle in region.Module.Obstacles)if(InsideObstacle(point-region.Center,obstacle)){open=false;break;}
                    if(!open)break;
                }
                Tiles[y*Width+x]=open;
            }
        }
        public bool IsOpen(MapPoint point)
        {int x=(int)Math.Floor(point.X)-Left,y=(int)Math.Floor(point.Y)-Bottom;return x>=0&&y>=0&&x<Width&&y<Height&&Tiles[y*Width+x];}
        // Matches RoomLayoutRules and StageNavigation: real player foot offset, half size, and .04 clearance.
        public bool IsClear(MapPoint root,float halfX=.38f,float halfY=.27f,float offsetY=-.28f)
        {
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)if(!IsOpen(root+new MapPoint(x*halfX,offsetY+y*halfY)))return false;
            return true;
        }
        public static bool Contains(MapPoint[] polygon,MapPoint point)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].Y>point.Y)!=(polygon[j].Y>point.Y)&&point.X<(polygon[j].X-polygon[i].X)*(point.Y-polygon[i].Y)/(polygon[j].Y-polygon[i].Y)+polygon[i].X)inside=!inside;
            return inside;
        }
        public static bool InsideObstacle(MapPoint point,MapRect obstacle)
        {
            var delta=point-obstacle.Center;float x=Math.Abs(delta.X/Math.Max(.1f,obstacle.Width*.5f)),y=Math.Abs(delta.Y/Math.Max(.1f,obstacle.Height*.5f));
            return x*x*x*x+y*y*y*y<1;
        }
        static float PathDistanceSquared(MapPoint point,MapPoint[] path)
        {
            float best=float.MaxValue;
            for(int i=1;i<path.Length;i++)
            {
                var d=path[i]-path[i-1];var p=point-path[i-1];float t=Math.Max(0,Math.Min(1,(p.X*d.X+p.Y*d.Y)/Math.Max(.001f,d.LengthSquared)));
                best=Math.Min(best,(point-(path[i-1]+d*t)).LengthSquared);
            }
            return best;
        }
    }
    public sealed class GeneratedNavigation
    {
        readonly GeneratedWalkMask mask;
        readonly bool[] clear;
        readonly int columns,rows;
        public GeneratedNavigation(GeneratedWalkMask mask,float halfX=.38f,float halfY=.27f,float offsetY=-.28f)
        {
            this.mask=mask;columns=mask.Width*2;rows=mask.Height*2;clear=new bool[columns*rows];
            for(int i=0;i<clear.Length;i++)clear[i]=mask.IsClear(Point(i),halfX,halfY,offsetY);
        }
        MapPoint Point(int id)=>new MapPoint(mask.Left+(id%columns+.5f)*.5f,mask.Bottom+(id/columns+.5f)*.5f);
        int Nearest(MapPoint point)
        {
            int x=(int)Math.Floor((point.X-mask.Left)*2),y=(int)Math.Floor((point.Y-mask.Bottom)*2);int chosen=-1;float best=float.MaxValue;
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            {
                int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=columns||ny>=rows)continue;int id=ny*columns+nx;
                float distance=(Point(id)-point).LengthSquared;
                if(clear[id]&&distance<best){chosen=id;best=distance;}
            }
            return chosen;
        }
        public bool[] Flood(MapPoint from,IReadOnlyList<MapRect> seals=null)
        {
            var visited=new bool[clear.Length];int start=Nearest(from);if(start<0)return visited;
            var queue=new int[clear.Length];int read=0,write=0;queue[write++]=start;visited[start]=true;
            while(read<write)
            {
                int id=queue[read++],x=id%columns,y=id/columns;
                Try(id-1,x>0);Try(id+1,x+1<columns);Try(id-columns,y>0);Try(id+columns,y+1<rows);
                void Try(int next,bool inBounds)
                {
                    if(!inBounds||visited[next]||!clear[next])return;
                    if(seals!=null)
                    {
                        var point=Point(next)+new MapPoint(0,-.28f);
                        foreach(var seal in seals)if(seal.Contains(point))return;
                    }
                    if(!mask.IsClear((Point(id)+Point(next))*.5f))return;
                    visited[next]=true;queue[write++]=next;
                }
            }
            return visited;
        }
        public bool Reached(bool[] flood,MapPoint point){int id=Nearest(point);return id>=0&&flood[id];}
    }
    public static class GeneratedStageGeometry
    {
        public static GeneratedStageValidation Validate(GeneratedStage stage,bool verifySeals=true)
        {
            var report=new GeneratedStageValidation();
            if(stage.Regions==null||stage.Regions.Length<8){report.Errors.Add("Missing role graph");return report;}
            var ids=new HashSet<string>();var edges=new HashSet<string>();int merchants=0,services=0;
            foreach(var r in stage.Regions)
            {
                if(!ids.Add(r.Id))report.Errors.Add("Duplicate region ID "+r.Id);
                if(r.Role==RegionRole.Merchant)merchants++;if(r.Role==RegionRole.FieldUpgrade)services++;
                if(r.Neighbors.Count!=r.Connections.Count)report.Errors.Add("Adjacency mismatch "+r.Id);
                for(int i=r.Index+1;i<stage.Regions.Length;i++)
                    if(r.Module.Bounds.Translated(r.Center).Overlaps(stage.Regions[i].Module.Bounds.Translated(stage.Regions[i].Center),1))report.Errors.Add("Overlapping room bounds "+r.Id);
            }
            if(merchants!=1||services<2)report.Errors.Add("Missing merchant or field service branch");
            if(stage.Entrance.Role!=RegionRole.Entrance||stage.Boss.Role!=RegionRole.Boss)report.Errors.Add("Invalid endpoint roles");
            foreach(var edge in stage.Connections)
            {
                if(!edges.Add(edge.Id)||!ids.Contains(edge.From)||!ids.Contains(edge.To))report.Errors.Add("Invalid edge "+edge.Id);
                var a=stage.FindRegion(edge.From);var b=stage.FindRegion(edge.To);
                if(!a.Neighbors.Contains(b.Id)||!b.Neighbors.Contains(a.Id)||!a.Connections.Contains(edge.Id)||!b.Connections.Contains(edge.Id))report.Errors.Add("Non-reciprocal edge "+edge.Id);
                if(edge.Width<4.5f)report.Errors.Add("Narrow corridor "+edge.Id);
            }
            if(report.Errors.Count>0)return report;
            var mask=new GeneratedWalkMask(stage);foreach(bool tile in mask.Tiles)if(tile)report.WalkableTiles++;
            var navigation=new GeneratedNavigation(mask);var visited=navigation.Flood(stage.Entrance.PlayerPosition);
            foreach(var r in stage.Regions)
            {
                if(!mask.IsClear(r.PlayerPosition)||!navigation.Reached(visited,r.PlayerPosition))report.Errors.Add("Unreachable region "+r.Id);else report.ReachableRegions++;
                var reservations=new List<MapPoint>{r.RewardPosition,r.ServicePosition,r.ExitPosition};
                foreach(var spawn in r.Module.SpawnCandidates)reservations.Add(r.Center+spawn);
                foreach(var point in reservations)
                {
                    // Enemy foot is .78 x .46; the larger .90 x .70 envelope also leaves a small safety margin.
                    if(!mask.IsClear(point,.45f,.35f,-.2f)||!navigation.Reached(visited,point))report.Errors.Add("Invalid actor/reservation footprint "+r.Id+" @ "+point.X+","+point.Y);else report.ReachableReservations++;
                }
                foreach(var support in r.Module.DecorationSupports)
                {
                    for(float y=support.Y+.5f;y<support.Top;y++)for(float x=support.X+.5f;x<support.Right;x++)
                        if(!mask.IsOpen(r.Center+new MapPoint(x,y)))report.Errors.Add("Unsupported decoration pad "+r.Id);
                }
            }
            // Connectivity is symmetric: the same flood proves every optional anchor can return to the entrance.
            if(verifySeals&&report.Errors.Count==0)
            {
                foreach(var edge in stage.Connections)
                {
                    var a=stage.FindRegion(edge.From);var b=stage.FindRegion(edge.To);
                    var blocked=navigation.Flood(a.PlayerPosition,new[]{GateBounds(edge,true)});
                    if(navigation.Reached(blocked,b.PlayerPosition))report.Errors.Add("Departure seal bypass "+edge.Id);else report.SealedConnections++;
                    blocked=navigation.Flood(b.PlayerPosition,new[]{GateBounds(edge,false)});
                    if(navigation.Reached(blocked,a.PlayerPosition))report.Errors.Add("Arrival seal bypass "+edge.Id);
                }
                foreach(var r in stage.Regions)
                {
                    if(r.Role!=RegionRole.Combat&&r.Role!=RegionRole.Boss)continue;
                    var seals=new List<MapRect>();
                    foreach(var edge in stage.Connections)
                        if(edge.From==r.Id||edge.To==r.Id)seals.Add(GateBounds(edge,edge.From==r.Id));
                    var locked=navigation.Flood(r.PlayerPosition,seals);
                    foreach(var neighbor in r.Neighbors)if(navigation.Reached(locked,stage.FindRegion(neighbor).PlayerPosition))report.Errors.Add("Multi-entrance combat seal bypass "+r.Id);
                }
            }
            return report;
        }
        public static MapRect GateBounds(GeneratedConnection edge,bool departure)
        {
            var position=departure?edge.DepartureGate:edge.ArrivalGate;
            bool vertical=edge.FromPort==CardinalPort.North||edge.FromPort==CardinalPort.South;
            // EncounterBarrier collider plus StageNavigation's player clearance inflation.
            float width=vertical?edge.Width+.4f+.85f:.3f+.85f,height=vertical?.3f+.65f:edge.Width+.4f+.65f;
            return new MapRect(position.X-width/2,position.Y-height/2,width,height);
        }
    }
}
