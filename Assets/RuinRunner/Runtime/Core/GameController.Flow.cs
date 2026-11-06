using UnityEngine;

namespace RuinRunner
{
    /// <summary>Room entry, death, per-frame input, pause and returning to menus.</summary>
    public sealed partial class GameController
    {
        private void EnterRoom(int targetRoom)
        {
            if (!IsExitOpen || waveTransition || IsGameOver || IsStageTransitioning || IsPaused || targetRoom == CurrentRoom ||
                DungeonMap == null || !DungeonMap.CanEnter(CurrentRoom, targetRoom))
                return;

            if (StageValidationInput.Enabled)
                Debug.Log("[Portal] Enter target=" + targetRoom + " from=" + CurrentRoom);

            IsExitOpen = false;
            waveTransition = true;
            CurrentRoom = targetRoom;
            // Position and camera remain continuous while the seal closes behind the player.
            StartCoroutine(BeginCurrentRoom());
        }

        public void PlayerDied()
        {
            BossEntrancePresentation.Cancel(this);
            if (IsGameOver || !IsRunActive || IsStageClearPending || IsStageTransitioning)
                return;
            GrantMetaReward(false);
            IsGameOver = true;
            sessionPhase = GameSessionPhase.Result;
            IsVictory = false;
            RefreshTimeScale();
        }

        private void GrantMetaReward(bool victory)
        {
            if (metaRewardGranted)
                return;
            metaRewardGranted = true;
            if (legacyFixture)
                LastMetaReward = MetaProgression.AwardRun(Stats.EnemiesDefeated, priorStageVisitedRegions + (DungeonMap?.VisitedRegionCount ?? 0), victory, StagesCleared);
            else
                BeginServerSettlement(victory);
        }

        private void Update()
        {
            if (modalCloseFrame >= 0 && modalCloseFrame < Time.frameCount)
            {
                modalCloseFrame = -1;
                RefreshTimeScale();
            }
            TickBackendRecovery();
            TickServices();
            // Closing a service consumes this frame's Escape/Interact input.
            // Otherwise Escape also opens the pause menu immediately afterward.
            if (IsServiceOpen || IsStageTransitioning || InkTransition.LocksInput || modalCloseFrame == Time.frameCount)
                return;
            if (IsRunActive && !IsTown && !IsStageTransitioning && DungeonMap != null && Player != null)
            {
                DungeonMap.TrackPosition(Player.transform.position);
                // A high-speed dash can cross a thin trigger within a physics step. Actual region geometry provides the same adjacency-gated transition.
                if (DungeonMap.IsProcedural && DungeonMap.PhysicalRegion >= 0 && DungeonMap.PhysicalRegion != CurrentRoom)
                    EnterRoom(DungeonMap.PhysicalRegion);
                ActiveChest = null;
                var nearestChest = float.PositiveInfinity;
                foreach (var region in DungeonMap.Regions)
                {
                    if (region.Chest == null || region.ChestOpened)
                        continue;
                    var distance = Vector2.SqrMagnitude(region.Chest.transform.position - Player.transform.position);
                    if (distance < nearestChest)
                    {
                        nearestChest = distance;
                        ActiveChest = region.Chest;
                    }
                }
            }
            if (IsMapOpen && (!IsRunActive || IsStageTransitioning || IsChoosingAccessory || IsInventoryOpen || IsStatsOpen || IsGameOver || IsManuallyPaused))
            {
                IsMapOpen = false;
                RefreshTimeScale();
            }
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
                if (IsExpedition)
                    Stats.Tick(Time.deltaTime);
                Inventory.Tick(Time.deltaTime);
            }

            if (IsGameOver)
            {
                if (Settings.GetKeyDown(GameAction.Reload))
                    RestartRun();
                return;
            }

            if (IsMapOpen)
            {
                if (Settings.GetKeyDown(GameAction.Map) || Settings.GetKeyDown(GameAction.Pause))
                {
                    IsMapOpen = false;
                    modalCloseFrame = Time.frameCount;
                    RefreshTimeScale();
                }
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
                    if (SelectedEquipment == null)
                        FocusInventoryQuickSlot(Inventory.ActiveSlotIndex);
                }
                else
                    modalCloseFrame = Time.frameCount;
                GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .45f);
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
                if (HasDisassemblyConfirmation)
                {
                    CancelEquipmentDisassembly();
                    return;
                }
                if (IsInventoryOpen || IsStatsOpen)
                {
                    if (IsInventoryOpen)
                        modalCloseFrame = Time.frameCount;
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
                for (var choice = 0; choice < CurrentAccessoryChoices.Length; choice++)
                    if (Settings.GetKeyDown(GameSettingsService.AccessoryChoiceAction(choice)))
                    {
                        SelectAccessory(GetAccessoryChoice(choice));
                        break;
                    }
                return;
            }

            if (IsInventoryOpen)
            {
                ReadInventoryInput();
                return;
            }

            if (!IsPaused && Player != null)
            {
                if (!IsTown)
                    UpdateStageExitHold();
                if (IsStageResultReady && IsNearStageExit && Settings.GetKeyDown(GameAction.Interact) && (PendingStageRewards == 0 || !IsPlayerNearCollectible()))
                {
                    ShowStageResult();
                    return;
                }
                if (Settings.GetKeyDown(GameAction.Weapon1))
                    SelectWeaponSlot(0);
                if (Settings.GetKeyDown(GameAction.Weapon2))
                    SelectWeaponSlot(1);
                if (Settings.GetKeyDown(GameAction.Weapon3))
                    SelectWeaponSlot(2);
                if (Settings.GetKeyDown(GameAction.Reload) && Inventory.StartReload())
                    GameAudio.Play(GameSound.Inventory, AudioChannel.Inventory, .4f);
            }

        }

        private bool IsPlayerNearCollectible()
        {
            if (Player == null)
                return false;
            foreach (var pickup in RuntimeSceneObjects.FindAll<FieldLootPickup>())
                if (pickup != null && Vector2.Distance(Player.transform.position, pickup.transform.position) <= 1.6f)
                    return true;
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
            if (EconomyOperationPending || SessionOperationPending || BackendBusy)
                return;
            BossEntrancePresentation.Cancel(this);
            if (!IsRunActive)
                return;
            sessionEpoch++;
            GameAudioController.SetStage(0);
            IsMapOpen = false;
            StopAllCoroutines();
            stageEpoch++;
            IsStageTransitioning = false;
            ResetTimeEffect();
            RuntimeSceneObjects.ClearSession();
            if (Player != null)
                Destroy(Player.gameObject);
            if (DungeonMap != null && DungeonMap.Root != null)
                Destroy(DungeonMap.Root);
            Player = null;
            Progression = null;
            DungeonMap = null;
            Boss = null;
            ActiveChest = null;
            MetaProgression = null;
            IsRunActive = false;
            sessionPhase = GameSessionPhase.MainMenu;
            if (town != null)
            {
                Destroy(town.gameObject);
                town = null;
            }
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
            if (!legacyFixture)
            {
                ReturnToTown();
                return;
            }
            IsMapOpen = false;
            StopAllCoroutines();
            stageEpoch++;
            IsStageTransitioning = false;
            ResetTimeEffect();
            RuntimeSceneObjects.ClearSession();
            if (Player != null)
                Destroy(Player.gameObject);
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
    }
}
