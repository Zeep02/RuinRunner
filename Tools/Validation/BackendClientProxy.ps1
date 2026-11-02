param([switch]$Stop,[switch]$Campaign)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $root 'Backend/.local'
$script=Join-Path $PSScriptRoot 'BackendClientDropProxy.py'
$python='C:/Users/Hyeon/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$pidFile=Join-Path $local 'proxy-process.json'
if(Test-Path -LiteralPath $pidFile){
    $owned=Get-Content -LiteralPath $pidFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($owned.pid)" -ErrorAction SilentlyContinue
    if($process){
        if(!$process.CommandLine.Contains($script) -or $process.ExecutablePath -ne ([IO.Path]::GetFullPath($python))){throw 'Proxy process identity mismatch'}
        if(!$Stop){throw 'Proxy already running. Stop it before a fresh response-loss test.'}
        Stop-Process -Id $owned.pid
    }
    Remove-Item -LiteralPath $pidFile
}
if($Stop){'Owned response-loss proxy stopped.';return}
$stamp=[DateTime]::Now.ToString('yyyyMMdd-HHmmss')
$proxyArgs=@(('"'+$script+'"'),'--port','5187')
if($Campaign){$proxyArgs+='--campaign'}
$process=Start-Process -FilePath $python -ArgumentList $proxyArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $local "logs/proxy-$stamp.log") -RedirectStandardError (Join-Path $local "logs/proxy-$stamp-error.log")
@{pid=$process.Id;script=$script;executable=[IO.Path]::GetFullPath($python)} | ConvertTo-Json | Set-Content -LiteralPath $pidFile -Encoding UTF8
Start-Sleep -Milliseconds 200
if($process.HasExited){throw 'Proxy did not start; inspect its timestamped log.'}
"Response-loss proxy listening on 127.0.0.1:5187, PID $($process.Id). Restart before each Unity fixture."
