using System.Collections;
using System.Collections.Generic;
using RuinRunner.Generation;
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
            private set { waveBanner = value; if (!string.IsNullOrEmpty(value)) WaveBannerSince = Time.unscaledTime; }
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
            get { return !IsRunActive || sessionPhase == GameSessionPhase.BossIntro || IsServiceOpen || IsStageTransitioning || IsChoosingAccessory || IsMapOpen || modalCloseFrame == Time.frameCount || IsInventoryOpen || IsStatsOpen  || IsGameOver || IsManuallyPaused; }
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
            PrototypeAudio.Configure(Settings);
            CurrentFrontEndPage = FrontEndPage.MainMenu;
            WaveBanner = string.Empty;
            LootMessage = string.Empty;
            CameraFollow = CameraFollow.Create();
            CameraFollow.ConfigureCombatFeedback(PlayHitStop, PlayBossDefeatSlowMotion, PlayMeleeFinisherHitStop);
            RuinRunner.CameraFollow.DramaticSlow = PlayDramaticSlow;
            RuinRunner.CameraFollow.MeleeHitStop = PlayMeleeHitStop;
            gameObject.AddComponent<PrototypeHud>().Initialize(this);
            RefreshTimeScale();

            var validationArguments = System.Environment.GetCommandLineArgs();
            var automatedRun = System.Array.IndexOf(validationArguments, StageValidationInput.CommandLineArgument) >= 0 &&
                               (!Application.isBatchMode || System.Array.IndexOf(validationArguments, "--headless-campaign-validation") >= 0);
            if (automatedRun)
            {
                StageValidationInput.ConfigureRandomSeed();
                var recoveryRequested = System.Array.IndexOf(validationArguments,"--validation-recovery-ticket") >= 0;
                var isolatedAllowed = recoveryRequested
                    ? AutomatedStageValidation.AuthorizeRecoveryProfile(SaveSlots)
                    : SaveSlots.IsValidationProfile && !SaveSlots.GetSummary(1).Exists &&
                      !System.IO.Directory.Exists(System.IO.Path.Combine(SaveSlots.SaveDirectory,"BackendRecovery"));
                if (!isolatedAllowed || recoveryRequested && System.Array.IndexOf(validationArguments,"--legacy-combat-fixture") >= 0)
                {
                    Debug.LogError("[Stage E2E] A new isolated profile or verified recovery ticket is required; no slot was overwritten.");
                    Application.Quit(3);
                    return;
                }
                if(System.Array.IndexOf(validationArguments,"--legacy-combat-fixture")>=0)
                {
                    SaveSlots.OpenSlot(1);
                    BeginRunForActiveSlot();
                }
                gameObject.AddComponent<AutomatedStageValidation>().Initialize(this);
            }

            if (!automatedRun && System.Array.IndexOf(validationArguments, CombatSandbox.CommandLineArgument) >= 0)
            {
                SaveSlots.OpenTransientForTests();
                BeginRunForActiveSlot();
                gameObject.AddComponent<CombatSandbox>().Initialize(this);
                return;
            }

#if UNITY_STANDALONE && !UNITY_EDITOR
            if (!automatedRun && (Application.isBatchMode || System.Array.IndexOf(validationArguments, "--smoke-test") >= 0))
            {
                SaveSlots.OpenTransientForTests();
                BeginRunForActiveSlot();
                gameObject.AddComponent<RuntimeSmokeTest>().Initialize(this);
            }
