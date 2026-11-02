# Backend audit — 2026-10-04

후속 구현·실제 campaign API/PostgreSQL 56개 검증 상태는 [BackendCampaign-20261004.md](BackendCampaign-20261004.md)에 기록한다. 이 파일은 변경 전 감사 자료로 보존하며, 아래 pending 목록을 현재 구현 상태로 해석하지 않는다.

This audit is based on the current C# implementation, before the new backend was connected to Unity. The objective at `C:/Users/Hyeon/.codex/attachments/86c694d9-cc77-4d1e-b95d-3de44c51966c/goal-objective.md` supersedes the prior solo-only objective.

## Existing implementation

- `Save/SaveSlotService.cs`: three device-local JSON slots, atomic temporary-file replacement, `.bak` recovery, isolated validation profiles. No account identity, ownership, API, DB, durable request receipt, run UUID or concurrency version exists. `SaveActive()` catches errors and returns no success result; this must not be used to report confirmed server persistence.
- `SaveSlotData.cs`: version 1, legacy shards, historical run/victory/kill/depth counters and numeric `MetaUpgradeType` ranks. No run continuation snapshot or permanent equipment collection is saved. Old `meta-progression.json` is imported into a fresh slot 1 without a durable import marker.
- `MetaProgressionService.cs`: client-owned shard balance; `TryUnlock()` deducts and saves a rank. `AwardRun()` computes shards and mutates counters. Duplicate result suppression is only an in-memory `GameController.metaRewardGranted` gate.
- `OwnedEquipment.cs`: process-local incrementing `long Identity`; good for current UI selection, insufficient as a persistent or network identity. Add a separate UUID/run-scoped stable instance ID while retaining the existing UI identity until callers migrate. Ammo/reload and per-copy state already travel together correctly.
- `RunRandom.cs`: integer seed and separate stream/index mixing already separate content rolls from visual randomness. Seed/generation/content/build versions are not saved to durable run records.
- Content is currently authored in C# (`WeaponLootTable`, `AccessoryLootTable`, `RunContent`) plus mutable `GameBalanceConfig` ScriptableObject overrides. Publishing a server catalog does not automatically remove this split source; that Unity extraction and runtime binding remain integration work.
- Device-local `GameSettingsService` is a useful separate boundary; resolution, audio and key bindings should remain local.
- `Packages/manifest.json` has multiplayer-center editor tooling, not an authenticated game service. No relevant HTTP/API/SQL runtime implementation or project-owned server was found. Unity's pipeline control server is editor tooling and must not be reused as a game backend.

## Environment and ownership

Initially the global dotnet host was 9.0.14 with desktop/core runtimes 6/7/8/9, but no .NET SDK or ASP.NET runtime. PostgreSQL/Docker/Podman executables, conventional installs and running services were not found. Global installations were not changed.

The backend implementation uses portable `.NET SDK 10.0.401` and `PostgreSQL 18.6` beneath `Backend/.local`. .NET 10 is the current LTS line according to the [official support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core); the exact SDK archive/hash comes from [Microsoft release metadata](https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json). PostgreSQL binaries are from EDB's link on the [PostgreSQL Windows download page](https://www.postgresql.org/download/windows/) and [EDB binary listing](https://www.enterprisedb.com/download-postgresql-binaries). Npgsql 10.0.3 is pinned; its asynchronous data source/parameter API follows [Npgsql documentation](https://www.npgsql.org/doc/basic-usage.html).

Backend agent owns `Backend/**`, `Tools/Backend/**`, `Docs/Backend*.md`. The main agent owns Unity save/UI/controller integration and content migration. The map agent owns generator output; agreed generator version is `modules-v1.0.0`.

## Data boundaries

| Authority | Data |
|---|---|
| PostgreSQL/API | Guest identity and sessions, owned profiles/slots, permanent state, unlocked/selected IDs, shard ledger, immutable catalog snapshots, pinned runs, exact operation receipts and legacy migration evidence |
| Versioned content source | Checked-in `Backend/Content/*.json`, published immutable per-version snapshots; full game catalog extraction is pending |
| Unity assets | Native images, animation, sound, scene, prefab and spatial modules, referenced by stable IDs |
| Local recovery only | Guest credential protected per OS user, original legacy backups, unresolved outbound commands with their original operation IDs, last confirmed read cache |
| Device settings | Resolution/audio/keys remain in `GameSettingsService` |
| Runtime simulation | Transform, AI, attacks, projectiles, per-frame animation and temporary inventory state; no per-frame DB writes |

## Required Unity integration

Replace direct `SaveSlotService` mutation with an async repository/command coordinator. Separate connecting, village, expedition, intro, results and pending-settlement states. Starting a profile must enter village rather than directly calling the current `BeginRunForActiveSlot()`.

Before outbound mutations, persist a recovery command with a random operation UUID and its immutable payload. Keep the same UUID when retrying after timeout/response loss, even after authentication renewal. Remove it only after receiving the matching server receipt. Do not merge a stale cached profile over a newer revision. A 409 requires fetching current state and explicit command reconciliation; do not blindly alter the operation payload and retry. Purchases/configuration wait for confirmation; a failed HTTP call must not stop an already running battle. Results may remain pending while the run state survives locally.

Copy each actual legacy save and recovery backup before import, hash exact UTF-8 source bytes, record migration receipts per stable player/profile, and preserve original enum numeric values. The server converts the frozen old rank costs to one refund and labels imported progress untrusted. It accepts only a pristine server slot; no import overwrites progressed server state. Existing player save files were not opened or modified during backend tests.

Full catalog replacement, server-validated merchant stock/gold/trades, run-owned equipment snapshots, field transformation/awakening records, talents' gameplay implementation and Unity retry UX remain separate integration milestones. The first backend stage must not be reported as all of objective I complete.
