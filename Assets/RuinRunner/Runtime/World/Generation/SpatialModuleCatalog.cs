using System;
using System.Collections.Generic;

namespace RuinRunner.Generation
{
    public static class SpatialModuleCatalog
    {
        // No authored building is rotated or reflected. Each theme has eight different floor/obstacle compositions.
        static readonly ModuleDefinition[][] catalog = { OuterRuins(), FloodedHalls(), SealCore() };
        public static IReadOnlyList<ModuleDefinition> ForStage(int stage)
        { if (stage < 0 || stage >= catalog.Length) throw new ArgumentOutOfRangeException(nameof(stage)); return catalog[stage]; }
        static MapRect R(float x, float y, float w, float h) => new MapRect(x, y, w, h);
        static MapRect[] P(params MapRect[] pieces) => pieces;
        static ModuleDefinition[] OuterRuins() => new[]
        {
            M("ruins-cross-court", "무너진 교차 마당", "Ruins", P(R(-16,-5,32,10), R(-9,-12,18,24)), P(R(-6,5,3,3),R(3,-9,3,3))),
            M("ruins-offset-garden", "엇갈린 정원", "Ruins", P(R(-18,-4,36,8),R(-4,-13,8,26),R(-15,2,15,8),R(0,-10,15,8)), P(R(-12,5,3,3),R(8,-8,3,3))),
            M("ruins-collapsed-plaza", "붕괴 광장", "Ruins", P(R(-16,-9,32,18),R(-10,-13,20,26)), P(R(-10,-6,4,4),R(5,3,5,4),R(7,-7,3,3))),
            M("ruins-procession-road", "행렬의 옛길", "Ruins", P(R(-20,-5,40,10),R(-6,-11,12,22),R(-16,-8,8,16)), P(R(-13,3,3,3),R(9,-4,5,2))),
            M("ruins-split-foundation", "갈라진 기단", "Ruins", P(R(-17,-4,34,8),R(-4,-14,8,28),R(-14,-11,11,10),R(3,1,11,10)), P(R(-11,-8,4,4),R(7,5,4,4))),
            M("ruins-three-terraces", "세 단의 정원", "Ruins", P(R(-18,-5,36,10),R(-12,-10,24,20),R(-5,-14,10,28)), P(R(-9,5,3,3),R(6,-8,3,3),R(9,4,3,3))),
            M("ruins-broken-cloister", "끊긴 회랑", "Ruins", P(R(-18,-10,36,20),R(-7,-13,14,26)), P(R(-12,-6,5,3),R(-12,3,5,3),R(7,-6,5,3),R(7,3,5,3))),
            M("ruins-gate-wings", "날개 성문", "Ruins", P(R(-20,-4,40,8),R(-5,-12,10,24),R(-16,-9,9,18),R(7,-7,10,14)), P(R(-13,3,3,4),R(10,-5,4,3)))
        };
        static ModuleDefinition[] FloodedHalls() => new[]
        {
            M("flood-cistern-docks", "저수조 선착장", "Flooded", P(R(-18,-4,36,8),R(-5,-14,10,28),R(-14,0,28,10)), P(R(-10,5,4,3),R(6,5,4,3))),
            M("flood-parallel-weirs", "쌍둑 수로", "Flooded", P(R(-18,-11,36,22),R(-5,-14,10,28)), P(R(-13,-7,9,3),R(4,4,9,3))),
            M("flood-stair-basin", "계단 물받이", "Flooded", P(R(-18,-4,36,8),R(-13,-8,26,16),R(-8,-13,16,26)), P(R(-10,3,4,3),R(5,-9,3,4))),
            M("flood-crook-pier", "굽은 부두", "Flooded", P(R(-20,-4,40,8),R(-5,-12,10,24),R(-16,-9,18,7),R(0,2,15,7)), P(R(-13,-7,5,3),R(8,4,4,3))),
            M("flood-lock-gallery", "수문 열주실", "Flooded", P(R(-17,-10,34,20),R(-8,-14,16,28)), P(R(-11,-7,3,5),R(-11,3,3,4),R(8,-7,3,4),R(8,2,3,5))),
            M("flood-reservoir-arms", "가지 저수로", "Flooded", P(R(-19,-5,38,10),R(-5,-14,10,28),R(-13,0,9,11),R(4,-11,9,11)), P(R(-11,5,4,3),R(7,-8,4,3))),
            M("flood-sunken-court", "가라앉은 뜰", "Flooded", P(R(-15,-11,30,22),R(-19,-4,38,8),R(-6,-14,12,28)), P(R(-11,-6,4,4),R(6,3,4,5),R(7,-7,4,3))),
            M("flood-pump-chamber", "펌프실", "Flooded", P(R(-19,-5,38,10),R(-13,-10,26,20),R(-4,-14,8,28)), P(R(-10,3,6,4),R(4,-7,6,4)))
        };
        static ModuleDefinition[] SealCore() => new[]
        {
            M("seal-cruciform", "봉인 십자실", "Core", P(R(-18,-6,36,12),R(-7,-14,14,28)), P(R(-5,8,3,3),R(2,-11,3,3))),
            M("seal-offset-vault", "어긋난 금고", "Core", P(R(-18,-4,36,8),R(-4,-14,8,28),R(-14,0,12,11),R(2,-11,12,11)), P(R(-11,5,5,3),R(6,-8,5,3))),
            M("seal-pillared-crown", "기둥 왕관실", "Core", P(R(-17,-10,34,20),R(-10,-14,20,28)), P(R(-12,-7,4,4),R(-12,3,4,4),R(8,-7,4,4),R(8,3,4,4))),
            M("seal-long-sanctum", "긴 성소", "Core", P(R(-21,-5,42,10),R(-8,-12,16,24)), P(R(-16,-3,5,2),R(11,1,5,2),R(-6,6,3,3))),
            M("seal-forked-archive", "갈래 기록실", "Core", P(R(-19,-4,38,8),R(-4,-14,8,28),R(-15,-11,11,8),R(4,3,11,8)), P(R(-12,-9,4,4),R(8,5,4,4))),
            M("seal-three-rings", "삼중 봉인대", "Core", P(R(-19,-4,38,8),R(-14,-9,28,18),R(-7,-14,14,28)), P(R(-11,3,3,4),R(8,-7,3,4),R(-5,8,3,3),R(2,-11,3,3))),
            M("seal-twin-reliquary", "쌍 유물실", "Core", P(R(-20,-5,40,10),R(-5,-14,10,28),R(-16,-10,10,20),R(6,-10,10,20)), P(R(-13,-6,4,4),R(9,3,4,4))),
            M("seal-broken-citadel", "갈라진 성채", "Core", P(R(-18,-10,36,20),R(-7,-14,14,28),R(-21,-3,42,6)), P(R(-13,2,6,5),R(7,-7,6,5),R(-12,-7,3,3)))
        };

