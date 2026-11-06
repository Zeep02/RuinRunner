using System;
using System.IO;
using System.Threading.Tasks;
using RuinRunner.Backend;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        BackendFlowCoordinator backendFlow;
        public bool BackendReady => backendFlow != null && backendFlow.Ready;
        public bool BackendBusy => backendFlow != null && (backendFlow.Busy || backendFlow.EconomyBusy);
        public bool BackendHasPending => backendFlow != null && backendFlow.HasPending;
        public string BackendStatus => backendFlow?.Status ?? "연결 전";
        public string BackendErrorCode => backendFlow?.ErrorCode;
        public ProfileView BackendProfile => backendFlow?.Profile;
        public RunView BackendRun => backendFlow?.Run;
        public ContentCatalog BackendCatalog => backendFlow?.Catalog;
        public EconomyView BackendEconomy => backendFlow?.Economy;
        public bool BackendHasUnfinishedRun => BackendRun != null && BackendRun.status == "active";

        void EnsureBackend()
        {
            if (backendFlow != null)
                return;
            var directory = SaveSlots.IsValidationProfile ? Path.Combine(SaveSlots.SaveDirectory, "BackendRecovery")
                : Path.Combine(Application.persistentDataPath, "RuinRunner", "BackendRecovery");
            // Public endpoint only. Credentials remain CurrentUser DPAPI outside Assets/build content.
            var endpoint = Environment.GetEnvironmentVariable("RUINRUNNER_API_URL");
            backendFlow = new BackendFlowCoordinator(SaveSlots, new BackendClientOptions
            {
                ApiBaseUri = string.IsNullOrWhiteSpace(endpoint) ? "http://127.0.0.1:5186/" : endpoint,
                StorageDirectory = directory
            });
            backendFlow.EconomyCommitted += ApplyEconomyReceipt;
        }
        public Task<bool> ConnectBackendAsync()
        {
            EnsureBackend();
            return backendFlow.ConnectAsync();
        }
        public Task<bool> SelectBackendSlotAsync(int slot, string name = null)
        {
            EnsureBackend();
            return backendFlow.SelectSlotAsync(slot, name);
        }
        public Task<bool> RetryBackendAsync()
        {
            EnsureBackend();
            return backendFlow.RetryAsync();
        }
        public Task<bool> SaveBackendConfigurationAsync(string weaponId, string[] talents)
        {
            EnsureBackend();
            return backendFlow.ConfigureAsync(weaponId, talents);
        }
        public Task<bool> UnlockBackendAsync(string kind, string contentId)
        {
            EnsureBackend();
            return backendFlow.UnlockAsync(kind, contentId);
        }
        public Task<bool> StartBackendRunAsync(int? requestedSeed = null)
        {
            EnsureBackend();
            return backendFlow.StartAsync(requestedSeed, "modules-v1.0.0", "RuinRunner-" + Application.version);
        }
        public Task<bool> SettleBackendRunAsync(string outcome, int kills, int rooms, int stages)
        {
            EnsureBackend();
            return backendFlow.SettleAsync(outcome, kills, rooms, stages);
        }
        public Task<bool> AbandonBackendRunAsync() => SettleBackendRunAsync("abandoned", 0, 0, 0);
        public Task<bool> RefreshBackendEconomyAsync()
        {
            EnsureBackend();
            return backendFlow.RefreshEconomyAsync();
        }
        public Task<EconomyReceipt> TradeBackendAsync(TradeRequest request)
        {
            EnsureBackend();
            return backendFlow.TradeAsync(request);
        }
        public Task<EconomyReceipt> ClaimBackendRewardAsync(RewardRequest request)
        {
            EnsureBackend();
            return backendFlow.RewardAsync(request);
        }
        public void TickBackendRecovery()
        {
            if (backendFlow != null)
                _ = backendFlow.TickRecoveryAsync();
            RefreshSettlementProjection();
        }
        public void DisposeBackend()
        {
            if (backendFlow == null)
                return;
            backendFlow.EconomyCommitted -= ApplyEconomyReceipt;
            backendFlow.Dispose();
            backendFlow = null;
        }
    }
}
