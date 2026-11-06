using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner.Generation
{
    public sealed class GeneratedUnityStage
    {
        public GeneratedStage Graph;
        public StageLayoutDefinition Definition;
        public RoomTemplate[] Rooms;
        public Vector2[] Centers;
        public StageTerrainLayout BuildTerrain(bool deferSnappedMask = false)
        {
            return new StageTerrainLayout(Rooms, Centers, Definition.connections, Definition.trails, Definition.exploration, snapToDungeonTiles: true, deferSnappedMask: deferSnappedMask);
        }
    }
    public static class GeneratedStageUnityAdapter
    {
        public static GeneratedUnityStage Create(int seed, int stage) => Convert(ProceduralStageGenerator.Generate(seed, stage));
        public static GeneratedUnityStage Convert(GeneratedStage graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            var result = new GeneratedUnityStage { Graph = graph, Rooms = new RoomTemplate[graph.Regions.Length], Centers = new Vector2[graph.Regions.Length] };
            var definition = new StageLayoutDefinition
            {
                regions = new StageRegionData[graph.Regions.Length],
                connections = new StagePathData[graph.Connections.Length],
                trails = Array.Empty<StagePathData>(),
                exploration = Array.Empty<StagePathData>()
            };
            var explorationRewards = new List<ExplorationRewardData>();
            foreach (var region in graph.Regions)
            {
                var module = region.Module;
                var room = ScriptableObject.CreateInstance<RoomTemplate>();
                room.roomId = region.Id;
                room.displayName = module.DisplayName;
                room.roomSize = new Vector2(module.Bounds.Width, module.Bounds.Height);
                room.walkableOutline = Array.ConvertAll(module.Outline, Vector);
                room.playerSpawn = Vector(module.PlayerAnchor);
                room.platforms = Array.ConvertAll(module.Obstacles, r => new RoomPlatformData(r.Center.X, r.Center.Y, r.Width, r.Height));
                room.decorations = Array.Empty<RoomDecorationData>();
                room.bossRoom = region.Role == RegionRole.Boss;
                room.automaticWaveSize = 3;
                if (region.Role == RegionRole.Combat || region.Role == RegionRole.Boss)
                {
                    room.enemies = RunContent.Encounters(graph.Seed, region.Index, room.bossRoom, graph.StageIndex);
                    for (int i = 0; i < room.enemies.Length; i++)
                        room.enemies[i].position = room.bossRoom ? Vector2.zero : Vector(module.SpawnCandidates[i % module.SpawnCandidates.Length]);
                }
                else
                    room.enemies = Array.Empty<EnemyPlacementData>();
                result.Rooms[region.Index] = room;
                result.Centers[region.Index] = Vector(region.Center);
                definition.regions[region.Index] = new StageRegionData
                {
                    name = module.DisplayName,
                    center = Vector(region.Center),
                    size = room.roomSize,
                    outline = room.walkableOutline,
                    land = room.platforms,
                    spawns = Array.ConvertAll(module.SpawnCandidates, Vector)
                };
                if (region.Role == RegionRole.Reward)
                    explorationRewards.Add(new ExplorationRewardData { name = "보급함", region = region.Index, position = Vector(region.RewardPosition) });
            }
            for (int i = 0; i < graph.Connections.Length; i++)
                definition.connections[i] = new StagePathData { points = Array.ConvertAll(graph.Connections[i].Points, Vector), width = graph.Connections[i].Width };
            definition.explorationRewards = explorationRewards.ToArray();
            result.Definition = definition;
            return result;
        }
        public static Vector2 Vector(MapPoint point) => new Vector2(point.X, point.Y);
        // Called by the Unity-owning integrator. Independent console tests cannot prove native renderer/physics parity.
        public static void AssertTerrainMatchesGeneratedMask(GeneratedUnityStage stage, StageTerrainLayout terrain)
        {
            var expected = new GeneratedWalkMask(stage.Graph);
            for (int y = 0; y < expected.Height; y++)
                for (int x = 0; x < expected.Width; x++)
                {
                    var point = new MapPoint(expected.Left + x + .5f, expected.Bottom + y + .5f);
                    if (expected.IsOpen(point) != terrain.IsOpen(Vector(point)))
                        throw new InvalidOperationException("Generated/Unity terrain mismatch at " + point.X + "," + point.Y);
                }
            foreach (var region in stage.Graph.Regions)
            {
                var point = Vector(region.PlayerPosition);
                if (!StageNavigation.IsClear(terrain, point) || StageNavigation.FindPath(terrain, Vector(stage.Graph.Entrance.PlayerPosition), point) == null)
                    throw new InvalidOperationException("Native navigation cannot reach " + region.Id);
            }
        }
    }
}