#endif
        }

        public async void OpenSaveSlots()
        {
            if (IsRunActive) return;
            CurrentFrontEndPage = FrontEndPage.SaveSlots;
            if(!BackendReady)await ConnectBackendAsync();
            PrototypeAudio.Play(PrototypeSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void OpenOptions()
        {
            if (IsRunActive) return;
            CurrentFrontEndPage = FrontEndPage.Options;
            PrototypeAudio.Play(PrototypeSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void BackToMainMenuPage()
        {
            if (IsRunActive || SessionOperationPending || BackendBusy) return;
            sessionEpoch++;
            CancelProfileReset();
            CurrentFrontEndPage = FrontEndPage.MainMenu;
            PrototypeAudio.Play(PrototypeSound.UiClick, AudioChannel.Interface, .5f);
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
            PrototypeAudio.Play(PrototypeSound.UiClick, AudioChannel.Interface, .45f);
        }

        public void ToggleFullscreen()
        {
            Settings.ToggleFullscreen();
            PrototypeAudio.Play(PrototypeSound.UiClick, AudioChannel.Interface, .5f);
        }

        public void SetBinding(GameAction action, KeyCode key, bool alternate)
        {
            Settings.SetBinding(action, key, alternate);
            PrototypeAudio.Play(PrototypeSound.UiClick, AudioChannel.Interface, .45f);
        }

        public void ResetBindings()
        {
            Settings.ResetBindings();
            PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Interface, .55f);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private void BeginRunForActiveSlot()
        {
            legacyFixture = true;
            MetaProgression = new MetaProgressionService(SaveSlots);
            IsRunActive = true;
            StartCoroutine(StartRun());
            RefreshTimeScale();
        }

        private IEnumerator StartRun()
        {
            sessionEpoch++;
            settlementRunId = projectedSettlementRunId = null;
            if (DungeonMap != null && DungeonMap.Root != null) Destroy(DungeonMap.Root);
            stageEpoch++;
            sessionPhase = GameSessionPhase.Transition;
            InkTransition.Hold(CampaignStage.Get(0).Name);
            ScreenVignette.Ensure(this);
            oathTracker = new EquipmentOathTracker();
            usedServices.Clear(); pendingLoot.Clear(); lootByEvent.Clear(); serviceByOperation.Clear(); processedEconomyReceipts.Clear(); rewardSequence=0;
            CurrentStageIndex = 0;
            WeaponLootTable.SetDropStage(CurrentStageIndex);AccessoryLootTable.SetDropStage(CurrentStageIndex);
            PrototypeAudioController.SetStage(0);
            StagesCleared = 0;
            IsStageTransitioning = true;
            RefreshTimeScale();
            yield return null;
            RunSeed = legacyFixture ? RoomTemplateCatalog.SelectNormalRunSeed() : BackendRun.seed;
            priorStageVisitedRegions=0;
            var epoch = stageEpoch;
            yield return DungeonMap.BuildStageProgressively(map => DungeonMap=map,
                room => { if (epoch == stageEpoch) EnterRoom(room); }, RunSeed, CurrentStageIndex);
            Debug.Log("[Run] Started seed=" + RunSeed + " slot=" + SaveSlots.ActiveSlot);

            Stats.Reset();
            Inventory.Reset();
            ResetEquipmentSelection();
            Inventory.ConfigureExpansion(0);
            CurrentRoom = DungeonMap.EntranceRoomIndex;
            EnemiesAlive = 0;
            Boss = null;
            ActiveChest = null;
            Progression = null;
            IsGameOver = false;
            IsVictory = false;
            IsChoosingAccessory = false;
            IsInventoryOpen = false;
            IsStatsOpen = false;
            IsManuallyPaused = false;
            IsExitOpen = false;
            waveTransition = true;
            LootMessage = string.Empty;
            LastMetaReward = 0;
            metaRewardGranted = false;
            rewardSpawnedForRoom = false;
            IsStageClearPending = false;
            IsStageResultReady = false;
            StageIntroUntil = Time.unscaledTime + 5.5f;
            CurrentAccessoryChoices = new AccessoryDefinition[0];
            accessoryOfferIndex = 0;
            accessorySelectionFrame = -1;
            DungeonMap.ResetExits();
            RefreshTimeScale();

            // 무기 없이 장검 판정만 사용하던 숨은 기본값을 제거한다.
            // 새 런은 실제 무기 데이터를 1번 퀵슬롯에 넣어 외형, UI, 전투 판정이 일치한다.
            Inventory.TryAddConfirmed(legacyFixture ? "trainee_blade" : BackendRun.loadout.startingWeaponId, InventoryEntryKind.Weapon, "", true);

            Player = PlayerController.Create(
                DungeonMap.GetSpawnPoint(CurrentRoom),
                () => IsPaused,
                () => EnemiesAlive == 0 && !waveTransition,
                PlayerDied,
                CameraFollow,
                Inventory,
                Settings);
            Player.EquipWeapon(Inventory.EquippedWeapon);
            Progression = Player.gameObject.AddComponent<PlayerProgression>();
            Progression.Initialize(OnLevelRewardEarned);
            Player.GoldQuery=()=>(int)RunGold;
            Player.ConfigureMastery(SaveSlots.ActiveData?.unlockedTalentIds);
            Player.ConfigureCombatGrowth(ActiveTalents.Has, NotifyCombatAction);
            Player.IsTrainingCombat = false;
            CameraFollow.Target = Player.transform;
            CameraFollow.FocusRoom(DungeonMap.Terrain.Bounds.center, DungeonMap.Terrain.Bounds.size, true);
            CreateExplorationRewards();
            CreateFieldServices();
            InitializeRunEconomy();

            Player.SetEntranceLock(true);
            yield return new WaitForSecondsRealtime(.9f);
            IsStageTransitioning = false;
            sessionPhase = GameSessionPhase.Expedition;
            RefreshTimeScale();
            yield return InkTransition.Reveal(Player);
            Player.SetEntranceLock(false);

            yield return new WaitForSeconds(.7f);
            StartCoroutine(BeginCurrentRoom());
        }

        private IEnumerator BeginCurrentRoom()
        {
            if (IsGameOver || IsTown) yield break;
            DungeonMap.MarkVisited(CurrentRoom);
            var room = DungeonMap.GetRoom(CurrentRoom);
            var state=DungeonMap.Regions[CurrentRoom];
            if(state.Phase==EncounterRegionPhase.Cleared)
            {waveTransition=false;IsExitOpen=true;yield break;}
            if(!DungeonMap.RequiresCombat(CurrentRoom))
            {
                DungeonMap.UnlockExit(CurrentRoom);currentWaveIndex=0;roomWaveCount=0;
                waveTransition=false;IsExitOpen=true;yield break;
            }
            DungeonMap.BeginEncounter(CurrentRoom);
            IsExitOpen=false;
            waveTransition = true;
            rewardSpawnedForRoom = false;
            currentWaveIndex = 0;
            roomWaveCount = room.WaveCount;
            WaveBanner = room.displayName;
            if (room.bossRoom) WaveBanner += " — 수호자의 방";
            WaveBannerUntil = Time.unscaledTime + 2f;
            yield return new WaitForSeconds(.55f);

            yield return StartCoroutine(BeginWave(0));
        }

        private IEnumerator BeginWave(int waveIndex)
        {
            if (IsGameOver || IsTown) yield break;
            waveTransition = true;
            currentWaveIndex = waveIndex;
            var room = DungeonMap.GetRoom(CurrentRoom);
            if (roomWaveCount > 1)
            {
                WaveBanner = (waveIndex + 1) + "/" + roomWaveCount;
                WaveBannerUntil = Time.unscaledTime + 1.8f;
            }

            var telegraphs = WaveDirector.CreateTelegraphs(room, waveIndex, DungeonMap.GetRoomCenter(CurrentRoom));
            yield return new WaitForSeconds(EnemySpawnTelegraph.Duration);
            foreach (var telegraph in telegraphs)
                if (telegraph != null) telegraph.Complete();

            var result = WaveDirector.SpawnWave(
                room,
                waveIndex,
                Player,
                () => IsPaused,
                CaptureEnemyDeathCallback(),
                CameraFollow,
                DungeonMap.GetRoomCenter(CurrentRoom),
                DungeonMap.IsAuthored ? DungeonMap.Terrain : null);
            EnemiesAlive = result.EnemyCount;
            Boss = result.Boss;
            waveTransition = false;
            if(Boss!=null)BossEntrancePresentation.Begin(this,Boss,CurrentStage.Name,"수호자가 깨어납니다",()=>SetBossIntroPhase(false));
            if (EnemiesAlive == 0)
            {
                // Imported explicit wave indices may have gaps. Never unlock the
                // reward early and silently discard a later wave (including a boss).
                if (currentWaveIndex + 1 < roomWaveCount)
                {
                    waveTransition = true;
                    StartCoroutine(BeginNextWave());
                }
                else StartCoroutine(SpawnRoomReward());
            }
        }

        public void EnemyDied(EnemyController enemy)
        {
            if (!IsRunActive || IsGameOver || IsStageTransitioning || enemy == null) return;
            if (IsTown) return;
            Stats.RegisterEnemyDefeated();
            AwardEnemyGold(enemy);
            if (Player != null) Player.OnEnemyDefeated(enemy);
            if (Progression != null)
                ExperienceOrb.Create(enemy.transform.position, enemy.ExperienceReward * (ActiveTalents.Has(TalentId.CursedPower) ? 2 : 1), Progression,
                    () => EnemiesAlive == 0 && !waveTransition && currentWaveIndex + 1 >= roomWaveCount);

            var defeatedStageBoss = enemy == Boss;
            if (!enemy.IsBoss && UnityEngine.Random.value < EnemyPotionChance) DropHealthPotion(enemy.transform.position);
            if (defeatedStageBoss) Boss = null;
            EnemiesAlive = Mathf.Max(0, EnemiesAlive - 1);
            if (EnemiesAlive == 0 && !IsGameOver && !waveTransition)
            {
                if (defeatedStageBoss) BeginStageClearSafety();
                if (currentWaveIndex + 1 < roomWaveCount)
                {
                    waveTransition = true;
                    StartCoroutine(BeginNextWave());
                }
                else
                {
                    // The last one of a room hangs in the air for a moment.
                    SigFx.Play("RoomClearRing", enemy.transform.position, 0, -1, true, false, .8f);
                    if (!defeatedStageBoss) { PlayDramaticSlow(.34f, .22f); CameraFollow.Shake(.2f, .22f); }
                    StartCoroutine(SpawnRoomReward());
                }
            }
        }

        private IEnumerator BeginNextWave()
        {
            RuntimeSceneObjects.ClearProjectiles();
            WaveBanner = "";
            WaveBannerUntil = Time.unscaledTime + 1.2f;
            yield return new WaitForSeconds(.75f);
            yield return StartCoroutine(BeginWave(currentWaveIndex + 1));
        }

        private IEnumerator SpawnRoomReward()
        {
            if (rewardSpawnedForRoom) yield break;
            rewardSpawnedForRoom = true;
            waveTransition = true;
            RuntimeSceneObjects.ClearProjectiles();
            yield return new WaitForSeconds(.65f);
            if (IsGameOver) yield break;
            var rewardRoom = CurrentRoom;
            AwardEncounterMaterials(rewardRoom);
            // A room's plan may fill its middle; the generator found the chest a free spot.
            var center = DungeonMap.Graph != null ? GeneratedStageUnityAdapter.Vector(DungeonMap.Graph.Regions[rewardRoom].RewardPosition) : DungeonMap.GetRoomCenter(rewardRoom) + RoomLayoutRules.RewardPosition;
            var epoch = stageEpoch;
            ChestRewards rewards = null; RewardChest chest = null; var sampledLuck = float.NaN;
            ChestRewards ResolveRewards()
            {
                var luck = Player.Stats.Luck;
                if (rewards == null || chest?.IsOpened != true && sampledLuck != luck)
                {
                    sampledLuck = luck;
                    rewards = RunContent.RollChest(RunSeed, CurrentStageIndex * 16 + rewardRoom, luck, DungeonMap.GetRoom(rewardRoom).bossRoom);
                }
                return rewards;
            }
            // Preview responds to current Luck; opening seals that exact roll for the delayed reveal.
            ActiveChest = chest = RewardChest.Create(center, () => { if (epoch == stageEpoch) OpenRoomChest(rewardRoom, ResolveRewards()); }, Settings,
                () => !IsPaused && !waveTransition && epoch == stageEpoch, RewardPresentation.Highest(ResolveRewards()), () => RewardPresentation.Highest(ResolveRewards()));
            DungeonMap.Regions[rewardRoom].Chest = ActiveChest;
            DungeonMap.UnlockExit(rewardRoom);
            IsExitOpen = true;
            WaveBanner = "";
            WaveBannerUntil = Time.unscaledTime + 2.6f;
            waveTransition = false;
        }

        private void OpenRoomChest(int rewardRoom, ChestRewards rewards)
        {
            var state = DungeonMap.Regions[rewardRoom];
            if (state.ChestOpened) return;
            state.ChestOpened = true;
            if(!legacyFixture)_ = ClaimGold("chest:"+CurrentStageIndex+":"+rewardRoom,"chest");
            var origin = state.Chest == null ? (Vector2)Player.transform.position : (Vector2)state.Chest.transform.position;
            var scatterIndex = 0;
            var bossReward = DungeonMap.GetRoom(rewardRoom).bossRoom;
            DropGold(origin,BackendCatalog?.economy?.chestGold??0);
            foreach (var weapon in new[] { rewards.Weapon, rewards.BonusWeapon })
            {
                if (weapon == null) continue;
                SpawnEquipmentPickup(origin,weapon,null,scatterIndex++,bossReward,"chest-item:"+CurrentStageIndex+":"+rewardRoom+":"+scatterIndex);
            }
            if (rewards.Accessory != null)
            {
                var accessory = rewards.Accessory;
                SpawnEquipmentPickup(origin,null,accessory,scatterIndex++,bossReward,"chest-accessory:"+CurrentStageIndex+":"+rewardRoom);
            }
            if (UnityEngine.Random.value < ChestPotionChance) DropHealthPotion(origin,scatterIndex++);
            Debug.Log("[Run] Chest seed=" + RunSeed + " encounter=" + rewardRoom + " weapon=" + rewards.Weapon.Id +
                " accessory=" + (rewards.Accessory == null ? "none" : rewards.Accessory.Id));
            var experience = bossReward ? 45 : 20 + rewardRoom * 6;
            ExperienceOrb.CreateBurst(origin, experience, bossReward ? 11 : 7, Progression,
                () => EnemiesAlive == 0 && !waveTransition);
            ActiveChest = null;
            state.Chest = null;

            if (IsStageClearPending && bossReward)
            {
                // Once per chest: a stated reward, not a side effect of replacing the Player.
                Player.Stats.Heal(Player.MaxHealth * CampaignStage.ClearHealingFraction);
                LootMessage = "체력 30% 회복";
                LootMessageUntil = Time.unscaledTime + 4f;
                StartCoroutine(PrepareStageResult());
                return;
            }

            WaveBanner = "보상 획득";
            WaveBannerUntil = Time.unscaledTime + 2.4f;
        }

        private bool CollectWeaponPickup(WeaponDefinition weapon)
        {
            if (!Inventory.AddWeapon(weapon)) return false;
            Player.EquipWeapon(Inventory.EquippedWeapon);
            CompletePickup("무기 획득  " + weapon.Rarity + " · " + weapon.Name);
            return true;
        }

        private bool CollectAccessoryPickup(AccessoryDefinition accessory)
        {
            if (!Inventory.AddAccessory(accessory)) return false;
            Player.ApplyAccessory(accessory);
            CompletePickup("장신구 획득  " + accessory.Name);
            return true;
        }

        private void CompletePickup(string message)
        {
            var setMessages = new List<string>();
            foreach (var setBonus in Inventory.CollectNewSetBonuses())
            {
                Player.ApplySetBonus(setBonus);
                setMessages.Add(setBonus.Name);
            }
            LootMessage = setMessages.Count == 0 ? message : message + "\n세트 활성  " + string.Join(", ", setMessages.ToArray());
            LootMessageUntil = Time.unscaledTime + 1.8f;
        }

        private void BeginStageClearSafety()
        {
            IsStageClearPending = true;
            StagesCleared = Mathf.Max(StagesCleared, CurrentStageIndex + 1);
            IsStageResultReady = false;
            RuntimeSceneObjects.ClearProjectiles();
            if (Player != null) Player.SetStageClearSafety(true);
        }

        private IEnumerator PrepareStageResult()
        {
            RuntimeSceneObjects.ClearProjectiles();
            while (!IsGameOver)
            {
                var choicesResolved = Progression == null || (Progression.PendingLevelRewards == 0 && !IsChoosingAccessory);
                if (choicesResolved && !HasUnclaimedBossExperience()) break;
                yield return null;
            }
            if (IsGameOver || IsTown) yield break;

            IsStageResultReady = true;
            CreateStageExit();
            WaveBanner = CurrentStage.Name + " 해방";
            WaveBannerUntil = Time.unscaledTime + 4f;
        }

        public void ShowStageResult()
        {
            if (!IsStageResultReady || IsPaused || HasUnclaimedBossExperience() ||
                Progression != null && Progression.PendingLevelRewards > 0 || !IsNearStageExit || PendingStageRewards > 0) return;
            if (!CurrentStage.IsFinal)
            {
                BeginNextStage();
                return;
            }
            GrantMetaReward(true);
            IsChoosingAccessory = false;
            CurrentAccessoryChoices = new AccessoryDefinition[0];
            IsVictory = true;
            IsGameOver = true;
            sessionPhase = GameSessionPhase.Result;
            IsStageResultReady = false;
            PrototypeAudio.Play(PrototypeSound.Victory, AudioChannel.Music, .6f);
            RefreshTimeScale();
        }

        private bool HasUnclaimedBossExperience()
        {
            if (DungeonMap == null) return false;
            var bossRoom = DungeonMap.BossRoomIndex;
            var center = DungeonMap.GetRoomCenter(bossRoom);
            var radius = DungeonMap.GetRoom(bossRoom).Size.magnitude * .5f + 3f;
            foreach (var orb in RuntimeSceneObjects.FindAll<ExperienceOrb>())
                if (!orb.IsCollected && Vector2.Distance(orb.transform.position, center) < radius) return true;
            return false;
        }

        private void OnLevelRewardEarned()
        {
            if (IsGameOver) return;
            if (Player != null) SigFx.Play("LevelUpBurst", Player.transform.position, 0, -1, true, false, .7f);
            if (!IsChoosingAccessory)
                CurrentAccessoryChoices = RunContent.Choices(RunSeed, accessoryOfferIndex++, Player.Stats.Luck);
            IsChoosingAccessory = true;
            RefreshTimeScale();
        }

        public void SelectAccessory(AccessoryDefinition definition)
        {
            if (!IsChoosingAccessory || Player == null || Progression == null || definition == null) return;
            if (Time.frameCount == accessorySelectionFrame || System.Array.IndexOf(CurrentAccessoryChoices, definition) < 0) return;
            if (!Progression.SpendLevelReward()) return;
            accessorySelectionFrame = Time.frameCount;
            // A full bag leaves the reward on the floor instead of losing it.
            if(!legacyFixture||!CollectAccessoryPickup(definition))SpawnEquipmentPickup(Player.transform.position,null,definition,0,IsStageClearPending,"level-choice:"+accessoryOfferIndex);
            PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .55f);
            IsChoosingAccessory = Progression.PendingLevelRewards > 0;
            CurrentAccessoryChoices = IsChoosingAccessory
                ? RunContent.Choices(RunSeed, accessoryOfferIndex++, Player.Stats.Luck)
                : new AccessoryDefinition[0];
            RefreshTimeScale();
        }

        private int accessorySelectionFrame = -1;
        private int accessoryOfferIndex;

        public void FocusInventoryCell(int cell)
        {
            if (IsInventoryOpen && cell >= 0 && cell < Inventory.UnlockedCapacity)
            { InventoryFocusQuickSlot = -1; InventoryFocusCell = cell; FocusEquipment(Inventory.GetOwnedCell(cell)?.Identity ?? 0); }
        }

        public void FocusInventoryQuickSlot(int slot)
        {
            if (!IsInventoryOpen || slot < 0 || slot >= Inventory.QuickSlots.Count) return;
            InventoryFocusQuickSlot = slot;
            FocusEquipment(Inventory.QuickSlots[slot].Item?.Identity ?? 0);
        }

        private void ReadInventoryInput()
        {
            if (InventoryFocusQuickSlot >= 0)
            {
                var slot = InventoryFocusQuickSlot;
                if (Settings.GetKeyDown(GameAction.MoveLeft)) FocusInventoryQuickSlot(Mathf.Max(0, slot - 1));
                else if (Settings.GetKeyDown(GameAction.MoveRight)) FocusInventoryQuickSlot(Mathf.Min(Inventory.QuickSlots.Count - 1, slot + 1));
                else if (Settings.GetKeyDown(GameAction.MoveDown))
                    FocusInventoryCell(Inventory.BagCell(Mathf.Min(Inventory.UnlockedCapacity / 4 - 1, Mathf.RoundToInt((95 + slot * 205 - 34) / 78f)), 0));
                ReadEquipmentMaintenanceInput();
                return;
            }
            var column = InventorySystem.BagColumn(InventoryFocusCell);
            var row = InventorySystem.BagRow(InventoryFocusCell);
            if (Settings.GetKeyDown(GameAction.MoveLeft)) column--;
            else if (Settings.GetKeyDown(GameAction.MoveRight)) column++;
            else if (Settings.GetKeyDown(GameAction.MoveUp))
            {
                if (row == 0) { FocusInventoryQuickSlot(Mathf.Clamp(Mathf.FloorToInt((34 + column * 78) / 205f), 0, 2)); return; }
                row--;
            }
            else if (Settings.GetKeyDown(GameAction.MoveDown)) row++;
            if (column != InventorySystem.BagColumn(InventoryFocusCell) || row != InventorySystem.BagRow(InventoryFocusCell))
                FocusInventoryCell(Inventory.BagCell(column, row));
            if (ReadEquipmentMaintenanceInput()) return;
            var entry = Inventory.GetCell(InventoryFocusCell);
            if (entry == null || entry.Kind != InventoryEntryKind.Weapon) return;
            for (var slot = 0; slot < Inventory.QuickSlots.Count; slot++)
                if (Settings.GetKeyDown(slot == 0 ? GameAction.Weapon1 : slot == 1 ? GameAction.Weapon2 : GameAction.Weapon3))
                { AssignWeaponToSlot(entry.ItemIndex, slot); break; }
        }

        public void AssignWeaponToSlot(int weaponIndex, int slotIndex)
        {
            if(EconomyOperationPending||BackendHasPending)return;
            var weapon = Inventory.AssignToQuickSlot(weaponIndex, slotIndex);
            Player.EquipWeapon(weapon);
            if (weapon != null)
            {
                PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .5f);
                LootMessage = (slotIndex + 1) + "번 슬롯 장착 — " + weapon.Name;
                LootMessageUntil = Time.unscaledTime + 2f;
            }
        }

        public void UnequipWeaponSlot(int slotIndex, int targetCell = -1)
        {
            if(EconomyOperationPending||BackendHasPending)return;
            var previous = slotIndex >= 0 && slotIndex < Inventory.QuickSlots.Count
                ? Inventory.QuickSlots[slotIndex].Weapon
                : null;
            if (previous == null) return;

            var weapon = Inventory.UnequipQuickSlot(slotIndex, targetCell);
            if (Inventory.QuickSlots[slotIndex].Weapon == previous) return;
            Player.EquipWeapon(weapon);
            PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .5f);
            LootMessage = previous.Name + " 장착 해제";
            LootMessageUntil = Time.unscaledTime + 1.5f;
        }

        public void SwapWeaponSlots(int firstSlotIndex, int secondSlotIndex)
        {
            if(EconomyOperationPending||BackendHasPending)return;
            if (firstSlotIndex == secondSlotIndex) return;
            Inventory.SwapQuickSlots(firstSlotIndex, secondSlotIndex);
            Player.EquipWeapon(Inventory.EquippedWeapon);
            PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .45f);
            LootMessage = (firstSlotIndex + 1) + "번과 " + (secondSlotIndex + 1) + "번 슬롯 교체";
            LootMessageUntil = Time.unscaledTime + 1.5f;
        }

        private void SelectWeaponSlot(int slotIndex)
        {
            var previous = Inventory.ActiveSlotIndex;
            var weapon = Inventory.SelectQuickSlot(slotIndex);
            Player.EquipWeapon(weapon);
            if (weapon != null && previous != Inventory.ActiveSlotIndex)
            {
                PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .4f);
                LootMessage = (Inventory.ActiveSlotIndex + 1) + "번 무기 — " + weapon.Name;
                LootMessageUntil = Time.unscaledTime + 1.2f;
            }
        }

        private void EnterRoom(int targetRoom)
        {
            if (!IsExitOpen || waveTransition || IsGameOver || IsStageTransitioning || IsPaused || targetRoom == CurrentRoom ||
                DungeonMap == null || !DungeonMap.CanEnter(CurrentRoom,targetRoom)) return;

            if (StageValidationInput.Enabled) Debug.Log("[Portal] Enter target=" + targetRoom + " from=" + CurrentRoom);

            IsExitOpen = false;
            waveTransition = true;
            CurrentRoom = targetRoom;
            // Position and camera remain continuous while the seal closes behind the player.
            StartCoroutine(BeginCurrentRoom());
        }

        public void PlayerDied()
        {
            BossEntrancePresentation.Cancel(this);
            if (IsGameOver || !IsRunActive || IsStageClearPending || IsStageTransitioning) return;
            GrantMetaReward(false);
            IsGameOver = true;
            sessionPhase = GameSessionPhase.Result;
            IsVictory = false;
            RefreshTimeScale();
        }

        private void GrantMetaReward(bool victory)
        {
            if (metaRewardGranted) return;
            metaRewardGranted = true;
            if (legacyFixture) LastMetaReward = MetaProgression.AwardRun(Stats.EnemiesDefeated,priorStageVisitedRegions+(DungeonMap?.VisitedRegionCount??0),victory,StagesCleared);
            else BeginServerSettlement(victory);
        }

        private void Update()
        {
            if (modalCloseFrame >= 0 && modalCloseFrame < Time.frameCount)
            { modalCloseFrame = -1; RefreshTimeScale(); }
            TickBackendRecovery();
            TickServices();
            // Closing a service consumes this frame's Escape/Interact input.
            // Otherwise Escape also opens the pause menu immediately afterward.
            if (IsServiceOpen || IsStageTransitioning || InkTransition.LocksInput || modalCloseFrame == Time.frameCount) return;
            if (IsRunActive && !IsTown && !IsStageTransitioning && DungeonMap != null && Player != null)
            {
                DungeonMap.TrackPosition(Player.transform.position);
                // A high-speed dash can cross a thin trigger within a physics step. Actual region geometry provides the same adjacency-gated transition.
                if(DungeonMap.IsProcedural&&DungeonMap.PhysicalRegion>=0&&DungeonMap.PhysicalRegion!=CurrentRoom)
                    EnterRoom(DungeonMap.PhysicalRegion);
                ActiveChest = null;
                var nearestChest = float.PositiveInfinity;
                foreach (var region in DungeonMap.Regions)
                {
                    if (region.Chest == null || region.ChestOpened) continue;
                    var distance = Vector2.SqrMagnitude(region.Chest.transform.position - Player.transform.position);
                    if (distance < nearestChest) { nearestChest = distance; ActiveChest = region.Chest; }
                }
            }
            if (IsMapOpen && (!IsRunActive || IsStageTransitioning || IsChoosingAccessory || IsInventoryOpen || IsStatsOpen  || IsGameOver || IsManuallyPaused))
            { IsMapOpen = false; RefreshTimeScale(); }
            if (!IsRunActive)
            {
                if (Settings.GetKeyDown(GameAction.Pause) && CurrentFrontEndPage != FrontEndPage.MainMenu)
                    BackToMainMenuPage();
                return;
            }

            if (!IsPaused)
            {
                if (Time.unscaledTime < StageIntroUntil &&
                    (Settings.GetKey(GameAction.MoveLeft) || Settings.GetKey(GameAction.MoveRight) ||
                     Settings.GetKey(GameAction.MoveUp) || Settings.GetKey(GameAction.MoveDown) ||
                     Settings.GetKeyDown(GameAction.Attack) || Settings.GetKeyDown(GameAction.Guard) ||
                     Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                    StageIntroUntil = 0f;
                if (IsExpedition) Stats.Tick(Time.deltaTime);
                Inventory.Tick(Time.deltaTime);
            }

            if (IsGameOver)
            {
                if (Settings.GetKeyDown(GameAction.Reload)) RestartRun();
                return;
            }

            if (IsMapOpen)
            {
                if (Settings.GetKeyDown(GameAction.Map) || Settings.GetKeyDown(GameAction.Pause))
                { IsMapOpen = false; modalCloseFrame = Time.frameCount; RefreshTimeScale(); }
                // The map owns input until closed, including inventory, reload,
                // weapon selection and interaction on the same frame as closing.
                return;
            }
            if (!IsTown && !IsPaused && Settings.GetKeyDown(GameAction.Map))
            {
                IsMapOpen = true;
                RefreshTimeScale();
                return;
            }

            if (!IsChoosingAccessory && !IsGameOver && !IsManuallyPaused && !IsStatsOpen &&
                Settings.GetKeyDown(GameAction.Inventory))
            {
                IsInventoryOpen = !IsInventoryOpen;
                CancelEquipmentDisassembly();
                if (IsInventoryOpen)
                {
                    InventoryFocusCell = Mathf.Clamp(InventoryFocusCell, 0, Inventory.UnlockedCapacity - 1);
                    if (SelectedEquipment == null) FocusInventoryQuickSlot(Inventory.ActiveSlotIndex);
                }
                else modalCloseFrame = Time.frameCount;
                PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .45f);
                RefreshTimeScale();
                return;
            }

            if (!IsChoosingAccessory && !IsGameOver && !IsManuallyPaused && !IsInventoryOpen && Settings.GetKeyDown(GameAction.Stats))
            {
                IsStatsOpen = !IsStatsOpen;
                RefreshTimeScale();
            }

            if (!IsChoosingAccessory && !IsGameOver && Settings.GetKeyDown(GameAction.Pause))
            {
                if (HasDisassemblyConfirmation) { CancelEquipmentDisassembly(); return; }
                if (IsInventoryOpen || IsStatsOpen )
                {
                    if (IsInventoryOpen) modalCloseFrame = Time.frameCount;
                    CancelEquipmentDisassembly();
                    IsInventoryOpen = false;
                    IsStatsOpen = false;
                    RefreshTimeScale();
                }
                else
                {
                    SetManualPause(!IsManuallyPaused);
                }
            }

            if (IsChoosingAccessory)
            {
                for(var choice=0;choice<CurrentAccessoryChoices.Length;choice++)
                    if(Settings.GetKeyDown(GameSettingsService.AccessoryChoiceAction(choice)))
                    { SelectAccessory(GetAccessoryChoice(choice)); break; }
                return;
            }

            if (IsInventoryOpen)
            {
                ReadInventoryInput();
                return;
            }

            if (!IsPaused && Player != null)
            {
                if (!IsTown) UpdateStageExitHold();
                if (IsStageResultReady && IsNearStageExit && Settings.GetKeyDown(GameAction.Interact) && (PendingStageRewards == 0 || !IsPlayerNearCollectible()))
                {
                    ShowStageResult();
                    return;
                }
                if (Settings.GetKeyDown(GameAction.Weapon1)) SelectWeaponSlot(0);
                if (Settings.GetKeyDown(GameAction.Weapon2)) SelectWeaponSlot(1);
                if (Settings.GetKeyDown(GameAction.Weapon3)) SelectWeaponSlot(2);
                if (Settings.GetKeyDown(GameAction.Reload) && Inventory.StartReload())
                    PrototypeAudio.Play(PrototypeSound.Inventory, AudioChannel.Inventory, .4f);
            }

        }

        private bool IsPlayerNearCollectible()
        {
            if (Player == null) return false;
            foreach (var pickup in RuntimeSceneObjects.FindAll<FieldLootPickup>())
                if (pickup != null && Vector2.Distance(Player.transform.position, pickup.transform.position) <= 1.6f) return true;
            return false;
        }

        private void SetManualPause(bool paused)
        {
            IsManuallyPaused = paused;
            RefreshTimeScale();
        }

        public void ResumeGame()
        {
            SetManualPause(false);
        }

        public void ReturnToMainMenu()
        {
            if(EconomyOperationPending||SessionOperationPending||BackendBusy)return;
            BossEntrancePresentation.Cancel(this);
            if (!IsRunActive) return;
            sessionEpoch++;
            PrototypeAudioController.SetStage(0);
            IsMapOpen = false;
            StopAllCoroutines();
            stageEpoch++;
            IsStageTransitioning = false;
            ResetTimeEffect();
            RuntimeSceneObjects.ClearSession();
            if (Player != null) Destroy(Player.gameObject);
            if (DungeonMap != null && DungeonMap.Root != null) Destroy(DungeonMap.Root);
            Player = null;
            Progression = null;
            DungeonMap = null;
            Boss = null;
            ActiveChest = null;
            MetaProgression = null;
            IsRunActive = false;
            sessionPhase = GameSessionPhase.MainMenu;
            if (town != null) { Destroy(town.gameObject); town = null; }
            OpenService = NearbyService = null;
            Inventory.Reset();
            ResetEquipmentSelection();
            IsChoosingAccessory = false;
            IsInventoryOpen = false;
            IsStatsOpen = false;
            IsGameOver = false;
            IsManuallyPaused = false;
            IsExitOpen = false;
            IsStageClearPending = false;
            IsStageResultReady = false;
            CurrentFrontEndPage = FrontEndPage.MainMenu;
            SaveSlots.CloseActiveSlot();
            CameraFollow.Target = null;
            RefreshTimeScale();
        }

        public void RestartRun()
        {
            BossEntrancePresentation.Cancel(this);
            if (!legacyFixture) { ReturnToTown(); return; }
            IsMapOpen = false;
            StopAllCoroutines();
            stageEpoch++;
            IsStageTransitioning = false;
            ResetTimeEffect();
            RuntimeSceneObjects.ClearSession();
            if (Player != null) Destroy(Player.gameObject);
            Player = null;
            IsChoosingAccessory = false;
            IsInventoryOpen = false;
            IsStatsOpen = false;
            IsGameOver = false;
            IsManuallyPaused = false;
            IsStageClearPending = false;
            IsStageResultReady = false;
            RefreshTimeScale();
            StartCoroutine(StartRun());
        }

        private void RefreshTimeScale()
        {
            Time.timeScale = IsPaused ? 0f : timeEffectActive ? timeEffectScale : 1f;
        }

        private void ResetTimeEffect()
        {
            timeEffectRoutine = null;
            timeEffectActive = false;
            timeEffectScale = 1f;
        }

        private void PlayHitStop(bool critical, bool boss)
        {
            if (IsPaused || timeEffectActive) return;
            if (critical) PlayTimeEffect(.075f, .05f);
            else PlayTimeEffect(boss ? .03f : .035f, .12f);
        }

        private void PlayMeleeFinisherHitStop()
        {
            if (IsPaused || IsStageClearPending || timeEffectActive) return;
            PlayTimeEffect(.07f, .06f);
        }

        // Boss phase changes and other authored beats take over any short hit stop.
        public void PlayDramaticSlow(float realSeconds, float scale)
        {
            if (IsPaused) return;
            PlayTimeEffect(realSeconds, scale);
        }

        private void PlayBossDefeatSlowMotion()
        {
            PlayTimeEffect(1.15f, .18f);
        }

        private void PlayMeleeHitStop(float realSeconds, float scale)
        {
            if (IsPaused || IsStageClearPending) return;
            if (timeEffectActive && timeEffectEndsAt - Time.unscaledTime > realSeconds) return;
            PlayTimeEffect(realSeconds, scale);
        }

        private float timeEffectEndsAt;
        private void PlayTimeEffect(float realSeconds, float scale)
        {
            timeEffectEndsAt = Time.unscaledTime + realSeconds;
            if (timeEffectRoutine != null) StopCoroutine(timeEffectRoutine);
            timeEffectRoutine = StartCoroutine(TimeEffectRoutine(realSeconds, scale));
        }

        private IEnumerator TimeEffectRoutine(float realSeconds, float scale)
        {
            timeEffectActive = true;
            timeEffectScale = Mathf.Clamp(scale, .01f, 1f);
            RefreshTimeScale();
            yield return new WaitForSecondsRealtime(realSeconds);
            timeEffectActive = false;
            timeEffectScale = 1f;
            timeEffectRoutine = null;
            RefreshTimeScale();
        }

        private AccessoryDefinition GetAccessoryChoice(int index)
        {
            return index >= 0 && index < CurrentAccessoryChoices.Length ? CurrentAccessoryChoices[index] : null;
        }
    }
}
