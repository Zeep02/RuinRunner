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
        public bool IsNearStageExit => Player != null && Vector2.Distance(Player.transform.position, StageExitPosition) <= 1.8f;
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
        public int PendingStageRewards
        {
            get
            {
                var count = 0;
                foreach (var item in RuntimeSceneObjects.FindAll<FieldLootPickup>())
                    if (item.RequiresStageDecision) count++;
                return count;
            }
        }
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
            LootMessage = "장비를 두었습니다 · 봉인석으로 이동";
            LootMessageUntil = Time.unscaledTime + 4f;
        }
        private void CreateStageExit()
        {
            var portal = new GameObject("StageExit_" + CurrentStageIndex);
            portal.transform.SetParent(DungeonMap.Root.transform, false);
            portal.transform.position = StageExitPosition;
            var view = portal.AddComponent<SpriteRenderer>();
            view.sprite = PixelWorldArt.GetSprite(WorldInteractionArt.PortalActive);
            view.sortingOrder = 900 - Mathf.RoundToInt(StageExitPosition.y * 100);
            portal.transform.localScale = Vector3.one * 1.6f;
        }
        private void BeginNextStage()
        {
            priorStageVisitedRegions+=DungeonMap==null?0:DungeonMap.VisitedRegionCount;
            // Invalidate callbacks first; teardown's deferred Destroy cannot re-enter this run.
            stageEpoch++;
            StopAllCoroutines();
            ResetTimeEffect();
            sessionPhase = GameSessionPhase.Transition;
            IsStageTransitioning = true;
            IsStageResultReady = false;
            IsMapOpen = IsInventoryOpen = IsStatsOpen = IsUnlockOpen = false;
            waveTransition = true;
            Player.SetStageClearSafety(true);
            RuntimeSceneObjects.ClearSession();
            if (DungeonMap?.Root != null)
            {
                DungeonMap.Root.SetActive(false);
                Destroy(DungeonMap.Root);
            }
            Boss = null; ActiveChest = null; EnemiesAlive = 0;
            RefreshTimeScale();
            StartCoroutine(LoadNextStage());
        }
        private IEnumerator LoadNextStage()
        {
            yield return new WaitForSecondsRealtime(.65f);
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
            IsStageTransitioning = false;
            sessionPhase = GameSessionPhase.Expedition;
            StageIntroUntil = Time.unscaledTime + 5.5f;
            RefreshTimeScale();
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
                    if(ActiveTalents.Has(TalentId.SupplyChoice)&&SupplyUsesMana)Player.Stats.RestoreMana(Player.MaxMana*.5f);
                    else Player.Stats.Heal(Player.MaxHealth*.10f);
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
