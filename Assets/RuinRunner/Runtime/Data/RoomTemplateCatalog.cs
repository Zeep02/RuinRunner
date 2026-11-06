using System;
using UnityEngine;

namespace RuinRunner
{
    public static class RoomTemplateCatalog
    {
        private static readonly System.Collections.Generic.Queue<string>[] recentStructures =
        {new System.Collections.Generic.Queue<string>(),new System.Collections.Generic.Queue<string>(),new System.Collections.Generic.Queue<string>()};
        public static int SelectNormalRunSeed()
        {
            var args = System.Environment.GetCommandLineArgs();
            var seed = RunRandom.NewSeed();
            bool explicitSeed = System.Array.IndexOf(args, "--run-seed") >= 0 || System.Array.IndexOf(args, "--validation-seed") >= 0;
            RuinRunner.Generation.GeneratedStage[] generated = null;
            bool repeats = false;
            for (int attempt = 0; attempt < (explicitSeed ? 1 : 8); attempt++)
            {
                generated = new RuinRunner.Generation.GeneratedStage[CampaignStage.Count];
                repeats = false;
                for (int stage = 0; stage < generated.Length; stage++)
                {
                    generated[stage] = RuinRunner.Generation.ProceduralStageGenerator.Generate(seed, stage);
                    repeats |= recentStructures[stage].Contains(generated[stage].StructureSignature);
                }
                if (explicitSeed || !repeats)
                    break;
                if (attempt < 7)
                    seed = RunRandom.NewSeed();
            }
            foreach (var graph in generated)
            {
                var recent = recentStructures[graph.StageIndex];
                recent.Enqueue(graph.StructureSignature);
                while (recent.Count > 16)
                    recent.Dequeue();
            }
            Debug.Log("[Run Generation] seed=" + seed + " version=" + RuinRunner.Generation.ProceduralStageGenerator.Version + " explicit=" + explicitSeed + " repeatedRecent=" + repeats);
            return seed;
        }
        public static RoomTemplate[] CreateRandomRun()
        {
            var normals = new System.Collections.Generic.List<RoomTemplate>();
            RoomTemplate boss = null;
            foreach (var room in Resources.LoadAll<RoomTemplate>("Rooms"))
            {
                if (room.bossRoom)
                {
                    if (room.roomId == "warden_throne" || boss == null)
                        boss = room;
                }
                else if (room.selectionWeight > 0)
                    normals.Add(room);
            }
            normals.Sort((a, b) => string.CompareOrdinal(a.roomId, b.roomId));
            if (normals.Count < 3 || boss == null)
                throw new InvalidOperationException("Three encounters and the Warden template are required.");
            var result = new RoomTemplate[4];
            for (var i = 0; i < 3; i++)
            {
                var weight = 0f;
                foreach (var room in normals)
                    weight += room.selectionWeight;
                var roll = UnityEngine.Random.value * weight;
                var chosen = normals.Count - 1;
                for (var j = 0; j < normals.Count; j++)
                {
                    roll -= normals[j].selectionWeight;
                    if (roll <= 0)
                    {
                        chosen = j;
                        break;
                    }
                }
                result[i] = normals[chosen];
                normals.RemoveAt(chosen);
            }
            result[3] = boss;
            return result;
        }
    }
}
