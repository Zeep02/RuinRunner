# RuinRunner API v1 — client handoff

Implemented root: `http://127.0.0.1:5186`. All payloads/responses are camelCase UTF-8 JSON. Protected endpoints require `Authorization: Bearer <accessToken>`; tokens/credentials must never appear in logs. DTO definitions in `Backend/RuinRunner.Api/Contracts.cs` are authoritative. No Unity files were modified by this backend work.

## Identity

`POST /v1/auth/guest`: `{installationId, guestSecret}`. `installationId` is a random UUID, `guestSecret` is 32 cryptographically random bytes encoded as base64url without `=` (43 characters). Persist both once in an OS-user-protected store; never generate a fresh identity just because a request times out. The response contains `playerId`, `accessToken`, `expiresUtc` (two hours). Repeating the same credential returns the same player with a new session. Knowing another installation UUID alone is insufficient. DB stores hashes only. Re-login with the stored credential when a token expires; the account is not recovered from a hardware ID.

The guest route has a per-IP rate limit. HTTPS is required for remote deployment; loopback HTTP is development only.

## Profiles and commands

Every mutating request requires a random `operationId` UUID generated before the request. The same ID and identical payload return the committed response even after reconnect/server restart. Reusing an ID with a different payload returns 409. The server derives ownership from the authenticated token, never from a payload's player ID. Other owners' IDs return 404.

| Method and route | Request fields | Result |
|---|---|---|
| GET `/v1/profiles` | None | `{profiles:[ProfileView]}` for this owner |
| POST `/v1/profiles` | operationId, slot (1..3), name (1..32 chars) | ProfileView; existing slot is 409 |
| GET `/v1/profiles/{id}` | None | ProfileView |
| GET `/v1/profiles/{id}/active-run` | None | `{run:RunView}` or `{run:null}`; owner-scoped recovery |
| PUT `/v1/profiles/{id}/configuration` | operationId, expectedRevision, startingWeaponId, activeTalentIds[] | Updated ProfileView; village-only |
| POST `/v1/profiles/{id}/unlocks` | operationId, expectedRevision, kind (`weapon`/`talent`), contentId, contentVersion | Updated ProfileView; price and unlock validation server-owned |
| POST `/v1/profiles/{id}/legacy-import` | operationId, expectedRevision, migrationVersion=1, sourceSha256, sourceJson | `{profile,refundedLegacy,migrationVersion,sourceSha256,trustOrigin}` |

ProfileView: `profileId, playerId, slot, name, revision, legacyShards, state`.
State: `totalRuns, victories, campaignVictories, bestCampaignStage, deepestRoom, totalKills, startingWeaponId, unlockedWeaponIds[], unlockedTalentIds[], activeTalentIds[], trustOrigin, legacyMigrationVersion`.

`expectedRevision` is a JSON integer field (not an If-Match header). Creation returns revision 0. Config/unlock/import/settlement advance it. Configuration permits at most one selected talent per branch and the catalog's overall cap. An active run locks village configuration.

Import source is exact UTF-8 JSON text from `SaveSlotData` version 1. Hash its original bytes, preserve the original backup, and send the unchanged source string. If an original file has a UTF-8 BOM, strip BOM only if hashing the same BOM-free bytes and preserve the untouched disk backup separately. The server recomputes the SHA-256 and stores the original JSON snapshot. Old base costs are frozen: numeric types 0/1 cost `3+2*rank`, types 2..6 cost `4+2*rank`; rank limits remain 5 except type6=3. Imported shard balance plus spent ranks is preserved as one refund, marked `legacy-local-untrusted`. Source receipts and the profile's import marker prevent repeated refunds. Existing progressed slots reject import.

## Expedition lifecycle

| Method and route | Request | Result |
|---|---|---|
| POST `/v1/runs` | operationId, profileId, expectedProfileRevision, requestedSeed (nullable), contentVersion, generatorVersion, buildId | RunView |
| GET `/v1/runs/{id}` | None | RunView |
| POST `/v1/runs/{id}/settle` | operationId, expectedRunRevision, outcome (`victory`/`defeat`/`abandoned`), enemiesDefeated, roomsReached, stagesCleared | `{run,profile}` |

RunView: `runId,profileId,revision,status,seed,contentVersion,generatorVersion,buildId,loadout,legacyReward,trustOrigin`.
`loadout` is the frozen ProfileState at departure. Only one active run per profile exists. Starting a run does not award anything. All settlement writes (result, counters, balance, ledger and replay receipt) use one DB transaction. A second settlement with a new operation UUID rejects rather than granting twice. An abandoned or zero-progress run gives zero legacy, preventing start/quit farming of the historical minimum. Completed victory requires all three stages; other result bounds come from the pinned catalog.

The server computes shard rewards from the catalog, but trusts bounded client-reported battle counters. `trustOrigin=client-reported-singleplayer` means this is not server-authoritative combat or an anti-cheat claim. Profile/player creation is not a public competitive account system.

## Content publication

GET `/v1/catalogs/current` and `/v1/catalogs/{version}` require authentication. Current default is `integration-v1`, explicitly an integration-only catalog containing the existing rust sword starter, dagger unlock price, settlement rules and allowed generator versions `legacy-fixed-layout-v1` / `modules-v1.0.0`. It does **not** claim to be the full new gameplay catalog. Parent integration must replace C#/SO balance authoring with a single published source and validated generated/cached Unity data.

`Backend/Content/*.json` is the publication source; stored DB content is an immutable exact-version snapshot with SHA-256. Editing a published file fails startup rather than silently changing an ongoing run. Add a new version and set `RR_CONTENT_VERSION` for future runs; existing runs settle against their frozen old version. API and Unity must reject unknown action/asset IDs before activating a catalog.

## Failure handling

Errors: `{code,message,traceId}`. 401 renews authentication; 404 is missing/not-owned; 409 signals revision, already-processed state, or operation reuse; 429 is retry delay; 503 is unavailable storage. A mutation receipt may exist even if its response was lost. Retry the original operation to find out. Do not fall back to a local success message. No credentials or SQL parameter values are included in error responses.

Use async `UnityWebRequest`/coroutine or Task adapters, no `.Wait()`/`.Result` on the Unity main thread. Networking is a boundary for commands/results, not a per-frame combat loop. Expose pending settlement and an explicit reconnect action with short Korean status text.
