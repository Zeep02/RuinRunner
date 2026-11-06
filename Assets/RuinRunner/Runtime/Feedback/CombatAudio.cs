using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Bounded 2D voices, independent cosmetic randomness and authored clips. Every clip is levelled on first use,
    /// so a caller's volume means the same thing for all of them.</summary>
    public sealed class CombatAudio : MonoBehaviour
    {
        // Loudest 50 ms of a clip at full volume, and the highest sample it may reach. Source recordings differ by 20 dB.
        const float TargetLoudness = .35f, PeakLimit = .9f;
        // The same sound landing again this soon adds only volume, and more than this many at once only noise.
        const float MinRepeatSeconds = .045f;
        const int MaxSameClip = 3;
        static CombatAudio instance;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<AudioClip> levelled = new HashSet<AudioClip>();
        static readonly System.Random variation = new System.Random(5429);
        readonly AudioSource[] voices = new AudioSource[16];
        readonly bool[] paused = new bool[16], interfaceVoice = new bool[16];
        readonly float[] started = new float[16];
        static GameSettingsService settings;
        public static void Configure(GameSettingsService value)
        {
            settings = value;
        }
        public static AudioClip Clip(string id)
        {
            if (!clips.TryGetValue(id, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/Combat/" + id);
                clips[id] = clip;
            }
            return clip;
        }
        static CombatAudio Ensure()
        {
            if (instance != null)
                return instance;
            var root = new GameObject("CombatAudioVoices");
            instance = root.AddComponent<CombatAudio>();
            for (var i = 0; i < instance.voices.Length; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(root.transform, false);
                var voice = child.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0;
                voice.dopplerLevel = 0;
                instance.voices[i] = voice;
            }
            return instance;
        }
        public static void PlayClip(AudioClip clip, AudioChannel channel, float volume, float pitch = 1, bool pauseWithGame = false)
        {
            if (clip == null)
                return;
            var ui = !pauseWithGame && (channel == AudioChannel.Interface || channel == AudioChannel.Inventory);
            if (!ui && Time.timeScale <= 0)
                return;
            var mix = settings == null ? 1 : settings.GetVolume(channel);
            if (mix <= 0)
                return;
            Level(clip);
            var pool = Ensure();
            var slot = -1;
            var same = 0;
            var now = Time.unscaledTime;
            for (var i = 0; i < pool.voices.Length; i++)
            {
                var voice = pool.voices[i];
                if (voice.clip == clip && voice.isPlaying)
                {
                    same++;
                    if (now - pool.started[i] < MinRepeatSeconds)
                        return;
                }
                if (slot < 0 && !voice.isPlaying && !pool.paused[i])
                    slot = i;
            }
            // Hit floods never restart a louder attack or grow an unbounded set of AudioSources.
            if (slot < 0 || same >= MaxSameClip)
                return;
            var source = pool.voices[slot];
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume * Trim(channel) * mix);
            source.pitch = pitch;
            pool.interfaceVoice[slot] = ui;
            pool.started[slot] = now;
            source.Play();
        }
        // The hero's actions lead; everything else sits under them.
        static float Trim(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Player:
                    return 1;
                case AudioChannel.Monster:
                case AudioChannel.Effects:
                case AudioChannel.Music:
                    return .7f;
                case AudioChannel.Inventory:
                    return .6f;
                case AudioChannel.Ambience:
                    return .5f;
                default:
                    return .45f;
            }
        }
        static void Level(AudioClip clip)
        {
            if (!levelled.Add(clip))
                return;
            var data = new float[clip.samples * clip.channels];
            // A compressed or still-loading clip plays as it is.
            if (data.Length == 0 || !clip.GetData(data, 0))
            {
                levelled.Remove(clip);
                return;
            }
            var window = Mathf.Max(1, (int)(clip.frequency * .05f)) * clip.channels;
            float peak = 0, sum = 0, loudest = 0;
            for (var i = 0; i < data.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                sum += data[i] * data[i];
                if (i >= window)
                    sum -= data[i - window] * data[i - window];
                loudest = Mathf.Max(loudest, sum / window);
            }
            if (peak <= 0)
                return;
            var gain = Mathf.Min(TargetLoudness / Mathf.Sqrt(loudest), PeakLimit / peak);
            for (var i = 0; i < data.Length; i++)
                data[i] *= gain;
            clip.SetData(data, 0);
        }
        public static void Native(string id, AudioChannel channel, float volume, float pitch = 1, bool pauseWithGame = false)
        {
            PlayClip(Clip(id), channel, volume, pitch * (.96f + (float)variation.NextDouble() * .08f), pauseWithGame);
        }
        public static void PlayWeapon(WeaponFamily family, bool special = false, string weaponId = null)
        {
            var id = family == WeaponFamily.Staff || family == WeaponFamily.Orb ? "magic" : family == WeaponFamily.Spear ? "sword" : family == WeaponFamily.Dagger ? "dagger" : family == WeaponFamily.Longsword ? "sword" : family == WeaponFamily.Katana ? "katana" :
                family == WeaponFamily.Greatsword ? "cleave" : family == WeaponFamily.Mace ? "mace" : family == WeaponFamily.Bow ? "bow" :
                family == WeaponFamily.Crossbow ? "crossbow" : family == WeaponFamily.Shotgun ? "shotgun" : family == WeaponFamily.Cannon ? "cannon" :
                family == WeaponFamily.Rifle ? "rifle" : "pistol";
            Native(id, AudioChannel.Player, special ? .52f : .42f, special ? .90f : 1);
            // Enchanted rounds retain an arcane layer; physical actions use
            // their own bowstring, blade, or powder recording.
        }
        public static void PlayImpact(bool heavy) => Native(heavy ? "heavy-impact" : "impact", AudioChannel.Monster, heavy ? .45f : .24f);
        public static void StopCombat()
        {
            if (instance == null)
                return;
            for (var i = 0; i < instance.voices.Length; i++)
                if (!instance.interfaceVoice[i])
                {
                    instance.voices[i].Stop();
                    instance.paused[i] = false;
                }
        }
        void Update()
        {
            for (var i = 0; i < voices.Length; i++)
            {
                if (interfaceVoice[i])
                    continue;
                if (Time.timeScale <= 0 && !paused[i] && voices[i].isPlaying)
                {
                    voices[i].Pause();
                    paused[i] = true;
                }
                else if (Time.timeScale > 0 && paused[i])
                {
                    voices[i].UnPause();
                    paused[i] = false;
                }
            }
        }
    }
}
