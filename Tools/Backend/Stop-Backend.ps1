param([switch]$ApiOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $root 'Backend/.local'
$pidFile=Join-Path $local 'api-process.json'
if(Test-Path -LiteralPath $pidFile){
    $owned=Get-Content -LiteralPath $pidFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $p=Get-CimInstance Win32_Process -Filter "ProcessId=$($owned.pid)" -ErrorAction SilentlyContinue
    if($p){
        if($p.ExecutablePath -ne $owned.executable -or !$p.CommandLine.Contains($owned.dll)){throw 'Refusing to stop a process outside this backend.'}
        Stop-Process -Id $owned.pid
    }
    Remove-Item -LiteralPath $pidFile
}
if(!$ApiOnly){
    $data=Join-Path $local 'data/postgres'
    if(Test-Path -LiteralPath (Join-Path $data 'postmaster.pid')){
        & (Join-Path $local 'postgresql/pgsql/bin/pg_ctl.exe') '-D' $data '-m' 'fast' '-w' 'stop'
        if($LASTEXITCODE -ne 0){throw 'Owned PostgreSQL did not stop cleanly.'}
    }
}
