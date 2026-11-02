using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// Bounded native Profiler sample capture. No gameplay or input changes.
public static class UiProfile20261021
{
    static bool enabled, editor, active;
    static int cursor;
    static double deadline;
    static string folder;
    static readonly JArray frames=new JArray();
    public static string Main()
    {
        if(active)throw new InvalidOperationException("Capture already active");
        folder=Path.GetFullPath("Logs/Validation/2026-10-21-goal/ui-profile-"+DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(folder);frames.Clear();
        enabled=ProfilerDriver.enabled;editor=ProfilerDriver.profileEditor;
        ProfilerDriver.profileEditor=false;ProfilerDriver.enabled=true;
        cursor=ProfilerDriver.lastFrameIndex;deadline=EditorApplication.timeSinceStartup+30;
        active=true;EditorApplication.update+=Update;
        return folder;
    }
    static void Update()
    {
        if(!active)return;
        int last=ProfilerDriver.lastFrameIndex;
        for(int index=Math.Max(cursor+1,ProfilerDriver.firstFrameIndex);index<=last;index++)
        using(var data=ProfilerDriver.GetRawFrameDataView(index,0))
        {
            if(!data.valid||data.frameTimeMs<25)continue;
            var samples=new JArray();
            for(int sample=0;sample<data.sampleCount;sample++)
                if(data.GetSampleTimeMs(sample)>=1)
                    samples.Add(new JObject{["name"]=data.GetSampleName(sample),["ms"]=data.GetSampleTimeMs(sample)});
            frames.Add(new JObject{["frame"]=index,["ms"]=data.frameTimeMs,["samples"]=samples});
        }
        cursor=last;
        if(EditorApplication.timeSinceStartup>=deadline)Finish();
    }
    public static string Finish()
    {
        if(!active)return folder;
        active=false;EditorApplication.update-=Update;
        ProfilerDriver.enabled=enabled;ProfilerDriver.profileEditor=editor;
        File.WriteAllText(folder+"/long-frames.json",frames.ToString(),new UTF8Encoding(false));
        return folder;
    }
}
