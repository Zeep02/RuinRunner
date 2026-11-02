using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class PresentationImportQA20260930
{
    static string DetectPipeline()
    {
        var asset=GraphicsSettings.currentRenderPipeline;if(asset==null)return "BuiltIn";
        var name=asset.GetType().FullName??"";
        return name.Contains("Universal")?"URP":name.Contains("HighDefinition")?"HDRP":"Custom";
    }
    public static string Main()
    {
        var report=new JObject{{"pipeline",DetectPipeline()},{"antiAliasing",QualitySettings.antiAliasing}};
        var inputs=new[]{"combat-presentation-import.json","reward-presentation-import.json","equipment-adopted.json"};
        var frames=0;var audio=0;var errors=new JArray();
        foreach(var name in inputs)
        {
            var manifest=JObject.Parse(File.ReadAllText("ArtDirection/Review-20260930/"+name,Encoding.UTF8));
            var rows=(JArray)(manifest["imagesAndSounds"]??manifest["images"]);
            foreach(JObject row in rows)
            {
                var path=(string)row["path"];
                if(path.EndsWith(".wav"))
                {
                    audio++;var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if(clip==null||clip.length<=0)errors.Add(path+" missing sound");continue;
                }
                frames++;var importer=AssetImporter.GetAtPath(path) as TextureImporter;var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(importer==null||sprite==null){errors.Add(path+" missing sprite");continue;}
                if(importer.filterMode!=FilterMode.Point||importer.mipmapEnabled||importer.textureCompression!=TextureImporterCompression.Uncompressed)
                    errors.Add(path+" sampling/compression");
                if(Mathf.Abs(sprite.pixelsPerUnit-(float)row["ppu"])>.001f)errors.Add(path+" PPU differs from adopted size");
                if(row["grounded"]!=null&&(bool)row["grounded"]&&sprite.pivot!=new Vector2(16,0))errors.Add(path+" chest foot pivot");
            }
        }
        var cameras=new JArray();foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>())
        {
            var components=new JArray();foreach(var c in camera.GetComponents<Component>())if(c!=null)components.Add(c.GetType().FullName);
            cameras.Add(new JObject{{"name",camera.name},{"orthographic",camera.orthographic},{"MSAA",camera.allowMSAA},{"HDR",camera.allowHDR},{"dynamicResolution",camera.allowDynamicResolution},{"components",components}});
        }
        report["sprites"]=frames;report["audioClips"]=audio;report["cameras"]=cameras;report["errors"]=errors;
        File.WriteAllText("ArtDirection/Review-20260930/presentation-import-qa.json",report.ToString(),new UTF8Encoding(false));
        if(errors.Count>0)throw new Exception(report.ToString());return report.ToString();
    }
}
