param(
    [ValidatePattern('^[A-Za-z0-9_-]{1,60}$')][string]$Name='windows-validation',
    [switch]$EnableFaults,
    [ValidateSet('any','victory')][string]$SettlementStatus='any',
    [ValidateRange(0,10)][double]$DropDelaySeconds=0,
    [switch]$NewState,
    [switch]$Status,
    [switch]$Stop
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script=Join-Path $PSScriptRoot 'ValidationFaultProxy.py'
$ownedRoot=Join-Path $repo 'Backend/.local/fault-proxy-5200'
$marker=Join-Path $ownedRoot 'process.json'
if(Test-Path -LiteralPath $marker){
    $saved=Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json
    $process=Get-CimInstance Win32_Process -Filter "ProcessId=$($saved.pid)" -ErrorAction SilentlyContinue
    if($process){
        if(!$process.CommandLine.Contains($script) -or $process.ExecutablePath -ne $saved.executable){throw 'Proxy process ownership mismatch; no process was stopped.'}
        if($Stop){Stop-Process -Id $saved.pid;Remove-Item -LiteralPath $marker;Write-Output 'Owned 5200 validation proxy stopped; all evidence retained.';return}
        if($Status){Get-Content -LiteralPath $saved.evidence -Raw -Encoding UTF8;return}
        throw 'Proxy is already running. Stop it before starting a new validation guest/session.'
    }
    if($Status){Get-Content -LiteralPath $saved.evidence -Raw -Encoding UTF8;return}
}
if($Stop){Write-Output 'No owned 5200 validation proxy is running.';return}
if($Status){throw 'No proxy evidence marker is available.'}
if(!$NewState){throw 'Use -NewState to create a fresh evidence file; no previous state is overwritten.'}
# The proxy destination is fixed in source to the dedicated test API; inspect its
# existing isolated marker without reading credentials or restarting any service.
$upstreamRoot=Join-Path $repo 'Backend/.local/isolated/goal-catalog-20261009'
$upstream=Get-Content -LiteralPath (Join-Path $upstreamRoot 'instance.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($upstream.apiPort -ne 5197 -or $upstream.databasePort -ne 55440 -or !([IO.Path]::GetFullPath($upstream.dll)).StartsWith($upstreamRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Dedicated 5197 upstream marker does not match the isolated fixture.'}
$health=Invoke-RestMethod -Uri 'http://127.0.0.1:5197/health/ready' -TimeoutSec 5
if($health.status -ne 'ready'){throw 'Dedicated 5197 API is unavailable.'}
$python=(Get-Command python.exe -ErrorAction Stop).Source
$stamp=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6)
$evidenceRoot=Join-Path $repo ('Backend/Validation/FaultProxy/'+$Name+'-'+$stamp)
New-Item -ItemType Directory -Force -Path $ownedRoot,$evidenceRoot | Out-Null
$evidence=Join-Path $evidenceRoot 'state.json'
$arguments=@(('"'+$script+'"'),'--state-file',('"'+$evidence+'"'))
$arguments+=@('--settlement-status',$SettlementStatus)
$arguments+=@('--drop-delay-seconds',$DropDelaySeconds.ToString([Globalization.CultureInfo]::InvariantCulture))
if($EnableFaults){$arguments+='--enable-faults'}
$process=Start-Process -FilePath $python -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidenceRoot 'launch.log') -RedirectStandardError (Join-Path $evidenceRoot 'error.log')
@{pid=$process.Id;script=$script;executable=$python;evidence=$evidence;faultsEnabled=[bool]$EnableFaults} | ConvertTo-Json | Set-Content -LiteralPath $marker -Encoding UTF8
$ready=$false
for($i=0;$i -lt 30;$i++){
    $process.Refresh();if($process.HasExited){break}
    if(Test-Path -LiteralPath $evidence){
        try{$snapshot=Invoke-RestMethod -Uri 'http://127.0.0.1:5200/__faults/status' -TimeoutSec 2;if($snapshot.faultsEnabled -eq [bool]$EnableFaults){$ready=$true;break}}catch{}
    }
    Start-Sleep -Milliseconds 100
}
if(!$ready){throw 'Validation proxy did not become ready; inspect its evidence launch/error logs.'}
Write-Output "Validation proxy: http://127.0.0.1:5200/ -> dedicated 5197; faults=$([bool]$EnableFaults); evidence=$evidence"
