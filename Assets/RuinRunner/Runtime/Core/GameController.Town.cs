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
        TownWorld town;
        EquipmentOathTracker oathTracker=new EquipmentOathTracker();
        bool legacyFixture;
        internal bool UsesLegacyFixture=>legacyFixture;
        public void SetBossIntroPhase(bool active)
        {
            if(!IsRunActive||IsTown||IsGameOver)return;
            sessionPhase=active?GameSessionPhase.BossIntro:GameSessionPhase.Expedition;
            IsMapOpen=IsInventoryOpen=IsStatsOpen=false;RefreshTimeScale();
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
            stageEpoch++;StopAllCoroutines();ResetTimeEffect();RuntimeSceneObjects.ClearSession();CombatStain.ClearAll();UiFx.FilmBars(false);
            if(Player!=null)Destroy(Player.gameObject);
            if(DungeonMap?.Root!=null)Destroy(DungeonMap.Root);
            if(town!=null)Destroy(town.gameObject);
            DungeonMap=null;Boss=null;ActiveChest=null;Progression=null;EnemiesAlive=0;waveTransition=false;
            IsRunActive=true;sessionPhase=GameSessionPhase.Town;IsGameOver=IsVictory=false;IsStageTransitioning=false;
            IsInventoryOpen=IsStatsOpen=IsMapOpen=IsChoosingAccessory=IsManuallyPaused=false;
            IsStageClearPending=IsStageResultReady=IsExitOpen=false;OpenService=NearbyService=null;StageIntroUntil=0;
            MetaProgression=new MetaProgressionService(SaveSlots);ActiveTalents=new TalentLoadout(SaveSlots.ActiveData?.activeTalentIds);
            Inventory.Reset();ResetEquipmentSelection();
            Inventory.TryAddConfirmed(SaveSlots.ActiveData?.startingWeaponId??"trainee_blade",InventoryEntryKind.Weapon,"town-practice",true);
            town=TownWorld.Create();
            Player=PlayerController.Create(TownWorld.Arrival,()=>IsPaused,()=>true,()=>{},CameraFollow,Inventory,Settings);
            Player.ConfigureMastery(SaveSlots.ActiveData?.unlockedTalentIds);Player.ConfigureCombatGrowth(ActiveTalents.Has,NotifyCombatAction);Player.IsTrainingCombat=true;
            Player.EquipWeapon(Inventory.EquippedWeapon);CameraFollow.Target=Player.transform;
            CameraFollow.FocusRoom(Vector2.zero,TownWorld.Size,true);
            TownTrainingTarget.Create(this,town.transform,TownWorld.TrainingPosition);
            PrototypeAudioController.SetStage(0);RefreshTimeScale();
            SessionMessage=LastMetaReward>0?"유산 +"+LastMetaReward:"";
            ScreenVignette.Ensure(this);
            InkTransition.Hold();Player.SetEntranceLock(true);StartCoroutine(RevealTown());
        }
        async System.Threading.Tasks.Task CoverTownAsync(int epoch,string profileId)
        {
            var covering=true;
            StartCoroutine(CoverTown(()=>covering=false));
            while(covering&&SessionMatches(epoch,profileId))await System.Threading.Tasks.Task.Yield();
        }
        System.Collections.IEnumerator CoverTown(Action done)
        {
            if(Player!=null)yield return InkTransition.Cover(Player.FeetPoint,CampaignStage.Get(0).Name);
            done();
        }
        System.Collections.IEnumerator RevealTown()
        {
            yield return InkTransition.Reveal(Player);
            if(Player!=null)Player.SetEntranceLock(false);
        }
        public async void DepartTown()
        {
            if(!IsTown||SessionOperationPending||BackendBusy||BackendHasPending)return;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            SessionOperationPending=true;
            try
            {
                if(!await SettleInterruptedRunAsync())return;
                if(!await StartBackendRunAsync(SaveSlots.IsValidationProfile&&StageValidationInput.Enabled?StageValidationInput.RequestedSeed:null))return;
                if(!SessionMatches(epoch,profileId)||!IsTown)return;
                BindExchangeCatalog();WeaponLootTable.BindPinnedCatalog(BackendCatalog);AccessoryLootTable.BindPinnedCatalog(BackendCatalog);
                ActiveTalents=new TalentLoadout(BackendRun.loadout.activeTalentIds);
                CloseService();
                // Still the town until the ink has covered it, so nothing starts drawing the run's HUD over it.
                if(Player!=null)Player.SetEntranceLock(true);
                await CoverTownAsync(epoch,profileId);
                if(!SessionMatches(epoch,profileId)||!IsTown)return;
                sessionPhase=GameSessionPhase.Transition;
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
            catch(Exception e){Debug.LogException(e);SessionMessage="";}
            finally{SessionOperationPending=false;}
        }
        public async void ResolveInterruptedExpedition()
        {
            if(!IsTown||!BackendHasUnfinishedRun||SessionOperationPending)return;
            int epoch=sessionEpoch;string runId=BackendRun.runId;
            SessionOperationPending=true;
            try{if(await AbandonBackendRunAsync()&&SessionMatches(epoch,runId:runId)){MetaProgression=new MetaProgressionService(SaveSlots);SessionMessage="";}}
            finally{SessionOperationPending=false;}
        }
        // An expedition cut short can never be resumed, so the town settles it the moment it would get in the way.
        async System.Threading.Tasks.Task<bool> SettleInterruptedRunAsync()
        {
            if(!BackendHasUnfinishedRun)return true;
            if(!await AbandonBackendRunAsync())return false;
            MetaProgression=new MetaProgressionService(SaveSlots);return true;
        }
        public async void ChooseStartingWeapon(string id)
        {
            if(!IsTown||OpenService?.Kind!=FieldServiceKind.StartingGear||SessionOperationPending)return;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            SessionOperationPending=true;
            try
            {
                if(!await SettleInterruptedRunAsync())return;
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
                if(ids.Count>=TalentLoadout.MaximumActive)return;ids.Add(id);
            }
            SessionOperationPending=true;
            try
            {
                if(!await SettleInterruptedRunAsync())return;
                if(!await SaveBackendConfigurationAsync(SaveSlots.ActiveData.startingWeaponId,ids.ToArray()))return;
                if(!SessionMatches(epoch,profileId)||!IsTown||Player==null)return;
                ActiveTalents=new TalentLoadout(ids);Player.ConfigureCombatGrowth(ActiveTalents.Has,NotifyCombatAction);SessionMessage="";
            }
            finally{SessionOperationPending=false;}
        }
        public async void UnlockTownChoice(string kind,string id)
        {
            if(!IsTown||SessionOperationPending)return;SessionOperationPending=true;
            int epoch=sessionEpoch;string profileId=BackendProfile?.profileId;
            try{if(await SettleInterruptedRunAsync()&&await UnlockBackendAsync(kind,id)&&SessionMatches(epoch,profileId)){MetaProgression=new MetaProgressionService(SaveSlots);if(Player!=null){Player.ConfigureMastery(SaveSlots.ActiveData?.unlockedTalentIds);Player.ConfigureCombatGrowth(ActiveTalents.Has,NotifyCombatAction);}SessionMessage="";}}
            finally{SessionOperationPending=false;}
        }
        public void CloseService(){OpenService=null;modalCloseFrame=Time.frameCount;RefreshTimeScale();}
        void TickServices()
        {
            NearbyService=null;
            if(Player==null||IsGameOver||IsStageTransitioning||sessionPhase==GameSessionPhase.BossIntro)return;
            if(IsServiceOpen){if(EconomyOperationPending||SessionOperationPending)return;if(Settings.GetKeyDown(GameAction.Pause)||Settings.GetKeyDown(GameAction.Interact))CloseService();return;}
            if(IsPaused||EnemiesAlive>0||waveTransition)return;
            float nearest=FieldServiceObject.TownServiceReach;
            foreach(var obj in FieldServiceObject.Present)
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
