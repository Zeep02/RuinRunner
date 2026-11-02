using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One live Editor owns imports and builds. Every candidate receives a new folder.
public static class BuildGoalCandidate20261009
{
    static string output;
    public static string Main()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Idle Edit Mode required.");
        output=Path.GetFullPath("Builds/GoalCandidate-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        if(Directory.Exists(output))throw new IOException("Candidate directory already exists.");
        Directory.CreateDirectory(output);
        Write(new JObject{["status"]="scheduled",["output"]=output,["unityVersion"]=Application.unityVersion});
        EditorApplication.delayCall+=Build;
        return output;
    }
    static void Build()
    {
        try
        {
            var sources=new JObject();
            using(var hash=SHA256.Create())
                foreach(var file in Directory.GetFiles("Assets/RuinRunner","*.cs",SearchOption.AllDirectories).OrderBy(p=>p))
                    sources[file.Replace('\\','/')]=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(file))).Replace("-","");
            File.WriteAllText(Path.Combine(output,"source-manifest.json"),sources.ToString(),new UTF8Encoding(false));
            Write(new JObject{["status"]="building",["output"]=output,["unityVersion"]=Application.unityVersion});
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=new[]{"Assets/RuinRunner/Scenes/Demo.unity"},
                locationPathName=Path.Combine(output,"RuinRunner.exe"),
                target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.None
            });
            var issues=new JArray();
            foreach(var step in report.steps)
                foreach(var message in step.messages)
                    if(message.type==LogType.Error||message.type==LogType.Exception||message.type==LogType.Warning)
                        issues.Add(new JObject{["type"]=message.type.ToString(),["message"]=message.content});
            Write(new JObject{["status"]=report.summary.result.ToString(),["output"]=output,["unityVersion"]=Application.unityVersion,
                ["errors"]=report.summary.totalErrors,["warnings"]=report.summary.totalWarnings,["seconds"]=report.summary.totalTime.TotalSeconds,
                ["bytes"]=(long)report.summary.totalSize,["issues"]=issues,["wholeGameAcceptanceComplete"]=false});
            Debug.Log("[Goal candidate] "+report.summary.result+" "+output);
        }
        catch(Exception error)
        {
            Write(new JObject{["status"]="Failed",["error"]=error.ToString(),["output"]=output});
            Debug.LogException(error);
        }
    }
    static void Write(JObject state)=>File.WriteAllText(Path.Combine(output,"build-result.json"),state.ToString(),new UTF8Encoding(false));
}
