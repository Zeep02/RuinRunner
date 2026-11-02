using System;
using System.Collections.Generic;

namespace RuinRunner.Generation
{
    // Deliberately independent of UnityEngine: the same generation contract runs in validation and on a server.
    [Serializable] public struct MapPoint
    {
        public float X, Y;
        public MapPoint(float x, float y) { X = x; Y = y; }
        public static MapPoint operator +(MapPoint a, MapPoint b) => new MapPoint(a.X + b.X, a.Y + b.Y);
        public static MapPoint operator -(MapPoint a, MapPoint b) => new MapPoint(a.X - b.X, a.Y - b.Y);
        public static MapPoint operator *(MapPoint a, float scale) => new MapPoint(a.X * scale, a.Y * scale);
        public float LengthSquared => X * X + Y * Y;
    }
    [Serializable] public struct MapRect
    {
        public float X, Y, Width, Height;
        public MapRect(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        public float Right => X + Width;
        public float Top => Y + Height;
        public MapPoint Center => new MapPoint(X + Width / 2, Y + Height / 2);
        public bool Contains(MapPoint p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Top;
        public bool Overlaps(MapRect b, float padding = 0) => X - padding < b.Right && Right + padding > b.X && Y - padding < b.Top && Top + padding > b.Y;
        public MapRect Translated(MapPoint point) => new MapRect(X + point.X, Y + point.Y, Width, Height);
    }
    public enum RegionRole { Entrance, Combat, Boss, Merchant, FieldUpgrade, Reward }
    public enum CardinalPort { East, North, West, South }
    public enum GenerationStream : uint { Layout = 101, Encounter = 201, Reward = 301, Merchant = 401, FieldUpgrade = 501, Decoration = 601 }

    [Serializable] public sealed class ModuleDefinition
    {
        public string Id, DisplayName, Theme;
        public MapRect Bounds;
        public MapPoint[] Outline;
        public MapRect[] Obstacles;
        // Floor pieces are authoring data; Outline is the one contiguous exterior contour consumed by Unity.
        public MapRect[] FloorPieces;
        public MapPoint[] SpawnCandidates;
        public MapPoint RewardAnchor, ServiceAnchor, PlayerAnchor, ExitAnchor;
        public MapRect[] DecorationSupports;
        public MapPoint Port(CardinalPort direction)
        {
            switch (direction)
            {
                case CardinalPort.East: return new MapPoint(Bounds.Right, 0);
                case CardinalPort.North: return new MapPoint(0, Bounds.Top);
                case CardinalPort.West: return new MapPoint(Bounds.X, 0);
                default: return new MapPoint(0, Bounds.Y);
            }
        }
    }
    [Serializable] public sealed class GeneratedRegion
    {
        public string Id, ModuleId, Service;
        public int Index, MainPathOrder = -1, GridX, GridY;
        public RegionRole Role;
        public MapPoint Center;
        public ModuleDefinition Module;
        public readonly List<string> Connections = new List<string>();
        public readonly List<string> Neighbors = new List<string>();
        public MapPoint RewardPosition => Center + Module.RewardAnchor;
        public MapPoint ServicePosition => Center + Module.ServiceAnchor;
        public MapPoint PlayerPosition => Center + Module.PlayerAnchor;
        public MapPoint ExitPosition => Center + Module.ExitAnchor;
    }
    [Serializable] public sealed class GeneratedConnection
    {
        public string Id, From, To;
        public int FromIndex, ToIndex;
        public CardinalPort FromPort, ToPort;
        public MapPoint[] Points;
        public MapPoint DepartureGate, ArrivalGate;
        public float Width;
        public bool MainPath;
    }
    [Serializable] public sealed class GenerationDiagnostics
    {
        public int Attempts;
        public bool UsedFallback, RepeatedRecentStructure;
        public readonly List<string> RejectedAttempts = new List<string>();
        public string ValidationSummary;
    }
    [Serializable] public sealed class GeneratedStage
    {
        public string GeneratorVersion, StructureSignature, EntranceId, BossId;
        public int Seed, StageIndex;
        public GeneratedRegion[] Regions;
        public GeneratedConnection[] Connections;
        public GenerationDiagnostics Diagnostics;
        public GeneratedRegion Entrance => FindRegion(EntranceId);
        public GeneratedRegion Boss => FindRegion(BossId);
        public GeneratedRegion FindRegion(string id)
        { foreach (var region in Regions) if (region.Id == id) return region; throw new ArgumentException("Unknown region: " + id); }
    }
    public struct GenerationRandom
    {
        uint state;
        public GenerationRandom(int seed, int stage, GenerationStream stream, int index = 0)
        {
            unchecked
            {
                state = (uint)seed ^ ((uint)stage * 0x9e3779b9u) ^ ((uint)stream * 0x85ebca6bu) ^ ((uint)index * 0xc2b2ae35u);
                state ^= state >> 16; state *= 0x7feb352du; state ^= state >> 15; state *= 0x846ca68bu; state ^= state >> 16;
                if (state == 0) state = 0x6d2b79f5u;
            }
        }
        public uint NextUInt() { unchecked { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; } }
        public int Next(int max)
        {
            if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
            uint limit = uint.MaxValue - uint.MaxValue % (uint)max, value;
            do value = NextUInt(); while (value >= limit);
            return (int)(value % (uint)max);
        }
        public void Shuffle<T>(IList<T> values)
        { for (int i = values.Count - 1; i > 0; i--) { int j = Next(i + 1); T value = values[i]; values[i] = values[j]; values[j] = value; } }
    }
}
