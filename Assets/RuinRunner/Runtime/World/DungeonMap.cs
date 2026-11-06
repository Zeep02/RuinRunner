using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RuinRunner.Generation;

namespace RuinRunner
{
    public enum EncounterRegionPhase { Undiscovered, Discovered, Combat, Cleared }
    public sealed class EncounterRegionState
    {
        public EncounterRegionPhase Phase;
        public RewardChest Chest;
        public bool ChestOpened;
        public bool Visited;
    }
    public sealed class DungeonMap
    {
        public const float HorizontalSpacing = 54f;
        public const float VerticalSpacing = 36f;

        private readonly RoomExit[] exits;
        private readonly RoomTemplate[] rooms;
        private readonly Vector2[] centers;
        private readonly Vector2Int[] gridPositions;
        private readonly RoomTravelDirection[] travelDirections;

        public int RoomCount { get; }
        public GameObject Root { get; }
        public StageTerrainLayout Terrain { get; private set; }
        public bool IsAuthored { get; private set; }
        public EncounterRegionState[] Regions { get; }
        public int PhysicalRegion { get; private set; }
        public int MapRevision { get; private set; }
        public StageLayoutDefinition Definition { get; private set; }
        public int StageIndex { get; private set; }
        public GeneratedStage Graph { get; private set; }
        public bool IsProcedural => Graph != null;
        public int ConnectionCount => exits.Length;
        public int EntranceRoomIndex => Graph == null ? 0 : Graph.Entrance.Index;
        public int BossRoomIndex
        {
            get { if(Graph!=null)return Graph.Boss.Index;for(int i=0;i<rooms.Length;i++)if(rooms[i].bossRoom)return i;return rooms.Length-1; }
        }
        public Vector2 StageExitPosition => Graph == null ? GetRoomCenter(BossRoomIndex)+new Vector2(0,3) : GeneratedStageUnityAdapter.Vector(Graph.Boss.ExitPosition);
        public int VisitedRegionCount { get { int count=0;foreach(var region in Regions)if(region.Visited)count++;return count; } }
        public bool BossPrerequisitesCleared
        {
            get
            {
                for(int i=0;i<RoomCount;i++)if(i!=BossRoomIndex&&RequiresCombat(i)&&Regions[i].Phase!=EncounterRegionPhase.Cleared)return false;
                return true;
            }
        }

        private DungeonMap(RoomTemplate[] roomTemplates, GameObject root, RoomExit[] roomExits,
            Vector2[] roomCenters, Vector2Int[] roomGridPositions, RoomTravelDirection[] directions)
        {
            rooms = roomTemplates;
            RoomCount = roomTemplates.Length;
            Root = root;
            exits = roomExits;
            centers = roomCenters;
            gridPositions = roomGridPositions;
            travelDirections = directions;
            Regions = new EncounterRegionState[roomTemplates.Length];
            for (var i = 0; i < Regions.Length; i++) Regions[i] = new EncounterRegionState();
        }

        public static DungeonMap Build(RoomTemplate[] roomTemplates, Action<int> roomEntered)
        {
            var root = new GameObject("DungeonMap");
            var exits = new RoomExit[Mathf.Max(0, roomTemplates.Length - 1)];
            var centers = new Vector2[roomTemplates.Length];
            var gridPositions = new Vector2Int[roomTemplates.Length];
            var directions = new RoomTravelDirection[Mathf.Max(0, roomTemplates.Length - 1)];
            var occupied = new HashSet<Vector2Int> { Vector2Int.zero };

            for (var roomIndex = 0; roomIndex < roomTemplates.Length - 1; roomIndex++)
            {
                var chosen = ChooseFreeDirection(gridPositions[roomIndex], roomTemplates[roomIndex].preferredExitDirection, occupied);
                directions[roomIndex] = chosen;
                gridPositions[roomIndex + 1] = gridPositions[roomIndex] + DirectionOffset(chosen);
                occupied.Add(gridPositions[roomIndex + 1]);
            }

            for (var roomIndex = 0; roomIndex < roomTemplates.Length; roomIndex++)
                centers[roomIndex] = new Vector2(gridPositions[roomIndex].x * HorizontalSpacing, gridPositions[roomIndex].y * VerticalSpacing);

            var terrain = new StageTerrainLayout(roomTemplates, centers);
            ContinuousTerrainRenderer.Build(root.transform, terrain);
            for (var roomIndex = 0; roomIndex < roomTemplates.Length; roomIndex++)
            {
                var roomRoot = new GameObject("EncounterZone" + roomIndex); roomRoot.transform.SetParent(root.transform, false);
                if (roomTemplates[roomIndex].decorations != null)
                    foreach (var decoration in roomTemplates[roomIndex].decorations)
                    {
                        var position = centers[roomIndex] + decoration.position;
                        if (terrain.IsOpen(position)) EnvironmentProp.Create(roomRoot.transform, position, decoration.kind, Mathf.Max(.5f, decoration.scale));
                    }

                if (roomIndex < directions.Length)
                {
                    var opposite = (RoomTravelDirection)(((int)directions[roomIndex] + 2) % 4);
                    exits[roomIndex] = RoomExit.CreateConnection(root.transform,
                        centers[roomIndex] + roomTemplates[roomIndex].ExitPoint(directions[roomIndex]),
                        centers[roomIndex + 1] + roomTemplates[roomIndex + 1].ExitPoint(opposite),
                        directions[roomIndex], roomIndex + 1, roomEntered);
                }
            }

            return new DungeonMap(roomTemplates, root, exits, centers, gridPositions, directions) { Terrain = terrain };
        }

