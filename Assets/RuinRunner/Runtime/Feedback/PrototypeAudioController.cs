using UnityEngine;

namespace RuinRunner
{
    // Two fixed decks keep stage transitions continuous without adding voices each time.
    public sealed class PrototypeAudioController : MonoBehaviour
    {
        private const float CrossfadeSeconds = 2.2f;
        // Measured in the recorded mix: the music's loudest passages sit about 9 dB under a sword swing at default settings.
        private const float MusicGain = .38f;
        // Measured on the stage files: the tracks sit within 2 dB, their loudest passages within 4 dB, and the ambience beds 27 dB apart.
        private static readonly float[] MusicStageGain = { 1f, .75f, .95f };
        private static readonly float[] AmbienceStageGain = { 1f, .6f, .07f };
        public static float MusicLevel(int stage) => MusicGain * MusicStageGain[stage];
        private static PrototypeAudioController instance;
        private GameSettingsService settings;
        private readonly AudioSource[] musicSources = new AudioSource[2];
        private readonly AudioSource[] ambienceSources = new AudioSource[2];
        private readonly float[] gains = new float[2];
        private readonly float[] startingGains = new float[2];
        private readonly int[] deckStage = new int[2];
        private int activeDeck = -1;
        private int currentStage = -1;
        private int requestedStage;
        private double fadeStarted;
        private bool crossfading;
        private int loadingStage = -1;
        private AudioClip loadingMusic;
        private AudioClip loadingAmbience;

        public static void SetStage(int stage)
        {
            if (instance == null || stage < 0 || stage > 2) return;
            // A rapid transition keeps only the latest request; it never cuts an audible deck.
            instance.requestedStage = stage;
        }

        public static void Create(GameSettingsService gameSettings)
        {
            if (instance != null) { instance.settings = gameSettings; return; }
            instance = new GameObject("AudioController").AddComponent<PrototypeAudioController>();
            instance.Initialize(gameSettings);
        }

        private void Initialize(GameSettingsService gameSettings)
        {
            settings = gameSettings;
            for (var i = 0; i < 2; i++)
            {
                musicSources[i] = CreateLoopSource("CampaignMusic_" + i, 32);
                ambienceSources[i] = CreateLoopSource("CampaignAmbience_" + i, 180);
            }
            BeginStage(0);
        }

        private AudioSource CreateLoopSource(string sourceName, int priority)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.volume = 0f;
            return source;
        }

        private void BeginStage(int stage)
        {
            var music = Resources.Load<AudioClip>("Audio/Campaign/stage" + (stage + 1) + "-music");
            var ambience = Resources.Load<AudioClip>("Audio/Campaign/stage" + (stage + 1) + "-ambience");
            if (music == null || ambience == null)
            {
                // Preserve the currently audible track if an asset is missing.
                requestedStage = currentStage;
                Debug.LogError("[CampaignAudio] Missing music or ambience for stage " + (stage + 1));
                return;
            }
            loadingStage = stage;
            loadingMusic = music;
            loadingAmbience = ambience;
            music.LoadAudioData();
            ambience.LoadAudioData();
        }

        private void StartPreparedStage()
        {
            var incoming = activeDeck == 0 ? 1 : 0;
            musicSources[incoming].clip = loadingMusic;
            ambienceSources[incoming].clip = loadingAmbience;
            musicSources[incoming].volume = ambienceSources[incoming].volume = 0f;
            // Streaming gets a small DSP lead; both beds share the same start time.
            fadeStarted = AudioSettings.dspTime + .12;
            musicSources[incoming].PlayScheduled(fadeStarted);
            ambienceSources[incoming].PlayScheduled(fadeStarted);
            for (var i = 0; i < 2; i++) startingGains[i] = gains[i];
            activeDeck = incoming;
            deckStage[incoming] = loadingStage;
            currentStage = loadingStage;
            loadingStage = -1;
            loadingMusic = loadingAmbience = null;
            crossfading = true;
        }

        private void Update()
        {
            if (settings == null) return;
            if (!crossfading && loadingStage < 0 && currentStage != requestedStage && requestedStage >= 0) BeginStage(requestedStage);
            if (loadingStage >= 0)
            {
                if (loadingStage != requestedStage)
                {
                    loadingStage = -1;
                    loadingMusic = loadingAmbience = null;
                }
                else if (loadingMusic.loadState == AudioDataLoadState.Failed || loadingAmbience.loadState == AudioDataLoadState.Failed)
                {
                    Debug.LogError("[CampaignAudio] Could not load stage " + (loadingStage + 1));
                    requestedStage = currentStage;
                    loadingStage = -1;
                    loadingMusic = loadingAmbience = null;
                }
                else if (loadingMusic.loadState == AudioDataLoadState.Loaded && loadingAmbience.loadState == AudioDataLoadState.Loaded)
                    StartPreparedStage();
            }
            if (crossfading)
            {
                // DSP time also advances while the inventory pauses simulation or hit-stop runs.
                var t = Mathf.Clamp01((float)((AudioSettings.dspTime - fadeStarted) / CrossfadeSeconds));
                var blend = t * t * (3f - 2f * t);
                for (var i = 0; i < 2; i++)
                    gains[i] = Mathf.Sqrt(Mathf.Lerp(startingGains[i] * startingGains[i], i == activeDeck ? 1f : 0f, blend));
                if (t >= 1f)
                {
                    crossfading = false;
                    var outgoing = 1 - activeDeck;
                    musicSources[outgoing].Stop();
                    ambienceSources[outgoing].Stop();
                    musicSources[outgoing].clip = ambienceSources[outgoing].clip = null;
                }
            }
            for (var i = 0; i < 2; i++)
            {
                musicSources[i].volume = settings.GetVolume(AudioChannel.Music) * MusicLevel(deckStage[i]) * gains[i];
                ambienceSources[i].volume = settings.GetVolume(AudioChannel.Ambience) * AmbienceStageGain[deckStage[i]] * gains[i];
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