        static ModuleDefinition M(string id, string name, string theme, MapRect[] floor, MapRect[] obstacles)
        {
            float left = float.MaxValue, bottom = float.MaxValue, right = float.MinValue, top = float.MinValue;
            foreach (var piece in floor) { left = Math.Min(left,piece.X); bottom = Math.Min(bottom,piece.Y); right = Math.Max(right,piece.Right); top = Math.Max(top,piece.Top); }
            var module = new ModuleDefinition { Id=id, DisplayName=name, Theme=theme, FloorPieces=floor, Obstacles=obstacles,
                Bounds=new MapRect(left,bottom,right-left,top-bottom), Outline=TraceExterior(floor),
                PlayerAnchor=new MapPoint(-2,0), RewardAnchor=new MapPoint(0,-2.75f), ServiceAnchor=new MapPoint(0,2.5f), ExitAnchor=new MapPoint(0,5),
                SpawnCandidates=new[] {new MapPoint(-4,0),new MapPoint(4,0),new MapPoint(0,4),new MapPoint(0,-4),new MapPoint(-3,3),new MapPoint(3,-3)},
                DecorationSupports=new[] {R(-2,7,4,2),R(-2,-9,4,2)} };
            return module;
        }
        static MapPoint[] TraceExterior(MapRect[] floor)
        {
            var cells = new HashSet<long>();
            foreach (var r in floor) for(int y=(int)r.Y;y<r.Top;y++) for(int x=(int)r.X;x<r.Right;x++) cells.Add(Key(x,y));
            var edges = new Dictionary<long,long>();
            foreach(var key in cells)
            {
                int x=(int)(key>>32),y=(int)key;
                if(!cells.Contains(Key(x,y-1))) edges.Add(Key(x,y),Key(x+1,y));
                if(!cells.Contains(Key(x+1,y))) edges.Add(Key(x+1,y),Key(x+1,y+1));
                if(!cells.Contains(Key(x,y+1))) edges.Add(Key(x+1,y+1),Key(x,y+1));
                if(!cells.Contains(Key(x-1,y))) edges.Add(Key(x,y+1),Key(x,y));
            }
            long start=long.MaxValue; foreach(var key in edges.Keys) if(key<start)start=key;
            long current=start;var points=new List<MapPoint>();
            do { points.Add(new MapPoint((int)(current>>32),(int)current)); current=edges[current]; if(points.Count>edges.Count)throw new InvalidOperationException("Unclosed module boundary"); } while(current!=start);
            if(points.Count!=edges.Count)throw new InvalidOperationException("Module floor has a hole or disconnected component; express holes as obstacles");
            for(int i=points.Count-1;i>=0;i--)
            {
                var a=points[(i+points.Count-1)%points.Count];var b=points[i];var c=points[(i+1)%points.Count];
                if((b.X-a.X)*(c.Y-b.Y)==(b.Y-a.Y)*(c.X-b.X))points.RemoveAt(i);
            }
            return points.ToArray();
        }
        internal static long Key(int x,int y) => ((long)x<<32)|(uint)y;
    }
}
