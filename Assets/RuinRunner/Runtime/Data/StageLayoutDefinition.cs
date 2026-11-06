using System;
using UnityEngine;

namespace RuinRunner
{
    [Serializable]
    public sealed class StagePathData
    {
        public Vector2[] points;
        public float width = 4.5f;
    }
    [Serializable]
    public sealed class StageRegionData
    {
        public string name;
        public Vector2 center;
        public Vector2 size;
        public Vector2[] outline;
        public RoomPlatformData[] land;
        public Vector2[] spawns;
    }
    [Serializable]
    public sealed class ExplorationRewardData
    {
        public string name;
        public int region;
        public Vector2 position;
    }
    /// <summary>Terrain transfer data. Load reads legacy authored fixtures; normal runs use GeneratedStageUnityAdapter.</summary>
    [Serializable]
    public sealed class StageLayoutDefinition
    {
        public StageRegionData[] regions;
        public StagePathData[] connections;
        public StagePathData[] trails;
        public StagePathData[] exploration;
        public ExplorationRewardData[] explorationRewards;
        public static StageLayoutDefinition Load(int stageIndex = 0)
        {
            var resource = CampaignStage.Get(stageIndex).Resource;
            var json = Resources.Load<TextAsset>("Stages/" + resource);
            if (json == null)
                throw new InvalidOperationException("Missing campaign geography: " + resource);
            var stage = JsonUtility.FromJson<StageLayoutDefinition>(json.text);
            if (stage.regions == null || stage.regions.Length != 4 || stage.connections.Length != 3)
                throw new InvalidOperationException(resource + " needs four regions and three encounter connections");
            return stage;
        }
        public RoomTemplate[] CreateEncounterRooms(RoomTemplate[] encounters, int seed = 0, int stageIndex = 0)
        {
            if (encounters.Length != regions.Length)
                throw new InvalidOperationException("Encounter count differs from authored geography");
            var rooms = new RoomTemplate[regions.Length];
            for (var i = 0; i < rooms.Length; i++)
            {
                var area = regions[i];
                var room = UnityEngine.Object.Instantiate(encounters[i]);
                room.displayName = area.name;
                room.roomSize = area.size;
                room.walkableOutline = area.outline;
                room.platforms = area.land ?? Array.Empty<RoomPlatformData>();
                room.decorations = Array.Empty<RoomDecorationData>();
                room.playerSpawn = i == 0 ? new Vector2(-8, 0) : Vector2.zero;
                room.bossRoom = i == rooms.Length - 1;
                var placements = RunContent.Encounters(seed, i, room.bossRoom, stageIndex);
                for (var k = 0; k < placements.Length; k++)
                {
                    placements[k].position = EnemyRoles.IsBoss(placements[k].type) ? Vector2.zero : area.spawns[k % area.spawns.Length];
                }
                room.enemies = placements;
                rooms[i] = room;
            }
            return rooms;
        }
    }
    public sealed class StageRuntimeOwnership : MonoBehaviour
    {
        public RoomTemplate[] Rooms;
        private void OnDestroy()
        {
            if (Rooms == null)
                return;
            foreach (var room in Rooms)
                if (room != null)
                {
                    if (Application.isPlaying)
                        Destroy(room);
                    else
                        DestroyImmediate(room);
                }
        }
    }
}
