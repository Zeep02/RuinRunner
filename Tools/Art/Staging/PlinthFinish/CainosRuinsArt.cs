using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Native 32PPU ruins, with paths derived from the real stage layout.</summary>
    public static class CainosRuinsArt
    {
        const string Root="External/Cainos/";
        public static Sprite Sprite(string id)=>IndividualArt.Sprite(Root+id);
        static uint Hash(int x,int y){unchecked{return (uint)(x*73856093^y*19349663)*2654435761u;}}

        public static Sprite Ground(Vector2Int cell)
        {
            var hash=Hash(cell.x,cell.y);
            var growth=Mathf.PerlinNoise(cell.x*.13f+71,cell.y*.13f+37);
            var x=0;var y=0;
            // Quiet patches alternate with low tufts; no large region is forced
            // to repeat the entirely blank tile just because noise is below .54.
            if(hash%100<Mathf.Lerp(30,85,Mathf.InverseLerp(.25f,.75f,growth)))
            {x=(int)(hash%4);y=(int)(hash/7%4);}
            if(growth>.65f&&hash%31==0){x=4+(int)(hash%4);y=(int)(hash/13%4);}
            return Sprite("Grass/"+x.ToString("00")+"-"+y.ToString("00"));
        }

        public static void Build(Transform parent,StageTerrainLayout terrain,EnvironmentPlacementRules placement=null)
        {
            var root=new GameObject("Cainos outer ruins");root.transform.SetParent(parent,false);
            var occupied=new List<Rect>();
            BuildWoodland(root.transform,terrain,occupied);
            var pavingPaths=InteriorPaths(terrain);
            var court=terrain.Centers[0];
            for(var i=0;i<terrain.Rooms.Length;i++)if(terrain.Rooms[i].bossRoom){court=terrain.Centers[i];break;}
            for(var y=Mathf.FloorToInt(terrain.Bounds.yMin);y<terrain.Bounds.yMax;y++)
            for(var x=Mathf.FloorToInt(terrain.Bounds.xMin);x<terrain.Bounds.xMax;x++)
            {
                var cell=new Vector2Int(x,y);var point=(Vector2)cell+Vector2.one*.5f;
                if(!terrain.IsOpen(point))continue;
                for(var sy=0;sy<2;sy++)for(var sx=0;sx<2;sx++)
                {
                    var row=y*2+sy;
                    // Half-stone row offsets break continuous vertical joints.
                    // Every position is still on the original 32 PPU pixel grid.
                    var p=(Vector2)cell+new Vector2(.25f+sx*.5f+(row&1)*.25f,.25f+sy*.5f);
                    if(!OpenPaver(terrain,p))continue;
                    var clearance=PavingClearance(terrain,pavingPaths,p);
                    var edge=(Mathf.PerlinNoise(p.x*.42f+19,p.y*.42f+43)-.5f)*.75f;
                    if(clearance<edge)continue;
                    var hash=PavingHash(x*2+sx,row);var erosion=Mathf.PerlinNoise(p.x*.55f+83,p.y*.55f+17);
                    if(clearance<.45f&&(hash%4==0||erosion>.66f))continue;
                    if(erosion>.70f&&hash%3!=0)continue;
                    Draw(root.transform,"Worn paving",Sprite("Paving/"+(hash%32).ToString("00")),p,1,false);
                }
                // Short ground cover belongs to banks and verges, leaving the
                // travel lane and combat center legible. It adds no collision.
                var h=Hash(x,y);
                if(PavingClearance(terrain,pavingPaths,point)<-.6f&&(point-court).sqrMagnitude>22&&h%19==0&&
                   Mathf.PerlinNoise(x*.16f+17,y*.16f+59)>.55f&&
                   (placement==null||(!placement.IsReserved(new Rect(point-Vector2.one*.4f,Vector2.one*.8f))&&placement.HasFloor(new Rect(point-Vector2.one*.4f,Vector2.one*.8f)))))
                    Draw(root.transform,"Verge grass",Sprite("Plants/grass-"+(1+h%15).ToString("00")),point,2,false);
            }
            for(var region=0;region<terrain.Rooms.Length;region++)
            {
                var center=terrain.Centers[region];var room=terrain.Rooms[region];
                foreach(var island in room.platforms)
                {
                    var bounds=ClosedBounds(terrain,center+island.position,island.size);
                    if(bounds.width<1||bounds.height<1)continue;
                    BuildPlinth(root.transform,bounds);
                    var foot=new Vector2(Mathf.Round(bounds.center.x*32)/32,bounds.yMin+.5f);
                    Place(root.transform,room.bossRoom?"statue":"broken-pillar",foot,occupied);
                }
                // Repeated architectural bays have a purpose: lamps frame the
                // entry/court; a single guardian statue marks each end of the route.
                if(region==0||room.bossRoom)
                    if(NorthWall(terrain,center,room.Size,center.x,1.3f,out var shrine))
                        Place(root.transform,"statue",shrine,occupied,placement);
                foreach(var side in new[]{-1,1})
                    for(var attempt=0;attempt<4;attempt++)
                    {
                        if(!NorthWall(terrain,center,room.Size,center.x+side*(5+attempt),.5f,out var lamp))continue;
                        if(Place(root.transform,"lantern",lamp,occupied,placement))break;
                    }
            }
            placement?.BuildSupportedProps(root.transform,0,occupied);
        }

        static bool OpenPaver(StageTerrainLayout terrain,Vector2 point)
        {
            // Do not let an offset stone intrude into a wall or solid island.
            for(var y=-1;y<=1;y+=2)for(var x=-1;x<=1;x+=2)
                if(!terrain.IsOpen(point+new Vector2(x,y)*.24f))return false;
            return true;
        }
        static uint PavingHash(int x,int y)
        {
            // Mix high bits before choosing one of 32 source stones; modulo of
            // the old linear hash repeated the same short diagonal sequences.
            unchecked{var h=Hash(x,y);h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);}
        }

        static List<StagePathData> InteriorPaths(StageTerrainLayout terrain)
        {
            var result=new List<StagePathData>();
            foreach(var paths in new[]{terrain.Connections,terrain.Trails,terrain.Exploration})
                foreach(var path in paths)
                {
                    result.Add(path);
                    if(path.points.Length==0)continue;
                    // Corridor geometry stops at the doorway. Continue its
                    // surface to the room's meeting point, without changing land.
                    foreach(var end in new[]{path.points[0],path.points[path.points.Length-1]})
                    {
                        var nearest=-1;var distance=float.PositiveInfinity;
                        for(var i=0;i<terrain.Centers.Length;i++)
                        {
                            var size=terrain.Rooms[i].Size+Vector2.one*2;
                            if(!new Rect(terrain.Centers[i]-size*.5f,size).Contains(end))continue;
                            var d=(end-terrain.Centers[i]).sqrMagnitude;
                            if(d<distance){nearest=i;distance=d;}
                        }
                        if(nearest>=0&&distance>.01f)
                            result.Add(new StagePathData{points=new[]{end,terrain.Centers[nearest]},width=path.width});
                    }
                }
            return result;
        }
        static float PavingClearance(StageTerrainLayout terrain,List<StagePathData> paths,Vector2 point)
        {
            var result=float.NegativeInfinity;
            foreach(var path in paths)
                result=Mathf.Max(result,Mathf.Clamp(path.width*.19f,.6f,.95f)-StageTerrainLayout.PathDistance(point,path));
            for(var i=0;i<terrain.Centers.Length;i++)
            {
                var delta=point-terrain.Centers[i];
                var radius=terrain.Rooms[i].bossRoom?new Vector2(3.8f,2.6f):i==1?new Vector2(2.4f,1.65f):new Vector2(1.9f,1.4f);
                // Surviving courtyard masonry opens out at path junctions;
                // broad, worn edges leave the surrounding grass visible.
                var d=new Vector2(Mathf.Abs(delta.x)/radius.x,Mathf.Abs(delta.y)/radius.y);
                result=Mathf.Max(result,(1-Mathf.Pow(Mathf.Pow(d.x,4)+Mathf.Pow(d.y,4),.25f))*radius.y);
            }
            return result;
        }
        static void BuildWoodland(Transform parent,StageTerrainLayout terrain,List<Rect> occupied)
        {
            // Closed country continues behind the low ruin walls. A flat source
            // texel supplies the distant turf; trees keep their native pixel size.
            var background=Draw(parent,"Outer woodland ground",Sprite("Grass/solid"),terrain.Bounds.center,-8,false);
            background.transform.localScale=new Vector3((terrain.Bounds.width+24)*32,(terrain.Bounds.height+24)*32,1);
            background.color=new Color(.82f,.87f,.76f,1);
            for(var y=Mathf.FloorToInt(terrain.Bounds.yMin);y<terrain.Bounds.yMax;y+=4)
            for(var x=Mathf.FloorToInt(terrain.Bounds.xMin);x<terrain.Bounds.xMax;x+=5)
            {
                var hash=Hash(x,y);var foot=new Vector2(x+.5f+(hash%3)*.5f,y+.5f);
                var clear=true;var nearFloor=false;
                for(var sy=-2;sy<=3;sy++)for(var sx=-2;sx<=2;sx++)
                    if(terrain.IsOpen(foot+new Vector2(sx,sy)))clear=false;
                if(!clear)continue;
                for(var sy=-5;sy<=5;sy+=2)for(var sx=-5;sx<=5;sx+=2)
                    nearFloor|=terrain.IsOpen(foot+new Vector2(sx,sy));
                if(!nearFloor||hash%5==0)continue;
                var sprite=Sprite("Plants/t"+(1+hash%3));var b=sprite.bounds;
                var rect=new Rect(foot+(Vector2)b.min,(Vector2)b.size);
                var overlap=false;foreach(var old in occupied)if(rect.Overlaps(old)){overlap=true;break;}
                if(overlap)continue;
                Draw(parent,"Outer woodland tree",sprite,foot,0,true);occupied.Add(rect);
            }
        }
        static void BuildPlinth(Transform parent,Rect bounds)
        {
            // Solid native stone still exactly covers the existing closed cells.
            // Small surviving pavers break up the broad unmarked top without
            // altering the platform footprint, collision or source pixel size.
            for(var y=bounds.yMin;y<bounds.yMax;y++)for(var x=bounds.xMin;x<bounds.xMax;x++)
                Draw(parent,"Ruined plinth top",Sprite(PavingHash(Mathf.RoundToInt(x),Mathf.RoundToInt(y))%5==0?"Stone/quiet":"Stone/worn"),new Vector2(x+.5f,y+.5f),12,false);
            for(var row=0;bounds.yMin+.875f+row*.5f<=bounds.yMax-.5f;row++)
            for(var column=0;bounds.xMin+.5f+column*.5f+(row&1)*.25f<=bounds.xMax-.5f;column++)
            {
                var point=new Vector2(bounds.xMin+.5f+column*.5f+(row&1)*.25f,bounds.yMin+.875f+row*.5f);
                var hash=PavingHash(Mathf.RoundToInt(point.x*4),Mathf.RoundToInt(point.y*4));
                if(hash%9==0)continue;
                Draw(parent,"Ruined plinth worn slab",Sprite("StonePaving/"+(hash%32).ToString("00")),point,13,false);
            }
            for(var x=bounds.xMin;x<bounds.xMax;x+=.25f)
            {
                var i=((Mathf.RoundToInt(x*4)%4)+4)%4;
                Draw(parent,"Ruined plinth face",Sprite("Ribbon/face-"+i),new Vector2(x+.125f,bounds.yMin+.25f),14,false);
                Draw(parent,"Ruined plinth lip",Sprite("Ribbon/cap-"+i),new Vector2(x+.125f,bounds.yMin+.625f),16,false);
                Draw(parent,"Ruined plinth rear rim",Sprite("Ribbon/cap-"+i),new Vector2(x+.125f,bounds.yMax-.125f),16,false);
            }
            // Quarter-unit source strips meet exactly; the side strips stop
            // before the front/rear rows, so corners are drawn only once.
            for(var y=bounds.yMin+.75f;y<bounds.yMax-.25f;y+=.25f)
            {
                var i=((Mathf.RoundToInt(y*4)%4)+4)%4;
                var left=Draw(parent,"Ruined plinth left rim",Sprite("Ribbon/cap-"+i),new Vector2(bounds.xMin+.125f,y+.125f),16,false);
                left.transform.localRotation=Quaternion.Euler(0,0,90);
                var right=Draw(parent,"Ruined plinth right rim",Sprite("Ribbon/cap-"+i),new Vector2(bounds.xMax-.125f,y+.125f),16,false);
                right.transform.localRotation=Quaternion.Euler(0,0,-90);
            }
        }
        static Rect ClosedBounds(StageTerrainLayout terrain,Vector2 center,Vector2 size)
        {
            var min=Vector2.one*float.PositiveInfinity;var max=Vector2.one*float.NegativeInfinity;
            for(var y=Mathf.FloorToInt(center.y-size.y*.5f);y<Mathf.CeilToInt(center.y+size.y*.5f);y++)
            for(var x=Mathf.FloorToInt(center.x-size.x*.5f);x<Mathf.CeilToInt(center.x+size.x*.5f);x++)
                if(!terrain.IsOpen(new Vector2(x+.5f,y+.5f))){min=Vector2.Min(min,new Vector2(x,y));max=Vector2.Max(max,new Vector2(x+1,y+1));}
            return float.IsInfinity(min.x)?default:new Rect(min,max-min);
        }
        static bool NorthWall(StageTerrainLayout terrain,Vector2 center,Vector2 size,float preferredX,float halfWidth,out Vector2 point)
        {
            var x=Mathf.Floor(preferredX)+.5f;
            for(var y=Mathf.Floor(center.y)+.5f;y<center.y+size.y*.5f+3;y++)
            {
                var supported=true;
                for(var dx=-halfWidth;dx<=halfWidth+.01f;dx+=.25f)
                    if(terrain.IsOpen(new Vector2(x+dx,y))||!terrain.IsOpen(new Vector2(x+dx,y-1))){supported=false;break;}
                if(supported){point=new Vector2(x,y-.25f);return true;}
            }
            point=default;return false;
        }
        static bool Place(Transform parent,string id,Vector2 point,List<Rect> occupied,EnvironmentPlacementRules placement=null)
        {
            var sprite=Sprite("Props/"+id);var b=sprite.bounds;
            var rect=new Rect(point+(Vector2)b.min-Vector2.one*.18f,(Vector2)b.size+Vector2.one*.36f);
            if(placement!=null&&placement.IsReserved(rect))return false;
            foreach(var old in occupied)if(rect.Overlaps(old))return false;
            Draw(parent,"Ruins "+id,sprite,point,0,true);occupied.Add(rect);return true;
        }
        static SpriteRenderer Draw(Transform parent,string name,Sprite sprite,Vector2 point,int order,bool depth)
        {
            if(sprite==null)throw new System.InvalidOperationException("Missing Cainos Sprite: "+name);
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.position=point;
            var view=obj.AddComponent<SpriteRenderer>();view.sprite=sprite;
            view.sortingLayerName=depth?"Default":"WorldFloor";view.sortingOrder=depth?900-Mathf.RoundToInt(point.y*100):order;
            return view;
        }
    }
}
