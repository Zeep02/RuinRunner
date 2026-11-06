using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Plays the game's sounds through the real voices, settings and limiter and records the final mix, for measuring.
    /// Writes mix.wav and timeline.csv (label, first frame). --audio-level-tour folder</summary>
    public sealed class AudioLevelTour : MonoBehaviour
    {
        public const string Argument = "--audio-level-tour";
        static readonly string[] Weapons = { "dagger", "sword", "katana", "cleave", "mace", "bow", "crossbow", "pistol", "rifle", "shotgun", "cannon", "magic" };
        static readonly string[] Others = { "impact", "heavy-impact", "hurt", "block", "dash", "hazard-water", "reward-common", "reward-uncommon", "reward-rare", "reward-epic", "reward-legendary", "reward-pickup", "upgrade" };
        CampaignAudioCapture capture;
        readonly StringBuilder timeline = new StringBuilder("label,frame\n");

        void Mark(string label) => timeline.Append(label).Append(',').Append(capture.Frames).Append('\n');

        IEnumerator Start()
        {
            var args = System.Environment.GetCommandLineArgs();
            var folder = args[System.Array.IndexOf(args, Argument) + 1];
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            var follow = CameraFollow.Create();
            var settings = new GameSettingsService(Path.Combine(folder, "settings.json"), false);
            capture = follow.gameObject.AddComponent<CampaignAudioCapture>();
            capture.Configure(AudioSettings.outputSampleRate);
            GameAudio.Configure(settings);
            for (var stage = 0; stage < 3; stage++)
            {
                GameAudioController.SetStage(stage);
                yield return new WaitForSecondsRealtime(3f);
                Mark("music" + stage);
                yield return new WaitForSecondsRealtime(5f);
            }
            GameAudioController.SetStage(0);
            yield return new WaitForSecondsRealtime(3f);
            foreach (var id in Weapons)
            {
                Mark(id);
                CombatAudio.Native(id, AudioChannel.Player, .42f);
                yield return new WaitForSecondsRealtime(.55f);
            }
            foreach (var id in Others)
            {
                Mark(id);
                CombatAudio.Native(id, id.StartsWith("reward") ? AudioChannel.Inventory : id == "impact" || id == "heavy-impact" || id == "hazard-water" ? AudioChannel.Monster : AudioChannel.Player, id == "heavy-impact" ? .45f : id == "impact" ? .4f : .42f);
                yield return new WaitForSecondsRealtime(.55f);
            }
            Mark("ui");
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .5f);
            yield return new WaitForSecondsRealtime(.4f);
            // The worst case: a shotgun and a room of enemies hit on the same instant, again and again.
            Mark("pileup");
            for (var i = 0; i < 40; i++)
            {
                CombatAudio.Native("impact", AudioChannel.Monster, .55f);
                CombatAudio.Native(i % 2 == 0 ? "shotgun" : "cleave", AudioChannel.Player, .52f);
                CombatAudio.Native("heavy-impact", AudioChannel.Monster, .45f);
                CombatAudio.Native("magic", AudioChannel.Monster, .5f);
                yield return new WaitForSecondsRealtime(.075f);
            }
            Mark("end");
            yield return new WaitForSecondsRealtime(.5f);
            capture.Write(Path.Combine(folder, "mix.wav"));
            File.WriteAllText(Path.Combine(folder, "timeline.csv"), timeline.ToString(), new UTF8Encoding(false));
            Application.Quit(0);
        }
    }
}