        public static DungeonMap BuildStage(RoomTemplate[] encounters, Action<int> entered, int seed = 0, int stageIndex = 0)
            => BuildStage(entered,seed,stageIndex);

        public static DungeonMap BuildStage(Action<int> entered,int seed=0,int stageIndex=0)
        {
            var map=CreateStage(seed,stageIndex);
            DungeonTilesetRenderer.Build(map.Root.transform,map.Terrain,stageIndex,map.Graph);
            map.CompleteStage(entered,seed);
            return map;
        }

        public static IEnumerator BuildStageProgressively(Action<DungeonMap> own,Action<int> entered,int seed,int stageIndex)
        {
            var map=CreateStage(seed,stageIndex,true);
            // Assign ownership before the first yield so cancellation destroys partial work too.
            // Keep owners active: Unity may omit OnDestroy for never-active components.
            // The controller pauses play and the HUD covers construction with its loading screen.
            own(map);
            yield return map.Terrain.SnapToDungeonTilesProgressively();
            yield return DungeonTilesetRenderer.BuildProgressively(map.Root.transform,map.Terrain,stageIndex,map.Graph);
            map.CompleteStage(entered,seed);
        }

        static DungeonMap CreateStage(int seed,int stageIndex,bool deferSnappedMask=false)
        {
            var built=GeneratedStageUnityAdapter.Create(seed,stageIndex);var graph=built.Graph;
            var root=new GameObject("GeneratedStage_"+stageIndex+"_"+graph.StructureSignature);
            root.AddComponent<StageRuntimeOwnership>().Rooms=built.Rooms;
            var terrain=built.BuildTerrain(deferSnappedMask);var grid=new Vector2Int[built.Rooms.Length];
            for(int i=0;i<grid.Length;i++)grid[i]=new Vector2Int(graph.Regions[i].GridX,graph.Regions[i].GridY);
            var gates=new RoomExit[graph.Connections.Length];var directions=new RoomTravelDirection[gates.Length];
            var map=new DungeonMap(built.Rooms,root,gates,built.Centers,grid,directions)
            {Terrain=terrain,IsAuthored=true,Definition=built.Definition,StageIndex=stageIndex,Graph=graph};
            return map;
        }
        void CompleteStage(Action<int> entered,int seed)
        {
            var graph=Graph;
            for(int i=0;i<exits.Length;i++)
            {
                var edge=graph.Connections[i];travelDirections[i]=(RoomTravelDirection)edge.FromPort;
                exits[i]=RoomExit.CreateGraphConnection(Root.transform,edge,StageTerrainArt.Theme(StageIndex),entered);
            }
            ResetExits();
            Debug.Log("[Generated Stage] version="+graph.GeneratorVersion+" seed="+seed+" stage="+StageIndex+" signature="+graph.StructureSignature+
                " regions="+graph.Regions.Length+" attempts="+graph.Diagnostics.Attempts+" fallback="+graph.Diagnostics.UsedFallback);
        }

