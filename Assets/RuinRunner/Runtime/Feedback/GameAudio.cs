using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    public enum GameSound
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

    public static class GameAudio
    {
        private static readonly Dictionary<GameSound, AudioClip> Clips = new Dictionary<GameSound, AudioClip>();
        private static GameSettingsService settings;

        public static void Configure(GameSettingsService gameSettings)
        {
            settings = gameSettings;
            CombatAudio.Configure(gameSettings);
            GameAudioController.Create(gameSettings);
        }

        public static void Play(GameSound sound, AudioChannel channel, float volume = 1f)
        {
            var nativeId = sound == GameSound.Hit ? "impact" : sound == GameSound.Hurt ? "hurt" :
                sound == GameSound.Dash ? "dash" : sound == GameSound.Block ? "block" :
                sound == GameSound.Attack ? "sword" : sound == GameSound.HeavyAttack ? "cleave" :
                sound == GameSound.Upgrade ? "level-up" : sound == GameSound.Shoot ? "magic" : null;
            if (nativeId != null && CombatAudio.Clip(nativeId) != null)
            {
                CombatAudio.Native(nativeId, channel, volume);
                return;
            }
            AudioClip clip;
            if (!Clips.TryGetValue(sound, out clip))
            {
                clip = CreateClip(sound);
                Clips[sound] = clip;
            }
            CombatAudio.PlayClip(clip, channel, volume);
        }

        private static AudioClip CreateClip(GameSound sound)
        {
            const int sampleRate = 44100;
            var duration = sound == GameSound.Victory ? .7f : sound == GameSound.Upgrade ? .38f : sound == GameSound.HeavyAttack ? .28f : .16f;
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
                var noiseAmount = sound == GameSound.Hit || sound == GameSound.Dash || sound == GameSound.Block ? .32f : .05f;
                samples[i] = (tone * (1f - noiseAmount) + noise * noiseAmount) * envelope * .35f;
            }

            var clip = AudioClip.Create("SFX_" + sound, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float FrequencyFor(GameSound sound, float progress, ref float envelope)
        {
            switch (sound)
            {
                case GameSound.Jump:
                    return Mathf.Lerp(280f, 620f, progress);
                case GameSound.Dash:
                    return Mathf.Lerp(190f, 70f, progress);
                case GameSound.Attack:
                    return Mathf.Lerp(520f, 170f, progress);
                case GameSound.HeavyAttack:
                    return Mathf.Lerp(260f, 65f, progress);
                case GameSound.Block:
                    return progress < .2f ? 820f : 180f;
                case GameSound.Hit:
                    return 110f;
                case GameSound.Hurt:
                    return Mathf.Lerp(180f, 90f, progress);
                case GameSound.Shoot:
                    return Mathf.Lerp(430f, 250f, progress);
                case GameSound.Upgrade:
                    return progress < .5f ? 520f : 780f;
                case GameSound.UiClick:
                    return progress < .25f ? 620f : 430f;
                case GameSound.Inventory:
                    return progress < .45f ? 330f : 520f;
                case GameSound.Victory:
                    envelope = Mathf.Clamp01((1f - progress) * 1.5f);
                    return progress < .33f ? 440f : progress < .66f ? 554f : 659f;
                default:
                    return 220f;
            }
        }
    }
}
