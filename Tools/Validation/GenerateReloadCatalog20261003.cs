using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using RuinRunner;
using RuinRunner.EditorTools;
using Newtonsoft.Json.Linq;

public static class GenerateReloadCatalog20261003
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play Mode");
        var pipeline=GraphicsSettings.currentRenderPipeline;
        if(pipeline!=null)throw new Exception("Existing built-in renderer changed: "+pipeline.GetType());
        string result=NativeHeroAtlasPrebuild.Generate();
        var data=JObject.Parse(File.ReadAllText("ArtDirection/Review-20261003/weapon-reload/reload-import-v2.json",Encoding.UTF8));
        var catalog=AssetDatabase.LoadAssetAtPath<NativeHeroFrameCatalog>("Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV2.asset");
        if(catalog==null||catalog.frames.Length!=10018)throw new Exception("Reload catalog missing");
        int samples=0;
        foreach(JObject row in data["frames"])
        {
            Vector2 h;bool b;
            var sprite=WeaponReloadFrames.Sample(row.Value<int>("clip"),row.Value<int>("entry"),row.Value<int>("direction"),row.Value<int>("variant"),row.Value<int>("key"),out h,out b);
            int index=row.Value<int>("runtimeIndex");
            if(sprite!=catalog.frames[index]||AssetDatabase.GetAssetPath(sprite)!=row.Value<string>("runtimePath")||h!=new Vector2((float)row["grip"][0],(float)row["grip"][1]))throw new Exception("Runtime lookup/source/hand mismatch");
            samples++;
        }
        var proof=new JObject{{"result","PASS"},{"pipeline","Built-in"},{"sources",catalog.frames.Length},{"samples",samples},{"catalogResult",result},{"scope","Actual importer geometry/settings and runtime sample mapping. Import request exceeded60s but completed; source authoring/copy was not repeated."}};
        File.WriteAllText("ArtDirection/Review-20261003/weapon-reload/editor-import-catalog.json",proof.ToString(),new UTF8Encoding(false));
        return proof.ToString();
    }
}
