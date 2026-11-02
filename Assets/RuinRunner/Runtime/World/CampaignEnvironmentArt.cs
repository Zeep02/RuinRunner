using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Architecture-attached dressing with reserved visual footprints.</summary>
    public static class CampaignEnvironmentArt
    {
        public const float MonumentWallInset=.375f;
        public static void Build(Transform parent,StageTerrainLayout terrain,int stage,EnvironmentPlacementRules placement=null)
        {
            var steps=BuildSteps(parent,terrain,stage,placement,false);
            while(steps.MoveNext()){}
        }
        public static IEnumerator BuildProgressively(Transform parent,StageTerrainLayout terrain,int stage,EnvironmentPlacementRules placement)
            =>BuildSteps(parent,terrain,stage,placement,true);
        static IEnumerator BuildSteps(Transform parent,StageTerrainLayout terrain,int stage,EnvironmentPlacementRules placement,bool progressive)
        {
            if(stage==0)
            {
                if(progressive)yield return CainosRuinsArt.BuildProgressively(parent,terrain,placement);
                else CainosRuinsArt.Build(parent,terrain,placement);
                yield break;
            }
            var budget=new TerrainBuildBudget(progressive);
            var root=new GameObject("Campaign environment landmarks");root.transform.SetParent(parent,false);
            var occupied=new List<Rect>();var theme=StageTerrainArt.Theme(stage);
            // Every solid island has one complete object instead of unrelated stacked props.
            for(var region=0;region<terrain.Rooms.Length;region++)
            foreach(var obstacle in terrain.Rooms[region].platforms)
            {
                var bounds=ClosedCells(terrain,terrain.Centers[region]+obstacle.position,obstacle.size);
                if(bounds.width==0)continue;
                if(stage==1)Place(root.transform,"FloodedCistern",IndividualArt.Sprite("Campaign/Terrain/Flooded/Cistern"),bounds.center,1,32,false,occupied);
                else
                {
                    // Fixed authored cluster width: 86 pixels at 32PPU. No per-frame inference.
                    var scale=bounds.width/(86f/32f);
                    Place(root.transform,theme+"Cluster",IndividualArt.Sprite("Campaign/Environment/"+theme+"Cluster"),bounds.center,scale,32,false,occupied);
                }
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
            for(var region=0;region<terrain.Rooms.Length;region++)
            {
                var center=terrain.Centers[region];var room=terrain.Rooms[region];
                // One ritual floor belongs to the guardian arena, not every combat room.
                if(stage==1 && room.bossRoom && OpenFootprint(terrain,center,2.1f))
                    Place(root.transform,theme+"Mosaic",IndividualArt.Sprite("Campaign/Environment/"+theme+"Mosaic"),center,1,4,false,occupied);
                if(region==0 || room.bossRoom)
                {
                    for(var attempt=0;attempt<Mathf.FloorToInt(room.Size.x)-3;attempt++)
                    {
                        var offset=attempt==0?0:((attempt+1)/2)*(attempt%2==1?1:-1);
                        if(!NorthWall(terrain,center,room.Size,center.x+offset,1.55f,out var point))continue;
                        point+=Vector2.down*MonumentWallInset;
                        if(!Place(root.transform,theme+"Monument",IndividualArt.Sprite("Campaign/Environment/"+theme+"Monument"),point,1,0,true,occupied,placement))continue;
                        foreach(var side in new[]{-1,1})
                            if(NorthWall(terrain,center,room.Size,point.x+side*3,.55f,out var relic))
                                Place(root.transform,theme+"WallRelic",StageTerrainArt.Sprite(stage,"Props/urn",relic),relic,1,24,false,occupied,placement);
                        break;
                    }
                }
                // Lamps follow architectural bays and avoid the reserved monuments/relics.
                foreach(var side in new[]{-1,1})
                for(var attempt=0;attempt<4;attempt++)
                {
                    if(!NorthWall(terrain,center,room.Size,center.x+side*(6+attempt),.55f,out var point))continue;
                    if(Place(root.transform,theme+"WallLamp",StageTerrainArt.Sprite(stage,"Props/lamp",point),point,1,24,false,occupied,placement))break;
                }
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
            if(placement!=null)
            {
                if(progressive)yield return placement.BuildSupportedPropsProgressively(root.transform,stage,occupied);
                else placement.BuildSupportedProps(root.transform,stage,occupied);
            }
        }
        static bool NorthWall(StageTerrainLayout terrain,Vector2 roomCenter,Vector2 roomSize,float preferredX,float halfWidth,out Vector2 result)
        {
            var x=Mathf.Floor(preferredX)+.5f;
            for(var y=Mathf.Floor(roomCenter.y)+.5f;y<roomCenter.y+roomSize.y*.5f+3;y++)
            {
                var point=new Vector2(x,y);if(!terrain.IsOpen(point+Vector2.down))continue;
                var supported=true;
                for(var dx=-halfWidth;dx<=halfWidth+.01f;dx+=.25f)
                    if(terrain.IsOpen(point+new Vector2(dx,0)) || !terrain.IsOpen(point+new Vector2(dx,-1))){supported=false;break;}
                if(supported){result=point;return true;}
            }
            result=default;return false;
        }
        static Rect ClosedCells(StageTerrainLayout terrain,Vector2 center,Vector2 size)
        {
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(var y=Mathf.FloorToInt(center.y-size.y*.5f);y<Mathf.CeilToInt(center.y+size.y*.5f);y++)
            for(var x=Mathf.FloorToInt(center.x-size.x*.5f);x<Mathf.CeilToInt(center.x+size.x*.5f);x++)
                if(!terrain.IsOpen(new Vector2(x+.5f,y+.5f))){min=Vector2.Min(min,new Vector2(x,y));max=Vector2.Max(max,new Vector2(x+1,y+1));}
            return float.IsInfinity(min.x)?default:new Rect(min,max-min);
        }
        static bool OpenFootprint(StageTerrainLayout terrain,Vector2 center,float half)
        {
            for(var y=-half;y<=half+.01f;y+=.25f)for(var x=-half;x<=half+.01f;x+=.25f)
                if(!terrain.IsOpen(center+new Vector2(x,y)))return false;
            return true;
        }
        static bool Place(Transform parent,string name,Sprite sprite,Vector2 point,float scale,int order,bool depthSorted,List<Rect> occupied,EnvironmentPlacementRules placement=null)
        {
            if(sprite==null)throw new System.InvalidOperationException("Missing themed environment sprite: "+name);
            var bounds=sprite.bounds;var min=point+(Vector2)bounds.min*scale;
            var rect=new Rect(min,(Vector2)bounds.size*scale);
            var padded=new Rect(rect.position-Vector2.one*.18f,rect.size+Vector2.one*.36f);
            if(placement!=null&&placement.IsReserved(padded))return false;
            foreach(var used in occupied)if(padded.Overlaps(used))return false;
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.position=point;obj.transform.localScale=Vector3.one*scale;
            var renderer=obj.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
            renderer.sortingLayerName=depthSorted?"Default":"WorldFloor";renderer.sortingOrder=depthSorted?900-Mathf.RoundToInt(point.y*100):order;
            occupied.Add(padded);return true;
        }
    }
}

