using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Native stone sizes and spatial wear patches, independent of collision geometry.</summary>
    public static class NativeStageGround
    {
        private static readonly Vector2Int[] Neighbors={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        public static Color Tint(int stage)=>stage==1?new Color(.42f,.62f,.85f):Color.white;
        public static Sprite Ground(int stage,Vector2Int cell,StageTerrainLayout terrain,HashSet<Vector2Int> floor)
        {
            if(stage==2)return IndividualArt.Sprite("External/ChampiDungeon/Ground");
            var point=(Vector2)cell+Vector2.one*.5f;
            var boundary=4;
            foreach(var direction in Neighbors)
                for(var distance=1;distance<boundary;distance++)
                    if(!floor.Contains(cell+direction*distance)){boundary=distance;break;}
            var traffic=Traffic(terrain,point);
            var wear=Mathf.PerlinNoise(cell.x*.18f+51.5f,cell.y*.18f+23.5f);
            if(stage==1)
            {
                // Moisture collects against walls/cisterns; foot traffic leaves
                // quieter stone. Continuous noise prevents square patch borders.
                wear+=boundary<=2?.13f:-.20f;
                if(traffic)wear-=.22f;
                var column=wear>.70f?4:wear>.59f?3:wear>.48f?2:Hash(cell.x,cell.y)%2;
                return IndividualArt.Sprite("External/RafaelRuins/Floor/"+(Mod(cell.y,2)*5+column).ToString("00"));
            }
            return IndividualArt.Sprite("External/FoozleDungeon/Floor/03");
        }
        private static bool Traffic(StageTerrainLayout terrain,Vector2 point)
        {
            foreach(var paths in new[]{terrain.Connections,terrain.Trails,terrain.Exploration})
                foreach(var path in paths)
                {
                    if(StageTerrainLayout.PathDistance(point,path)<Mathf.Clamp(path.width*.28f,.7f,1.25f))return true;
                    if(path.points.Length==0)continue;
                    foreach(var end in new[]{path.points[0],path.points[path.points.Length-1]})
                    {
                        var nearest=-1;var best=float.PositiveInfinity;
                        for(var i=0;i<terrain.Centers.Length;i++)
                        {
                            var size=terrain.Rooms[i].Size+Vector2.one*2;
                            if(!new Rect(terrain.Centers[i]-size*.5f,size).Contains(end))continue;
                            var distance=(end-terrain.Centers[i]).sqrMagnitude;
                            if(distance<best){best=distance;nearest=i;}
                        }
                        if(nearest<0)continue;
                        var delta=terrain.Centers[nearest]-end;
                        var t=Mathf.Clamp01(Vector2.Dot(point-end,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                        if(Vector2.Distance(point,end+delta*t)<1.25f)return true;
                    }
                }
            return false;
        }
        private static int Mod(int value,int modulus)=>(value%modulus+modulus)%modulus;
        private static int Hash(int x,int y){unchecked{var h=(uint)(x*73856093)^(uint)(y*19349663);h^=h>>13;h*=1274126177;return (int)(h&0x7fffffff);}}
    }
}
