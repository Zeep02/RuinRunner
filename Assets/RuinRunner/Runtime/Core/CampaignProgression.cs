using System;
using System.Collections;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        public int CurrentStageIndex { get; private set; }
        public int StagesCleared { get; private set; }
        public CampaignStage CurrentStage => CampaignStage.Get(CurrentStageIndex);
        public bool IsStageTransitioning { get; private set; }
        public Vector2 StageExitPosition => DungeonMap == null ? Vector2.zero : DungeonMap.StageExitPosition;
        private const float StageExitReach = 1.8f;
        public bool IsNearStageExit => Player != null && Vector2.Distance(Player.transform.position, StageExitPosition) <= StageExitReach;
        private int stageEpoch;
        private float leaveEquipmentHeld;
        public float LeaveEquipmentProgress => Mathf.Clamp01(leaveEquipmentHeld / 1.2f);
        private void UpdateStageExitHold()
        {
            if (!IsStageResultReady || !IsNearStageExit || IsPaused || PendingStageRewards == 0 ||
                !Settings.GetKey(GameAction.Interact))
            { leaveEquipmentHeld = 0; return; }
            leaveEquipmentHeld += Time.unscaledDeltaTime;
            if (leaveEquipmentHeld >= 1.2f) { LeaveUncollectedStageEquipment(); leaveEquipmentHeld = 0; }
        }
        public int PendingStageRewards => FieldLootPickup.PendingDecisions;
        private Action<EnemyController> CaptureEnemyDeathCallback()
        {
            var epoch = stageEpoch;
            return enemy => { if (epoch == stageEpoch) EnemyDied(enemy); };
        }
        // An explicit choice at the portal lets a full bag leave loot without a softlock.
        public void LeaveUncollectedStageEquipment()
        {
            if (!IsStageResultReady || !IsNearStageExit || IsPaused) return;
            foreach (var item in RuntimeSceneObjects.FindAll<FieldLootPickup>()) item.RequiresStageDecision = false;
        }
        private void CreateStageExit()
        {
            var portal = new GameObject("StageExit_" + CurrentStageIndex);
            portal.transform.SetParent(DungeonMap.Root.transform, false);
            portal.transform.position = StageExitPosition;
            var view = portal.AddComponent<SpriteRenderer>();
            view.sortingOrder = 900 - Mathf.RoundToInt(StageExitPosition.y * 100);
            ProximityGate.Attach(portal, view, StageExitReach);
            // The way on opens with something to see from across the room.
            SigFx.Play("LootBeam", StageExitPosition, 0, -1, true, false, 1.4f);
            SigFx.Play("RoomClearRing", StageExitPosition, 0, -1, true, false, .9f);
            CameraFollow.Shake(.25f, .2f);
            AmbientFx.Create(portal.transform, StageExitPosition, "AmbPortal", .62f).GetComponent<SpriteRenderer>().sortingOrder = view.sortingOrder + 1;
        }
        const float MinimumTitleSeconds = 1.3f;
        private void BeginNextStage()
        {
            priorStageVisitedRegions+=DungeonMap==null?0:DungeonMap.VisitedRegionCount;
            // Invalidate callbacks first; teardown's deferred Destroy cannot re-enter this run.
            stageEpoch++;
            StopAllCoroutines();
            ResetTimeEffect();
            IsStageResultReady = false;
            IsMapOpen = IsInventoryOpen = IsStatsOpen = false;
            waveTransition = true;
            Player.SetStageClearSafety(true);
            Player.SetEntranceLock(true);
            RefreshTimeScale();
            StartCoroutine(LoadNextStage());
        }
        private IEnumerator LoadNextStage()
        {
            // The stage stays up and moving until the ink has swallowed it.
            yield return InkTransition.Cover(Player.FeetPoint, CampaignStage.Get(CurrentStageIndex + 1).Name);
            sessionPhase = GameSessionPhase.Transition;
            IsStageTransitioning = true;
            RuntimeSceneObjects.ClearSession();
            CombatStain.ClearAll();
            if (DungeonMap?.Root != null)
            {
                DungeonMap.Root.SetActive(false);
                Destroy(DungeonMap.Root);
            }
            Boss = null; ActiveChest = null; EnemiesAlive = 0;
            RefreshTimeScale();
            var titleSince = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(.3f);
            CurrentStageIndex++;
            WeaponLootTable.SetDropStage(CurrentStageIndex);AccessoryLootTable.SetDropStage(CurrentStageIndex);
            PrototypeAudioController.SetStage(CurrentStageIndex);
            var epoch = stageEpoch;
            yield return DungeonMap.BuildStageProgressively(map => DungeonMap=map,
                room => { if (epoch == stageEpoch) EnterRoom(room); }, RunSeed, CurrentStageIndex);
            CurrentRoom = DungeonMap.EntranceRoomIndex;
            DungeonMap.ResetExits();
            // Same Player / Inventory / Stats / Progression instances. This is the only stage teleport.
            Player.Teleport(DungeonMap.GetSpawnPoint(CurrentRoom));
            CameraFollow.FocusRoom(DungeonMap.Terrain.Bounds.center, DungeonMap.Terrain.Bounds.size, true);
            CreateExplorationRewards();
            CreateFieldServices();
            IsStageClearPending = false;
            IsExitOpen = false;
            rewardSpawnedForRoom = false;
            Player.SetStageClearSafety(false);
            var titleLeft = MinimumTitleSeconds - (Time.unscaledTime - titleSince);
            if (titleLeft > 0f) yield return new WaitForSecondsRealtime(titleLeft);
            IsStageTransitioning = false;
            sessionPhase = GameSessionPhase.Expedition;
            StageIntroUntil = Time.unscaledTime + 5.5f;
            RefreshTimeScale();
            yield return InkTransition.Reveal(Player);
            Player.SetEntranceLock(false);
            Debug.Log("[Campaign] Enter stage=" + (CurrentStageIndex + 1) + " seed=" + RunSeed + " level=" + Progression.Level + " kills=" + Stats.EnemiesDefeated);
            yield return BeginCurrentRoom();
        }
        private void CreateExplorationRewards()
        {
            var rewards = DungeonMap.Definition?.explorationRewards;
            if (rewards == null) return;
            var map = DungeonMap; var epoch = stageEpoch;
            for (var index = 0; index < rewards.Length; index++)
            {
                var reward = rewards[index]; var rewardIndex = index; var opened = false;
                AccessoryDefinition accessory = null; RewardChest chest = null; var sampledLuck = float.NaN;
                AccessoryDefinition ResolveAccessory()
                {
                    var luck = Player.Stats.Luck;
                    if (accessory == null || chest?.IsOpened != true && sampledLuck != luck)
                    {
                        sampledLuck = luck;
                        accessory = RunRandom.Sample(RunSeed, 41, CurrentStageIndex * 16 + rewardIndex, () => AccessoryLootTable.Roll(luck + 3));
                    }
                    return accessory;
                }
                chest = RewardChest.Create(reward.position, () =>
                {
                    if (opened || epoch != stageEpoch) return;
                    opened = true;
                    map.Regions[reward.region].ChestOpened = true;
                    accessory = ResolveAccessory();
                    SpawnEquipmentPickup(reward.position,null,accessory,0,false,"exploration:"+CurrentStageIndex+":"+rewardIndex);
                    Player.Stats.Heal(Player.MaxHealth*.10f);
                    LootMessage = "보급함 발견";
                    LootMessageUntil = Time.unscaledTime + 4;
                    Debug.Log("[Campaign] Exploration reward stage=" + CurrentStageIndex + " id=" + rewardIndex);
                }, Settings, () => epoch == stageEpoch && !IsPaused && map.Regions[reward.region].Phase == EncounterRegionPhase.Cleared,
                    ResolveAccessory().Rarity, () => ResolveAccessory().Rarity);
                chest.ExplorationRegion = reward.region;
                chest.HintName = reward.name;
            }
        }
    }
}
