param(
    [string]$PlayerDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('campaign-v1','campaign-v2','campaign-v3','campaign-v4','campaign-v5')][string]$ContentVersion='campaign-v5',
    [switch]$BackendOnly
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Release destination must be new; use a unique candidate directory.'}
if(!$BackendOnly){
    if(!$PlayerDirectory){throw 'PlayerDirectory is required for a full game release'}
    $playerSource=[IO.Path]::GetFullPath($PlayerDirectory)
    $executables=@(Get-ChildItem -LiteralPath $playerSource -File -Filter '*.exe' | Where-Object {$_.Name -notlike 'UnityCrashHandler*'})
    if($executables.Count -ne 1){throw 'Expected one Unity Windows game executable in PlayerDirectory'}
    if(!(Test-Path -LiteralPath (Join-Path $playerSource ($executables[0].BaseName+'_Data')))){throw 'Unity player data folder missing'}
}
$local=Join-Path $repo 'Backend/.local'
$dotnet=Join-Path $local 'dotnet/dotnet.exe'
$server=Join-Path $output 'Server'
$api=Join-Path $server 'api'
$runtime=Join-Path $server 'dotnet'
$postgres=Join-Path $server 'postgresql'
$licenses=Join-Path $output 'Licenses'
New-Item -ItemType Directory -Force -Path $output,$server,$runtime,$postgres,$licenses | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1';$env:DOTNET_CLI_HOME=Join-Path $local 'dotnet-home';$env:NUGET_PACKAGES=Join-Path $local 'nuget'
& $dotnet 'publish' (Join-Path $repo 'Backend/RuinRunner.Api/RuinRunner.Api.csproj') '-c' 'Release' '--no-restore' '-o' $api '--nologo'
if($LASTEXITCODE -ne 0){throw 'Release API publish failed'}
foreach($file in Get-ChildItem -LiteralPath $api -Filter '*.pdb' -File){Remove-Item -LiteralPath $file.FullName}
# Bundle only runtime components. SDK, package caches, all DB clusters and credentials stay outside.
Copy-Item -LiteralPath $dotnet -Destination $runtime
Copy-Item -LiteralPath (Join-Path $local 'dotnet/host') -Destination $runtime -Recurse
New-Item -ItemType Directory -Path (Join-Path $runtime 'shared') | Out-Null
foreach($name in @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')){Copy-Item -LiteralPath (Join-Path $local ('dotnet/shared/'+$name)) -Destination (Join-Path $runtime 'shared') -Recurse}
foreach($name in @('LICENSE.txt','ThirdPartyNotices.txt')){Copy-Item -LiteralPath (Join-Path $local ('dotnet/'+$name)) -Destination (Join-Path $licenses ('dotnet-'+$name))}
$pgSource=Join-Path $local 'postgresql/pgsql'
foreach($name in @('bin','lib','share')){Copy-Item -LiteralPath (Join-Path $pgSource $name) -Destination $postgres -Recurse}
foreach($name in @('server_license.txt','commandlinetools_3rd_party_licenses.txt')){Copy-Item -LiteralPath (Join-Path $pgSource $name) -Destination (Join-Path $licenses ('postgresql-'+$name))}
Copy-Item -LiteralPath (Join-Path $pgSource 'doc/src/sgml/html/legalnotice.html') -Destination (Join-Path $licenses 'postgresql-legalnotice.html')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ReleaseTemplates/Npgsql-LICENSE.txt') -Destination $licenses
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'ReleaseTemplates') -File | Where-Object {$_.Extension -in '.ps1','.cmd' -or $_.Name -eq 'SERVER-README.txt'} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $output}
$playerName=''
if(!$BackendOnly){
    $game=Join-Path $output 'Game';New-Item -ItemType Directory -Path $game | Out-Null
    Get-ChildItem -LiteralPath $playerSource | Where-Object {$_.Name -notlike '*DoNotShip*' -and $_.Extension -ne '.pdb'} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $game -Recurse}
    $playerName=$executables[0].Name
}
Copy-Item -LiteralPath (Join-Path $repo 'ASSET_SOURCES.md') -Destination (Join-Path $licenses 'GameAssetSources.md')
New-Item -ItemType Directory -Path (Join-Path $licenses 'GameAssets') | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Assets/StreamingAssets/ThirdPartyNotices') -File | Where-Object {$_.Extension -ne '.meta'} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenses 'GameAssets')}
@{apiPort=5186;databasePort=55432;contentVersion=$ContentVersion;playerExecutable=$playerName} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'public-config.json') -Encoding UTF8
$forbidden=Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {$_.Name -in 'secrets.json','pgpass','postmaster.pid','PG_VERSION' -or $_.Extension -in '.dpapi','.dump'}
if($forbidden){throw 'Unexpected private data appeared in package; candidate was not finalized'}
$readme=@'
Ruin Runner local Windows package

