using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RuinRunner
{
    public enum GameSessionPhase { MainMenu, Town, Expedition, BossIntro, Result, Paused, Transition }
    public sealed partial class GameController
    {
        GameSessionPhase sessionPhase=GameSessionPhase.MainMenu;
        public GameSessionPhase Phase=>IsManuallyPaused?GameSessionPhase.Paused:sessionPhase;
        public bool IsTown=>sessionPhase==GameSessionPhase.Town;
        public bool IsExpedition=>sessionPhase==GameSessionPhase.Expedition||sessionPhase==GameSessionPhase.BossIntro;
        public TalentLoadout ActiveTalents { get; private set; }=new TalentLoadout();
        public FieldServiceObject NearbyService { get; private set; }
        public FieldServiceObject OpenService { get; private set; }
        public bool IsServiceOpen=>OpenService!=null;
        public bool SessionOperationPending { get; private set; }
        public bool IsPreparingTownArt { get; private set; }
        readonly CancellationTokenSource townArtLifetime = new CancellationTokenSource();
        public string SessionMessage { get; private set; }="";
        public bool ShowItemDetails { get; set; }
        TownWorld town;
        EquipmentOathTracker oathTracker=new EquipmentOathTracker();
        bool legacyFixture;
        internal bool UsesLegacyFixture=>legacyFixture;
        public void SetBossIntroPhase(bool active)
        {
            if(!IsRunActive||IsTown||IsGameOver)return;
            sessionPhase=active?GameSessionPhase.BossIntro:GameSessionPhase.Expedition;
            IsMapOpen=IsInventoryOpen=IsStatsOpen=IsUnlockOpen=false;RefreshTimeScale();
        }
        public async void OpenProfileTown(int slot)
        {
            if(IsRunActive||SessionOperationPending)return;
            int epoch=sessionEpoch;
            SessionOperationPending=true;
            try
            {
                if(!BackendReady&&!await ConnectBackendAsync()){TraceProfileEntry("connect rejected",epoch);return;}
                if(!SessionMatches(epoch)){TraceProfileEntry("session changed before selection",epoch);return;}
                if(!await SelectBackendSlotAsync(slot)){TraceProfileEntry("selection rejected",epoch);return;}
                if(!SessionMatches(epoch)){TraceProfileEntry("session changed after selection",epoch);return;}
                IsPreparingTownArt=true;
                await PlayerAnimationController.PrepareAsync(townArtLifetime.Token);
                if(!SessionMatches(epoch)){TraceProfileEntry("session changed after preparation",epoch);return;}
                legacyFixture=false;LastMetaReward=0;ResetProfileSlot=0;EnterTown();
                TraceProfileEntry("town entered",epoch);
            }
            catch(OperationCanceledException) when(sessionDisposed) { }
            catch(Exception e){Debug.LogException(e);SessionMessage="연결을 확인한 뒤 다시 시도하세요.";}
            finally{IsPreparingTownArt=false;SessionOperationPending=false;}
        }
        void TraceProfileEntry(string step,int epoch)
        {
            if(StageValidationInput.Enabled)
                Debug.Log("[Session Entry] "+step+"; epoch="+epoch+"/"+sessionEpoch+"; disposed="+sessionDisposed+
                    "; status="+BackendStatus+"; code="+BackendErrorCode);
        }
        void EnterTown()
        {
            sessionEpoch++;
            if(!legacyFixture){BindExchangeCatalog();WeaponLootTable.BindPinnedCatalog(BackendCatalog);AccessoryLootTable.BindPinnedCatalog(BackendCatalog);}
            BossEntrancePresentation.Cancel(this);
            stageEpoch++;StopAllCoroutines();ResetTimeEffect();RuntimeSceneObjects.ClearSession();
            if(Player!=null)Destroy(Player.gameObject);
            if(DungeonMap?.Root!=null)Destroy(DungeonMap.Root);
            if(town!=null)Destroy(town.gameObject);
            DungeonMap=null;Boss=null;ActiveChest=null;Progression=null;EnemiesAlive=0;waveTransition=false;
            IsRunActive=true;sessionPhase=GameSessionPhase.Town;IsGameOver=IsVictory=false;IsStageTransitioning=false;
            IsInventoryOpen=IsStatsOpen=IsUnlockOpen=IsMapOpen=IsChoosingAccessory=IsManuallyPaused=false;
            IsStageClearPending=IsStageResultReady=IsExitOpen=false;OpenService=NearbyService=null;StageIntroUntil=0;
            MetaProgression=new MetaProgressionService(SaveSlots);ActiveTalents=new TalentLoadout(SaveSlots.ActiveData?.activeTalentIds);
            Inventory.Reset();ResetEquipmentSelection();
            Inventory.TryAddConfirmed(SaveSlots.ActiveData?.startingWeaponId??"rust_sword",InventoryEntryKind.Weapon,"town-practice",true);
            town=TownWorld.Create();
            Player=PlayerController.Create(TownWorld.Arrival,()=>IsPaused,()=>true,()=>{},CameraFollow,Inventory,Settings);
            Player.ConfigureCombatGrowth(ActiveTalents.Has,NotifyCombatAction);Player.IsTrainingCombat=true;
            Player.EquipWeapon(Inventory.EquippedWeapon);CameraFollow.Target=Player.transform;
            CameraFollow.FocusRoom(Vector2.zero,TownWorld.Size,true);
            TownTrainingTarget.Create(this,town.transform,TownWorld.TrainingPosition);
            PrototypeAudioController.SetStage(0);RefreshTimeScale();
            SessionMessage=LastMetaReward>0?"잘 돌아왔어요. 유산 +"+LastMetaReward:"준비가 끝나면 동쪽 원정 입구로.";
        }
        public async void DepartTown()
        {
            if(!IsTown||SessionOperationPending||BackendBusy||BackendHasPending||BackendHasUnfinishedRun)return;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            SessionOperationPending=true;
            try
            {
                if(!await StartBackendRunAsync(SaveSlots.IsValidationProfile&&StageValidationInput.Enabled?StageValidationInput.RequestedSeed:null))return;
                if(!SessionMatches(epoch,profileId)||!IsTown)return;
                BindExchangeCatalog();WeaponLootTable.BindPinnedCatalog(BackendCatalog);AccessoryLootTable.BindPinnedCatalog(BackendCatalog);
                ActiveTalents=new TalentLoadout(BackendRun.loadout.activeTalentIds);
                CloseService();sessionPhase=GameSessionPhase.Transition;
                if(town!=null){Destroy(town.gameObject);town=null;}
                if(Player!=null){Destroy(Player.gameObject);Player=null;}
                StartCoroutine(StartRun());
            }
            catch(Exception e){Debug.LogException(e);SessionMessage="출정 준비를 확인할 수 없습니다.";}
            finally{SessionOperationPending=false;}
        }
        public async void ReturnToTown()
        {
            if(!IsGameOver||SessionOperationPending||EconomyOperationPending||BackendBusy)return;
            int epoch=sessionEpoch;string runId=BackendRun?.runId;
            SessionOperationPending=true;
            try
            {
                if(!legacyFixture&&BackendHasPending&&!await RetryBackendAsync())return;
                if(!SessionMatches(epoch,runId:runId))return;
                if(!legacyFixture&&BackendHasUnfinishedRun)
                    if(!await SettleBackendRunAsync(IsVictory?"victory":"defeat",Stats.EnemiesDefeated,priorStageVisitedRegions+(DungeonMap?.VisitedRegionCount??0),StagesCleared))return;
                if(!SessionMatches(epoch,runId:runId)||!legacyFixture&&(BackendHasPending||BackendHasUnfinishedRun))return;
                RefreshSettlementProjection();
                EnterTown();
            }
            catch(Exception e){Debug.LogException(e);SessionMessage="정산 대기 중입니다. 다시 시도할 수 있습니다.";}
            finally{SessionOperationPending=false;}
        }
        public async void ResolveInterruptedExpedition()
        {
            if(!IsTown||!BackendHasUnfinishedRun||SessionOperationPending)return;
            int epoch=sessionEpoch;string runId=BackendRun.runId;
            SessionOperationPending=true;
            try{if(await AbandonBackendRunAsync()&&SessionMatches(epoch,runId:runId)){MetaProgression=new MetaProgressionService(SaveSlots);SessionMessage="이전 원정을 정리했습니다. 새로 출정할 수 있습니다.";}}
            finally{SessionOperationPending=false;}
        }
        public async void ChooseStartingWeapon(string id)
        {
            if(!IsTown||OpenService?.Kind!=FieldServiceKind.StartingGear||SessionOperationPending)return;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            SessionOperationPending=true;
            try
            {
                if(!await SaveBackendConfigurationAsync(id,ActiveTalents.Ids))return;
                if(!SessionMatches(epoch,profileId)||!IsTown||Player==null)return;
                Inventory.Reset();Inventory.TryAddConfirmed(id,InventoryEntryKind.Weapon,"town-practice",true);Player.EquipWeapon(Inventory.EquippedWeapon);
                SessionMessage="시작 무기 · "+Inventory.EquippedWeapon.Name;
            }
            finally{SessionOperationPending=false;}
        }
        public async void ToggleTalent(string id)
        {
            if(!IsTown||OpenService?.Kind!=FieldServiceKind.Talents||SessionOperationPending)return;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            var t=TalentCatalog.Find(id);if(t==null)return;
            var ids=new List<string>(ActiveTalents.Ids);
            if(!ids.Remove(id))
            {
                ids.RemoveAll(v=>TalentCatalog.Find(v)?.Family==t.Family);
                if(ids.Count>=TalentLoadout.MaximumActive){SessionMessage="활성 재능은 세 개까지입니다.";return;}ids.Add(id);
            }
            SessionOperationPending=true;
            try
            {
                if(!await SaveBackendConfigurationAsync(SaveSlots.ActiveData.startingWeaponId,ids.ToArray()))return;
                if(!SessionMatches(epoch,profileId)||!IsTown||Player==null)return;
                ActiveTalents=new TalentLoadout(ids);Player.ConfigureCombatGrowth(ActiveTalents.Has,NotifyCombatAction);SessionMessage="수련 구성을 바꿨습니다.";
            }
            finally{SessionOperationPending=false;}
        }
        public async void UnlockTownChoice(string kind,string id)
        {
            if(!IsTown||SessionOperationPending)return;SessionOperationPending=true;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            try{if(await UnlockBackendAsync(kind,id)&&SessionMatches(epoch,profileId)){MetaProgression=new MetaProgressionService(SaveSlots);SessionMessage="새 선택지가 열렸습니다.";}}
            finally{SessionOperationPending=false;}
        }
        public void CloseService(){OpenService=null;modalCloseFrame=Time.frameCount;RefreshTimeScale();}
        void TickServices()
        {
            NearbyService=null;
            if(Player==null||IsGameOver||IsStageTransitioning||sessionPhase==GameSessionPhase.BossIntro)return;
            if(IsServiceOpen){if(EconomyOperationPending||SessionOperationPending)return;if(Settings.GetKeyDown(GameAction.Pause)||Settings.GetKeyDown(GameAction.Interact))CloseService();return;}
            if(IsPaused||EnemiesAlive>0||waveTransition)return;
            float nearest=2f;
            foreach(var obj in RuntimeSceneObjects.FindAll<FieldServiceObject>())
            {
                if(obj.RegionIndex>=0&&(DungeonMap==null||DungeonMap.Regions[obj.RegionIndex].Phase!=EncounterRegionPhase.Cleared))continue;
                float d=Vector2.Distance(Player.transform.position,obj.transform.position);
                if(d<nearest){nearest=d;NearbyService=obj;}
            }
            if(NearbyService!=null&&Settings.GetKeyDown(GameAction.Interact))
            {OpenService=NearbyService;ServiceSelectedEquipment=Inventory.ActiveSlot.Item?.Identity??0;SessionMessage="";RefreshTimeScale();}
        }
        public async void RetrySessionConnection()
        {
            if(SessionOperationPending||BackendBusy)return;SessionOperationPending=true;
            try{if(BackendHasPending)await RetryBackendAsync();else if(!BackendReady)await ConnectBackendAsync();if(BackendHasUnfinishedRun)await RefreshBackendEconomyAsync();}
            finally{SessionOperationPending=false;}
        }
        public void NotifyCombatAction(CombatGrowthEvent e)
        {
            if(!IsExpedition||e.IsTraining||e.IsRecursive)return;
            e.Time=Time.time;
            if(oathTracker.Apply(Inventory,e)){Inventory.NotifyAwakening();LootMessage="서약 완성";LootMessageUntil=Time.unscaledTime+2;PrototypeAudio.Play(PrototypeSound.Upgrade,AudioChannel.Interface,.55f);}
        }
    }
}