        public RoomExit GetConnection(int index) => index >= 0 && index < exits.Length ? exits[index] : null;
        public string GetRegionId(int index) => Graph == null ? rooms[index].roomId : Graph.Regions[index].Id;
        public RegionRole GetRegionRole(int index) => Graph == null ? (rooms[index].bossRoom?RegionRole.Boss:RegionRole.Combat) : Graph.Regions[index].Role;
        public bool RequiresCombat(int index) => GetRegionRole(index)==RegionRole.Combat||GetRegionRole(index)==RegionRole.Boss;
        public IEnumerable<int> GetNeighbors(int index)
        {
            if(Graph==null){if(index>0)yield return index-1;if(index+1<RoomCount)yield return index+1;yield break;}
            foreach(var id in Graph.Regions[index].Neighbors)yield return Graph.FindRegion(id).Index;
        }
        public bool AreAdjacent(int from,int to)
        {if(from<0||to<0||from>=RoomCount||to>=RoomCount)return false;foreach(var neighbor in GetNeighbors(from))if(neighbor==to)return true;return false;}
        public bool CanEnter(int from,int to)
        {
            if(!AreAdjacent(from,to)||Regions[from].Phase==EncounterRegionPhase.Combat)return false;
            if(to==BossRoomIndex&&!BossPrerequisitesCleared)return false;
            return Regions[to].Phase!=EncounterRegionPhase.Combat;
        }
        public void MarkVisited(int index)
        {
            if(!Regions[index].Visited){Regions[index].Visited=true;MapRevision++;}
            if(Regions[index].Phase==EncounterRegionPhase.Undiscovered){Regions[index].Phase=EncounterRegionPhase.Discovered;MapRevision++;}
        }
        public RoomExit GetConnectionBetween(int first,int second)
        {
            if(Graph==null)return Mathf.Abs(first-second)==1?GetConnection(Mathf.Min(first,second)):null;
            foreach(var gate in exits)if(gate!=null&&gate.Connects(first,second))return gate;return null;
        }
        public Vector2 GetEntryPosition(int from,int to)
        {var connection=GetConnectionBetween(from,to);return connection==null?GetRoomCenter(to):connection.GetEntryPoint(to);}
        public int NextUnclearedCombat(int current)
        {
            // Routing targets are roles, not numeric indices; return the first hop toward the next required encounter.
            int target=-1;
            if(Graph!=null)
            {
                int order=int.MaxValue;
                foreach(var region in Graph.Regions)if(RequiresCombat(region.Index)&&Regions[region.Index].Phase!=EncounterRegionPhase.Cleared&&region.MainPathOrder<order)
                {target=region.Index;order=region.MainPathOrder;}
            }
            else for(int i=0;i<RoomCount;i++)if(Regions[i].Phase!=EncounterRegionPhase.Cleared){target=i;break;}
            if(target<0||target==current)return target;
            var previous=new int[RoomCount];for(int i=0;i<previous.Length;i++)previous[i]=-1;
            var queue=new Queue<int>();queue.Enqueue(current);previous[current]=current;
            while(queue.Count>0)
            {
                int cursor=queue.Dequeue();if(cursor==target)break;
                foreach(int next in GetNeighbors(cursor))if(previous[next]<0){previous[next]=cursor;queue.Enqueue(next);}
            }
            if(previous[target]<0)return -1;
            while(previous[target]!=current)target=previous[target];return target;
        }

        public Vector2 GetRoomCenter(int roomIndex) => centers[Mathf.Clamp(roomIndex, 0, RoomCount - 1)];
        public Vector2Int GetRoomGridPosition(int roomIndex) => gridPositions[Mathf.Clamp(roomIndex, 0, RoomCount - 1)];

        public Vector2 GetSpawnPoint(int roomIndex)
        {
            var center = GetRoomCenter(roomIndex);
            if(Graph!=null)return center+GetRoom(roomIndex).playerSpawn;
            if (roomIndex <= 0) return center + GetRoom(roomIndex).playerSpawn;
            switch (travelDirections[roomIndex - 1])
            {
                case RoomTravelDirection.Left: return center + GetRoom(roomIndex).Entrance(RoomTravelDirection.Right);
                case RoomTravelDirection.Up: return center + GetRoom(roomIndex).Entrance(RoomTravelDirection.Down);
                case RoomTravelDirection.Down: return center + GetRoom(roomIndex).Entrance(RoomTravelDirection.Up);
                default: return center + GetRoom(roomIndex).Entrance(RoomTravelDirection.Left);
            }
        }

