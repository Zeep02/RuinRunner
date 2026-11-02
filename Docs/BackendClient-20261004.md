# Unity backend client boundary

현재 진행 상태와 새 정상 흐름/경제 계약은 [BackendCampaign-20261004.md](BackendCampaign-20261004.md)를 따른다. 아래 내용과 21개 Unity 검증 기록은 `integration-v1` 단계의 역사적 경계 검증이다. 이후 BackendFlowCoordinator, SaveSlotService 서버 투영, GameController.Backend normal flow 및 campaign-v1을 추가했으며, 과거의 '정상 흐름 미연결' 문장은 그 당시 상태를 뜻한다.

Production code is confined to the new `Assets/RuinRunner/Runtime/Backend` directory. Existing controller, save, UI, inventory, animation and world files were not edited. This is a working transport/recovery boundary; binding it to the normal main-menu/village/expedition flow remains the main agent's integration task.

Construct `RuinBackendClient` on Unity's main thread and await its methods. It captures Unity's synchronization context for UnityWebRequest creation/abort/completion. Network requests use async completions and cancellation, not blocking waits. DPAPI, JSON recovery-file IO and backups run on workers. Dispose it with the owning lifecycle; already persisted commands survive cancellation. Do not create a second facade per button click.

```csharp
var client = new RuinRunner.Backend.RuinBackendClient(
    new RuinRunner.Backend.BackendClientOptions {
        ApiBaseUri = "http://127.0.0.1:5186/",
        StorageDirectory = System.IO.Path.Combine(
            UnityEngine.Application.persistentDataPath, "RuinRunner", "Backend")
    });
await client.ConnectAsync();
var profiles = await client.GetProfilesAsync();
```

Production runtime address is an injected public option; only loopback HTTP is accepted. Remote URLs must use HTTPS, cannot include credentials/query/fragment, and requests do not follow redirects. Do not ship a DB password or migration secret with the Unity application. The existing Newtonsoft package is reused; no package or project setting was changed.

## Persistence and failure semantics

- A random guest credential is protected with Windows CurrentUser DPAPI, scoped to the normalized API origin. Concurrent first launches use a single winning credential. Corrupt/missing-access credentials fail explicitly rather than silently replacing the account. Access tokens stay in memory. HTTP 401 renews via the same protected credential and retries the identical request once.
- Mutation methods assign a random operation UUID to an empty request field before sending. The exact method, path, payload, player and origin are encrypted and atomically written before HTTP dispatch. Timeout, cancellation, 429, 503 or a malformed success response leaves the command pending. A valid success is durably marked confirmed before returning. A definite 4xx conflict is recorded as rejected.
- `GetPendingOperationsOfflineAsync()` is available without a network call. `RetryOperationAsync<T>(operationId)` replays the stored payload and UUID; never reconstruct a pending mutation from current inventory/UI values. The DTO type must match its command. `GetPendingOperationsAsync()` additionally authenticates to the owner.
- A confirmed operation replay returns its original server receipt. `ReadLastConfirmedProfileAsync()` keeps the greatest confirmed revision, so replaying an old create/config command cannot overwrite a newer local read cache. `ReadConfirmedRunAsync(runId)` is scoped to that run and cannot replace a different run. Cached values are recovery/read context, never a claimed successful new server save.
- `GetActiveRunAsync(profileId)` asks the owner-scoped API for `{run}` and recovers the run ID after process loss. A null run means no active expedition; do not generate a replacement run merely because a request failed.
- `GetPinnedCatalogAsync(run)` stores exact raw JSON/hash by version and origin and checks the generation version. An already cached immutable version is usable during an active-run outage. New/unknown versions require the API. Main-agent integration must validate the catalog's asset/action IDs against this build before applying it; the current `integration-v1` catalog is explicitly not the full gameplay catalog.
- `BackupLegacyAsync(path)` reads only the selected file and optional `.bak`, preserves exact original bytes in DPAPI backups, records SHA-256, and hashes BOM-free UTF-8 text for the import request. `RestoreLegacyBackupToNewFileAsync()` restores exact original bytes including BOM into a new file only. It refuses to overwrite the user's save. No actual player save was read during tests.

Use `BackendClientException.Code`, `StatusCode`, `CanRetry` and `OutcomeUnknown` to drive short status messages. A 409 needs fresh state/reconciliation; never silently alter an existing command and resend its operation UUID. No local-success fallback is provided. The facade does not stop or pause gameplay on an HTTP failure.

## Independent and Unity verification

The actual production client plus Unity fixture compiles under C# 9/.NET Standard 2.1 against the installed Unity 6000.6 DLLs, with zero warnings/errors. `Tools/Validation/BackendClientStoreTests` executes real Windows DPAPI/disk tests; reports are under `Backend/Validation/client-store-*.json`. These are not a Unity-player transport claim.

The parent enabled the missing `com.unity.modules.unitywebrequest` built-in module through Unity's package API, confirmed actual Unity compilation, and ran the fixture in the Editor. `Backend/Validation/unity-client-20261004-115532-43df6e.json` records **21 checks PASS** using actual UnityWebRequest, protected local storage, the response-loss proxy and PostgreSQL. In particular, committed create and settlement responses were deliberately lost; reconstructing the client preserved the same guest and original operation UUIDs, and retry granted each result once. Real HTTP 401 renewal, legacy backup/import/refund, stale revision errors, pinned catalog reuse before authentication and newer cache preservation also passed. This is a connected Editor fixture, not a normal-game or Windows-player completion claim; its report explicitly records `gameplayIntegration:false`.

After that successful check, the owned proxy PID 28020 was stopped using `BackendClientProxy.ps1 -Stop`; the PID file was removed and the process was verified absent. The API/PostgreSQL remain running and `/health/ready` returned `ready`. A new fixture run requires starting a fresh proxy.

The parent controls all Unity operations. To test real UnityWebRequest and deliberately lost responses:

1. Keep API/PostgreSQL running with `Tools/Backend/Start-Backend.ps1`.
2. Start fresh `Tools/Validation/BackendClientProxy.ps1`. It binds only 127.0.0.1:5187, forwards to the API, and drops the first successful create and settlement response after upstream commit. It never logs credentials or payloads.
3. Run `Tools/Validation/BackendClientIntegration20261004.cs`, entry `BackendClientIntegration20261004.Main`, through the parent's Unity run_script command. It starts asynchronously and returns a report path. Wait for `Backend/Validation/unity-client-*.json` and inspect Console.
4. Read the PASS/FAIL report, not just the immediate start message. Tests use newly generated guest credentials and a unique private storage directory, never user slots. They check process-style client reconstruction, lost response replay, real 401 renewal, import/refund, stale revision, catalog pinning and one-time settlement.
5. Stop only this proxy using `Tools/Validation/BackendClientProxy.ps1 -Stop`. Restart it before a new fixture; its loss triggers are single use. Keep the API/DB for parent integration, or stop their exact owned processes with `Tools/Backend/Stop-Backend.ps1` when no Unity check uses them.

The pre-import draft folder is a preserved snapshot, not a second source. Compilation and store tests now reference the actual Assets source. No Windows player/normal-game API binding or full-game database completion is asserted by these independent checks.
