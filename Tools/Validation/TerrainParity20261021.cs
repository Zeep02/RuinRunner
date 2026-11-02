using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using RuinRunner;
using UnityEngine;

// Supplied isolated terrain only. No live run, save, or combat state is modified.
public static class TerrainParity20261021
{
    public static string Main(string mode)
    {
        var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(!Application.isPlaying||game==null||game.IsRunActive)throw new InvalidOperationException("Idle Play Mode menu required.");
        var root=new GameObject("TerrainParity_TEMP");
        root.AddComponent<TerrainParityRunner20261021>().StartCoroutine(Run(root,mode));
        return "Logs/Validation/2026-10-21-goal/terrain-"+mode+".json";
    }
    static IEnumerator Run(GameObject host,string mode)
    {
        var results=new JArray();
        for(int pass=0;pass<(mode=="baseline"?1:2);pass++)for(int stage=0;stage<3;stage++)
        {
            DungeonMap map=null;
            var time=System.Diagnostics.Stopwatch.StartNew();
            if(mode=="baseline")map=DungeonMap.BuildStage(_=>{},2026100903,stage);
            else
            {
                // Reflection keeps this fixture compilable before the new API exists.
                var method=typeof(DungeonMap).GetMethod("BuildStageProgressively");
                yield return (IEnumerator)method.Invoke(null,new object[]{(Action<DungeonMap>)(m=>map=m),(Action<int>)(_=>{}),2026100903,stage});
            }
            time.Stop();
            var row=new JObject{["pass"]=pass,["stage"]=stage,["signature"]=map.Graph.StructureSignature,
                ["hierarchyHash"]=Fingerprint(map.Root),["objects"]=map.Root.GetComponentsInChildren<Transform>(true).Length,
                ["elapsedMs"]=time.Elapsed.TotalMilliseconds};
            UnityEngine.Object.Destroy(map.Root);yield return null;yield return null;
            row["survivingStageRoots"]=UnityEngine.Object.FindObjectsByType<StageRuntimeOwnership>(FindObjectsInactive.Include).Length;
            row["textures"]=Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            row["meshes"]=Resources.FindObjectsOfTypeAll<Mesh>().Length;
            row["materials"]=Resources.FindObjectsOfTypeAll<Material>().Length;
            row["roomTemplates"]=Resources.FindObjectsOfTypeAll<RoomTemplate>().Length;
            results.Add(row);
        }
        var output=new JObject{["suppliedTerrainOnly"]=true,["rows"]=results};
        if(mode!="baseline")
        {
            var old=JObject.Parse(File.ReadAllText("Logs/Validation/2026-10-21-goal/terrain-baseline.json",Encoding.UTF8));
            output["matchesOriginalGeometry"]=results.All(r=>old["rows"].Any(b=>(int)b["stage"]==(int)r["stage"]&&(string)b["hierarchyHash"]==(string)r["hierarchyHash"]&&(string)b["signature"]==(string)r["signature"]));
        }
        File.WriteAllText("Logs/Validation/2026-10-21-goal/terrain-"+mode+".json",output.ToString(),new UTF8Encoding(false));
        UnityEngine.Object.Destroy(host);
    }
    static string Fingerprint(GameObject root)
    {
        var s=new StringBuilder();
        foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            s.Append(t.name).Append('|').Append(t.gameObject.layer).Append('|');
            V(s,t.localPosition);V(s,t.localScale);V(s,t.localEulerAngles);
            foreach(var c in t.GetComponents<BoxCollider2D>()){V(s,c.offset);V(s,c.size);s.Append(c.isTrigger);}
            foreach(var r in t.GetComponents<SpriteRenderer>())
            {s.Append(r.sprite?.name).Append('|').Append(r.sprite?.texture.name).Append('|').Append(r.sortingLayerName).Append('|').Append(r.sortingOrder).Append('|').Append(r.flipX).Append(r.flipY);s.Append(r.color.ToString());}
            foreach(var f in t.GetComponents<MeshFilter>())
            {foreach(var v in f.sharedMesh.vertices)V(s,v);foreach(var n in f.sharedMesh.triangles)s.Append(n).Append(',');}
            s.AppendLine();
        }
        using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()))).Replace("-","");
    }
    static void V(StringBuilder s,Vector3 v){s.Append(v.x.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(v.y.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(v.z.ToString("R",CultureInfo.InvariantCulture)).Append('|');}
}
public sealed class TerrainParityRunner20261021:MonoBehaviour{}
