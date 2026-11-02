using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace RuinRunner.Backend
{
    public enum BackendConnectionState { Disconnected, Connecting, Connected }

    /// <summary>Async command boundary. Construct and call on Unity's main thread; dispose on owner destruction.</summary>
    public sealed class RuinBackendClient : IDisposable
    {
        readonly Uri baseUri;
        readonly string origin;
        readonly int timeout;
        readonly int mainThread;
        readonly SynchronizationContext context;
        readonly BackendPrivateStore store;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly SemaphoreSlim authenticationGate = new SemaphoreSlim(1, 1);
        readonly SemaphoreSlim mutationGate = new SemaphoreSlim(1, 1);
        GuestSession session;
        bool disposed;
        public BackendConnectionState ConnectionState { get; private set; }
        public string PlayerId => session?.playerId;
        public string ApiOrigin => origin;
        public event Action<BackendConnectionState> ConnectionChanged;
        public RuinBackendClient(BackendClientOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            context = SynchronizationContext.Current ?? throw new InvalidOperationException("Construct the backend client on the Unity main thread.");
            mainThread = Thread.CurrentThread.ManagedThreadId;
            baseUri = new Uri(options.ApiBaseUri.TrimEnd('/') + "/", UriKind.Absolute);
            if (baseUri.UserInfo.Length != 0 || baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0 || (baseUri.Scheme != "https" && !(baseUri.Scheme == "http" && baseUri.IsLoopback)))
                throw new ArgumentException("Use HTTPS for remote API addresses; credentials must not be placed in URLs.");
            origin = baseUri.AbsoluteUri; timeout = Math.Max(2, Math.Min(60, options.TimeoutSeconds));
            store = new BackendPrivateStore(options.StorageDirectory);
        }
        void AssertMainThread()
        {
            if (disposed) throw new ObjectDisposedException(nameof(RuinBackendClient));
            if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new InvalidOperationException("Call the Unity backend facade on the main thread.");
        }
        static void Operation(ref string id)
        { if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString(); else Id(id); }
        static string Id(string id)
        { if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty) throw new ArgumentException("A non-empty UUID is required."); return parsed.ToString(); }
        void State(BackendConnectionState state)
        { if (ConnectionState == state) return; ConnectionState = state; ConnectionChanged?.Invoke(state); }
        static async Task<T> Disk<T>(Func<T> action)
        {
            try { return await Task.Run(action); }
            catch (BackendClientException) { throw; }
            catch (Exception e) when (!(e is OperationCanceledException))
            { throw new BackendClientException(0, "local_recovery_unavailable", "복구 기록을 읽거나 저장하지 못했습니다."); }
        }
        public async Task ConnectAsync(CancellationToken cancellation = default)
        { AssertMainThread(); await Authenticate(false, cancellation); }
        async Task Authenticate(bool force, CancellationToken cancellation)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation))
            {
                await authenticationGate.WaitAsync(linked.Token);
                try
                {
                    if (!force && session != null && DateTimeOffset.TryParse(session.expiresUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires) && expires > DateTimeOffset.UtcNow.AddSeconds(30)) return;
                    State(BackendConnectionState.Connecting);
                    var identity = await Disk(() => store.GetIdentity(origin));
                    var raw = await Send("POST", "v1/auth/guest", JsonConvert.SerializeObject(identity), null, linked.Token);
                    if (raw.status < 200 || raw.status >= 300) throw Failure(raw, false);
                    session = Deserialize<GuestSession>(raw.json);
                    if (string.IsNullOrEmpty(session.playerId) || string.IsNullOrEmpty(session.accessToken)) throw new BackendClientException(0, "invalid_session", "인증 응답을 확인하지 못했습니다.", true);
                    await Disk(() => { store.Write("bindings", origin, new PlayerBinding { playerId = session.playerId, apiOrigin = origin }); return true; });
                    State(BackendConnectionState.Connected);
                }
                catch { State(BackendConnectionState.Disconnected); throw; }
                finally { authenticationGate.Release(); }
            }
        }
        async Task<Raw> Authorized(string method, string path, string payload, CancellationToken cancellation)
        {
            await Authenticate(false, cancellation);
            var usedToken = session.accessToken;
            var raw = await Send(method, path, payload, usedToken, cancellation);
            if (raw.status == 401)
            {
                // If another request already renewed, reuse that session. Commands keep their original payload and UUID.
                if (session.accessToken == usedToken) await Authenticate(true, cancellation);
                raw = await Send(method, path, payload, session.accessToken, cancellation);
            }
            return raw;
        }
        async Task<T> Read<T>(string path, CancellationToken cancellation)
        {
            AssertMainThread(); var raw = await Authorized("GET", path, null, cancellation);
            if (raw.status < 200 || raw.status >= 300) throw Failure(raw, false);
            return Deserialize<T>(raw.json);
        }
        public Task<ProfileList> GetProfilesAsync(CancellationToken cancellation = default) => Read<ProfileList>("v1/profiles", cancellation);
        public async Task<ProfileView> GetProfileAsync(string profileId, CancellationToken cancellation = default)
        { var result = await Read<ProfileView>("v1/profiles/" + Id(profileId), cancellation); await CacheProfile(result); return result; }
        public async Task<RunView> GetRunAsync(string runId, CancellationToken cancellation = default)
        { var run = await Read<RunView>("v1/runs/" + Id(runId), cancellation); await CacheRun(run); return run; }
        public async Task<ActiveRunResponse> GetActiveRunAsync(string profileId, CancellationToken cancellation = default)
        { var response = await Read<ActiveRunResponse>("v1/profiles/" + Id(profileId) + "/active-run", cancellation); if (response.run != null) await CacheRun(response.run); return response; }
        public Task<ContentCatalog> GetCurrentCatalogAsync(CancellationToken cancellation = default) => Read<ContentCatalog>("v1/catalogs/current", cancellation);
        public Task<EconomyView> GetEconomyAsync(string runId, CancellationToken cancellation = default) => Read<EconomyView>("v1/runs/" + Id(runId) + "/economy", cancellation);
        public Task<EconomyReceipt> TradeAsync(string runId, TradeRequest request, CancellationToken cancellation = default)
        { Operation(ref request.operationId); return Mutate<EconomyReceipt>(request.operationId, "POST", "v1/runs/" + Id(runId) + "/trades", request, cancellation); }
        public Task<EconomyReceipt> ClaimRewardAsync(string runId, RewardRequest request, CancellationToken cancellation = default)
        { Operation(ref request.operationId); return Mutate<EconomyReceipt>(request.operationId, "POST", "v1/runs/" + Id(runId) + "/rewards", request, cancellation); }
        Task<bool> CacheProfile(ProfileView profile) => Disk(() => { store.SaveProfile(profile, origin); return true; });
        Task<bool> CacheRun(RunView run) => Disk(() =>
        {
            var key = origin + ":" + run.runId;
            var previous = store.Read<RunView>("runs", key);
            if (previous == null || previous.revision <= run.revision) store.Write("runs", key, run);
            return true;
        });
        public async Task<ProfileView> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellation = default)
        {
            Operation(ref request.operationId); var result = await Mutate<ProfileView>(request.operationId, "POST", "v1/profiles", request, cancellation); await CacheProfile(result); return result;
        }
        public async Task<ProfileView> ConfigureAsync(string profileId, ConfigurationRequest request, CancellationToken cancellation = default)
        {
            Operation(ref request.operationId); var result = await Mutate<ProfileView>(request.operationId, "PUT", "v1/profiles/" + Id(profileId) + "/configuration", request, cancellation); await CacheProfile(result); return result;
        }
        public Task<ProfileResetChallenge> BeginProfileResetAsync(string profileId, BeginProfileResetRequest request, CancellationToken cancellation = default)
        { Operation(ref request.operationId); return Mutate<ProfileResetChallenge>(request.operationId, "POST", "v1/profiles/" + Id(profileId) + "/reset-challenge", request, cancellation); }
        public async Task<ProfileResetReceipt> ConfirmProfileResetAsync(string profileId, ConfirmProfileResetRequest request, CancellationToken cancellation = default)
        {
            Operation(ref request.operationId); Id(request.challengeId);
            var result = await Mutate<ProfileResetReceipt>(request.operationId, "POST", "v1/profiles/" + Id(profileId) + "/reset", request, cancellation);
            await CacheProfile(result.profile); return result;
        }
        public async Task<ProfileView> UnlockAsync(string profileId, UnlockRequest request, CancellationToken cancellation = default)
        {
            Operation(ref request.operationId); var result = await Mutate<ProfileView>(request.operationId, "POST", "v1/profiles/" + Id(profileId) + "/unlocks", request, cancellation); await CacheProfile(result); return result;
        }
        public async Task<ImportReceipt> ImportLegacyAsync(string profileId, LegacyImportRequest request, CancellationToken cancellation = default)
        {
            Operation(ref request.operationId); var result = await Mutate<ImportReceipt>(request.operationId, "POST", "v1/profiles/" + Id(profileId) + "/legacy-import", request, cancellation); await CacheProfile(result.profile); return result;
        }
        public async Task<RunView> StartRunAsync(StartRunRequest request, CancellationToken cancellation = default)
        { Operation(ref request.operationId); var run = await Mutate<RunView>(request.operationId, "POST", "v1/runs", request, cancellation); await CacheRun(run); return run; }
        public async Task<SettlementReceipt> SettleRunAsync(string runId, SettleRunRequest request, CancellationToken cancellation = default)
        {
            Operation(ref request.operationId); var result = await Mutate<SettlementReceipt>(request.operationId, "POST", "v1/runs/" + Id(runId) + "/settle", request, cancellation); await CacheProfile(result.profile); await CacheRun(result.run); return result;
        }
        async Task<T> Mutate<T>(string operationId, string method, string path, object request, CancellationToken cancellation)
        {
            AssertMainThread(); await Authenticate(false, cancellation);
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation))
            {
                await mutationGate.WaitAsync(linked.Token);
                try
                {
                    string payload = JsonConvert.SerializeObject(request);
                    var command = await Disk(() => store.Read<PendingOperation>("operations", operationId));
                    if (command != null)
                    {
                        if (command.playerId != session.playerId || command.apiOrigin != origin || command.method != method || command.path != path || command.payload != payload)
                            throw new BackendClientException(409, "operation_reused", "기존 처리 식별자의 내용을 바꿀 수 없습니다.");
                    }
                    else
                    {
                        command = new PendingOperation { operationId = operationId, playerId = session.playerId, apiOrigin = origin, method = method, path = path, payload = payload, createdUtc = DateTime.UtcNow.ToString("O") };
                        await Disk(() => { store.Write("operations", operationId, command); return true; });
                    }
                    return Deserialize<T>(await Execute(command, linked.Token));
                }
                finally { mutationGate.Release(); }
            }
        }
        async Task<string> Execute(PendingOperation command, CancellationToken cancellation)
        {
            if (command.status == "confirmed") return command.responseJson;
            if (command.status == "rejected") throw new BackendClientException(command.lastStatusCode, command.lastErrorCode, "저장하지 못한 변경입니다. 최신 상태를 확인하세요.");
            Raw raw;
            try { raw = await Authorized(command.method, command.path, command.payload, cancellation); }
            catch (OperationCanceledException) { throw; } // The already durable command remains pending.
            catch (BackendClientException e)
            {
                command.lastErrorCode = e.Code; command.lastStatusCode = e.StatusCode; await Disk(() => { store.Write("operations", command.operationId, command); return true; });
                throw new BackendClientException(e.StatusCode, e.Code, e.Message, true, true);
            }
            if (raw.status >= 200 && raw.status < 300)
            {
                try { Newtonsoft.Json.Linq.JObject.Parse(raw.json); }
                catch (JsonException)
                {
                    command.lastErrorCode = "invalid_response";
                    await Disk(() => { store.Write("operations", command.operationId, command); return true; });
                    throw new BackendClientException(raw.status, "invalid_response", "저장 응답을 확인하지 못했습니다. 같은 요청으로 다시 확인합니다.", true, true);
                }
                command.status = "confirmed"; command.responseJson = raw.json; command.lastErrorCode = null;
                await Disk(() => { store.Write("operations", command.operationId, command); return true; });
                return raw.json;
            }
            var error = Failure(raw, true); command.lastErrorCode = error.Code; command.lastStatusCode = raw.status;
            if (raw.status >= 400 && raw.status < 500 && raw.status != 401 && raw.status != 408 && raw.status != 429) command.status = "rejected";
            await Disk(() => { store.Write("operations", command.operationId, command); return true; }); throw error;
        }
        public async Task<PendingOperation[]> GetPendingOperationsAsync(CancellationToken cancellation = default)
        { AssertMainThread(); await Authenticate(false, cancellation); return await Disk(() => store.Pending(session.playerId, origin)); }
        public Task<PendingOperation[]> GetPendingOperationsOfflineAsync()
        {
            AssertMainThread(); return Disk(() => { var binding = store.Read<PlayerBinding>("bindings", origin); return binding == null ? Array.Empty<PendingOperation>() : store.Pending(binding.playerId, origin); });
        }
        public async Task<T> RetryOperationAsync<T>(string operationId, CancellationToken cancellation = default)
        {
            AssertMainThread(); Id(operationId); await Authenticate(false, cancellation);
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation))
            {
                await mutationGate.WaitAsync(linked.Token);
                try
                {
                    var command = await Disk(() => store.Read<PendingOperation>("operations", operationId));
                    if (command == null || command.playerId != session.playerId || command.apiOrigin != origin) throw new BackendClientException(404, "operation_missing", "복구할 요청을 찾을 수 없습니다.");
                    var value = Deserialize<T>(await Execute(command, linked.Token));
                    if (value is ProfileView profile) await CacheProfile(profile);
                    else if (value is ImportReceipt import) await CacheProfile(import.profile);
                    else if (value is ProfileResetReceipt reset) await CacheProfile(reset.profile);
                    else if (value is SettlementReceipt receipt) { await CacheProfile(receipt.profile); await CacheRun(receipt.run); }
                    else if (value is RunView run) await CacheRun(run);
                    return value;
                }
                finally { mutationGate.Release(); }
            }
        }
        public Task<LegacyBackup> BackupLegacyAsync(string sourcePath)
        { AssertMainThread(); return Disk(() => store.BackupLegacy(sourcePath)); }
        internal Task<T> ReadRecoveryAsync<T>(string key) where T : class
        { AssertMainThread(); return Disk(() => store.Read<T>("normal-flow", origin + ":" + key)); }
        internal Task<bool> WriteRecoveryAsync<T>(string key, T value)
        { AssertMainThread(); return Disk(() => { store.Write("normal-flow", origin + ":" + key, value); return true; }); }
        internal Task<QueuedEconomyOperation[]> ReadEconomyQueueAsync()
        { AssertMainThread(); return Disk(() => store.EconomyQueue(origin)); }
        internal Task<QueuedEconomyOperation> ReadEconomyCommandAsync(string id)
        { AssertMainThread(); return Disk(() => store.Read<QueuedEconomyOperation>("economy-queue", origin + ":" + id)); }
        internal Task<bool> SaveEconomyCommandAsync(QueuedEconomyOperation command)
        { AssertMainThread(); return Disk(() => { store.Write("economy-queue", origin + ":" + command.operationId, command); return true; }); }
        public Task<bool> RestoreLegacyBackupToNewFileAsync(LegacyBackup backup, string destination)
        { AssertMainThread(); return Disk(() => { store.RestoreLegacyToNewFile(backup, destination); return true; }); }
        public Task<ProfileView> ReadLastConfirmedProfileAsync(string profileId)
        { AssertMainThread(); return Disk(() => store.Read<ProfileView>("profiles", origin + ":" + Id(profileId))); }
        public Task<RunView> ReadConfirmedRunAsync(string runId)
        { AssertMainThread(); return Disk(() => store.Read<RunView>("runs", origin + ":" + Id(runId))); }
        public async Task<CatalogSnapshot> GetPinnedCatalogAsync(RunView run, CancellationToken cancellation = default)
        {
            AssertMainThread(); if (run == null || string.IsNullOrEmpty(run.contentVersion)) throw new ArgumentException("A server-created run with a pinned content version is required.");
            var key = origin + ":" + run.contentVersion;
            var cached = await Disk(() => store.Read<CatalogSnapshot>("catalogs", key));
            if (cached != null)
            {
                if (cached.apiOrigin != origin || cached.version != run.contentVersion || cached.sha256 != BackendPrivateStore.Hash(Encoding.UTF8.GetBytes(cached.json)))
                    throw new BackendClientException(0, "catalog_cache_invalid", "콘텐츠 캐시를 확인하지 못했습니다.");
                var stored = Deserialize<ContentCatalog>(cached.json);
                if (stored.version != run.contentVersion || stored.generatorVersions == null || Array.IndexOf(stored.generatorVersions, run.generatorVersion) < 0)
                    throw new BackendClientException(409, "catalog_version_mismatch", "원정의 콘텐츠 버전이 일치하지 않습니다.");
                return cached; // A known immutable version can continue an already active run during a network outage.
            }
            var raw = await Authorized("GET", "v1/catalogs/" + Uri.EscapeDataString(run.contentVersion), null, cancellation);
            if (raw.status < 200 || raw.status >= 300) throw Failure(raw, false);
            var content = Deserialize<ContentCatalog>(raw.json);
            if (content.version != run.contentVersion || content.generatorVersions == null || Array.IndexOf(content.generatorVersions, run.generatorVersion) < 0)
                throw new BackendClientException(409, "catalog_version_mismatch", "원정의 콘텐츠 버전이 일치하지 않습니다.");
            var snapshot = new CatalogSnapshot { apiOrigin = origin, version = content.version, json = raw.json, sha256 = BackendPrivateStore.Hash(Encoding.UTF8.GetBytes(raw.json)) };
            await Disk(() => { store.Write("catalogs", key, snapshot); return true; }); return snapshot;
        }
        static T Deserialize<T>(string json)
        {
            try { var value = JsonConvert.DeserializeObject<T>(json); if (value == null) throw new JsonException(); return value; }
            catch (JsonException) { throw new BackendClientException(0, "invalid_response", "서버 응답을 확인하지 못했습니다.", true, true); }
        }
        sealed class Raw { public int status; public string json; }
        static BackendClientException Failure(Raw raw, bool mutation)
        {
            BackendErrorBody error = null; try { error = JsonConvert.DeserializeObject<BackendErrorBody>(raw.json); } catch (JsonException) { }
            bool retry = raw.status == 0 || raw.status == 401 || raw.status == 408 || raw.status == 429 || raw.status >= 500;
            return new BackendClientException(raw.status, error?.code ?? "connection_failed", error?.message ?? "연결 상태를 확인하세요.", retry, mutation && retry);
        }
        async Task<Raw> Send(string method, string path, string payload, string token, CancellationToken cancellation)
        {
            AssertMainThread(); cancellation.ThrowIfCancellationRequested();
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation))
            using (var request = new UnityWebRequest(new Uri(baseUri, path), method))
            {
                request.timeout = timeout; request.redirectLimit = 0; request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Accept", "application/json");
                if (payload != null) { request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)); request.SetRequestHeader("Content-Type", "application/json; charset=utf-8"); }
                if (token != null) request.SetRequestHeader("Authorization", "Bearer " + token);
                var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = request.SendWebRequest(); operation.completed += _ => done.TrySetResult(true); if (operation.isDone) done.TrySetResult(true);
                using (linked.Token.Register(() => context.Post(_ => { if (!done.Task.IsCompleted) { request.Abort(); done.TrySetCanceled(); } }, null)))
                    await done.Task;
                linked.Token.ThrowIfCancellationRequested();
                if (request.result == UnityWebRequest.Result.ConnectionError || request.responseCode == 0)
                { State(BackendConnectionState.Disconnected); throw new BackendClientException(0, "transport_unavailable", "연결이 끊겼습니다. 다시 연결해 저장을 확인하세요.", true, method != "GET"); }
                return new Raw { status = (int)request.responseCode, json = request.downloadHandler.text };
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; lifetime.Cancel();
            session = null; ConnectionChanged = null;
            // In-flight awaits release their semaphores before the objects become collectible.
        }
    }
}
