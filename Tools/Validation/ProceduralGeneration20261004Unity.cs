using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RuinRunner;
using RuinRunner.Generation;
using UnityEngine;

// Only the designated Unity operator runs this. It creates no scene GameObjects and destroys temporary RoomTemplate instances.
public static class ProceduralGeneration20261004Unity
{
    public static object Main()
    {
        var verified=new List<string>();
        for(int stage=0;stage<3;stage++)for(int sample=0;sample<3;sample++)
        {
            var built=GeneratedStageUnityAdapter.Create(2026100400+sample,stage);
            try
            {
                var terrain=built.BuildTerrain();GeneratedStageUnityAdapter.AssertTerrainMatchesGeneratedMask(built,terrain);
                foreach(var region in built.Graph.Regions)
                {
                    foreach(var spawn in built.Rooms[region.Index].enemies)
                        if(!StageNavigation.IsClear(terrain,built.Centers[region.Index]+spawn.position))throw new InvalidOperationException("Native enemy spawn footprint "+region.Id);
                }
                verified.Add("stage="+stage+" seed="+built.Graph.Seed+" signature="+built.Graph.StructureSignature+" rooms="+built.Rooms.Length);
            }
            finally { foreach(var room in built.Rooms)if(room!=null)UnityEngine.Object.DestroyImmediate(room); }
        }
        var path=Path.GetFullPath("Logs/Validation/2026-10-04-procedural-generation/unity-adapter-parity.txt");Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path,"PASS: native StageTerrainLayout snapped tiles and StageNavigation parity. Not a renderer or normal playthrough test.\n"+string.Join("\n",verified),new UTF8Encoding(false));
        return new{passed=true,count=verified.Count,generatorVersion=ProceduralStageGenerator.Version,output=path};
    }
}
