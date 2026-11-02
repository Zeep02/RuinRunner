using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using RuinRunner.Generation;

namespace RuinRunner.EditorTools
{
    public static class StageLayoutValidation
    {
        [MenuItem("Prototype/Stage Designer/Validate Campaign")]
        public static void Validate()
        {
            var modules=new HashSet<string>();int verified=0,fallback=0,retries=0;
            for(int stage=0;stage<CampaignStage.Count;stage++)
            {
                var signatures=new HashSet<string>();
                for(int index=0;index<100;index++)
                {
                    int seed=2026100400+index;var built=GeneratedStageUnityAdapter.Create(seed,stage);
                    try
                    {
                        var terrain=built.BuildTerrain();GeneratedStageUnityAdapter.AssertTerrainMatchesGeneratedMask(built,terrain);
                        var geometry=GeneratedStageGeometry.Validate(built.Graph,true);
                        Require(geometry.Errors.Count==0,string.Join("; ",geometry.Errors));
                        Require(signatures.Add(built.Graph.StructureSignature),"duplicate stage structure in 100-seed sample");
                        Require(built.Graph.Boss.Role==RegionRole.Boss,"explicit boss role");
                        foreach(var region in built.Graph.Regions)
                        {
                            modules.Add(region.ModuleId);var center=built.Centers[region.Index];
                            foreach(var enemy in built.Rooms[region.Index].enemies)
                                Require(StageNavigation.IsClear(terrain,center+enemy.position),"native enemy footprint "+region.Id);
                        }
                        Require(StageNavigation.IsClear(terrain,GeneratedStageUnityAdapter.Vector(built.Graph.Boss.ExitPosition)),"boss exit footprint");
                        foreach(var connection in built.Graph.Connections)
                        {
                            var a=built.Centers[connection.FromIndex];var b=built.Centers[connection.ToIndex];
                            bool vertical=connection.FromPort==CardinalPort.North||connection.FromPort==CardinalPort.South;
                            var dimensions=vertical?new Vector3(connection.Width+.4f,.3f,10):new Vector3(.3f,connection.Width+.4f,10);
                            Require(StageNavigation.FindPath(terrain,a,b,new[]{new Bounds(GeneratedStageUnityAdapter.Vector(connection.DepartureGate),dimensions)})==null,"native seal bypass "+connection.Id);
                        }
                        if(built.Graph.Diagnostics.UsedFallback)fallback++;retries+=built.Graph.Diagnostics.Attempts-1;verified++;
                        if((index+1)%10==0)Debug.Log("[Generated Geography] stage="+stage+" tested="+(index+1)+"/100 version="+ProceduralStageGenerator.Version);
                    }
                    finally{foreach(var room in built.Rooms)if(room!=null)UnityEngine.Object.DestroyImmediate(room);}
                }
            }
            Require(modules.Count==24,"all authored modules covered");Require(fallback==0,"unexpected fallback concentration");
            Debug.Log("[Generated Geography] PASS "+verified+" native terrain masks/navigation cases; modules="+modules.Count+" retries="+retries+" fallback="+fallback+". Geometry fixture, not normal campaign or final environment review.");
        }

        [MenuItem("Prototype/Stage Designer/Validate Legacy Authored Fixtures")]
        public static void ValidateAuthoredForValidation()
        {
            var random = UnityEngine.Random.state; var checks = 0;
            try
            {
                for (var stage = 0; stage < CampaignStage.Count; stage++)
                for (var seed = 0; seed < 10; seed++)
                {
                    var definition = StageLayoutDefinition.Load(stage);
                    var context = "stage=" + (stage+1) + " seed=" + (9100+seed) + " ";
                    UnityEngine.Random.InitState(9100 + seed);
                    var rooms = definition.CreateEncounterRooms(RoomTemplateCatalog.CreateRandomRun(), 9100 + seed, stage);
                    var centers = Array.ConvertAll(definition.regions, region => region.center);
                    var terrain = new StageTerrainLayout(rooms, centers, definition.connections, definition.trails, definition.exploration);
                    terrain.SnapToDungeonTiles();
                    var start = centers[0] + rooms[0].playerSpawn;
                    for (var i = 0; i < rooms.Length; i++)
                    {
                        Require(StageNavigation.IsClear(terrain, centers[i] + RoomLayoutRules.RewardPosition), context + "reward footprint " + i);
                        Require(StageNavigation.FindPath(terrain, start, centers[i] + RoomLayoutRules.RewardPosition) != null, context + "reward access " + i);
                        var reward=RunContent.RollChest(9100+seed, stage*16+i, 2, i==rooms.Length-1);
                        Require(reward.Weapon != null && (i < rooms.Length-1 || reward.Accessory != null), context+"reward data "+i);
                        foreach (var enemy in rooms[i].enemies)
                        {
                            Require(StageNavigation.IsClear(terrain, centers[i] + enemy.position), context + "enemy footprint " + i + " " + enemy.position);
                            Require(StageNavigation.FindPath(terrain, start, centers[i] + enemy.position) != null, context + "enemy access " + i);
                            checks++;
                        }
                    }
                    for (var i = 0; i < definition.connections.Length; i++)
                    {
                        var path = definition.connections[i]; var a = path.points[0]; var b = path.points[path.points.Length - 1];
                        var vertical = Mathf.Abs((path.points[1] - a).y) > .5f;
                        var dimensions = vertical ? new Vector3(path.width + .4f, .3f, 10) : new Vector3(.3f, path.width + .4f, 10);
                        var arrivalVertical = Mathf.Abs((b-path.points[path.points.Length-2]).y) > .5f;
                        var arrivalDimensions = arrivalVertical ? new Vector3(path.width+.4f,.3f,10) : new Vector3(.3f,path.width+.4f,10);
                        Require(StageNavigation.FindPath(terrain, centers[i], centers[i + 1], new[] { new Bounds(a, dimensions) }) == null, context + "departure seal bypass " + i);
                        Require(StageNavigation.FindPath(terrain, centers[i], centers[i + 1], new[] { new Bounds(b, arrivalDimensions) }) == null, context + "arrival seal bypass " + i);
                        Require(StageNavigation.FindPath(terrain, centers[i], centers[i + 1]) != null, context + "open corridor " + i);
                        checks += 3;
                    }
                    Require(definition.explorationRewards != null && definition.explorationRewards.Length > 0, context + "optional reward required");
                    foreach (var reward in definition.explorationRewards)
                    {
                        Require(StageNavigation.IsClear(terrain, reward.position), context+"optional footprint");
                        Require(StageNavigation.FindPath(terrain, centers[reward.region], reward.position) != null, context+"optional reward reachable");
                        Require(StageNavigation.FindPath(terrain, reward.position, centers[reward.region]) != null, context+"optional return route");
                        checks+=3;
                    }
                    Require(StageNavigation.IsClear(terrain, centers[3]+new Vector2(0,3)),context+"stage exit footprint");
                    Require(StageNavigation.FindPath(terrain,start,centers[3]+new Vector2(0,3))!=null,context+"stage exit reachable");
                    Debug.Log("[Campaign Geography] PASS " + context);
                    foreach (var room in rooms) UnityEngine.Object.DestroyImmediate(room);
                }
                Debug.Log("[Campaign Geography] PASS 3 stages x 10 seeds (9100..9109); " + checks + " footprint/routes/seal-cut/optional checks on shared terrain mask. Not a normal gameplay completion.");
            }
            finally { UnityEngine.Random.state = random; }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
