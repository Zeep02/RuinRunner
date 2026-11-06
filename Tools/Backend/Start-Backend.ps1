param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $root 'Backend/.local'
$dotnet=Join-Path $local 'dotnet/dotnet.exe'
$pg=Join-Path $local 'postgresql/pgsql/bin'
$pidFile=Join-Path $local 'api-process.json'
if(Test-Path -LiteralPath $pidFile){
    $old=Get-Content -LiteralPath $pidFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($old.pid)" -ErrorAction SilentlyContinue
    if($process -and $process.CommandLine.Contains($old.dll)){throw 'API is already running. Stop-Backend.ps1 -ApiOnly before rebuilding or restarting.'}
}
if(!(Test-Path -LiteralPath $dotnet) -or !(Test-Path -LiteralPath (Join-Path $pg 'pg_ctl.exe'))){throw 'Run Install-BackendTools.ps1 first.'}
$data=Join-Path $local 'data/postgres'
$logs=Join-Path $local 'logs'
$private=Join-Path $local 'private'
New-Item -ItemType Directory -Force -Path $logs,$private | Out-Null
# Local development credentials stay outside Assets, build outputs and reports.
$principal=[Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls $private '/inheritance:r' '/grant:r' "${principal}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
if($LASTEXITCODE -ne 0){throw 'Could not restrict credential directory'}
$secretPath=Join-Path $private 'secrets.json'
if(!(Test-Path -LiteralPath $secretPath)){
    $secrets=@{admin=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32));app=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))}
    $secrets | ConvertTo-Json | Set-Content -LiteralPath $secretPath -Encoding UTF8
}
$secrets=Get-Content -LiteralPath $secretPath -Raw -Encoding UTF8 | ConvertFrom-Json
if(!(Test-Path -LiteralPath (Join-Path $data 'PG_VERSION'))){
    New-Item -ItemType Directory -Force -Path $data | Out-Null
    $pwFile=Join-Path $private 'init-password.txt'
    [IO.File]::WriteAllText($pwFile,$secrets.admin,[Text.UTF8Encoding]::new($false))
    try {& (Join-Path $pg 'initdb.exe') '-D' $data '-U' 'postgres' '--encoding=UTF8' '--locale=C' '--auth=scram-sha-256' "--pwfile=$pwFile";if($LASTEXITCODE -ne 0){throw 'initdb failed'}} finally {Remove-Item -LiteralPath $pwFile -ErrorAction SilentlyContinue}
    Add-Content -LiteralPath (Join-Path $data 'postgresql.conf') -Encoding UTF8 -Value "`nlisten_addresses='127.0.0.1'`nport=55432`nlog_statement='none'`nlog_min_error_statement='panic'"
}
& (Join-Path $pg 'pg_ctl.exe') '-D' $data 'status' *> $null
if($LASTEXITCODE -ne 0){
    $start=Start-Process -FilePath (Join-Path $pg 'pg_ctl.exe') -ArgumentList @('-D',('"'+$data+'"'),'-l',('"'+(Join-Path $logs 'postgres.log')+'"'),'-w','start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'pg-start.log') -RedirectStandardError (Join-Path $logs 'pg-start-error.log')
    if(!$start.WaitForExit(30000)){throw 'pg_ctl startup exceeded 30 seconds'}
    if($start.ExitCode -ne 0){throw 'PostgreSQL start failed; see local logs.'}
}
$pgPass=Join-Path $private 'pgpass'
[IO.File]::WriteAllText($pgPass,"127.0.0.1:55432:*:postgres:$($secrets.admin)",[Text.UTF8Encoding]::new($false))
$env:PGPASSFILE=$pgPass
try {
    $exists=& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' '-d' 'postgres' '-Atc' "SELECT 1 FROM pg_database WHERE datname='ruinrunner'"
    if($LASTEXITCODE -ne 0){throw 'Database readiness failed'}
    if($exists -ne '1'){& (Join-Path $pg 'createdb.exe') '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' 'ruinrunner';if($LASTEXITCODE -ne 0){throw 'Database creation failed'}}
    $roleSql=Join-Path $private 'role.sql'
    $sql="DO `$rr`$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='rr_app') THEN CREATE ROLE rr_app LOGIN PASSWORD '$($secrets.app)'; END IF; END `$rr`$;"
    [IO.File]::WriteAllText($roleSql,$sql,[Text.UTF8Encoding]::new($false))
    try{& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' '-d' 'ruinrunner' '-v' 'ON_ERROR_STOP=1' '-f' $roleSql | Out-Null;if($LASTEXITCODE -ne 0){throw 'Application role setup failed'}}finally{Remove-Item -LiteralPath $roleSql -ErrorAction SilentlyContinue}
}finally{Remove-Item Env:PGPASSFILE -ErrorAction SilentlyContinue}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1';$env:DOTNET_NOLOGO='1';$env:DOTNET_CLI_HOME=Join-Path $local 'dotnet-home';$env:NUGET_PACKAGES=Join-Path $local 'nuget'
$project=Join-Path $root 'Backend/RuinRunner.Api/RuinRunner.Api.csproj'
if(!$SkipBuild){& $dotnet 'build' $project '-c' 'Release' '--nologo';if($LASTEXITCODE -ne 0){throw 'API build failed'}}
$dll=Join-Path $root 'Backend/RuinRunner.Api/bin/Release/net10.0/RuinRunner.Api.dll'
$env:RR_CONTENT_VERSION='campaign-v5'
$env:RR_DB="Host=127.0.0.1;Port=55432;Database=ruinrunner;Username=rr_app;Password=$($secrets.app);Timeout=5;Command Timeout=10;Maximum Pool Size=16"
$env:RR_MIGRATION_DB="Host=127.0.0.1;Port=55432;Database=ruinrunner;Username=postgres;Password=$($secrets.admin)"
try{& $dotnet $dll '--migrate';if($LASTEXITCODE -ne 0){throw 'Migration failed'}}finally{Remove-Item Env:RR_MIGRATION_DB -ErrorAction SilentlyContinue}
$pidFile=Join-Path $local 'api-process.json'
if(Test-Path -LiteralPath $pidFile){
    $old=Get-Content -LiteralPath $pidFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($old.pid)" -ErrorAction SilentlyContinue
    if($process -and $process.CommandLine.Contains($dll)){Remove-Item Env:RR_DB -ErrorAction SilentlyContinue;throw 'API is already running. Stop-Backend.ps1 -ApiOnly before rebuilding or restarting.'}
}
$env:ASPNETCORE_URLS='http://127.0.0.1:5186';$env:ASPNETCORE_ENVIRONMENT='Production'
try{
    $api=Start-Process -FilePath $dotnet -ArgumentList ('"'+$dll+'"') -WorkingDirectory (Split-Path $project) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api-error.log')
    @{pid=$api.Id;executable=$dotnet;dll=$dll;startedUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath $pidFile -Encoding UTF8
}finally{Remove-Item Env:RR_DB -ErrorAction SilentlyContinue}
$ready=$false
for($i=0;$i -lt 30;$i++){
    try{$health=Invoke-RestMethod -Uri 'http://127.0.0.1:5186/health/ready' -TimeoutSec 2;if($health.status -eq 'ready'){$ready=$true;break}}catch{}
    if($api.HasExited){break};Start-Sleep -Milliseconds 300
}
if(!$ready){throw 'API readiness failed; see local logs.'}
Write-Output "RuinRunner API ready at http://127.0.0.1:5186 (PID $($api.Id)); PostgreSQL 127.0.0.1:55432."