        public RoomTemplate GetRoom(int roomIndex) => rooms[Mathf.Clamp(roomIndex, 0, RoomCount - 1)];
        public RoomTravelDirection GetExitDirection(int roomIndex)
        {
            if(Graph==null)return travelDirections.Length==0?RoomTravelDirection.Right:travelDirections[Mathf.Clamp(roomIndex,0,travelDirections.Length-1)];
            int next=NextUnclearedCombat(roomIndex);if(next<0||next==roomIndex)return RoomTravelDirection.Right;
            var delta=GetRoomCenter(next)-GetRoomCenter(roomIndex);
            return Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x>0?RoomTravelDirection.Right:RoomTravelDirection.Left):(delta.y>0?RoomTravelDirection.Up:RoomTravelDirection.Down);
        }

        public bool HasUniqueRooms()
        {
            for (var first = 0; first < rooms.Length; first++)
            for (var second = first + 1; second < rooms.Length; second++)
                if (rooms[first] == rooms[second]) return false;
            return true;
        }

        public void UnlockExit(int roomIndex)
        {
            Regions[roomIndex].Phase = EncounterRegionPhase.Cleared; MapRevision++;
            if(Graph!=null){RefreshGraphSeals();return;}
            if (roomIndex >= 0 && roomIndex < exits.Length) exits[roomIndex].Unlock();
            if (roomIndex > 0 && roomIndex - 1 < exits.Length) exits[roomIndex - 1].ReleaseEntrance();
        }

        public void BeginEncounter(int roomIndex)
        {
            if (Regions[roomIndex].Phase == EncounterRegionPhase.Combat || Regions[roomIndex].Phase == EncounterRegionPhase.Cleared) return;
            Regions[roomIndex].Phase = EncounterRegionPhase.Combat; MapRevision++;
            if(Graph!=null){RefreshGraphSeals();return;}
            if (roomIndex > 0 && roomIndex - 1 < exits.Length) exits[roomIndex - 1].SealEntrance();
        }

        public void TrackPosition(Vector2 position)
        {
            var physical = -1;
            for (var i = 0; i < RoomCount; i++)
            {
                if (StageTerrainLayout.ContainsRoom(rooms[i], position - centers[i])) physical = i;
                if (Regions[i].Phase != EncounterRegionPhase.Undiscovered) continue;
                bool nearEntrance=false;
                if(Graph!=null){foreach(var gate in exits)if((gate.SourceRoom==i||gate.TargetRoom==i)&&Vector2.Distance(position,gate.GetEntryPoint(i))<7f){nearEntrance=true;break;}}
                else nearEntrance=i>0&&Vector2.Distance(position,exits[i-1].ArrivalPosition)<7f;
                if (physical == i || nearEntrance)
                { Regions[i].Phase = EncounterRegionPhase.Discovered; MapRevision++; }
            }
            if (physical != PhysicalRegion) { PhysicalRegion = physical; MapRevision++; }
        }

        public void ResetExits()
        {
            foreach (var region in Regions) { region.Phase = EncounterRegionPhase.Undiscovered; region.Chest = null; region.ChestOpened = false;region.Visited=false; }
            PhysicalRegion = EntranceRoomIndex; MapRevision++;
            foreach (var roomExit in exits)
                if (roomExit != null) roomExit.ResetExit();
            if(Graph!=null)RefreshGraphSeals();
        }

        private void RefreshGraphSeals()
        {
            bool ready=BossPrerequisitesCleared;
            foreach(var gate in exits)
            {
                if(gate==null)continue;
                bool combat=Regions[gate.SourceRoom].Phase==EncounterRegionPhase.Combat||Regions[gate.TargetRoom].Phase==EncounterRegionPhase.Combat;
                bool bossLock=!ready&&(gate.SourceRoom==BossRoomIndex||gate.TargetRoom==BossRoomIndex);
                gate.SetGraphClosed(combat||bossLock);
            }
        }

        private static RoomTravelDirection ChooseFreeDirection(Vector2Int origin, RoomTravelDirection preferred, HashSet<Vector2Int> occupied)
        {
            for (var offset = 0; offset < 4; offset++)
            {
                var direction = (RoomTravelDirection)(((int)preferred + offset) % 4);
                if (!occupied.Contains(origin + DirectionOffset(direction))) return direction;
            }
            return preferred;
        }

        private static Vector2Int DirectionOffset(RoomTravelDirection direction)
        {
            switch (direction)
            {
                case RoomTravelDirection.Up: return Vector2Int.up;
                case RoomTravelDirection.Left: return Vector2Int.left;
                case RoomTravelDirection.Down: return Vector2Int.down;
                default: return Vector2Int.right;
            }
        }

    }
}
