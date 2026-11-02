param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [Parameter(Mandatory=$true)][string]$Profile,
    [ValidateSet('melee','ranged','mobility','skill')][string]$Build='melee',
    [ValidateSet('campaign','adversity')][string]$Scenario='campaign',
    [string]$ApiUrl='http://127.0.0.1:5196/',
    [int]$Seed=824942335,
    [switch]$Record,
    [switch]$NoScreenshots,
    [switch]$Packaged,
    [switch]$CombatPressureTrace,
    [switch]$CheckLifecycle,
    [switch]$MeasurePerformance,
    [switch]$CpuClockOnly,
    [switch]$NoVsync,
    [switch]$Fullscreen,
    [switch]$Manual,
    [string]$PriorManualLaunch,
    [ValidateRange(0,3600)][int]$PerformanceSeconds=0,
    [ValidateSet('buy','settle')][string]$StopAfterResponseDrop,
    [string]$FaultStatePath,
    [string]$RecoveryTicket
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$validationRoot=Join-Path $repo 'Logs/Validation'
$ticketTool=Join-Path $repo 'Tools/Backend/Prepare-WindowsRecovery.py'
function Assert-BoundedPath([string]$Path,[string]$Boundary) {
    $resolved=[IO.Path]::GetFullPath($Path)
    if(-not $resolved.StartsWith([IO.Path]::GetFullPath($Boundary).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Path is outside the dedicated validation boundary.' }
    return $resolved
}
function Read-ProxyEvidence([string]$Path) {
    $state=Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if($state.listen -ne 'http://127.0.0.1:5200/' -or $state.upstream -ne 'http://127.0.0.1:5197/' -or $state.faultsEnabled -ne $true) { throw 'Dedicated 5200 to 5197 response-loss fixture required.' }
    return $state
}
function Assert-ProxyOwner([string]$Path) {
    $marker=Get-Content -LiteralPath (Join-Path $repo 'Backend/.local/fault-proxy-5200/process.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if([IO.Path]::GetFullPath($marker.evidence) -ne $Path -or $marker.faultsEnabled -ne $true) { throw 'Proxy evidence marker mismatch.' }
    $proxy=Get-CimInstance Win32_Process -Filter "ProcessId=$($marker.pid)" -ErrorAction Stop
    $expectedScript=Join-Path $repo 'Tools/Backend/ValidationFaultProxy.py'
    if(!$proxy -or $proxy.ExecutablePath -ne $marker.executable -or !$proxy.CommandLine.Contains($expectedScript)) { throw 'Owned response-loss proxy is not running.' }
}
$candidate=(Resolve-Path -LiteralPath $Executable).Path
$candidate=Assert-BoundedPath $candidate (Join-Path $repo $(if($Packaged){'deliverables'}else{'Builds'}))
if([IO.Path]::GetFileName($candidate) -ne 'RuinRunner.exe') { throw 'Expected a known RuinRunner candidate executable.' }
if($Packaged) {
    $packageRoot=Split-Path (Split-Path $candidate)
    $publicConfig=Get-Content -LiteralPath (Join-Path $packageRoot 'public-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if((Split-Path (Split-Path $candidate) -Leaf) -ne 'Game' -or $publicConfig.playerExecutable -ne 'RuinRunner.exe' -or
        !(Test-Path -LiteralPath (Join-Path $packageRoot 'Server/api/RuinRunner.Api.dll'))) { throw 'Expected a complete server/game release package.' }
}
if($Profile -notmatch '^[A-Za-z0-9_-]{1,80}$') { throw 'Validation profile requires one safe name.' }
$dataFolder=Join-Path ([IO.Path]::GetDirectoryName($candidate)) 'RuinRunner_Data'
$derivedValidation=[IO.Path]::GetFullPath((Join-Path $dataFolder '../../../Logs/Validation'))
$expectedValidation=if($Packaged){Join-Path $repo 'deliverables/Logs/Validation'}else{$validationRoot}
if($derivedValidation -ne [IO.Path]::GetFullPath($expectedValidation)) { throw 'Candidate layout would place validation saves outside its isolated validation boundary.' }
$profileDirectory=Join-Path $derivedValidation $Profile
$freshProfile=!(Test-Path -LiteralPath $profileDirectory)
$endpoint=[Uri]$ApiUrl
if (-not $endpoint.IsLoopback -or $endpoint.Scheme -notin @('http','https')) { throw 'Automated campaigns require a dedicated loopback test backend.' }
$output=Assert-BoundedPath $OutputRoot $validationRoot
if($StopAfterResponseDrop -and $RecoveryTicket) { throw 'Choose either interruption or ticket recovery.' }
if($Packaged -and ($StopAfterResponseDrop -or $RecoveryTicket)) { throw 'Recovery tickets require the existing Builds layout; package campaigns use a fresh isolated profile.' }
if($NoScreenshots -and $Record) { throw 'NoScreenshots cannot be combined with recording.' }
if($Manual -and ($StopAfterResponseDrop -or $RecoveryTicket -or $Scenario -ne 'campaign' -or $Record -or $CheckLifecycle)) { throw 'Manual UI validation cannot combine automated scenarios, recovery, or pilot-only recording.' }
if($PriorManualLaunch -and !$Manual) { throw 'PriorManualLaunch is only for native UI follow-up; it is not a recovery ticket.' }
if(($StopAfterResponseDrop -or $RecoveryTicket) -and ($endpoint.AbsoluteUri -ne 'http://127.0.0.1:5200/' -or $Scenario -ne 'campaign' -or $PerformanceSeconds -gt 0)) { throw 'Recovery uses only the dedicated 5200 campaign with no timed performance exit.' }
if($RecoveryTicket) {
    $RecoveryTicket=Assert-BoundedPath (Resolve-Path -LiteralPath $RecoveryTicket).Path $validationRoot
    & python.exe $ticketTool --validate-ticket $RecoveryTicket --executable $candidate --profile $Profile --api-url $endpoint.AbsoluteUri
    if($LASTEXITCODE -ne 0) { throw 'Recovery ticket did not pass pending/ownership verification; no game was launched.' }
    $ticket=Get-Content -LiteralPath $RecoveryTicket -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-ProxyOwner $ticket.proxyStatePath
} elseif(!$freshProfile -and !$PriorManualLaunch) { throw 'Choose a NEW isolated validation profile. Existing profiles require a verified interruption ticket or a matched native UI follow-up launch.' }
if($StopAfterResponseDrop) {
    if(!$FaultStatePath) { throw 'A new proxy state path is required for bounded interruption.' }
    $StopAfterResponseDrop=$StopAfterResponseDrop.ToLowerInvariant()
    $FaultStatePath=Assert-BoundedPath (Resolve-Path -LiteralPath $FaultStatePath).Path (Join-Path $repo 'Backend/Validation/FaultProxy')
    Assert-ProxyOwner $FaultStatePath
    $initialFault=Read-ProxyEvidence $FaultStatePath
    if($initialFault.freshGuestEnrolled -ne $false -or $initialFault.dropped.buy -or $initialFault.dropped.settle -or @($initialFault.operations).Count -ne 0) { throw 'Response-loss interruption must start with a fresh proxy and a new guest.' }
}
$runtime=Join-Path $dataFolder 'Managed/RuinRunner.Runtime.dll'
$exeSha=(Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$runtimeSha=(Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash
if($PriorManualLaunch) {
    $PriorManualLaunch=Assert-BoundedPath (Resolve-Path -LiteralPath $PriorManualLaunch).Path $validationRoot
    $prior=Get-Content -LiteralPath $PriorManualLaunch -Raw -Encoding UTF8 | ConvertFrom-Json
    $profileIndex=[Array]::IndexOf([object[]]$prior.args,'--validation-profile-name')
    if($freshProfile -or [IO.Path]::GetFileName($PriorManualLaunch) -ne 'launch.json' -or
        $prior.freshValidationProfile -ne $true -or $prior.exe -ne $candidate -or $prior.api -ne $endpoint.AbsoluteUri -or
        $prior.executableSha256 -ne $exeSha -or $prior.runtimeSha256 -ne $runtimeSha -or
        $profileIndex -lt 0 -or $prior.args[$profileIndex+1] -ne $Profile) { throw 'Native UI follow-up requires the exact candidate, endpoint and originally fresh validation profile.' }
    $priorProcess=Get-Process -Id $prior.pid -ErrorAction SilentlyContinue
    if($priorProcess -and $priorProcess.StartTime.ToUniversalTime().ToString('o') -eq $prior.processStartUtc) { throw 'The original validation player is still running.' }
}
if(Test-Path -LiteralPath (Join-Path $output 'launch.json')) { throw 'Choose a new evidence directory; prior run exists.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$runArgs=@('--validation-profile','--validation-profile-name',$Profile,
    '--validation-seed',$Seed.ToString(),'--validation-build',$Build,'--validation-scenario',$Scenario,'--validation-capture-dir',$output,
    '--validation-quit','-screen-fullscreen',$(if($Fullscreen){'1'}else{'0'}),'-screen-width','1920','-screen-height','1080',
    '-logFile',(Join-Path $output 'player.log'))
if(!$Manual) { $runArgs = @('--automated-stage-validation') + $runArgs }
if($RecoveryTicket) { $runArgs += @('--validation-recovery-ticket',$RecoveryTicket) }
if($Record) { $runArgs += '--validation-record' }
if($NoScreenshots) { $runArgs += '--validation-no-screenshots' }
if($CombatPressureTrace) { $runArgs += '--combat-pressure-trace' }
if($CheckLifecycle) { $runArgs += '--validation-lifecycle' }
if($MeasurePerformance) { $runArgs += @('--performance-dir',$output) }
if(($CpuClockOnly -or $NoVsync -or $PerformanceSeconds -gt 0) -and -not $MeasurePerformance) { throw 'Performance options require MeasurePerformance.' }
if($CpuClockOnly) { $runArgs += '--performance-cpu-clock-only' }
if($NoVsync) { $runArgs += '--performance-no-vsync' }
if($PerformanceSeconds -gt 0) { $runArgs += @('--performance-seconds',$PerformanceSeconds.ToString()) }
$quotedArgs=$runArgs | ForEach-Object { if($_ -match '\s') { '"'+$_+'"' } else { $_ } }
# The game is the visual artifact under inspection. Hiding its window prevents
# Unity from presenting frames and invalidates render/performance evidence.
$previousEndpoint=$env:RUINRUNNER_API_URL
try {
    $env:RUINRUNNER_API_URL=$endpoint.AbsoluteUri
    $process=Start-Process -FilePath $candidate -ArgumentList $quotedArgs -WindowStyle Normal -PassThru
} finally { $env:RUINRUNNER_API_URL=$previousEndpoint }
$processStartUtc=$process.StartTime.ToUniversalTime().ToString('o')
$launchPath=Join-Path $output 'launch.json'
[pscustomobject]@{pid=$process.Id;launcherPid=$PID;processStartUtc=$processStartUtc;args=$runArgs;exe=$candidate;api=$endpoint.AbsoluteUri;startedAt=(Get-Date).ToString('o');fullscreenRequested=[bool]$Fullscreen;executableSha256=$exeSha;runtimeSha256=$runtimeSha;freshValidationProfile=$freshProfile;stopAfterResponseDrop=$StopAfterResponseDrop;faultStatePath=$FaultStatePath;recoveryTicket=$RecoveryTicket;inputEvidence=$(if($Manual){'Native UI input only; automated-stage-validation component disabled.'}else{'GameAction combat/navigation; production UI callbacks for menu choices; not manual UI input'});foregroundEvidence='Caller must explicitly activate/verify the game via approved UI tooling; Unity focused alone is insufficient.'} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $launchPath -Encoding UTF8
Write-Output ('START PID '+$process.Id+' '+$Build+' normal rules; profile '+$Profile)
$forcedCheckpoint=$false
$nextProgress=[DateTime]::UtcNow.AddSeconds(30)
$pollMs=if($StopAfterResponseDrop){20}else{30000}
while(-not $process.WaitForExit($pollMs)) {
    if($StopAfterResponseDrop) {
        $state=Read-ProxyEvidence $FaultStatePath
        if($state.startedUtc -ne $initialFault.startedUtc) { throw 'Proxy session changed; owned game was not interrupted.' }
        $droppedId=$state.dropped.$StopAfterResponseDrop
        if($droppedId) {
            $operation=[Guid]::Parse($droppedId).ToString()
            $row=@($state.operations | Where-Object {$_.operationId -eq $operation -and $_.action -eq $StopAfterResponseDrop})
            if($row.Count -ne 1 -or $row[0].requests -ne 1 -or $row[0].droppedResponses -ne 1 -or @($row[0].upstreamStatuses).Count -ne 1 -or $row[0].upstreamStatuses[0] -ne 200) { throw 'First committed response loss was not observed before retry; no process stopped.' }
            # Only the child created by this invocation may be stopped. Its
            # retained Process handle, start time, executable and profile agree.
            $process.Refresh()
            $owned=Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)" -ErrorAction Stop
            $profilePattern='--validation-profile-name\s+"?'+[regex]::Escape($Profile)+'"?(\s|$)'
            if($process.HasExited -or !$owned -or $owned.ExecutablePath -ne $candidate -or
                $process.StartTime.ToUniversalTime().ToString('o') -ne $processStartUtc -or
                ![regex]::IsMatch($owned.CommandLine,$profilePattern) -or
                (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ne $exeSha -or
                (Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash -ne $runtimeSha) { throw 'Owned-process verification failed; no process stopped.' }
            # Preserve the proxy's exact UTF-8 bytes. ConvertFrom/To-Json would
            # convert its UTC string into a local DateTime on recent PowerShell.
            Copy-Item -LiteralPath $FaultStatePath -Destination (Join-Path $output 'fault-at-stop.json') -ErrorAction Stop
            $process.Kill()
            $process.WaitForExit()
            [pscustomobject]@{stoppedByLauncher=$true;pid=$process.Id;sourceProcessStartUtc=$processStartUtc;executableSha256=$exeSha;operationId=$operation;action=$StopAfterResponseDrop;stoppedUtc=[DateTime]::UtcNow.ToString('o')} |
                ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'crash-stop.json') -Encoding UTF8
            $forcedCheckpoint=$true
            break
        }
    }
    if([DateTime]::UtcNow -ge $nextProgress) {
        if(Test-Path -LiteralPath (Join-Path $output 'player.log')) {
            $last=Select-String -LiteralPath (Join-Path $output 'player.log') -Encoding UTF8 -Pattern '\[(Stage|Recovery) E2E\]' | Select-Object -Last 1
            if($last) { Write-Output $last.Line }
        }
        $nextProgress=[DateTime]::UtcNow.AddSeconds(30)
    }
}
$process.Refresh()
[pscustomobject]@{pid=$process.Id;exitCode=$process.ExitCode;completedAt=(Get-Date).ToString('o')} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'exit.json') -Encoding UTF8
Write-Output ('EXIT '+$process.ExitCode)
if($forcedCheckpoint) {
    & python.exe (Join-Path $repo 'Tools/Backend/Read-ValidationJournal.py') --profile $Profile --api-port 5200 --output (Join-Path $output 'journal-at-stop.json')
    if($LASTEXITCODE -ne 0) { throw 'Owned interruption recorded, but journal inspection failed. This is not recovery proof.' }
    & python.exe $ticketTool --launch $launchPath --fault-state $FaultStatePath --action $StopAfterResponseDrop --output (Join-Path $output 'crash-ticket.json')
    if($LASTEXITCODE -ne 0) { throw 'Owned interruption recorded, but unresolved pending evidence was not verified. This is not recovery proof.' }
    Write-Output 'CRASH CHECKPOINT: pending UUID preserved. Relaunch with this crash-ticket.json to test recovery; no recovery PASS is claimed.'
    return
}
if($StopAfterResponseDrop) { throw 'Game exited without the requested interruption. Retain evidence; no restart recovery proof.' }
if($process.ExitCode -ne 0) { throw 'Normal run did not exit successfully. Retain evidence for diagnosis.' }
if($RecoveryTicket) {
    $report=Get-Content -LiteralPath (Join-Path $output 'recovery-report.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if($report.recoveryPassed -ne $true -or $report.operationId -ne $ticket.operationId) { throw 'Recovery report did not confirm the ticket operation.' }
}
elseif(-not $Manual) {
    $journeyPath=Join-Path $output 'server-journey.json'
    if(-not (Test-Path -LiteralPath $journeyPath)) { throw 'Game exited without a completed campaign report. Exit code zero alone is not a pass.' }
    $journey=Get-Content -LiteralPath $journeyPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if($journey.serverJourneyPassed -ne $true -or $journey.cheatsOrSuppliedState -ne $false) { throw 'Normal campaign report did not pass. Retain the failed evidence.' }
}
