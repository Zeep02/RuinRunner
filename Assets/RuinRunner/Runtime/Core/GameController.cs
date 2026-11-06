using UnityEngine;

namespace RuinRunner
{
    public enum FrontEndPage
    {
        MainMenu,
        SaveSlots,
        Options
    }

    [DefaultExecutionOrder(-9000)]
    public sealed partial class GameController : MonoBehaviour
    {
        public PlayerController Player { get; private set; }
        public PlayerProgression Progression { get; private set; }
        public InventorySystem Inventory { get; private set; }
        public MetaProgressionService MetaProgression { get; private set; }
        public SaveSlotService SaveSlots { get; private set; }
        public GameSettingsService Settings { get; private set; }
        public CameraFollow CameraFollow { get; private set; }
        public EnemyController Boss { get; private set; }
        public RewardChest ActiveChest { get; private set; }
        public RunStats Stats { get; private set; }
        public DungeonMap DungeonMap { get; private set; }
        public int CurrentRoom { get; private set; }
        public int RunSeed { get; private set; }
        public int EnemiesAlive { get; private set; }
        public bool IsChoosingAccessory { get; private set; }
        public bool IsInventoryOpen { get; private set; }
        public int InventoryFocusCell { get; private set; }
        public int InventoryFocusQuickSlot { get; private set; } = -1;
        public bool IsMapOpen { get; private set; }
        public bool IsStatsOpen { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsVictory { get; private set; }
        public bool IsManuallyPaused { get; private set; }
        public bool IsExitOpen { get; private set; }
        public bool IsEncounterTransitioning => waveTransition;
        private string waveBanner;
        public string WaveBanner
        {
            get => waveBanner;
            private set
            {
                waveBanner = value;
                if (!string.IsNullOrEmpty(value))
                    WaveBannerSince = Time.unscaledTime;
            }
        }
        public float WaveBannerSince { get; private set; }
        public float WaveBannerUntil { get; private set; }
        public string LootMessage { get; private set; }
        public float LootMessageUntil { get; private set; }
        public int LastMetaReward { get; private set; }
        public bool IsRunActive { get; private set; }
        public bool IsStageClearPending { get; private set; }
        public bool IsStageResultReady { get; private set; }
        public float StageIntroUntil { get; private set; }
        public FrontEndPage CurrentFrontEndPage { get; private set; }
        public AccessoryDefinition[] CurrentAccessoryChoices { get; private set; } = new AccessoryDefinition[0];

        public bool IsPaused
        {
            get { return !IsRunActive || sessionPhase == GameSessionPhase.BossIntro || IsServiceOpen || IsStageTransitioning || IsChoosingAccessory || IsMapOpen || modalCloseFrame == Time.frameCount || IsInventoryOpen || IsStatsOpen || IsGameOver || IsManuallyPaused; }
        }
        private int modalCloseFrame = -1;

        // Wave/reward staging gates progression and XP, not movement or cooldowns.
        private bool waveTransition;
        private int currentWaveIndex;
        private int roomWaveCount;
        private bool metaRewardGranted;
        private bool rewardSpawnedForRoom;
        private int priorStageVisitedRegions;
        private Coroutine timeEffectRoutine;
        private bool timeEffectActive;
        private float timeEffectScale = 1f;

        private void Awake()
        {
            Stats = new RunStats();
            Inventory = new InventorySystem();
            GameBalance.ApplyLive();
            SaveSlots = new SaveSlotService();
            Settings = new GameSettingsService(SaveSlots.IsValidationProfile
                ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SaveSlots.SaveDirectory), "settings.json") : null);
            GameAudio.Configure(Settings);
            CurrentFrontEndPage = FrontEndPage.MainMenu;
            WaveBanner = string.Empty;
            LootMessage = string.Empty;
            CameraFollow = CameraFollow.Create();
            CameraFollow.ConfigureCombatFeedback(PlayHitStop, PlayBossDefeatSlowMotion, PlayMeleeFinisherHitStop);
            RuinRunner.CameraFollow.DramaticSlow = PlayDramaticSlow;
            RuinRunner.CameraFollow.MeleeHitStop = PlayMeleeHitStop;
            gameObject.AddComponent<GameHud>().Initialize(this);
            RefreshTimeScale();

            DiagnosticsHooks.AttachToSession?.Invoke(this);
        }

        public async void OpenSaveSlots()
        {
            if (IsRunActive)
                return;
            CurrentFrontEndPage = FrontEndPage.SaveSlots;
            if (!BackendReady)
                await ConnectBackendAsync();
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void OpenOptions()
        {
            if (IsRunActive)
                return;
            CurrentFrontEndPage = FrontEndPage.Options;
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void BackToMainMenuPage()
        {
            if (IsRunActive || SessionOperationPending || BackendBusy)
                return;
            sessionEpoch++;
            CancelProfileReset();
            CurrentFrontEndPage = FrontEndPage.MainMenu;
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void StartSlot(int slotNumber)
        {
            OpenProfileTown(slotNumber);
        }

        public bool DeleteSlot(int slotNumber)
        {
            RequestProfileReset(slotNumber);
            return false; // A request is not a confirmed reset; only the authenticated receipt changes the profile.
        }

        public void ChangeVolume(AudioChannel channel, float delta)
        {
            Settings.ChangeVolume(channel, delta);
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .45f);
        }

        public void ToggleFullscreen()
        {
            Settings.ToggleFullscreen();
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void SetBinding(GameAction action, KeyCode key, bool alternate)
        {
            Settings.SetBinding(action, key, alternate);
            GameAudio.Play(GameSound.UiClick, AudioChannel.Interface, .45f);
        }

        public void ResetBindings()
        {
            Settings.ResetBindings();
            GameAudio.Play(GameSound.Inventory, AudioChannel.Interface, .55f);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        internal void BeginRunForActiveSlot()
        {
            legacyFixture = true;
            MetaProgression = new MetaProgressionService(SaveSlots);
            IsRunActive = true;
            StartCoroutine(StartRun());
            RefreshTimeScale();
        }
    }
}
