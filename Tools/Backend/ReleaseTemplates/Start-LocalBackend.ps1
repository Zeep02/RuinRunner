param([string]$DataRoot,[int]$ApiPort=0,[int]$DatabasePort=0,[switch]$Stop,[switch]$Network)
$ErrorActionPreference='Stop'
$package=[IO.Path]::GetFullPath($PSScriptRoot)
$config=Get-Content -LiteralPath (Join-Path $package 'public-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$DataRoot){$DataRoot=Join-Path $env:LOCALAPPDATA 'RuinRunner/LocalBackend'}
$DataRoot=[IO.Path]::GetFullPath($DataRoot)
$data=Join-Path $DataRoot 'postgres'
$private=Join-Path $DataRoot 'private'
$logs=Join-Path $DataRoot 'logs'
$marker=Join-Path $private 'process.json'
$dotnet=Join-Path $package 'Server/dotnet/dotnet.exe'
$pg=Join-Path $package 'Server/postgresql/bin'
$dll=Join-Path $package 'Server/api/RuinRunner.Api.dll'
if(!$ApiPort){$ApiPort=[int]$config.apiPort};if(!$DatabasePort){$DatabasePort=[int]$config.databasePort}
if($apiPort -lt 1024 -or $apiPort -gt 65535 -or $databasePort -lt 1024 -or $databasePort -gt 65535){throw 'Invalid local port in public-config.json'}
if(Test-Path -LiteralPath $marker){
    $saved=Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($saved.pid)" -ErrorAction SilentlyContinue
    if($process -and $process.CommandLine -and $process.CommandLine.Contains($saved.dll)){
        if($saved.dll -ne $dll){throw 'A different release owns this running backend. Stop it with its own Stop Local Backend shortcut first.'}
        if($Stop){Stop-Process -Id $saved.pid}
        else{
            if([int]$saved.apiPort -ne $apiPort -or [int]$saved.databasePort -ne $databasePort){throw 'Stop this backend before changing its ports.'}
            if($Network -and $saved.network -ne $true){
                # Same package, loopback only: reopen the API for the network. The database keeps running.
                Stop-Process -Id $saved.pid;Wait-Process -Id $saved.pid -Timeout 15 -ErrorAction SilentlyContinue
            }else{
                $health=Invoke-RestMethod -Uri "http://127.0.0.1:$apiPort/health/ready" -TimeoutSec 5
                if($health.status -eq 'ready'){Write-Output "Local backend already ready: http://127.0.0.1:$apiPort/";return}
                throw 'Local backend is running but storage is unavailable. Stop and restart it.'
            }
        }
    }
}
if($Stop){
    if(Test-Path -LiteralPath (Join-Path $data 'postmaster.pid')){& (Join-Path $pg 'pg_ctl.exe') '-D' $data '-m' 'fast' '-w' 'stop';if($LASTEXITCODE -ne 0){throw 'Database did not stop cleanly'}}
    Write-Output 'Local backend stopped. Profiles and source saves are preserved.'
    return
}
if(@([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() | Where-Object {$_.Port -eq $apiPort}).Count){throw 'The selected API port is already occupied. Choose another free port or stop its owning application.'}
New-Item -ItemType Directory -Force -Path $DataRoot,$private,$logs | Out-Null
$principal=[Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls $DataRoot '/inheritance:r' '/grant:r' "${principal}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
if($LASTEXITCODE -ne 0){throw 'Could not protect local backend credentials'}
function New-RandomSecret {
    $bytes=New-Object byte[] 32;$random=[Security.Cryptography.RandomNumberGenerator]::Create()
    try{$random.GetBytes($bytes);return [BitConverter]::ToString($bytes).Replace('-','')}finally{$random.Dispose()}
}
$secretsPath=Join-Path $private 'secrets.json'
if(!(Test-Path -LiteralPath $secretsPath)){@{admin=New-RandomSecret;app=New-RandomSecret} | ConvertTo-Json | Set-Content -LiteralPath $secretsPath -Encoding UTF8}
$secrets=Get-Content -LiteralPath $secretsPath -Raw -Encoding UTF8 | ConvertFrom-Json
# Secret values are used only in private files/environment, never in child command lines.
if(!(Test-Path -LiteralPath (Join-Path $data 'PG_VERSION'))){
    $passwordFile=Join-Path $private 'init-password.txt'
    [IO.File]::WriteAllText($passwordFile,$secrets.admin,[Text.UTF8Encoding]::new($false))
    try{& (Join-Path $pg 'initdb.exe') '-D' $data '-U' 'postgres' '--encoding=UTF8' '--locale=C' '--auth=scram-sha-256' "--pwfile=$passwordFile" | Out-Null;if($LASTEXITCODE -ne 0){throw 'Local database initialization failed'}}finally{Remove-Item -LiteralPath $passwordFile -ErrorAction SilentlyContinue}
    Add-Content -LiteralPath (Join-Path $data 'postgresql.conf') -Encoding UTF8 -Value "`nlisten_addresses='127.0.0.1'`nport=$databasePort`nlog_statement='none'`nlog_min_error_statement='panic'"
}
& (Join-Path $pg 'pg_ctl.exe') '-D' $data 'status' *> $null
if($LASTEXITCODE -ne 0){
    $start=Start-Process -FilePath (Join-Path $pg 'pg_ctl.exe') -ArgumentList @('-D',('"'+$data+'"'),'-l',('"'+(Join-Path $logs 'postgres.log')+'"'),'-o',('"-p '+$databasePort+' -h 127.0.0.1"'),'-w','start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'pg-start.log') -RedirectStandardError (Join-Path $logs 'pg-start-error.log')
    # Retain the native handle before waiting: Windows PowerShell 5.1 can otherwise
    # lose ExitCode when its Start-Process wrapper observes the child exit first.
    $null=$start.Handle
    if(!$start.WaitForExit(30000)){throw 'Local PostgreSQL startup timed out'}
    $start.Refresh()
    if($start.ExitCode -ne 0){throw 'Local PostgreSQL startup failed. Read the local logs or check for a port conflict.'}
}
$runningCluster=Get-Content -LiteralPath (Join-Path $data 'postmaster.pid') -Encoding UTF8
if([int]$runningCluster[3] -ne $databasePort){throw 'Stop this backend before changing its database port in public-config.json.'}
$pgPass=Join-Path $private 'pgpass'
[IO.File]::WriteAllText($pgPass,"127.0.0.1:${databasePort}:*:postgres:$($secrets.admin)",[Text.UTF8Encoding]::new($false))
$env:PGPASSFILE=$pgPass
try{
    $exists=& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' $databasePort '-U' 'postgres' '-d' 'postgres' '-Atc' "SELECT 1 FROM pg_database WHERE datname='ruinrunner'"
    if($LASTEXITCODE -ne 0){throw 'Local database readiness failed'}
    if($exists -ne '1'){& (Join-Path $pg 'createdb.exe') '-h' '127.0.0.1' '-p' $databasePort '-U' 'postgres' 'ruinrunner';if($LASTEXITCODE -ne 0){throw 'Local database creation failed'}}
    $rolePath=Join-Path $private 'role.sql'
    [IO.File]::WriteAllText($rolePath,"DO `$rr`$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='rr_app') THEN CREATE ROLE rr_app LOGIN PASSWORD '$($secrets.app)'; END IF; END `$rr`$;",[Text.UTF8Encoding]::new($false))
    try{& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' $databasePort '-U' 'postgres' '-d' 'ruinrunner' '-v' 'ON_ERROR_STOP=1' '-f' $rolePath | Out-Null;if($LASTEXITCODE -ne 0){throw 'Local application account failed'}}finally{Remove-Item -LiteralPath $rolePath -ErrorAction SilentlyContinue}
}finally{Remove-Item Env:PGPASSFILE -ErrorAction SilentlyContinue}
$env:RR_DB="Host=127.0.0.1;Port=$databasePort;Database=ruinrunner;Username=rr_app;Password=$($secrets.app);Timeout=5;Command Timeout=10;Maximum Pool Size=16"
$env:RR_MIGRATION_DB="Host=127.0.0.1;Port=$databasePort;Database=ruinrunner;Username=postgres;Password=$($secrets.admin)"
$env:RR_CONTENT_VERSION=[string]$config.contentVersion
# The admin page (/admin) needs this token; "Open Admin Tool.cmd" copies it. Generated once, never shipped.
$adminTokenPath=Join-Path $private 'admin-token.txt'
if(!(Test-Path -LiteralPath $adminTokenPath)){[IO.File]::WriteAllText($adminTokenPath,(New-RandomSecret),[Text.UTF8Encoding]::new($false))}
try{
    try{& $dotnet $dll '--migrate';if($LASTEXITCODE -ne 0){throw 'Local schema/catalog migration failed'}}finally{Remove-Item Env:RR_MIGRATION_DB -ErrorAction SilentlyContinue}
    # Only the API opens to the network with -Network. PostgreSQL always stays on 127.0.0.1.
    $env:RR_ADMIN_TOKEN=([IO.File]::ReadAllText($adminTokenPath)).Trim()
    $env:ASPNETCORE_URLS=if($Network){"http://0.0.0.0:$apiPort"}else{"http://127.0.0.1:$apiPort"};$env:ASPNETCORE_ENVIRONMENT='Production'
    $api=Start-Process -FilePath $dotnet -ArgumentList ('"'+$dll+'"') -WorkingDirectory (Split-Path $dll) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api-error.log')
    @{pid=$api.Id;dll=$dll;apiPort=$apiPort;databasePort=$databasePort;network=[bool]$Network;startedUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding UTF8
}finally{Remove-Item Env:RR_DB,Env:RR_MIGRATION_DB,Env:RR_CONTENT_VERSION,Env:RR_ADMIN_TOKEN -ErrorAction SilentlyContinue}
$ready=$false
for($i=0;$i -lt 30;$i++){
    $api.Refresh()
    if($api.HasExited){break}
    try{if((Invoke-RestMethod -Uri "http://127.0.0.1:$apiPort/health/ready" -TimeoutSec 2).status -eq 'ready'){$api.Refresh();$ready=!$api.HasExited;break}}catch{}
    if($api.HasExited){break};Start-Sleep -Milliseconds 300
}
if(!$ready){throw 'Local API is unavailable. Read the logs in the data folder, then stop/restart the backend.'}
Write-Output "Local backend ready: http://127.0.0.1:$apiPort/ ; private data: $DataRoot"
