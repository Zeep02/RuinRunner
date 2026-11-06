using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace RuinRunner
{
    /// <summary>Opt-in real frame-time measurement through results, town and re-departure.</summary>
    public sealed class PerformanceRecorder : MonoBehaviour
    {
        [Serializable] private sealed class Measurement
        {
            public string category;
            public int samples;
            public float seconds,meanFps,p50Ms,p95Ms,p99Ms,worstMs;
            public int over16_7Ms,over33_3Ms;
        }
        [Serializable] private sealed class Report
        {
            public string utc,cpu,gpu,graphicsApi,os,policy,completionReason,focusPolicy;
            public int schemaVersion=2,ramMb,width,height,targetFps,vsync,resultCount;
            public bool victory,recordingEnabled,frameTimingEnabled,frameTimingRequested,nativeForegroundSupported,fullscreen;
            public string[] phaseLabels;
            public Measurement[] measurements,foregroundMeasurements,phaseMeasurements;
        }
        enum Phase { Menu,Town,Combat,Boss,Exploration,Map,Inventory,Stats,Unlock,Service,Reward,BossIntro,Result,Paused,Transition,Startup }
        static readonly string[] PhaseLabels={"menu","town","combat","boss","exploration","map","inventory","stats","unlock","service","reward","boss-intro","result","paused","transition","startup"};
        static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        readonly Dictionary<string,List<float>> frames=new Dictionary<string,List<float>>();
        readonly Dictionary<string,List<float>> foregroundFrames=new Dictionary<string,List<float>>();
        readonly Dictionary<string,List<float>> phaseFrames=new Dictionary<string,List<float>>();
        readonly string[,] phaseKeys=new string[PhaseLabels.Length,3];
        readonly FrameTiming[] latestTiming=new FrameTiming[1];
        GameController game;
        string folder,legacyCombatKey,nativeCombatKey;
        StreamWriter memory,timing;
        float nextTiming,stopAt=float.PositiveInfinity,nextMemory,warmUntil;
        int lastStage=-1,skipFrames,resultCount;
        bool quitAtLimit,collectFrameTiming=true,previousEligible,finished,wasGameOver,observedVictory;
        bool nativeForegroundSupported,previousNativeAvailable,previousNativeForeground,previousUnityFocus,previousBoss;
        uint ownProcessId;
        Phase previousPhase=Phase.Startup;
        string completionReason="running";

        public void Initialize(GameController owner,string path)
        {
            game=owner;folder=Path.GetFullPath(path);Directory.CreateDirectory(folder);
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--performance-no-vsync")>=0)
                QualitySettings.vSyncCount=0; // Explicit A/B diagnostic; no saved preference changes.
            InitializeNativeForeground();
            for(int phase=0;phase<PhaseLabels.Length;phase++)
            {phaseKeys[phase,0]=PhaseLabels[phase]+"-foreground-unknown";phaseKeys[phase,1]=PhaseLabels[phase]+"-background";phaseKeys[phase,2]=PhaseLabels[phase]+"-foreground";}
            memory=new StreamWriter(Path.Combine(folder,"memory.csv"),false,Utf8);
            memory.WriteLine("seconds,stage,room,active,objects,unity_allocated_bytes,mono_used_bytes,managed_bytes,texture_count,texture_native_bytes,texture_native_available,sample_cost_ms,phase_code,focused,native_foreground_available,native_foreground");
            timing=new StreamWriter(Path.Combine(folder,"frame-timing.csv"),false,Utf8);
            timing.WriteLine("seconds,focused,width,height,stage,room,combat,cpu_frame_ms,cpu_main_ms,cpu_render_ms,present_wait_ms,gpu_ms,timing_available,native_foreground_available,native_foreground,fullscreen,phase_code");
            var limit=Array.IndexOf(args,"--performance-seconds");
            collectFrameTiming=Array.IndexOf(args,"--performance-cpu-clock-only")<0;
            if(limit>=0&&limit+1<args.Length&&float.TryParse(args[limit+1],NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)&&seconds>0)
                stopAt=Time.realtimeSinceStartup+seconds;
            quitAtLimit=Array.IndexOf(args,"--validation-quit")>=0;
            Debug.Log("[Performance] Real-time sampling enabled: "+folder);
        }

        void Update()
        {
            if(game==null||finished)return;
            bool nativeForeground=ReadNativeForeground(out var nativeAvailable),unityFocus=Application.isFocused;
            var phase=CurrentPhase();bool changedStage=game.CurrentStageIndex!=lastStage;
            if(changedStage){lastStage=game.CurrentStageIndex;warmUntil=Time.unscaledTime+2;previousEligible=false;}
            if(collectFrameTiming)FrameTimingManager.CaptureFrameTimings();
            float elapsedMs=Time.unscaledDeltaTime*1000;
            // Keep every lifecycle frame. A state/stage boundary is a transition,
            // so synchronous generation stalls cannot disappear into warm-up exclusions.
            var intervalPhase=previousPhase==Phase.Startup?Phase.Startup:changedStage||phase!=previousPhase?Phase.Transition:phase;
            int nativeBucket=!nativeAvailable?0:nativeForeground?2:1;
            AddSample(phaseFrames,phaseKeys[(int)intervalPhase,nativeBucket],elapsedMs);
            previousPhase=phase;
            bool boss=game.Boss!=null;
            if(legacyCombatKey==null||changedStage||boss!=previousBoss||unityFocus!=previousUnityFocus||nativeAvailable!=previousNativeAvailable||nativeForeground!=previousNativeForeground)
            {
                var prefix=(game.CurrentStageIndex+1)+"-"+(boss?"boss":"normal");
                legacyCombatKey=prefix+(unityFocus?"-focused":"-background");
                nativeCombatKey=prefix+(!nativeAvailable?"-foreground-unknown":nativeForeground?"-foreground":"-background");
                previousBoss=boss;previousUnityFocus=unityFocus;previousNativeAvailable=nativeAvailable;previousNativeForeground=nativeForeground;
            }
            bool eligible=game.IsRunActive&&!game.IsPaused&&game.EnemiesAlive>0&&Time.unscaledTime>warmUntil;
            if(eligible&&previousEligible&&skipFrames==0)
            {
                // Keep the legacy Unity-focus rule; the new series uses OS ownership.
                AddSample(frames,legacyCombatKey,elapsedMs);
                AddSample(foregroundFrames,nativeCombatKey,elapsedMs);
            }
            previousEligible=eligible;
            if(Time.unscaledTime>=nextTiming)
            {
                nextTiming=Time.unscaledTime+1;
                bool available=collectFrameTiming&&FrameTimingManager.GetLatestTimings(1,latestTiming)>0;
                var value=available?latestTiming[0]:default;
                timing.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:F3},{1},{2},{3},{4},{5},{6},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4},{12},{13},{14},{15},{16}",
                    Time.unscaledTime,unityFocus?1:0,Screen.width,Screen.height,game.CurrentStageIndex+1,game.CurrentRoom+1,eligible?1:0,
                    value.cpuFrameTime,value.cpuMainThreadFrameTime,value.cpuRenderThreadFrameTime,value.cpuMainThreadPresentWaitTime,value.gpuFrameTime,available?1:0,
                    nativeAvailable?1:0,nativeForeground?1:0,Screen.fullScreen?1:0,(int)phase));
            }
            if(skipFrames>0)skipFrames--;
            if(Time.unscaledTime>=nextMemory)
            {
                nextMemory=Time.unscaledTime+10;SampleMemory(phase,unityFocus,nativeAvailable,nativeForeground);
                skipFrames=2; // Same combat exclusion; lifecycle samples keep these frames.
            }
            if(game.IsGameOver&&!wasGameOver)resultCount++;
            wasGameOver=game.IsGameOver;observedVictory|=game.IsVictory;
            // Result is part of the journey: keep sampling town, retry and cleanup.
            if(Time.realtimeSinceStartup>=stopAt)
            {
                completionReason="diagnostic duration limit; partial run";WriteReport();finished=true;
                if(quitAtLimit)Application.Quit(0);
            }
        }

        Phase CurrentPhase()
        {
            if(!game.IsRunActive)return Phase.Menu;
            if(game.IsStageTransitioning||game.IsEncounterTransitioning||game.Phase==GameSessionPhase.Transition)return Phase.Transition;
            if(game.IsMapOpen)return Phase.Map;
            if(game.IsInventoryOpen)return Phase.Inventory;
            if(game.IsStatsOpen)return Phase.Stats;
            if(game.IsServiceOpen)return Phase.Service;
            if(game.IsChoosingAccessory)return Phase.Reward;
            if(game.IsGameOver)return Phase.Result;
            if(game.IsManuallyPaused)return Phase.Paused;
            if(game.Phase==GameSessionPhase.BossIntro)return Phase.BossIntro;
            if(game.IsTown)return Phase.Town;
            if(game.EnemiesAlive>0)return game.Boss==null?Phase.Combat:Phase.Boss;
            return Phase.Exploration;
        }

        static void AddSample(Dictionary<string,List<float>> destination,string key,float elapsed)
        {
            if(!destination.TryGetValue(key,out var samples))destination.Add(key,samples=new List<float>(256));
            samples.Add(elapsed);
        }

        void SampleMemory(Phase phase,bool unityFocus,bool nativeAvailable,bool nativeForeground)
        {
            double started=Time.realtimeSinceStartupAsDouble;
            int objects=RuntimeSceneObjects.FindAll<GameObject>().Length;
            var textures=Resources.FindObjectsOfTypeAll<Texture>();long nativeBytes=0;
            foreach(var texture in textures)if(texture!=null)nativeBytes+=Profiler.GetRuntimeMemorySizeLong(texture);
            long allocated=Profiler.GetTotalAllocatedMemoryLong(),mono=Profiler.GetMonoUsedSizeLong(),managed=GC.GetTotalMemory(false);
            double costMs=(Time.realtimeSinceStartupAsDouble-started)*1000;
            // Zero can mean Profiler unavailable, not empty GPU memory.
            memory.WriteLine(string.Format(CultureInfo.InvariantCulture,"{0:F3},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F4},{12},{13},{14},{15}",
                Time.unscaledTime,game.CurrentStageIndex+1,game.CurrentRoom+1,game.IsRunActive?1:0,objects,allocated,mono,managed,
                textures.Length,nativeBytes,nativeBytes>0?1:0,costMs,(int)phase,unityFocus?1:0,nativeAvailable?1:0,nativeForeground?1:0));
            memory.Flush();timing.Flush();
        }

        void InitializeNativeForeground()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try{ownProcessId=GetCurrentProcessId();nativeForegroundSupported=ownProcessId!=0;}
            catch(DllNotFoundException){nativeForegroundSupported=false;}
            catch(EntryPointNotFoundException){nativeForegroundSupported=false;}
