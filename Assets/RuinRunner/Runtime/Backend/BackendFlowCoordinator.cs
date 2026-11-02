using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RuinRunner.Backend
{
    /// <summary>Normal profile/run flow. Local files are immutable import inputs or encrypted recovery journals.</summary>
    public sealed class BackendFlowCoordinator : IDisposable
    {
        readonly RuinBackendClient client;
        readonly SaveSlotService slots;
        readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);
        int economyRequests;
        DateTime nextEconomyRetryUtc;
        bool recoveryRunning;
        bool disposed;
        ConfirmProfileResetRequest resetRequest;
        public bool Ready { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "연결 전";
        public string ErrorCode { get; private set; }
        public bool HasPending { get; private set; }
        public ProfileView Profile { get; private set; }
        public RunView Run { get; private set; }
        public ContentCatalog Catalog { get; private set; }
        public ProfileView[] Profiles { get; private set; } = Array.Empty<ProfileView>();
        public long LastReward { get; private set; }
        public EconomyView Economy { get; private set; }
        public ProfileResetChallenge ResetChallenge { get; private set; }
        public bool EconomyBusy => economyRequests > 0;
        public event Action<EconomyReceipt> EconomyCommitted;
        public event Action<ProfileResetReceipt> ProfileResetCommitted;
        public BackendFlowCoordinator(SaveSlotService saveSlots, BackendClientOptions options)
        { slots = saveSlots ?? throw new ArgumentNullException(nameof(saveSlots)); client = new RuinBackendClient(options); }

        async Task<bool> Work(string status, Func<Task> action)
        {
            if (Busy || disposed) return false;
            Busy = true; Status = status; ErrorCode = null;
            await commandGate.WaitAsync();
            try { await action(); if(disposed)return false; Ready = true; HasPending = (await client.GetPendingOperationsAsync()).Length != 0 || (await client.ReadEconomyQueueAsync()).Length != 0; Status = HasPending ? "저장 확인 대기" : "저장됨"; return true; }
            catch (BackendClientException e)
            {
                ErrorCode = e.Code; HasPending = HasPending || e.CanRetry || e.OutcomeUnknown;
                if (e.StatusCode == 0 || e.StatusCode == 401) Ready = false;
                Status = e.OutcomeUnknown ? "저장 확인 필요 · 다시 연결" : e.Message;
                return false;
            }
            catch (OperationCanceledException) when (disposed) { return false; }
            catch (Exception e) when (!(e is OutOfMemoryException))
            { Ready = false; ErrorCode = "recovery_unavailable"; Status = "기록을 확인하지 못했습니다 · 다시 연결"; return false; }
            finally { Busy = false; commandGate.Release(); }
        }
        async Task Refresh()
        {
            Profiles = (await client.GetProfilesAsync()).profiles;
            slots.BindServerProfiles(Profiles);
            Catalog = await client.GetCurrentCatalogAsync();
            if (Profile != null)
            {
                Profile = Profiles.FirstOrDefault(x => x.profileId == Profile.profileId);
                if (Profile != null)
                {
                    slots.ApplyServerProfile(Profile);
                    var active = (await client.GetActiveRunAsync(Profile.profileId)).run;
                    // A replayed settlement may have closed the active run. Keep its final receipt available.
                    Run = active ?? (Run != null && Run.profileId == Profile.profileId ? await client.GetRunAsync(Run.runId) : null);
                    if (Run != null) { if(Run.status=="active") Catalog = JsonConvert.DeserializeObject<ContentCatalog>((await client.GetPinnedCatalogAsync(Run)).json); else LastReward=Run.legacyReward; if(Catalog.economy != null) Economy = await client.GetEconomyAsync(Run.runId); }
                    else Economy = null;
                }
                else { Run = null; Economy = null; LastReward = 0; }
            }
            await DrainEconomy();
        }
        async Task Replay()
        {
            foreach (var operation in await client.GetPendingOperationsAsync())
            {
                try { await client.RetryOperationAsync<JObject>(operation.operationId); }
                catch (BackendClientException e) when (!e.CanRetry && !e.OutcomeUnknown)
                { /* Definitive rejection is recorded by the client. Refresh server state; never alter its payload. */ }
            }
        }
        public Task<bool> ConnectAsync() => Work("연결 중", async () => { await client.ConnectAsync(); await Replay(); await Refresh(); });
        public Task<bool> RetryAsync() => ConnectAsync();
        public Task<bool> SelectSlotAsync(int slot, string name = null) => Work("프로필 불러오는 중", async () =>
        {
            CancelReset();
            if (slot < 1 || slot > 3) throw new BackendClientException(400, "slot_invalid", "슬롯을 확인하세요.");
            await client.ConnectAsync(); await Replay(); await Refresh();
            Profile = Profiles.FirstOrDefault(p => p.slot == slot);
            if (Profile == null)
                Profile = await client.CreateProfileAsync(new CreateProfileRequest { slot = slot, name = string.IsNullOrWhiteSpace(name) ? "모험가 " + slot : name });
            var legacy = slots.ExistingLegacyPath(slot);
            if (legacy != null && Profile.revision == 0 && Profile.state.legacyMigrationVersion == 0 && !Profile.state.legacyImportBlocked)
            {
                Status = "기존 기록 옮기는 중";
                var backup = await client.BackupLegacyAsync(legacy);
                var source = JObject.Parse(backup.sourceJson);
                // Old single-file progression did not have a slot wrapper. Preserve original backup and hash separately.
                if (source["slotNumber"] == null)
                {
                    source["version"] = 1; source["slotNumber"] = slot;
                    source["createdUtc"] = ""; source["lastPlayedUtc"] = "";
                }
                var json = source.ToString(Formatting.None);
                var request = new LegacyImportRequest { expectedRevision = Profile.revision, sourceJson = json,
                    sourceSha256 = BackendPrivateStore.Hash(Encoding.UTF8.GetBytes(json)) };
                var receipt = await client.ImportLegacyAsync(Profile.profileId, request);
                Profile = receipt.profile;
                await client.WriteRecoveryAsync("migration:" + Profile.profileId, new { profileId = Profile.profileId, backup.backupId,
                    backup.originalSha256, receipt.sourceSha256, receipt.migrationVersion, receipt.refundedLegacy });
            }
            // A newer server profile always wins. The old JSON is retained, never merged over server progress.
            Profile = await client.GetProfileAsync(Profile.profileId); slots.ApplyServerProfile(Profile);
            Run = (await client.GetActiveRunAsync(Profile.profileId)).run;
            if (Run != null) { Catalog = JsonConvert.DeserializeObject<ContentCatalog>((await client.GetPinnedCatalogAsync(Run)).json); if(Catalog.economy != null) Economy = await client.GetEconomyAsync(Run.runId); }
            else { Economy = null; Catalog = await client.GetCurrentCatalogAsync(); }
        });
        async Task RequireNoPendingResetOperations(string allowedOperation = null)
        {
            if ((await client.GetPendingOperationsAsync()).Any(x => x.operationId != allowedOperation) || (await client.ReadEconomyQueueAsync()).Length != 0)
                throw new BackendClientException(409, "reset_pending_operations", "저장 확인을 마친 뒤 초기화하세요.");
        }
        public void CancelReset() { ResetChallenge = null; resetRequest = null; }
        public Task<bool> BeginResetAsync(int slot) => Work("초기화 내용 확인 중", async () =>
        {
            CancelReset();
            if (slot < 1 || slot > 3) throw new BackendClientException(400, "slot_invalid", "슬롯을 확인하세요.");
            await client.ConnectAsync(); await RequireNoPendingResetOperations();
            Profiles = (await client.GetProfilesAsync()).profiles; slots.BindServerProfiles(Profiles);
            var target = Profiles.FirstOrDefault(x => x.slot == slot);
            if (target == null) throw new BackendClientException(404, "profile_missing", "초기화할 서버 프로필이 없습니다.");
            ResetChallenge = await client.BeginProfileResetAsync(target.profileId, new BeginProfileResetRequest { expectedRevision = target.revision });
        });
        public Task<bool> ConfirmResetAsync(ProfileResetChallenge challenge) => Work("프로필 초기화 확인 중", async () =>
        {
            if (challenge == null || ResetChallenge == null || challenge.challengeId != ResetChallenge.challengeId || challenge.profileId != ResetChallenge.profileId)
                throw new BackendClientException(409, "reset_confirmation_required", "초기화 내용을 먼저 확인하세요.");
            if (resetRequest == null) resetRequest = new ConfirmProfileResetRequest { operationId = Guid.NewGuid().ToString(), expectedRevision = challenge.profileRevision, challengeId = challenge.challengeId };
            await RequireNoPendingResetOperations(resetRequest.operationId);
            var receipt = await client.ConfirmProfileResetAsync(challenge.profileId, resetRequest);
            // Read the current revision too: an old replay receipt must not replace newer progress from another device.
            var confirmed = await client.GetProfileAsync(receipt.profile.profileId);
            Profiles = (await client.GetProfilesAsync()).profiles; slots.BindServerProfiles(Profiles);
            if (Profile != null && Profile.profileId == confirmed.profileId)
            {
                Profile = confirmed; slots.ApplyServerProfile(Profile);
                Run = (await client.GetActiveRunAsync(Profile.profileId)).run;
                Economy = Run != null ? await client.GetEconomyAsync(Run.runId) : null;
                LastReward = 0;
            }
            CancelReset();
            if (!disposed) ProfileResetCommitted?.Invoke(receipt);
        });
        void NeedProfile()
        { if (Profile == null) throw new BackendClientException(409, "profile_required", "프로필을 먼저 선택하세요."); }
        public Task<bool> ConfigureAsync(string weapon, string[] talents) => Work("구성 저장 중", async () =>
        {
            NeedProfile(); Profile = await client.ConfigureAsync(Profile.profileId, new ConfigurationRequest {
                expectedRevision = Profile.revision, startingWeaponId = weapon, activeTalentIds = talents ?? Array.Empty<string>() });
            slots.ApplyServerProfile(Profile);
        });
        public Task<bool> UnlockAsync(string kind, string id) => Work("해금 저장 중", async () =>
        {
            NeedProfile(); Profile = await client.UnlockAsync(Profile.profileId, new UnlockRequest {
                expectedRevision = Profile.revision, kind = kind, contentId = id, contentVersion = Catalog.version });
            slots.ApplyServerProfile(Profile);
        });
        public Task<bool> StartAsync(int? seed, string generatorVersion, string buildId) => Work("원정 준비 중", async () =>
        {
            NeedProfile(); Run = await client.StartRunAsync(new StartRunRequest { profileId = Profile.profileId,
                expectedProfileRevision = Profile.revision, requestedSeed = seed, contentVersion = Catalog.version,
                generatorVersion = generatorVersion, buildId = buildId });
            Catalog = JsonConvert.DeserializeObject<ContentCatalog>((await client.GetPinnedCatalogAsync(Run)).json);
            if(Catalog.economy != null) Economy = await client.GetEconomyAsync(Run.runId);
            LastReward=0;
        });
        public Task<bool> SettleAsync(string outcome, int kills, int rooms, int stages) => Work("원정 정산 중", async () =>
        {
            NeedProfile();
            if (Run == null) throw new BackendClientException(409, "run_required", "정산할 원정을 확인하세요.");
            if (Run.status != "active") { LastReward = Run.legacyReward; return; }
            await DrainEconomy();
            var receipt = await client.SettleRunAsync(Run.runId, new SettleRunRequest { expectedRunRevision = Run.revision,
                outcome = outcome, enemiesDefeated = kills, roomsReached = rooms, stagesCleared = stages });
            Run = receipt.run; Profile = receipt.profile; LastReward = Run.legacyReward; slots.ApplyServerProfile(Profile);
            Catalog = await client.GetCurrentCatalogAsync();
        });
        public Task<bool> RefreshEconomyAsync() => Work("거래 확인 중", async () =>
        { if(Run == null) throw new BackendClientException(409,"run_required","원정을 확인하세요."); await DrainEconomy(); Economy = await client.GetEconomyAsync(Run.runId); });
        public Task<EconomyReceipt> TradeAsync(TradeRequest request) => EconomyCommand(request, null);
        public Task<EconomyReceipt> RewardAsync(RewardRequest request) => EconomyCommand(null, request);
        async Task<EconomyReceipt> EconomyCommand(TradeRequest trade, RewardRequest reward)
        {
            if (disposed || Run == null || Run.status != "active") { Status = "진행 중인 원정을 확인하세요."; return null; }
            if (trade != null && string.IsNullOrEmpty(trade.operationId)) trade.operationId = Guid.NewGuid().ToString();
            if (reward != null && string.IsNullOrEmpty(reward.operationId)) reward.operationId = Guid.NewGuid().ToString();
            var id = trade != null ? trade.operationId : reward.operationId;
            var runId = Run.runId; economyRequests++;
            await commandGate.WaitAsync();
            try
            {
                if (disposed || Run == null || Run.runId != runId || Run.status != "active") return null;
                var queued = await client.ReadEconomyCommandAsync(id);
                if (queued == null)
                {
                    queued = new QueuedEconomyOperation { apiOrigin = client.ApiOrigin, runId = runId, operationId = id,
                        createdUtc = DateTime.UtcNow.ToString("O"), trade = trade, reward = reward };
                    // Durable before authentication/network I/O; combat never waits synchronously for the API.
                    await client.SaveEconomyCommandAsync(queued);
                }
                else if (queued.runId != runId || (trade == null) != (queued.trade == null))
                    throw new BackendClientException(409,"operation_reused","거래 식별자를 다시 사용할 수 없습니다.");
                else
                {
                    var incoming=JObject.FromObject((object)trade??reward);
                    var existing=JObject.FromObject((object)queued.trade??queued.reward);
                    incoming.Remove("expectedRevision"); existing.Remove("expectedRevision");
                    if(!JToken.DeepEquals(incoming,existing)) throw new BackendClientException(409,"operation_reused","기존 거래 요청을 바꿀 수 없습니다.");
                }
                if(queued.status=="pending" && DateTime.UtcNow<nextEconomyRetryUtc) { HasPending=true; Status="보상·거래 확인 대기"; return null; }
                await DrainEconomy();
                queued = await client.ReadEconomyCommandAsync(id);
                if (queued.status != "confirmed") return null;
                HasPending = (await client.ReadEconomyQueueAsync()).Length > 0;
                ErrorCode = null; Status = HasPending ? "거래 확인 대기" : "저장됨";
                return queued.receipt;
            }
            catch (BackendClientException e) { ErrorCode=e.Code; HasPending=e.CanRetry; if(e.CanRetry)nextEconomyRetryUtc=DateTime.UtcNow.AddSeconds(3); Status=e.CanRetry?"보상·거래 확인 대기":e.Message; return null; }
            catch (Exception e) when (!(e is OutOfMemoryException)) { ErrorCode="recovery_unavailable"; HasPending=true; Status="거래 기록 확인 필요"; return null; }
            finally { economyRequests--; commandGate.Release(); }
        }
        async Task DrainEconomy()
        {
            foreach (var queued in await client.ReadEconomyQueueAsync())
            {
                if(queued.status!="confirmed-unprojected")try
                {
                    if (!queued.prepared)
                    {
                        if (queued.trade != null) queued.trade.expectedRevision = (await client.GetEconomyAsync(queued.runId)).revision;
                        queued.prepared=true; await client.SaveEconomyCommandAsync(queued);
                    }
                    queued.receipt = queued.trade != null ? await client.TradeAsync(queued.runId,queued.trade) : await client.ClaimRewardAsync(queued.runId,queued.reward);
                    // Server commit is durable before local presentation. An interrupted/failed
                    // projection reuses this receipt and never sends another economy mutation.
                    queued.status="confirmed-unprojected"; await client.SaveEconomyCommandAsync(queued);
                }
                catch (BackendClientException e) when (!e.CanRetry && !e.OutcomeUnknown)
                {
                    queued.status="rejected"; await client.SaveEconomyCommandAsync(queued);
                    ErrorCode=e.Code; Status=e.Message;
                    if (Run != null && Run.runId == queued.runId) Economy=await client.GetEconomyAsync(queued.runId);
                    continue;
                }
                if (Run != null && Run.runId == queued.runId) Economy=queued.receipt.economy;
                if(disposed)return;
                var project=EconomyCommitted;
                if(project==null)throw new BackendClientException(409,"projection_unavailable","확정된 거래를 가방에 반영할 준비를 기다리고 있습니다.",true);
                try { project(queued.receipt); }
                catch(Exception e) when (!(e is OutOfMemoryException))
                { throw new BackendClientException(409,"projection_failed","확정된 거래의 가방 반영을 다시 확인합니다.",true,false,e); }
                queued.status="confirmed"; await client.SaveEconomyCommandAsync(queued);
            }
        }
        public async Task TickRecoveryAsync()
        {
            if(disposed || recoveryRunning || Busy || EconomyBusy || !HasPending || DateTime.UtcNow<nextEconomyRetryUtc || !commandGate.Wait(0)) return;
            recoveryRunning=true;
            try
            {
                await Replay(); await Refresh();
                HasPending=(await client.GetPendingOperationsAsync()).Length>0 || (await client.ReadEconomyQueueAsync()).Length>0;
                Ready=true; ErrorCode=null;
                Status=HasPending?"저장 확인 대기":"저장됨";
            }
            catch(BackendClientException e) { HasPending=true; ErrorCode=e.Code; Status="보상·거래 확인 대기"; nextEconomyRetryUtc=DateTime.UtcNow.AddSeconds(3); }
            catch(Exception e) when (!(e is OutOfMemoryException)) { HasPending=true; Status="거래 기록 확인 필요"; nextEconomyRetryUtc=DateTime.UtcNow.AddSeconds(5); }
            finally { recoveryRunning=false; commandGate.Release(); }
        }
        public void Dispose()
        { if(disposed)return; disposed=true; EconomyCommitted=null; ProfileResetCommitted=null; CancelReset(); client.Dispose(); }
    }
}
