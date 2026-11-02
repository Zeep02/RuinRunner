# Backend implementation verification

Status: real ASP.NET Core/PostgreSQL and a implemented Unity transport/recovery facade. Normal game menu/village/run binding, complete catalog/economy publication and final Windows player validation are not complete. No existing gameplay or save file was modified by this backend subtask.

API currently uses .NET 10.0.401 SDK / Npgsql 10.0.3 / PostgreSQL 18.6. Release compilation completed with zero errors and warnings. Current `Backend/RuinRunner.Api/bin/Release/net10.0/RuinRunner.Api.dll` SHA-256 is `D739D6032635DBBA663BF715EE3C9D80B295B7DCDC0F8E5866C0C45B3FDA892A` (the owned active-run lookup was added after the initial persistence test).

Actual checks:

| Evidence | What it establishes |
|---|---|
| `Backend/Validation/create-20261004-114830.json` | Real HTTP guest identity separation, owner-scoped profiles and runs, SHA-checked legacy import and one refund, server-priced unlock, eight simultaneous retries of the same purchase and settlement, stale revision rejection, pinned run configuration and preserved historical counters; 29 assertions |
| `Backend/Validation/negative-20261004-115421.json` | Invalid/expired bearer refusal and credential renewal, insufficient-funds rollback, distinct concurrent commands competing on one revision, malformed legacy rank rollback, missing content-version rejection, zero abandon reward; 8 assertions against current API assembly |
| `Backend/Validation/outage-20261004-113005.json` | PostgreSQL deliberately stopped while API remained live; HTTP 503 surfaced rather than a false local-save success |
| `Backend/Validation/restart-20261004-113007.json` | After actual API and PostgreSQL shutdown/restart: same identity, profile, run, and exact original settlement receipt restored; this preceded adding only the active-run read endpoint |
| `Backend/Validation/client-store-20261004-114738.json` | Actual Windows CurrentUser DPAPI identity reuse, no plaintext identity/credential bytes, encrypted pending/confirmed journal reconstruction and owner/origin isolation, stale cache protection, exact BOM-preserving original backup/restore, and overwrite refusal; 10 assertions |
| `Backend/Validation/unity-client-20261004-115532-43df6e.json` | Parent-run actual UnityWebRequest + DPAPI + PostgreSQL fixture: deliberately lost committed create/settlement responses, durable UUID replay after client reconstruction, real 401 renewal, backup/import/refund, stale revision rejection, pinned catalog and newer cache preservation; 21 assertions PASS, `gameplayIntegration:false` |
| `Backend/Validation/restore-20261004.json` | `pg_dump` restored into a new `rr_restore_20261004_1128` database without touching active DB: two profiles, 63 legacy shards, one victory, one legacy import and one applied migration match the backup point |
| `Backend/Validation/secret-scan-20261004.json` | Actual random development DB credentials were searched for in the backend source/compiled artifacts and new Unity source; 25 files, zero matches. This is scoped evidence, not a scan of a future Unity build |

The server's tests are black-box HTTP plus a deliberately scoped administrative expiry injection for generated test accounts. They do not assert server-authoritative combat, cheating prevention, realtime multiplayer or gameplay balance.

Unity production source and its fixture compile through the installed Unity DLLs under C# 9/.NET Standard 2.1 with zero errors/warnings. The parent separately enabled the missing `com.unity.modules.unitywebrequest` through the Unity package API, confirmed actual Unity compilation and executed the fixture. The actual report above confirms 21 checks PASS. This does not establish normal menu/village/run binding or a connected Windows executable.

Preserved failed development evidence: the initial PowerShell PostgreSQL startup waited for a descendant process tree; only the exact stalled startup shell was stopped, the database was retained, and startup now waits only for the pg_ctl process. One store-test reference compilation reported unassigned internal DTO fields; explicit defaults fixed it and the tests passed. No data reset was used.

After the parent confirmed the 21-pass Unity result, `Tools/Validation/BackendClientProxy.ps1 -Stop` stopped only the owned proxy PID 28020; process absence and PID-file removal were verified. API/PostgreSQL remain running for the parent, with `/health/ready` returning `ready`. `Tools/Backend/Stop-Backend.ps1` stops only this backend's verified API/cluster when no parent check uses them. See `BackendRunbook-20261004.md` for backup/restore and `BackendClient-20261004.md` for the exact fixture sequence and isolated credential directories.

Remaining scope is explicit: normal Unity flow binding, full single-source catalog generation/caching, merchant gold/stock and sale validation, owned equipment/field transformation transactions, talent gameplay, persistent run recovery policy, current Windows executable integration, and remote deployment execution. The integration catalog is deliberately labeled `integration-v1`; it is not a finished substitute for those systems.
