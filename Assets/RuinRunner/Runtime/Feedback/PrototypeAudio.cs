using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum PrototypeSound
    {
        Jump,
        Dash,
        Attack,
        HeavyAttack,
        Block,
        Hit,
        Hurt,
        Shoot,
        Upgrade,
        Victory,
        UiClick,
        Inventory
    }

    public static class PrototypeAudio
    {
        private static readonly Dictionary<PrototypeSound, AudioClip> Clips = new Dictionary<PrototypeSound, AudioClip>();
        private static GameSettingsService settings;

        public static void Configure(GameSettingsService gameSettings)
        {
            settings = gameSettings;
            CombatAudio.Configure(gameSettings);
            PrototypeAudioController.Create(gameSettings);
        }

        public static void Play(PrototypeSound sound, AudioChannel channel, float volume = 1f)
        {
            var nativeId=sound==PrototypeSound.Hit?"impact":sound==PrototypeSound.Hurt?"hurt":
                sound==PrototypeSound.Dash?"dash":sound==PrototypeSound.Block?"block":
                sound==PrototypeSound.Attack?"sword":sound==PrototypeSound.HeavyAttack?"cleave":
                sound==PrototypeSound.Upgrade?"upgrade":null;
            if(nativeId!=null&&CombatAudio.Clip(nativeId)!=null){CombatAudio.Native(nativeId,channel,volume);return;}
            AudioClip clip;
            if (!Clips.TryGetValue(sound, out clip))
            {
                clip = CreateClip(sound);
                Clips[sound] = clip;
            }
            CombatAudio.PlayClip(clip,channel,volume);
        }

        private static AudioClip CreateClip(PrototypeSound sound)
        {
            const int sampleRate = 44100;
            var duration = sound == PrototypeSound.Victory ? .7f : sound == PrototypeSound.Upgrade ? .38f : sound == PrototypeSound.HeavyAttack ? .28f : .16f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var progress = i / (float)sampleCount;
                var envelope = Mathf.Pow(1f - progress, 2f);
                var frequency = FrequencyFor(sound, progress, ref envelope);
                var tone = Mathf.Sin(time * frequency * Mathf.PI * 2f);
                var noise = Random.Range(-1f, 1f);
                var noiseAmount = sound == PrototypeSound.Hit || sound == PrototypeSound.Dash || sound == PrototypeSound.Block ? .32f : .05f;
                samples[i] = (tone * (1f - noiseAmount) + noise * noiseAmount) * envelope * .35f;
            }

            var clip = AudioClip.Create("SFX_" + sound, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float FrequencyFor(PrototypeSound sound, float progress, ref float envelope)
        {
            switch (sound)
            {
                case PrototypeSound.Jump: return Mathf.Lerp(280f, 620f, progress);
                case PrototypeSound.Dash: return Mathf.Lerp(190f, 70f, progress);
                case PrototypeSound.Attack: return Mathf.Lerp(520f, 170f, progress);
                case PrototypeSound.HeavyAttack: return Mathf.Lerp(260f, 65f, progress);
                case PrototypeSound.Block: return progress < .2f ? 820f : 180f;
                case PrototypeSound.Hit: return 110f;
                case PrototypeSound.Hurt: return Mathf.Lerp(180f, 90f, progress);
                case PrototypeSound.Shoot: return Mathf.Lerp(430f, 250f, progress);
                case PrototypeSound.Upgrade: return progress < .5f ? 520f : 780f;
                case PrototypeSound.UiClick: return progress < .25f ? 620f : 430f;
                case PrototypeSound.Inventory: return progress < .45f ? 330f : 520f;
                case PrototypeSound.Victory:
                    envelope = Mathf.Clamp01((1f - progress) * 1.5f);
                    return progress < .33f ? 440f : progress < .66f ? 554f : 659f;
                default: return 220f;
            }
        }
    }
}
