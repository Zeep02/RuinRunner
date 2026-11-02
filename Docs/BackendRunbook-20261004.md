# Local API/PostgreSQL operation

All commands run from `F:/codex/LoglikeCap` using PowerShell. They operate only on the backend's portable installation and private data. No Unity/Windows build is started by these scripts.

```powershell
./Tools/Backend/Install-BackendTools.ps1
./Tools/Backend/Start-Backend.ps1
./Tools/Backend/Stop-Backend.ps1 -ApiOnly
./Tools/Backend/Start-Backend.ps1
./Tools/Backend/Backup-Backend.ps1
./Tools/Backend/Stop-Backend.ps1
```

`Install` downloads the pinned SDK/DB archives into `Backend/.local/downloads`, validates the SDK's official SHA-512, records URLs and archive hashes, and extracts them without a global install. EDB does not supply a detached hash through this download page; its downloaded archive SHA-256 is recorded for repeatability, not described as an independently verified signature. It neither configures firewall rules nor creates a paid account.

`Start` creates an isolated UTF-8 PostgreSQL cluster on first use, with SCRAM authentication, loopback listener `127.0.0.1:55432`, database `ruinrunner`, and a separate non-superuser `rr_app`. The API listens only at `http://127.0.0.1:5186`. Credentials are random and stored under ACL-restricted `Backend/.local/private`; only this OS account and SYSTEM are granted access. They are not under Assets, not included in the API project, never passed on command lines and not printed. Existing databases/saves are not deleted. Migration admin connection exists only in the short migration process, not the running API.

`Start -SkipBuild` reuses a compiled binary but still checks/applies migrations and immutable content publication. Stop the owned API before rebuilding. Process cleanup checks executable and DLL ownership before stopping it. DB stop uses `pg_ctl` against this exact cluster, permitting PostgreSQL to roll back unfinished transactions cleanly.

The ordered SQL files are protected by an applied SHA-256 in `schema_migrations`. Add a migration; do not edit a migration already applied to persistent data. Publication source is `Backend/Content/*.json`; changed bytes for an already published version are rejected. `RR_CONTENT_VERSION` selects the new-run version, default `integration-v1`. Retain old catalogs while old runs exist.

## Verification and recovery

`GET /health/live` checks API process health; `/health/ready` also opens PostgreSQL. Local logs are in `Backend/.local/logs`. HTTP integration reports are in `Backend/Validation`; fresh test credentials are private and never included in reports. The current Python test command is:

```powershell
& 'C:/Users/Hyeon/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Tools/Backend/Test-Backend.py create
& 'C:/Users/Hyeon/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Tools/Backend/Test-Backend.py negative
./Tools/Backend/Stop-Backend.ps1
./Tools/Backend/Start-Backend.ps1 -SkipBuild
& 'C:/Users/Hyeon/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' Tools/Backend/Test-Backend.py restart
```

These use fresh generated test accounts and real HTTP/DB, not user saves. The negative phase intentionally expires only its own test sessions through administrative SQL. The outage phase is intended to be run with this DB stopped and API running; it expects HTTP 503. It is not an instruction to interrupt the user's active expedition. Complete any parent-agent Unity check before deliberately stopping shared services.

`Backup-Backend.ps1` creates a custom-format `pg_dump` plus SHA-256 receipt under the private backup directory. Dumps contain account/session hashes and player progress; treat them as private. Restore never drops or overwrites a database:

```powershell
./Tools/Backend/Restore-BackendToNewDatabase.ps1 -BackupFile '<absolute path to ruinrunner.dump>' -NewDatabase rr_restore_20261004_review
```

The destination must be a new name beginning `rr_restore_`; the active API database is unchanged. Initial data is generated through normal guest/profile creation and the versioned content seed. There is intentionally no broad reset/delete script; starting a clean test uses new accounts or a separately named database.

## Remote deployment preparation and limits

`Backend/Dockerfile` is a deployment recipe using the .NET 10 SDK/runtime and a non-root process. Docker is not installed here, so this recipe has not been built or deployed. `.dockerignore` excludes local tools, data, credentials and logs. The published API can alternatively run with a normal ASP.NET Core host.

Provision PostgreSQL and a restricted `rr_app` role on the target, provide `RR_DB` through the hosting secret store, run a one-off `--migrate` with `RR_MIGRATION_DB`, and remove the migration credential before normal launch. Provide `RR_CONTENT_VERSION`, bind the service behind a configured TLS reverse proxy, and restrict the DB network to the API. Keep DB backups private. Public deployment and any paid service require separate authorization and have not occurred.

Current backend does not implement realtime multiplayer, authoritative combat simulation, merchant run-gold/stock, field equipment transactions, full content extraction or Unity client retry UI. Those are integration work. `client-reported-singleplayer` and `legacy-local-untrusted` origins are explicit and must remain excluded from future competitive/shared economies until a trust policy is designed.
