using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The structures on solid islands, wall lamps, and the boss arena's floor mosaic.</summary>
    public static class CampaignEnvironmentArt
    {
                // How far along the wall a mounted lamp needs unbroken wall, so it never sits against a corner.
        const float WallArtClearance=1.5f,LampDropToFace=.75f;
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
                StageLandmarks.Place(root.transform,stage,terrain.Centers[region]+obstacle.position,obstacle.size,occupied);
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
            for(var region=0;region<terrain.Rooms.Length;region++)
            {
                var center=terrain.Centers[region];var room=terrain.Rooms[region];
                // One ritual floor belongs to the guardian arena, not every combat room.
                if(stage==1 && room.bossRoom && OpenFootprint(terrain,center,2.1f))
                    Place(root.transform,theme+"Mosaic",IndividualArt.Sprite("Campaign/Environment/"+theme+"Mosaic"),center,1,4,false,occupied);
                // Lamps hang on the wall's face, which drops below the wall line over the floor's top half-unit.
                foreach(var side in new[]{-1,1})
                for(var attempt=0;attempt<4;attempt++)
                {
                    if(!NorthWall(terrain,center,room.Size,center.x+side*(6+attempt),WallArtClearance,out var point))continue;
                    point+=Vector2.down*LampDropToFace;
                    if(Place(root.transform,theme+"WallLamp",StageTerrainArt.Sprite(stage,"Props/lamp",point),point,1,24,false,occupied,placement)){AmbientFx.Create(root.transform,point,stage==1?"AmbLampTeal":"AmbLampEmber").GetComponent<SpriteRenderer>().sortingLayerName="WorldFloor";break;}
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

