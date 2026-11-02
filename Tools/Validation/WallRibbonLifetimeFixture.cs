using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RuinRunner;

// Native-object lifetime test, separate from the normal Windows run.
public static class WallRibbonLifetimeFixture
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        Application.runInBackground=true;
        var host=new GameObject("Temporary wall lifetime inspection");
        host.AddComponent<WallLifetimeRunner>().StartCoroutine(WallLifetimeRunner.Run(host));
        return "Started nine map construction/destruction cycles";
    }
}
public sealed class WallLifetimeRunner:MonoBehaviour
{
    public static IEnumerator Run(GameObject host)
    {
        var folder="F:/codex/LoglikeCap/Logs/Validation/2026-09-21-wall-lifetime";Directory.CreateDirectory(folder);
        var lines=new List<string>();var released=0;
        for(var cycle=0;cycle<9;cycle++)
        {
            var map=DungeonMap.BuildStage(RoomTemplateCatalog.CreateRandomRun(),_=>{},9100+cycle,cycle%3);
            var ribbon=map.Root.GetComponentInChildren<ContinuousWallRibbon>();
            var assets=new List<UnityEngine.Object>();
            foreach(var f in ribbon.GetComponentsInChildren<MeshFilter>())assets.Add(f.sharedMesh);
            foreach(var r in ribbon.GetComponentsInChildren<MeshRenderer>())assets.Add(r.sharedMaterial);
            map.Root.SetActive(false);UnityEngine.Object.Destroy(map.Root);
            yield return null;yield return null;
            foreach(var asset in assets)
                if(asset!=null)throw new Exception("Wall native object survived map destruction: "+asset.name);
            released+=assets.Count;lines.Add("cycle="+cycle+" stage="+(cycle%3+1)+" released="+assets.Count);
        }
        lines.Add("PASS nine maps; "+released+" native meshes/materials released. Not normal gameplay evidence.");
        File.WriteAllLines(folder+"/result.txt",lines);Debug.Log("[Wall Lifetime] "+lines[lines.Count-1]);
        UnityEngine.Object.Destroy(host);
    }
}
