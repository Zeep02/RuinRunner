using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using RuinRunner;
using RuinRunner.EditorTools;

public static class GoalNativeCatalog20261009
{
    const string Report="ArtDirection/Review-20261009/hero-hurt-native/v5/catalog.json";
    public static string Main()
    {
        if(Application.isPlaying||EditorApplication.isCompiling)throw new Exception("Idle Edit Mode required.");
        File.WriteAllText(Report,"{\"status\":\"scheduled\"}",new UTF8Encoding(false));
        EditorApplication.delayCall+=Generate;return Report;
    }
    static void Generate()
    {
        double begin=EditorApplication.timeSinceStartup;
        try
        {
            if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)throw new Exception("Built-in pipeline expected.");
            var result=NativeHeroAtlasPrebuild.Generate();
            var catalog=AssetDatabase.LoadAssetAtPath<NativeHeroFrameCatalog>("Assets/RuinRunner/Resources/Art/NativeHero/WeaponHurtV1.asset");
            if(catalog==null||catalog.frames.Length!=5906)throw new Exception("Incomplete hurt catalog.");
            for(int i=0;i<catalog.frames.Length;i++)
                if(catalog.frames[i]==null||catalog.frames[i].rect!=new Rect(0,0,48,64)||catalog.frames[i].pivot!=new Vector2(24,22))
                    throw new Exception("Invalid native hurt reference at "+i);
            Write(new JObject{{"status","PASS generated catalogs/native references; Play validation pending"},{"frames",catalog.frames.Length},{"result",result},{"seconds",EditorApplication.timeSinceStartup-begin}});
        }
        catch(Exception error){Write(new JObject{{"status","FAIL"},{"error",error.ToString()}});Debug.LogException(error);}
    }
    static void Write(JObject value)=>File.WriteAllText(Report,value.ToString(),new UTF8Encoding(false));
}
