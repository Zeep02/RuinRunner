using System.Collections;
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
            var steps=BuildSteps(parent,terrain,placement,false);
            while(steps.MoveNext()){}
        }
        public static IEnumerator BuildProgressively(Transform parent,StageTerrainLayout terrain,EnvironmentPlacementRules placement)
            =>BuildSteps(parent,terrain,placement,true);
        static IEnumerator BuildSteps(Transform parent,StageTerrainLayout terrain,EnvironmentPlacementRules placement,bool progressive)
        {
            var root=new GameObject("Cainos outer ruins");root.transform.SetParent(parent,false);
            var occupied=new List<Rect>();
            var woodland=BuildWoodland(root.transform,terrain,occupied,progressive);
            while(woodland.MoveNext())yield return woodland.Current;
            var budget=new TerrainBuildBudget(progressive);
            var pavingPaths=InteriorPaths(terrain);
            var court=terrain.Centers[0];
            for(var i=0;i<terrain.Rooms.Length;i++)if(terrain.Rooms[i].bossRoom){court=terrain.Centers[i];break;}
            for(var y=Mathf.FloorToInt(terrain.Bounds.yMin);y<terrain.Bounds.yMax;y++)
            {
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
                if(PavingClearance(terrain,pavingPaths,point)<-.6f&&(point-court).sqrMagnitude>22&&h%7==0&&
                   Mathf.PerlinNoise(x*.16f+17,y*.16f+59)>.42f&&
                   (placement==null||(!placement.IsReserved(new Rect(point-Vector2.one*.4f,Vector2.one*.8f))&&placement.HasFloor(new Rect(point-Vector2.one*.4f,Vector2.one*.8f)))))
                    Draw(root.transform,"Verge grass",Sprite("Plants/grass-"+(1+h%15).ToString("00")),point,2,false);
            }
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
            for(var region=0;region<terrain.Rooms.Length;region++)
            {
                var center=terrain.Centers[region];var room=terrain.Rooms[region];
                foreach(var island in room.platforms)
                {
                    StageLandmarks.Place(root.transform,0,center+island.position,island.size,occupied);
                    if(budget.Exhausted){yield return null;budget.Resume();}
                }
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
            if(placement!=null)
            {
                if(progressive)yield return placement.BuildSupportedPropsProgressively(root.transform,0,occupied);
                else placement.BuildSupportedProps(root.transform,0,occupied);
            }
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
        static IEnumerator BuildWoodland(Transform parent,StageTerrainLayout terrain,List<Rect> occupied,bool progressive)
        {
            var budget=new TerrainBuildBudget(progressive);
            // Closed country continues behind the low ruin walls. A flat source
            // texel supplies the distant turf; trees keep their native pixel size.
            var background=Draw(parent,"Outer woodland ground",Sprite("Grass/solid"),terrain.Bounds.center,-8,false);
            background.transform.localScale=new Vector3((terrain.Bounds.width+24)*32,(terrain.Bounds.height+24)*32,1);
            background.color=new Color(.82f,.87f,.76f,1);
            for(var y=Mathf.FloorToInt(terrain.Bounds.yMin);y<terrain.Bounds.yMax;y+=4)
            {
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
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
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
