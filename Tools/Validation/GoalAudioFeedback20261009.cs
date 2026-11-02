using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using RuinRunner;

// Controlled production SFX/mix playback, not a normal combat or listening PASS.
// The original audio settings and listener are restored; no profile is opened.
public static class GoalAudioFeedback20261009
{
    static readonly List<string> timeline=new List<string>();
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false);
    static GameController game;
    static GameSettingsService oldCombat,isolated;
    static CampaignAudioCapture capture;
    static string folder;
    static double began;
    static float oldScale,oldVolume;
    static bool oldPause,running;
    public static string Main()
    {
        game=UnityEngine.Object.FindAnyObjectByType<GameController>();
        if(running||!Application.isPlaying||game==null||game.IsRunActive)throw new Exception("Idle Play menu required.");
        var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>();
        AudioListener listener=null;int enabled=0;
        foreach(var candidate in listeners)if(candidate.enabled){listener=candidate;enabled++;}
        if(enabled!=1)throw new Exception("Exactly one enabled listener required.");
        folder=Path.GetFullPath("Logs/Validation/2026-10-09-goal/audio-feedback-"+DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(folder);timeline.Clear();began=Time.realtimeSinceStartupAsDouble;
        oldScale=Time.timeScale;oldVolume=AudioListener.volume;oldPause=AudioListener.pause;
        oldCombat=(GameSettingsService)typeof(CombatAudio).GetField("settings",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        isolated=new GameSettingsService(Path.Combine(folder,"isolated-settings.json"),false);
        Time.timeScale=1;AudioListener.pause=false;AudioListener.volume=.8f;
        CombatAudio.Configure(isolated);PrototypeAudio.Configure(isolated);PrototypeAudioController.Create(isolated);
        capture=listener.gameObject.AddComponent<CampaignAudioCapture>();capture.Configure(AudioSettings.outputSampleRate);
        running=true;game.CameraFollow.StartCoroutine(Guard(Run()));return folder;
    }
    static void Mark(string label)=>timeline.Add((Time.realtimeSinceStartupAsDouble-began).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"\t"+label);
    static IEnumerator Run()
    {
        var controller=UnityEngine.Object.FindAnyObjectByType<PrototypeAudioController>();
        if(controller==null||!controller.isActiveAndEnabled)throw new Exception("Active production music controller required.");
        bool musicPlaying=false;
        foreach(var source in controller.GetComponentsInChildren<AudioSource>())
            if(source.name.StartsWith("CampaignMusic_")&&source.isPlaying&&source.volume>.01f)musicPlaying=true;
        if(!musicPlaying)throw new Exception("Start in a fresh menu with audible music; a stopped fixture deck is not listening evidence.");
        PrototypeAudioController.SetStage(0);Mark("stage one music and ambience");yield return new WaitForSecondsRealtime(2);
        foreach(WeaponFamily family in Enum.GetValues(typeof(WeaponFamily)))
        {
            Mark(family+" basic / special; production audio API at its normal gain");
            CombatAudio.PlayWeapon(family);yield return new WaitForSecondsRealtime(.55f);
            CombatAudio.PlayWeapon(family,true);yield return new WaitForSecondsRealtime(.75f);
        }
        Mark("Warden entrance / landing production gains");CombatAudio.Native("heavy-impact",AudioChannel.Monster,.28f,.8f);
        yield return new WaitForSecondsRealtime(.9f);CombatAudio.Native("heavy-impact",AudioChannel.Monster,.4f,1.05f);
        yield return new WaitForSecondsRealtime(.9f);
        PrototypeAudioController.SetStage(1);Mark("stage one to two crossfade");yield return new WaitForSecondsRealtime(3);
        Mark("Tide warning / active water");PrototypeAudio.Play(PrototypeSound.HeavyAttack,AudioChannel.Monster,.25f);
        yield return new WaitForSecondsRealtime(.65f);CombatAudio.Native("hazard-water",AudioChannel.Monster,.42f);
        yield return new WaitForSecondsRealtime(1);
        PrototypeAudioController.SetStage(2);Mark("stage two to three crossfade");yield return new WaitForSecondsRealtime(3);
        Mark("Seal ground impact / cross");CombatAudio.Native("heavy-impact",AudioChannel.Monster,.4f,.9f);
        yield return new WaitForSecondsRealtime(.75f);CombatAudio.Native("heavy-impact",AudioChannel.Monster,.27f,1.35f);
        yield return new WaitForSecondsRealtime(.8f);
        Mark("normal shotgun with four contact sounds");CombatAudio.PlayWeapon(WeaponFamily.Shotgun);
        yield return new WaitForSecondsRealtime(.15f);for(int i=0;i<4;i++)CombatAudio.PlayImpact(false);
        yield return new WaitForSecondsRealtime(1);
        foreach(var sound in new[]{PrototypeSound.UiClick,PrototypeSound.Inventory,PrototypeSound.Hit,PrototypeSound.Hurt,PrototypeSound.Dash,PrototypeSound.Block,PrototypeSound.Victory})
        {
            Mark("production feedback "+sound);PrototypeAudio.Play(sound,sound==PrototypeSound.Inventory?AudioChannel.Inventory:AudioChannel.Player,.35f);
            yield return new WaitForSecondsRealtime(.65f);
        }
        Mark("return to first stage music");PrototypeAudioController.SetStage(0);yield return new WaitForSecondsRealtime(3);
    }
    static IEnumerator Guard(IEnumerator run)
    {
        string failure=null;
        while(true)
        {
            bool next;object value=null;
            try{next=run.MoveNext();if(next)value=run.Current;}
            catch(Exception ex){failure=ex.ToString();break;}
            if(!next)break;yield return value;
        }
        double seconds=Time.realtimeSinceStartupAsDouble-began;
        if(capture==null||capture.Frames<AudioSettings.outputSampleRate||capture.Peak<.0001f||capture.Peak>=.95f)
            failure=failure??"Captured output missing, silent, or peak exceeds .95.";
        try
        {
            capture?.Write(Path.Combine(folder,"unity-output.wav"));
            File.WriteAllLines(Path.Combine(folder,"timeline.tsv"),timeline,Utf8);
            File.WriteAllText(Path.Combine(folder,"result.txt"),(failure==null?"PASS output capture and peak check":"FAIL "+failure)+
                "\nseconds="+seconds+" frames="+capture?.Frames+" peak="+capture?.Peak+
                "\nControlled production audio calls at default channel mix/master .8, real DSP clock. Not actual combat, headphone listening or game acceptance. No user save/settings writes.",Utf8);
        }
        finally
        {
            if(capture!=null)UnityEngine.Object.Destroy(capture);
            PrototypeAudio.Configure(game.Settings);CombatAudio.Configure(oldCombat);PrototypeAudioController.Create(game.Settings);
            PrototypeAudioController.SetStage(0);Time.timeScale=oldScale;AudioListener.volume=oldVolume;AudioListener.pause=oldPause;running=false;
        }
    }
}
