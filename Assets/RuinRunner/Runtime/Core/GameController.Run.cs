using System.Collections;
using System.Collections.Generic;
using RuinRunner.Generation;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Run start, rooms, waves, rewards and stage clear.</summary>
    public sealed partial class GameController
    {
        private IEnumerator StartRun()
        {
            sessionEpoch++;
            settlementRunId = projectedSettlementRunId = null;
            if (DungeonMap != null && DungeonMap.Root != null)
                Destroy(DungeonMap.Root);
            stageEpoch++;
            sessionPhase = GameSessionPhase.Transition;
            InkTransition.Hold(CampaignStage.Get(0).Name);
            ScreenVignette.Ensure(this);
            oathTracker = new EquipmentOathTracker();
            usedServices.Clear();
            pendingLoot.Clear();
            lootByEvent.Clear();
            serviceByOperation.Clear();
            processedEconomyReceipts.Clear();
            rewardSequence = 0;
            CurrentStageIndex = 0;
            WeaponLootTable.SetDropStage(CurrentStageIndex);
            AccessoryLootTable.SetDropStage(CurrentStageIndex);
            GameAudioController.SetStage(0);
            StagesCleared = 0;
            IsStageTransitioning = true;
            RefreshTimeScale();
            yield return null;
            RunSeed = legacyFixture ? RoomTemplateCatalog.SelectNormalRunSeed() : BackendRun.seed;
            priorStageVisitedRegions = 0;
            var epoch = stageEpoch;
            yield return DungeonMap.BuildStageProgressively(map => DungeonMap = map,
                room =>
                {
                    if (epoch == stageEpoch)
                        EnterRoom(room);
                }, RunSeed, CurrentStageIndex);
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
            Player.GoldQuery = () => (int)RunGold;
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
            if (IsGameOver || IsTown)
                yield break;
            DungeonMap.MarkVisited(CurrentRoom);
            var room = DungeonMap.GetRoom(CurrentRoom);
            var state = DungeonMap.Regions[CurrentRoom];
            if (state.Phase == EncounterRegionPhase.Cleared)
            {
                waveTransition = false;
                IsExitOpen = true;
                yield break;
            }
            if (!DungeonMap.RequiresCombat(CurrentRoom))
            {
                DungeonMap.UnlockExit(CurrentRoom);
                currentWaveIndex = 0;
                roomWaveCount = 0;
                waveTransition = false;
                IsExitOpen = true;
                yield break;
            }
            DungeonMap.BeginEncounter(CurrentRoom);
            IsExitOpen = false;
            waveTransition = true;
            rewardSpawnedForRoom = false;
            currentWaveIndex = 0;
            roomWaveCount = room.WaveCount;
            WaveBanner = room.displayName;
            if (room.bossRoom)
                WaveBanner += " — 수호자의 방";
            WaveBannerUntil = Time.unscaledTime + 2f;
            yield return new WaitForSeconds(.55f);

            yield return StartCoroutine(BeginWave(0));
        }

        private IEnumerator BeginWave(int waveIndex)
        {
            if (IsGameOver || IsTown)
                yield break;
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
                if (telegraph != null)
                    telegraph.Complete();

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
            if (Boss != null)
                BossEntrancePresentation.Begin(this, Boss, CurrentStage.Name, "수호자가 깨어납니다", () => SetBossIntroPhase(false));
            if (EnemiesAlive == 0)
            {
                // Imported explicit wave indices may have gaps. Never unlock the
                // reward early and silently discard a later wave (including a boss).
                if (currentWaveIndex + 1 < roomWaveCount)
                {
                    waveTransition = true;
                    StartCoroutine(BeginNextWave());
                }
                else
                    StartCoroutine(SpawnRoomReward());
            }
        }

        public void EnemyDied(EnemyController enemy)
        {
            if (!IsRunActive || IsGameOver || IsStageTransitioning || enemy == null)
                return;
            if (IsTown)
                return;
            Stats.RegisterEnemyDefeated();
            AwardEnemyGold(enemy);
            if (Player != null)
                Player.OnEnemyDefeated(enemy);
            if (Progression != null)
                ExperienceOrb.Create(enemy.transform.position, enemy.ExperienceReward * (ActiveTalents.Has(TalentId.CursedPower) ? 2 : 1), Progression,
                    () => EnemiesAlive == 0 && !waveTransition && currentWaveIndex + 1 >= roomWaveCount);

            var defeatedStageBoss = enemy == Boss;
            if (!enemy.IsBoss && UnityEngine.Random.value < EnemyPotionChance)
                DropHealthPotion(enemy.transform.position);
            if (defeatedStageBoss)
                Boss = null;
            EnemiesAlive = Mathf.Max(0, EnemiesAlive - 1);
            if (EnemiesAlive == 0 && !IsGameOver && !waveTransition)
            {
                if (defeatedStageBoss)
                    BeginStageClearSafety();
                if (currentWaveIndex + 1 < roomWaveCount)
                {
                    waveTransition = true;
                    StartCoroutine(BeginNextWave());
                }
                else
                {
                    // The last one of a room hangs in the air for a moment.
                    SigFx.Play("RoomClearRing", enemy.transform.position, 0, -1, true, false, .8f);
                    if (!defeatedStageBoss)
                    {
                        PlayDramaticSlow(.34f, .22f);
                        CameraFollow.Shake(.2f, .22f);
                    }
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
            if (rewardSpawnedForRoom)
                yield break;
            rewardSpawnedForRoom = true;
            waveTransition = true;
            RuntimeSceneObjects.ClearProjectiles();
            yield return new WaitForSeconds(.65f);
            if (IsGameOver)
                yield break;
            var rewardRoom = CurrentRoom;
            AwardEncounterMaterials(rewardRoom);
            // A room's plan may fill its middle; the generator found the chest a free spot.
            var center = DungeonMap.Graph != null ? GeneratedStageUnityAdapter.Vector(DungeonMap.Graph.Regions[rewardRoom].RewardPosition) : DungeonMap.GetRoomCenter(rewardRoom) + RoomLayoutRules.RewardPosition;
            var epoch = stageEpoch;
            ChestRewards rewards = null;
            RewardChest chest = null;
            var sampledLuck = float.NaN;
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
            ActiveChest = chest = RewardChest.Create(center, () =>
            {
                if (epoch == stageEpoch)
                    OpenRoomChest(rewardRoom, ResolveRewards());
            }, Settings,
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
            if (state.ChestOpened)
                return;
            state.ChestOpened = true;
            if (!legacyFixture)
                _ = ClaimGold("chest:" + CurrentStageIndex + ":" + rewardRoom, "chest");
            var origin = state.Chest == null ? (Vector2)Player.transform.position : (Vector2)state.Chest.transform.position;
            var scatterIndex = 0;
            var bossReward = DungeonMap.GetRoom(rewardRoom).bossRoom;
            DropGold(origin, BackendCatalog?.economy?.chestGold ?? 0);
            foreach (var weapon in new[] { rewards.Weapon, rewards.BonusWeapon })
            {
                if (weapon == null)
                    continue;
                SpawnEquipmentPickup(origin, weapon, null, scatterIndex++, bossReward, "chest-item:" + CurrentStageIndex + ":" + rewardRoom + ":" + scatterIndex);
            }
            if (rewards.Accessory != null)
            {
                var accessory = rewards.Accessory;
                SpawnEquipmentPickup(origin, null, accessory, scatterIndex++, bossReward, "chest-accessory:" + CurrentStageIndex + ":" + rewardRoom);
            }
            if (UnityEngine.Random.value < ChestPotionChance)
                DropHealthPotion(origin, scatterIndex++);
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
            if (!Inventory.AddWeapon(weapon))
                return false;
            Player.EquipWeapon(Inventory.EquippedWeapon);
            CompletePickup("무기 획득  " + weapon.Rarity + " · " + weapon.Name);
            return true;
        }

        private bool CollectAccessoryPickup(AccessoryDefinition accessory)
        {
            if (!Inventory.AddAccessory(accessory))
                return false;
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
            if (Player != null)
                Player.SetStageClearSafety(true);
        }

        private IEnumerator PrepareStageResult()
        {
            RuntimeSceneObjects.ClearProjectiles();
            while (!IsGameOver)
            {
                var choicesResolved = Progression == null || (Progression.PendingLevelRewards == 0 && !IsChoosingAccessory);
                if (choicesResolved && !HasUnclaimedBossExperience())
                    break;
                yield return null;
            }
            if (IsGameOver || IsTown)
                yield break;

            IsStageResultReady = true;
            CreateStageExit();
            WaveBanner = CurrentStage.Name + " 해방";
            WaveBannerUntil = Time.unscaledTime + 4f;
        }

        public void ShowStageResult()
        {
            if (!IsStageResultReady || IsPaused || HasUnclaimedBossExperience() ||
                Progression != null && Progression.PendingLevelRewards > 0 || !IsNearStageExit || PendingStageRewards > 0)
                return;
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
            GameAudio.Play(GameSound.Victory, AudioChannel.Music, .6f);
            RefreshTimeScale();
        }

        private bool HasUnclaimedBossExperience()
        {
            if (DungeonMap == null)
                return false;
            var bossRoom = DungeonMap.BossRoomIndex;
            var center = DungeonMap.GetRoomCenter(bossRoom);
            var radius = DungeonMap.GetRoom(bossRoom).Size.magnitude * .5f + 3f;
            foreach (var orb in RuntimeSceneObjects.FindAll<ExperienceOrb>())
                if (!orb.IsCollected && Vector2.Distance(orb.transform.position, center) < radius)
                    return true;
            return false;
        }

        private void OnLevelRewardEarned()
        {
            if (IsGameOver)
                return;
            if (Player != null)
                SigFx.Play("LevelUpBurst", Player.transform.position, 0, -1, true, false, .7f);
            if (!IsChoosingAccessory)
                CurrentAccessoryChoices = RunContent.Choices(RunSeed, accessoryOfferIndex++, Player.Stats.Luck);
            IsChoosingAccessory = true;
            RefreshTimeScale();
        }
    }
}
