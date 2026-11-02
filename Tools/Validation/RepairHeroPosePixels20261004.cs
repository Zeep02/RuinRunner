using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

// Native editable joint-art correction. Keep all pose indexes, pivots and GUIDs.
// Old hand outlines were copied beyond the author's y<40 arm replacement strip.
// Restore only that lower strip from each pose's original, recorded footfall.
public static class RepairHeroPosePixels20261004
{
    const string Review="ArtDirection/Review-20261004/hero-pixel-cleanup/";
    const string Root="Assets/RuinRunner/ArtSources/Hero/";
    static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static Color32[] Read(string path)
    {
        var t=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try{if(!t.LoadImage(File.ReadAllBytes(path))||t.width!=48||t.height!=64)throw new Exception(path);return t.GetPixels32();}
        finally{UnityEngine.Object.DestroyImmediate(t);}
    }
    static void Save(Color32[] pixels,string path)
    {
        var t=new Texture2D(48,64,TextureFormat.RGBA32,false);
        try{t.SetPixels32(pixels);t.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,t.EncodeToPNG());}
        finally{UnityEngine.Object.DestroyImmediate(t);}
    }
    public static object Prepare()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        if(File.Exists(Review+"prepared.json"))throw new Exception("Preserve existing prepared revision");
        var audit=JObject.Parse(File.ReadAllText(Review+"foot-provenance-audit.json",Encoding.UTF8));
        if(audit["conflicts"].HasValues)throw new Exception("Ambiguous original foot provenance");
        var rows=new JArray();var cache=new Dictionary<string,Color32[]>();int total=0;
        foreach(JObject row in audit["frames"])
        {
            string path=row.Value<string>("path"),source=row.Value<string>("legSource");
            if(!path.StartsWith(Root,StringComparison.Ordinal))throw new Exception("Unexpected repair target");
            var before=Read(path);var after=(Color32[])before.Clone();
            Color32[] native;if(!cache.TryGetValue(source,out native))cache[source]=native=Read(source);
            // Unity pixel arrays begin at the bottom. Top-left rows 40..63 are
            // bottom rows 0..23. Never alter the new arm, head or backpack.
            Array.Copy(native,0,after,0,24*48);
            int changed=0;for(int i=0;i<after.Length;i++)if(!after[i].Equals(before[i])&&(after[i].a!=0||before[i].a!=0))changed++;
            if(changed!=((JArray)row["changes"]).Count)throw new Exception("Audit no longer matches "+path);
            string relative=path.Substring(Root.Length),backup=Review+"before/"+relative,candidate=Review+"candidate/"+relative;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup);File.Copy(path+".meta",backup+".meta");Save(after,candidate);
            rows.Add(new JObject{{"path",path},{"candidate",candidate},{"backup",backup},{"legSource",source},{"beforeSha256",Hash(path)},{"afterSha256",Hash(candidate)},{"metaSha256",Hash(path+".meta")},{"guid",AssetDatabase.AssetPathToGUID(path)},{"changedPixels",changed}});total+=changed;
        }
        var result=new JObject{{"phase","prepared"},{"images",rows.Count},{"changedPixels",total},{"rows",rows}};
        File.WriteAllText(Review+"prepared.json",result.ToString(),new UTF8Encoding(false));return new{images=rows.Count,changedPixels=total};
    }
    public static object Apply()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        if(File.Exists(Review+"applied.json"))throw new Exception("Preserve existing applied receipt");
        var data=JObject.Parse(File.ReadAllText(Review+"prepared.json",Encoding.UTF8));
        foreach(JObject row in data["rows"])
        {
            string path=row.Value<string>("path");
            if(Hash(path)!=row.Value<string>("beforeSha256")||Hash(path+".meta")!=row.Value<string>("metaSha256")||Hash(row.Value<string>("candidate"))!=row.Value<string>("afterSha256"))throw new Exception("Changed since preparation "+path);
        }
        AssetDatabase.StartAssetEditing();
        try{foreach(JObject row in data["rows"]){string path=row.Value<string>("path");File.Copy(row.Value<string>("candidate"),path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}}
        finally{AssetDatabase.StopAssetEditing();}
        foreach(JObject row in data["rows"])
        {
            string path=row.Value<string>("path");var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(Hash(path)!=row.Value<string>("afterSha256")||Hash(path+".meta")!=row.Value<string>("metaSha256")||AssetDatabase.AssetPathToGUID(path)!=row.Value<string>("guid")||s==null||s.pivot!=new Vector2(24,22)||s.rect!=new Rect(0,0,48,64)||s.pixelsPerUnit!=32)throw new Exception("Import identity/geometry "+path);
        }
        data["phase"]="applied";File.WriteAllText(Review+"applied.json",data.ToString(),new UTF8Encoding(false));
        return new{images=(int)data["images"],changedPixels=(int)data["changedPixels"],metadataChanged=0};
    }
    public static string StartApply()
    {
        if(Application.isPlaying||File.Exists(Review+"apply-started.txt"))throw new Exception("Stop Play / inspect the existing operation");
        File.WriteAllText(Review+"apply-started.txt",DateTime.UtcNow.ToString("o"),new UTF8Encoding(false));
        EditorApplication.delayCall+=()=>{try{Apply();}catch(Exception e){File.WriteAllText(Review+"apply-error.txt",e.ToString(),new UTF8Encoding(false));Debug.LogException(e);}};
        return "Scheduled existing-PNG correction; inspect applied.json or apply-error.txt before any retry.";
    }
    public static object PrepareOutlines()
    {
        if(Application.isPlaying||File.Exists(Review+"outlines-prepared.json"))throw new Exception("Stop Play / preserve prepared correction");
        var data=JObject.Parse(File.ReadAllText(Review+"prepared.json",Encoding.UTF8));
        var rows=(JArray)data["rows"];var byPath=new Dictionary<string,JObject>();
        foreach(JObject r in rows)byPath[r.Value<string>("path")]=r;
        var outlines=JObject.Parse(File.ReadAllText(Review+"reviewed-outline-removals.json",Encoding.UTF8));int total=0;
        foreach(JObject item in outlines["frames"])
        {
            string path=item.Value<string>("path");if(!path.StartsWith(Root,StringComparison.Ordinal))throw new Exception(path);
            JObject row;bool existing=byPath.TryGetValue(path,out row);
            string relative=path.Substring(Root.Length),candidate=Review+"candidate/"+relative,backup=Review+"before/"+relative;
            if(!existing)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup);File.Copy(path+".meta",backup+".meta");
                row=new JObject{{"path",path},{"candidate",candidate},{"backup",backup},{"beforeSha256",Hash(path)},{"metaSha256",Hash(path+".meta")},{"guid",AssetDatabase.AssetPathToGUID(path)},{"changedPixels",0}};rows.Add(row);byPath[path]=row;
            }
            var p=Read(existing?candidate:path);int changed=0;
            foreach(JArray xy in item["pixels"])
            {
                int x=(int)xy[0],y=(int)xy[1],index=(63-y)*48+x;var c=p[index];
                if(y<30||y>=40||c.a==0||c.r!=0||c.g!=0||c.b!=0)throw new Exception("Not the reviewed detached outline "+path);
                p[index]=new Color32(0,0,0,0);changed++;
            }
            Save(p,candidate);row["afterSha256"]=Hash(candidate);row["changedPixels"]=(int)row["changedPixels"]+changed;row["detachedOutlinePixels"]=changed;total+=changed;
        }
        File.Copy(Review+"prepared.json",Review+"prepared-foot-strip.json");
        data["images"]=rows.Count;data["changedPixels"]=(int)data["changedPixels"]+total;data["detachedOutlinePixels"]=total;
        File.WriteAllText(Review+"prepared.json",data.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Review+"outlines-prepared.json",outlines.ToString(),new UTF8Encoding(false));
        return new{images=rows.Count,detachedOutlinePixels=total,allChangedPixels=(int)data["changedPixels"]};
    }
}
