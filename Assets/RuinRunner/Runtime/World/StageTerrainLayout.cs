using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>One continuous walk mask shared by terrain, collision, navigation checks and maps.</summary>
    public sealed class StageTerrainLayout
    {
        public const float CellSize = .125f;
        public const float CorridorWidth = 4.5f;
        public readonly Rect Bounds;
        public readonly int Width;
        public readonly int Height;
        public readonly bool[] Walkable;
        public readonly bool[] Path;
        public readonly RoomTemplate[] Rooms;
        public readonly Vector2[] Centers;
        public readonly StagePathData[] Connections;
        public readonly StagePathData[] Trails;
        public readonly StagePathData[] Exploration;

        public StageTerrainLayout(RoomTemplate[] rooms, Vector2[] centers, StagePathData[] connections = null, StagePathData[] trails = null, StagePathData[] exploration = null, bool snapToDungeonTiles = false, bool deferSnappedMask = false)
        {
            Rooms = rooms; Centers = centers;
            Connections = connections ?? CreateStraightConnections(centers);
            Trails = trails ?? System.Array.Empty<StagePathData>();
            Exploration = exploration ?? System.Array.Empty<StagePathData>();
            var min = centers[0] - rooms[0].Size * .5f;
            var max = centers[0] + rooms[0].Size * .5f;
            for (var i = 1; i < rooms.Length; i++)
            { min = Vector2.Min(min, centers[i] - rooms[i].Size * .5f); max = Vector2.Max(max, centers[i] + rooms[i].Size * .5f); }
            foreach (var paths in new[] { Connections, Exploration })
                foreach (var path in paths) foreach (var point in path.points)
                { min = Vector2.Min(min, point - Vector2.one * path.width); max = Vector2.Max(max, point + Vector2.one * path.width); }
            min = new Vector2(Mathf.Floor(min.x - 7), Mathf.Floor(min.y - 7));
            max = new Vector2(Mathf.Ceil(max.x + 7), Mathf.Ceil(max.y + 7));
            Bounds = new Rect(min, max - min); Width = Mathf.CeilToInt(Bounds.width / CellSize); Height = Mathf.CeilToInt(Bounds.height / CellSize);
            Walkable = new bool[Width * Height]; Path = new bool[Walkable.Length];
            // Generated stages consume only the snapped mask. Build it directly instead of
            // evaluating every .125-unit cell and immediately replacing that result.
            // Existing callers retain the fine mask unless they explicitly request snapping.
            if (snapToDungeonTiles) { if(!deferSnappedMask)SnapToDungeonTiles(); return; }
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
            {
                var point = Point(x, y); var open = false; var dirt = false;
                for (var i = 0; i < rooms.Length; i++)
                {
                    var local = point - centers[i];
                    open |= ContainsRoom(rooms[i], local);
                    // Sparse irregular clearings, not repeated decorated blocks.
                    var patch = local - new Vector2(rooms[i].Size.x * .1f, -rooms[i].Size.y * .08f);
                    dirt |= new Vector2(patch.x / 3.8f, patch.y / 2.4f).sqrMagnitude < 1f + Mathf.Sin(local.x * 1.8f) * .16f;
                }
                foreach (var connection in Connections)
                {
                    var distance = PathDistance(point, connection);
                    open |= distance < connection.width * .5f;
                    dirt |= distance < connection.width * .26f;
                }
                foreach (var trail in Trails) dirt |= PathDistance(point, trail) < trail.width * .5f;
                foreach (var branch in Exploration)
                {
                    var distance = PathDistance(point, branch);
                    open |= distance < branch.width * .5f;
                    dirt |= distance < branch.width * .26f;
                }
                // Room obstacles are fused into the same land mass, never individually outlined tiles.
                for (var i = 0; i < rooms.Length && open; i++)
                    if (rooms[i].platforms != null) foreach (var obstacle in rooms[i].platforms)
                    {
                        var p = point - centers[i] - obstacle.position;
                        var n = new Vector2(p.x / Mathf.Max(.1f, obstacle.size.x * .5f), p.y / Mathf.Max(.1f, obstacle.size.y * .5f));
                        if (Mathf.Pow(Mathf.Abs(n.x), 4) + Mathf.Pow(Mathf.Abs(n.y), 4) < 1f) { open = false; break; }
                    }
                Walkable[y * Width + x] = open; Path[y * Width + x] = open && dirt;
            }
        }

        private static StagePathData[] CreateStraightConnections(Vector2[] centers)
        {
            var result = new StagePathData[Mathf.Max(0, centers.Length - 1)];
            for (var i = 0; i < result.Length; i++) result[i] = new StagePathData { points = new[] { centers[i], centers[i + 1] }, width = CorridorWidth };
            return result;
        }

        public void SnapToDungeonTiles()
        {
            var steps=BuildSnappedMask(false);
            while(steps.MoveNext()){}
        }
        public IEnumerator SnapToDungeonTilesProgressively()=>BuildSnappedMask(true);
        IEnumerator BuildSnappedMask(bool progressive)
        {
            // Shared navigation/minimap/collision mask follows exactly the same one-unit cells as the art.
            // Evaluate the authored geometry at the EXACT tile center. Reading the fine-grid array
            // rounds a center to the next .125-cell midpoint (+.0625 on both axes), skewing odd-sized
            // obstacles and leaving asymmetric slivers. The same snapped mask drives every consumer.
            var tileWidth=Mathf.CeilToInt(Bounds.width);var tileHeight=Mathf.CeilToInt(Bounds.height);
            var tiles=new bool[tileWidth*tileHeight];
            var budget=new TerrainBuildBudget(progressive);
            for(var ty=0;ty<tileHeight;ty++)
            {
                for(var tx=0;tx<tileWidth;tx++)
                    tiles[ty*tileWidth+tx]=ContainsAuthoredLand(Bounds.min+new Vector2(tx+.5f,ty+.5f));
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var p = Point(x, y);
                    var tx=Mathf.FloorToInt(p.x-Bounds.xMin);var ty=Mathf.FloorToInt(p.y-Bounds.yMin);
                    Walkable[y * Width + x] = tx>=0 && ty>=0 && tx<tileWidth && ty<tileHeight && tiles[ty*tileWidth+tx];
                    Path[y * Width + x] = false;
                }
                if(budget.Exhausted){yield return null;budget.Resume();}
            }
        }
        private bool ContainsAuthoredLand(Vector2 point)
        {
            var open=false;
            for(var i=0;i<Rooms.Length;i++)open|=ContainsRoom(Rooms[i],point-Centers[i]);
            foreach(var path in Connections)open|=PathDistance(point,path)<path.width*.5f;
            foreach(var path in Exploration)open|=PathDistance(point,path)<path.width*.5f;
            if(!open)return false;
            for(var i=0;i<Rooms.Length;i++)
                if(Rooms[i].platforms!=null)foreach(var obstacle in Rooms[i].platforms)
                {
                    var p=point-Centers[i]-obstacle.position;
                    var n=new Vector2(p.x/Mathf.Max(.1f,obstacle.size.x*.5f),p.y/Mathf.Max(.1f,obstacle.size.y*.5f));
                    if(Mathf.Pow(Mathf.Abs(n.x),4)+Mathf.Pow(Mathf.Abs(n.y),4)<1f)return false;
                }
            return true;
        }
        public static float PathDistance(Vector2 point, StagePathData path)
        {
            var distance = float.PositiveInfinity;
            for (var i = 1; i < path.points.Length; i++) distance = Mathf.Min(distance, SegmentDistance(point, path.points[i - 1], path.points[i]));
            return distance;
        }

        public Vector2 Point(int x, int y) => Bounds.min + new Vector2(x + .5f, y + .5f) * CellSize;
        public bool IsOpen(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Walkable[y * Width + x];
        public bool IsOpen(Vector2 point) => IsOpen(Mathf.FloorToInt((point.x - Bounds.xMin) / CellSize), Mathf.FloorToInt((point.y - Bounds.yMin) / CellSize));
        public static bool ContainsRoom(RoomTemplate room, Vector2 local)
        {
            if (room.walkableOutline != null && room.walkableOutline.Length >= 3)
            {
                var inside = false; var polygon = room.walkableOutline;
                for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                    if ((polygon[i].y > local.y) != (polygon[j].y > local.y) &&
                        local.x < (polygon[j].x - polygon[i].x) * (local.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
                return inside;
            }
            var n = new Vector2(local.x / (room.Size.x * .5f), local.y / (room.Size.y * .5f));
            if (Mathf.Abs(n.x) > 1f || Mathf.Abs(n.y) > 1f) return false;
            // Authored cardinal access points stay available; unused mouths end at the outer land contour.
            if (Mathf.Abs(local.x) < CorridorWidth * .5f || Mathf.Abs(local.y) < CorridorWidth * .5f) return true;
            var angle = Mathf.Atan2(n.y, n.x);
            var variation = .12f * Mathf.Sin(angle * 3f + room.Size.x * .2f) + .07f * Mathf.Cos(angle * 5f);
            return Mathf.Pow(Mathf.Abs(n.x), 3.3f) + Mathf.Pow(Mathf.Abs(n.y), 3.3f) < .88f + variation;
        }
        private static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            var delta = to - from; var t = Mathf.Clamp01(Vector2.Dot(point - from, delta) / Mathf.Max(.001f, delta.sqrMagnitude));
            return Vector2.Distance(point, from + delta * t);
        }
    }
}