1. Extract/move the entire folder to a writable local drive.
2. Run "Start Local Game.cmd". First launch creates a private UTF-8 PostgreSQL database,
   applies migrations and immutable catalogs, starts the loopback API, then opens the game.
   No separate Unity, .NET SDK, PostgreSQL installation, administrator permission or paid account is needed.
3. After finishing the game, run "Stop Local Backend.cmd" to stop only this package's backend.
   Profiles remain saved. Back up %LOCALAPPDATA%\RuinRunner\LocalBackend and the game's protected
   recovery/identity files before moving to another Windows account or machine.

public-config.json contains ports, content version and the game filename only. Runtime secrets and
all database data are created under %LOCALAPPDATA%\RuinRunner\LocalBackend, outside this package.
"Start Local Game.cmd" keeps the API on 127.0.0.1; only "Start Server.cmd" opens it to the network. If the menu says the API is unavailable, run the local start
shortcut; if a port is occupied, stop the older owned backend or choose free ports in the public config.
Logs: %LOCALAPPDATA%\RuinRunner\LocalBackend\logs. Never delete its database to fix a connection error.

Admin tool: while the backend runs, open "Open Admin Tool.cmd" on the server PC. It opens http://127.0.0.1:<apiPort>/admin
and puts the admin token on the clipboard. Edits to monsters, weapons, accessories, economy and talents are saved as a new
catalog version and used by every expedition started afterwards; running expeditions keep their pinned version.
The page answers only on the server PC itself. Every version stays in the database, so an older one can be re-applied.

Playing across PCs: see SERVER-README.txt. Run "Start Server.cmd" on the server PC and "Join Server.cmd" on each game PC.
Public server: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Start-Game.ps1 -ApiUrl https://YOUR-API/
Plain HTTP is accepted only for private network addresses. No public service or paid hosting is provisioned by this package.

Isolated validation: Start-LocalBackend.ps1 -DataRoot <new-isolated-directory> -ApiPort <free-port>
-DatabasePort <free-port>, then Start-Game.ps1 with the same options and -ValidationProfile <new-name>.
Port overrides do not change the package manifest. Stop with the same DataRoot and port options.
Testing must not reset real player profiles or stop a backend used by another running game.

Content is pinned per expedition. This package selects the explicit contentVersion in public-config.json
for new expeditions and retains campaign-v1 for interrupted/older expeditions. Never edit published
catalog bytes or applied migrations in place. Combat results are client-reported single-player data.

Third-party runtime notices are in Licenses/. Game art/audio notices, when supplied with the player,
remain in the game build. Candidate status and gameplay limitations are reported separately.
'@
[IO.File]::WriteAllText((Join-Path $output 'README.txt'),$readme,[Text.UTF8Encoding]::new($false))
$files=Get-ChildItem -LiteralPath $output -Recurse -File | ForEach-Object {@{path=$_.FullName.Substring($output.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}}
@{createdUtc=[DateTime]::UtcNow.ToString('O');status='candidate';contentVersion=$ContentVersion;backendOnly=[bool]$BackendOnly;secretsIncluded=$false;databaseIncluded=$false;files=@($files)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Write-Output "Created local release candidate: $output"

# Only finalized packages directly in the project's deliverables are managed.
# Keep package contents unchanged so their SHA-256 manifests remain valid.
if ([IO.Path]::GetDirectoryName($output) -eq (Join-Path $repo 'deliverables')) {
    try {
        $retention = & (Join-Path $repo 'Tools/Prune-Builds.ps1') -ProjectRoot $repo -Kind Packages -CompletedPath $output -Apply
        Write-Output ("Package retention: latest + 2 previous; removed {0}" -f $retention.removed.Count)
    } catch {
        Write-Warning "Package created but retention failed: $_"
    }
}