#endif
        }
        bool ReadNativeForeground(out bool available)
        {
            available=false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(!nativeForegroundSupported)return false;
            try
            {
                var window=GetForegroundWindow();if(window==IntPtr.Zero)return false;
                available=GetWindowThreadProcessId(window,out var foregroundProcess)!=0;
                return available&&foregroundProcess==ownProcessId;
            }
            catch(DllNotFoundException){nativeForegroundSupported=false;}
            catch(EntryPointNotFoundException){nativeForegroundSupported=false;}
#endif
            return false;
        }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
#endif

        static Measurement[] Summarize(Dictionary<string,List<float>> groups)
        {
            var rows=new List<Measurement>();
            foreach(var pair in groups)
            {
                var values=pair.Value;if(values.Count==0)continue;
                values.Sort();double sum=0;int over16=0,over33=0;
                foreach(float value in values){sum+=value;if(value>16.7f)over16++;if(value>33.3f)over33++;}
                rows.Add(new Measurement{category=pair.Key,samples=values.Count,seconds=(float)(sum/1000),meanFps=(float)(values.Count*1000/sum),
                    p50Ms=values[(int)((values.Count-1)*.50f)],p95Ms=values[(int)((values.Count-1)*.95f)],p99Ms=values[(int)((values.Count-1)*.99f)],
                    worstMs=values[values.Count-1],over16_7Ms=over16,over33_3Ms=over33});
            }
            return rows.ToArray();
        }
        void WriteReport()
        {
            if(string.IsNullOrEmpty(folder))return;
            var report=new Report{utc=DateTime.UtcNow.ToString("O"),cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
                graphicsApi=SystemInfo.graphicsDeviceVersion,os=SystemInfo.operatingSystem,ramMb=SystemInfo.systemMemorySize,
                width=Screen.width,height=Screen.height,targetFps=Application.targetFrameRate,vsync=QualitySettings.vSyncCount,fullscreen=Screen.fullScreen,
                completionReason=completionReason,frameTimingEnabled=FrameTimingManager.IsFeatureEnabled(),frameTimingRequested=collectFrameTiming,
                victory=observedVictory,resultCount=resultCount,recordingEnabled=GetComponent<RunEvidenceRecorder>()!=null,nativeForegroundSupported=nativeForegroundSupported,
                phaseLabels=PhaseLabels,measurements=Summarize(frames),foregroundMeasurements=Summarize(foregroundFrames),phaseMeasurements=Summarize(phaseFrames),
                focusPolicy="measurements preserve legacy Application.isFocused categories. foregroundMeasurements and phaseMeasurements use native foreground-window ownership by this process; unknown never falls back to Unity focus. No other process/window names or IDs are recorded.",
                policy="Actual unscaled frame times. Steady combat excludes first 2s of each stage, first frame after pause, and 2 frames after 10s memory sampling. phaseMeasurements keep every frame, including state/stage transitions and diagnostic overhead; they are not steady-combat FPS. Sampling continues through result/town/re-departure until quit/duration limit. No forced GC or capture. CSV FrameTiming may lag; timing_available=0 means unknown, not zero cost. Texture native bytes use Unity Profiler (zero can mean unavailable, not zero VRAM). Recording/capture overhead must be considered separately."};
            File.WriteAllText(Path.Combine(folder,"performance.json"),JsonUtility.ToJson(report,true),Utf8);
            memory?.Flush();timing?.Flush();Debug.Log("[Performance] WROTE "+report.measurements.Length+" combat categories: "+folder);
        }
        void OnApplicationQuit()
        {
            if(!finished){completionReason=resultCount>0?"application quit; includes post-result lifecycle":"application quit; partial run";WriteReport();finished=true;}
            DisposeWriters();
        }
        void OnDestroy(){DisposeWriters();}
        void DisposeWriters(){memory?.Dispose();memory=null;timing?.Dispose();timing=null;}
    }
}
