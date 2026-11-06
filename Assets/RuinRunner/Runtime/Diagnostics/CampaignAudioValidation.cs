using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RuinRunner
{
    // Opt-in audio integration check. No combat automation or changes to user save/settings files.
    public sealed class CampaignAudioValidation : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private string folder;
        private GameController game;
        private GameSettingsService isolated;
        private GameAudioController controller;
        private float oldTimeScale, oldListenerVolume;
        private bool oldListenerPause;
        private bool standalone;
        private bool oldRunInBackground;
        private CampaignAudioCapture capture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-validate-campaign-audio") < 0)
                return;
            StartValidation(true);
        }

        public static string StartValidation(bool quitWhenFinished = false)
        {
            if (FindAnyObjectByType<CampaignAudioValidation>() != null)
                throw new Exception("Audio validation already running.");
            var runner = new GameObject("CampaignAudioValidation").AddComponent<CampaignAudioValidation>();
            runner.standalone = quitWhenFinished;
            runner.oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            runner.folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs/Validation/2026-10-01-campaign-music",
                (quitWhenFinished ? "windows-" : "editor-") + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(runner.folder);
            runner.StartCoroutine(runner.Guarded());
            return runner.folder;
        }

        private void Require(bool value, string label)
        {
            if (!value)
                throw new Exception(label);
            checks.Add("PASS " + label);
            File.AppendAllText(Path.Combine(folder, "progress.txt"), "PASS " + label + "\n", new System.Text.UTF8Encoding(false));
        }

        private IEnumerator Guarded()
        {
            var run = Run();
            Exception failure = null;
            while (true)
            {
                object value = null;
                bool more;
                try
                {
                    more = run.MoveNext();
                    if (more)
                        value = run.Current;
                }
                catch (Exception ex)
                {
                    failure = ex;
                    break;
                }
                if (!more)
                    break;
                yield return value;
            }
            if (capture != null)
            {
                capture.Write(Path.Combine(folder, "unity-output.wav"));
                Destroy(capture);
            }
            Time.timeScale = oldTimeScale;
            // A hidden standalone must keep ticking until Unity processes its quit request.
            if (!standalone)
                Application.runInBackground = oldRunInBackground;
            AudioListener.volume = oldListenerVolume;
            AudioListener.pause = oldListenerPause;
            if (game != null)
                GameAudioController.Create(game.Settings);
            GameAudioController.SetStage(game != null && game.IsRunActive ? game.CurrentStageIndex : 0);
            var result = (failure == null ? "PASS " + checks.Count(x => x.StartsWith("PASS ")) + " checks" : "FAIL " + failure) + "\n" + string.Join("\n", checks);
            File.WriteAllText(Path.Combine(folder, "result.txt"), result, new System.Text.UTF8Encoding(false));
            if (failure == null)
                Debug.Log("[CampaignAudioValidation] " + result);
            else
                Debug.LogError("[CampaignAudioValidation] " + result);
            if (standalone)
                Application.Quit(failure == null ? 0 : 1);
            Destroy(gameObject);
        }

        private AudioSource[] Sources() => controller.GetComponentsInChildren<AudioSource>();
        private AudioSource Music() => Sources().First(s => s.isPlaying && s.name.StartsWith("CampaignMusic_") && s.volume > .01f);
        private IEnumerator Run()
        {
            oldTimeScale = Time.timeScale;
            oldListenerVolume = AudioListener.volume;
            oldListenerPause = AudioListener.pause;
            var deadline = Time.realtimeSinceStartup + 15;
            while ((game = FindAnyObjectByType<GameController>()) == null || game.Settings == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new Exception("Game startup timeout");
                yield return null;
            }
            Require(!game.IsRunActive, "audio fixture stays in menu; no user slot or campaign state changed");
            isolated = new GameSettingsService(Path.Combine(folder, "isolated-settings.json"), false);
            GameAudioController.Create(isolated);
            AudioListener.volume = .8f;
            AudioListener.pause = false;
            controller = FindAnyObjectByType<GameAudioController>();
            var listeners = FindObjectsByType<AudioListener>();
            Require(listeners.Count(x => x.enabled) == 1, "one enabled AudioListener");
            capture = listeners.First(x => x.enabled).gameObject.AddComponent<CampaignAudioCapture>();
            capture.Configure(AudioSettings.outputSampleRate);
            Require(Sources().Length == 4, "fixed four sources for music and ambience crossfades");
            Require(Sources().All(s => s.spatialBlend == 0 && s.loop && !s.playOnAwake && s.pitch == 1), "2D original-pitch loop sources");
            for (var stage = 1; stage <= 3; stage++)
                foreach (var kind in new[] { "music", "ambience" })
                {
                    var clip = Resources.Load<AudioClip>("Audio/Campaign/stage" + stage + "-" + kind);
                    Require(clip != null && clip.channels == 2 && clip.frequency == 44100 && clip.loadType == AudioClipLoadType.Streaming,
                        "stage " + stage + " " + kind + " stereo 44.1k streaming asset");
                    if (kind == "music")
                        Require(clip.length > 80, "stage " + stage + " authored full-length loop");
                }
            GameAudioController.SetStage(0);
            yield return new WaitForSecondsRealtime(3.5f);
            var first = Music();
            Require(first.clip.name == "stage1-music" && first.isPlaying, "stage one replacement actually playing");
            float before = first.time;
            GameAudioController.SetStage(0);
            yield return new WaitForSecondsRealtime(.45f);
            Require(first.time > before, "same stage request preserves playback position");
            GameAudioController.SetStage(1);
            yield return new WaitForSecondsRealtime(.85f);
            var music = Sources().Where(s => s.name.StartsWith("CampaignMusic_")).ToArray();
            Require(music.All(s => s.isPlaying && s.volume > .01f), "both real music clips overlap during crossfade");
            Require(first.time > before, "outgoing clip continues instead of restarting or cutting");
            float energy = music.Sum(s => Mathf.Pow(s.volume / (isolated.GetVolume(AudioChannel.Music) * GameAudioController.MusicLevel(s.clip.name.StartsWith("stage1") ? 0 : 1)), 2));
            Require(Mathf.Abs(energy - 1) < .015f, "crossfade has equal power without doubled gain");
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(2.7f);
            Require(Music().clip.name == "stage2-music" && music.Count(s => s.isPlaying) == 1, "pause does not stall DSP fade and outgoing deck stops");
            Time.timeScale = oldTimeScale;
            Require(capture.Peak > .0001 && capture.Frames > AudioSettings.outputSampleRate, "Unity audio thread emits non-silent output");
            Require(capture.Peak < .95, "captured music and ambience do not clip");
            isolated.ChangeVolume(AudioChannel.Music, -1);
            yield return new WaitForSecondsRealtime(.2f);
            Require(music.All(s => s.volume == 0) && Sources().Any(s => s.name.StartsWith("CampaignAmbience_") && s.volume > 0),
                "music mute affects both decks but leaves ambience independent");
            isolated.ChangeVolume(AudioChannel.Ambience, -1);
            yield return new WaitForSecondsRealtime(.2f);
            Require(Sources().All(s => s.volume == 0), "ambience mute also reaches both decks");
            isolated.ChangeVolume(AudioChannel.Music, .6f);
            isolated.ChangeVolume(AudioChannel.Ambience, .5f);
            yield return new WaitForSecondsRealtime(.2f);
            Require(Music().isPlaying, "unmuting resumes existing loop");
            // Exercise a real decoder crossing the end of each imported loop.
            for (var stage = 0; stage < 3; stage++)
            {
                GameAudioController.SetStage(stage);
                yield return new WaitForSecondsRealtime(2.8f);
                var source = Music();
                Require(source.clip.name == "stage" + (stage + 1) + "-music", "select correct stage " + (stage + 1));
                source.time = source.clip.length - .3f;
                yield return new WaitForSecondsRealtime(.85f);
                Require(source.isPlaying && source.time < 2, "stage " + (stage + 1) + " decoder loops without stopping");
            }
            GameAudioController.SetStage(1);
            yield return new WaitForSecondsRealtime(.45f);
            GameAudioController.SetStage(0);
            GameAudioController.SetStage(2);
            GameAudioController.SetStage(0);
            yield return new WaitForSecondsRealtime(5.3f);
            Require(Music().clip.name == "stage1-music" && Sources().Length == 4, "rapid stage changes settle on latest request with no extra sources");
            var same = controller;
            GameAudioController.Create(isolated);
            Require(FindObjectsByType<GameAudioController>().Length == 1 && same == controller,
                "reconfigure does not duplicate controller");
            checks.Add("INFO outputRate=" + AudioSettings.outputSampleRate + " capturedFrames=" + capture.Frames + " peak=" + capture.Peak);
        }
    }

    public sealed class CampaignAudioCapture : MonoBehaviour
    {
        private readonly object gate = new object();
        private readonly List<float> samples = new List<float>();
        private int rate, channels;
        public float Peak { get; private set; }
        public int Frames { get; private set; }
        public void Configure(int sampleRate)
        {
            rate = sampleRate;
            MasterLimiter.OutputHandedOn = true;
        }
        private void OnAudioFilterRead(float[] data, int channelCount)
        {
            lock (gate)
            {
                if (rate != 0 && samples.Count <= rate * channelCount * 60)
                {
                    channels = channelCount;
                    samples.AddRange(data);
                    Frames += data.Length / channels;
                    foreach (var sample in data)
                        Peak = Mathf.Max(Peak, Mathf.Abs(sample));
                }
            }
            if (MasterLimiter.SilentRun)
                System.Array.Clear(data, 0, data.Length);
        }
        public void Write(string path)
        {
            lock (gate)
            {
                if (channels == 0)
                    return;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + samples.Count * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)channels);
                    writer.Write(rate);
                    writer.Write(rate * channels * 2);
                    writer.Write((short)(channels * 2));
                    writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(samples.Count * 2);
                    foreach (var sample in samples)
                        writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(sample * 32767), -32768, 32767));
                }
            }
        }
    }
}
