using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using RuinRunner;
using RuinRunner.Generation;

// Native Unity-only parity checkpoint. Parent runs this, never the console validation agent.
public static class ProceduralGeneration20261004TerrainInitialization
{
    public static object Main()
    {
        long cells=0;var records=new List<string>();
        for(int stage=0;stage<3;stage++)for(int sample=0;sample<3;sample++)
        {
            var built=GeneratedStageUnityAdapter.Create(2026100400+sample,stage);
            try
            {
                var clock=Stopwatch.StartNew();
                var before=new StageTerrainLayout(built.Rooms,built.Centers,built.Definition.connections,built.Definition.trails,built.Definition.exploration);
                before.SnapToDungeonTiles();var oldMs=clock.Elapsed.TotalMilliseconds;clock.Restart();
                var direct=built.BuildTerrain();var directMs=clock.Elapsed.TotalMilliseconds;
                if(before.Width!=direct.Width||before.Height!=direct.Height||before.Bounds!=direct.Bounds)throw new InvalidOperationException("Terrain bounds changed");
                for(int i=0;i<before.Walkable.Length;i++)if(before.Walkable[i]!=direct.Walkable[i]||before.Path[i]!=direct.Path[i])throw new InvalidOperationException("Terrain mask bit mismatch: "+i);
                GeneratedStageUnityAdapter.AssertTerrainMatchesGeneratedMask(built,direct);
                cells+=direct.Walkable.Length;records.Add("stage="+stage+" seed="+built.Graph.Seed+" cells="+direct.Walkable.Length+" beforeMs="+oldMs.ToString("F2")+" directMs="+directMs.ToString("F2"));
            }
            finally{foreach(var room in built.Rooms)if(room!=null)UnityEngine.Object.DestroyImmediate(room);}
        }
        var path=Path.GetFullPath("Logs/Validation/2026-10-04-procedural-generation/terrain-initialization-native.txt");Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path,"PASS: native full-grid mask bit equality + navigation. Not normal campaign or renderer validation.\ncells="+cells+"\n"+string.Join("\n",records),new UTF8Encoding(false));
        return new{passed=true,cases=records.Count,cells,output=path};
    }
}
