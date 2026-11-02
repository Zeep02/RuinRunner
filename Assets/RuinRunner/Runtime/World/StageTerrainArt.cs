using UnityEngine;

namespace RuinRunner
{
    /// <summary>Each stage owns its material artwork; topology is shared, textures are not.</summary>
    public static class StageTerrainArt
    {
        public static string Theme(int stage) => stage==0?"Outer":stage==1?"Flooded":"Core";
        public static Sprite Ground(int stage,Vector2Int cell,StageTerrainLayout terrain,System.Collections.Generic.HashSet<Vector2Int> floor)
        {
            if(stage==0)return CainosRuinsArt.Ground(cell);
            return NativeStageGround.Ground(stage,cell,terrain,floor);
        }
        public static Sprite Sprite(int stage,string id,Vector2 position)
        {
            if(stage==0&&id.StartsWith("Ribbon/"))return CainosRuinsArt.Sprite(id);
            if(id.StartsWith("Pool/"))return IndividualArt.Sprite("DungeonGathering/"+id);
            if(id=="Props/stones")id="Props/debris";
            if(id=="Torch/00")id="Props/lamp";
            if(id=="Urn/00")id="Props/urn";
            if(id.StartsWith("Floor/"))
            {
                var x=Mathf.FloorToInt(position.x);var y=Mathf.FloorToInt(position.y);
                id+="-"+((x%2+2)%2)+"-"+((y%2+2)%2);
            }
            return IndividualArt.Sprite("Campaign/Terrain/"+Theme(stage)+"/"+id);
        }
    }
}
