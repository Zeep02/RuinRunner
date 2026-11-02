param(
    [ValidatePattern('^[a-z0-9][a-z0-9-]{1,40}$')][string]$Instance='goal-20261009',
    [ValidateRange(1024,65535)][int]$ApiPort=5196,
    [ValidateRange(1024,65535)][int]$DatabasePort=55439,
    [string]$ContentVersion='campaign-v1',
    [switch]$SkipBuild,
    [switch]$Stop
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $repo 'Backend/.local'
$instanceRoot=Join-Path $local ('isolated/'+$Instance)
$dotnet=Join-Path $local 'dotnet/dotnet.exe'
$pg=Join-Path $local 'postgresql/pgsql/bin'
$data=Join-Path $instanceRoot 'data'
$private=Join-Path $instanceRoot 'private'
$logs=Join-Path $instanceRoot 'logs'
$output=Join-Path $instanceRoot 'api'
$pidFile=Join-Path $instanceRoot 'instance.json'
if(!(Test-Path -LiteralPath $dotnet) -or !(Test-Path -LiteralPath (Join-Path $pg 'pg_ctl.exe'))){throw 'Install the portable backend tools with Install-BackendTools.ps1 first.'}
if(Test-Path -LiteralPath $pidFile){
    $previous=Get-Content -LiteralPath $pidFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($previous.pid)" -ErrorAction SilentlyContinue
    if($process -and $process.CommandLine.Contains($previous.dll)){
        if(!$Stop){throw 'This isolated API is already running; use -Stop before updating it.'}
        Stop-Process -Id $previous.pid
    }
}
if($Stop){
    if(Test-Path -LiteralPath (Join-Path $data 'PG_VERSION')){& (Join-Path $pg 'pg_ctl.exe') '-D' $data '-m' 'fast' '-w' 'stop'; if($LASTEXITCODE -ne 0){throw 'Isolated database shutdown failed'}}
    Write-Output "Stopped isolated instance $Instance. Its data is preserved."
    return
}
New-Item -ItemType Directory -Force -Path $instanceRoot,$private,$logs | Out-Null
$principal=[Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls $private '/inheritance:r' '/grant:r' "${principal}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
if($LASTEXITCODE -ne 0){throw 'Could not restrict isolated credentials directory'}
$secretPath=Join-Path $private 'secrets.json'
if(!(Test-Path -LiteralPath $secretPath)){
    @{admin=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32));app=[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))} | ConvertTo-Json | Set-Content -LiteralPath $secretPath -Encoding UTF8
}
$secrets=Get-Content -LiteralPath $secretPath -Raw -Encoding UTF8 | ConvertFrom-Json
if(!(Test-Path -LiteralPath (Join-Path $data 'PG_VERSION'))){
    $pw=Join-Path $private 'init-password.txt'
    [IO.File]::WriteAllText($pw,$secrets.admin,[Text.UTF8Encoding]::new($false))
    try{& (Join-Path $pg 'initdb.exe') '-D' $data '-U' 'postgres' '--encoding=UTF8' '--locale=C' '--auth=scram-sha-256' "--pwfile=$pw" | Out-Null; if($LASTEXITCODE -ne 0){throw 'Isolated initdb failed'}}finally{Remove-Item -LiteralPath $pw -ErrorAction SilentlyContinue}
    Add-Content -LiteralPath (Join-Path $data 'postgresql.conf') -Encoding UTF8 -Value "`nlisten_addresses='127.0.0.1'`nport=$DatabasePort`nlog_statement='none'`nlog_min_error_statement='panic'"
}
& (Join-Path $pg 'pg_ctl.exe') '-D' $data 'status' *> $null
if($LASTEXITCODE -ne 0){
    $start=Start-Process -FilePath (Join-Path $pg 'pg_ctl.exe') -ArgumentList @('-D',('"'+$data+'"'),'-l',('"'+(Join-Path $logs 'postgres.log')+'"'),'-w','start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'pg-start.log') -RedirectStandardError (Join-Path $logs 'pg-start-error.log')
    if(!$start.WaitForExit(30000) -or $start.ExitCode -ne 0){throw 'Isolated PostgreSQL start failed'}
}
$pgPass=Join-Path $private 'pgpass'
[IO.File]::WriteAllText($pgPass,"127.0.0.1:${DatabasePort}:*:postgres:$($secrets.admin)",[Text.UTF8Encoding]::new($false))
$env:PGPASSFILE=$pgPass
try{
    $exists=& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' $DatabasePort '-U' 'postgres' '-d' 'postgres' '-Atc' "SELECT 1 FROM pg_database WHERE datname='ruinrunner_isolated'"
    if($LASTEXITCODE -ne 0){throw 'Isolated database readiness failed'}
    if($exists -ne '1'){& (Join-Path $pg 'createdb.exe') '-h' '127.0.0.1' '-p' $DatabasePort '-U' 'postgres' 'ruinrunner_isolated'; if($LASTEXITCODE -ne 0){throw 'Isolated database creation failed'}}
    $sqlPath=Join-Path $private 'role.sql'
    [IO.File]::WriteAllText($sqlPath,"DO `$rr`$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='rr_app') THEN CREATE ROLE rr_app LOGIN PASSWORD '$($secrets.app)'; END IF; END `$rr`$;",[Text.UTF8Encoding]::new($false))
    try{& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' $DatabasePort '-U' 'postgres' '-d' 'ruinrunner_isolated' '-v' 'ON_ERROR_STOP=1' '-f' $sqlPath | Out-Null; if($LASTEXITCODE -ne 0){throw 'Isolated app role failed'}}finally{Remove-Item -LiteralPath $sqlPath -ErrorAction SilentlyContinue}
}finally{Remove-Item Env:PGPASSFILE -ErrorAction SilentlyContinue}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_NOLOGO='1'
$env:DOTNET_CLI_HOME=Join-Path $local 'dotnet-home'; $env:NUGET_PACKAGES=Join-Path $local 'nuget'
if(!$SkipBuild){& $dotnet 'build' (Join-Path $repo 'Backend/RuinRunner.Api/RuinRunner.Api.csproj') '-c' 'Release' '--no-restore' '-o' $output '--nologo'; if($LASTEXITCODE -ne 0){throw 'Isolated API build failed'}}
$dll=Join-Path $output 'RuinRunner.Api.dll'
$env:RR_DB="Host=127.0.0.1;Port=$DatabasePort;Database=ruinrunner_isolated;Username=rr_app;Password=$($secrets.app);Timeout=5;Command Timeout=10;Maximum Pool Size=16"
$env:RR_MIGRATION_DB="Host=127.0.0.1;Port=$DatabasePort;Database=ruinrunner_isolated;Username=postgres;Password=$($secrets.admin)"
$env:RR_CONTENT_VERSION=$ContentVersion
try{& $dotnet $dll '--migrate'; if($LASTEXITCODE -ne 0){throw 'Isolated migration failed'}}finally{Remove-Item Env:RR_MIGRATION_DB -ErrorAction SilentlyContinue}
$env:ASPNETCORE_URLS="http://127.0.0.1:$ApiPort"; $env:ASPNETCORE_ENVIRONMENT='Production'
try{
    $api=Start-Process -FilePath $dotnet -ArgumentList ('"'+$dll+'"') -WorkingDirectory $output -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api-error.log')
    @{pid=$api.Id;dll=$dll;apiPort=$ApiPort;databasePort=$DatabasePort;contentVersion=$ContentVersion;startedUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath $pidFile -Encoding UTF8
}finally{Remove-Item Env:RR_DB,Env:RR_CONTENT_VERSION -ErrorAction SilentlyContinue}
$ready=$false
for($i=0;$i -lt 30;$i++){
    try{if((Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/health/ready" -TimeoutSec 2).status -eq 'ready'){$ready=$true;break}}catch{}
    if($api.HasExited){break}; Start-Sleep -Milliseconds 300
}
if(!$ready){throw 'Isolated API readiness failed; inspect its isolated logs.'}
Write-Output "Isolated API ready: http://127.0.0.1:$ApiPort/ ; database port $DatabasePort ; instance $Instance. No normal database was read or modified."
