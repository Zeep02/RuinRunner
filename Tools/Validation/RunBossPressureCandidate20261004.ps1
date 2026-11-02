[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildId,[ValidateSet('smoke','gpu','ui')][string]$Mode='smoke',[int]$Width=1280,[int]$Height=720)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $projectRoot 'Builds/CampaignCandidate-20261004-BT/RuinRunner.exe'
$build=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261004/boss-pressure/build-BT-summary.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($build.buildId -ne $BuildId -or $build.result -ne 'Succeeded' -or [IO.Path]::GetFullPath($build.outputPath) -ne $exe){throw 'Build identity/output does not match protected BT candidate.'}
$stamp=Get-Date -Format HHmmss
$profile='BossPressureBT-'+$Mode+'-'+$Width+'x'+$Height+'-'+$stamp
$folder=Join-Path $projectRoot ('Logs/Validation/2026-10-04-boss-pressure/windows-'+$Mode+'-BT-'+$Width+'x'+$Height+'-'+$stamp)
if(Test-Path -LiteralPath $folder){throw 'Use a fresh validation directory.'}
New-Item -ItemType Directory -Path $folder | Out-Null
$log=Join-Path $folder 'player.log'
$arguments=@('--validation-profile','--validation-profile-name',$profile,'-logFile',$log)
if($Mode -eq 'smoke'){$arguments+=@('-batchmode','-nographics','--smoke-test')}
else{$flag=if($Mode -eq 'gpu'){'--native-hero-atlas-validation'}else{'--ui-validation'};$arguments+=@($flag,'--validation-capture-dir',$folder,'-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height)}
$style=if($Mode -eq 'ui'){'Normal'}else{'Hidden'}
if($Mode -eq 'ui'){$arguments+=@('--player-hit-fixtures','--enemy-reaction-fixtures','--charger-motion-fixtures','--tidekeeper-motion-fixtures','--boss-hazard-fixtures','--sealheart-motion-fixtures','--tide-pool-placement-fixtures','--enemy-leap-fixtures','--warden-pattern-fixtures')}
$candidate=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle $style -PassThru
$proof=[ordered]@{buildId=$BuildId;mode=$Mode;executable=$exe;processId=$candidate.Id;profile=$profile;startedAtUtc=[DateTime]::UtcNow.ToString('o');log=$log;scope=if($Mode -eq 'smoke'){'Isolated startup/resource/function checks, not normal campaign.'}elseif($Mode -eq 'gpu'){'All current native frames on actual Windows GPU SpriteRenderer; not motion, gameplay or performance.'}else{'Staged UI/region/boss/ending/new-item, player-hit/shield, enemy anticipation/death/pause, four-direction charger, Tidekeeper/SealHeart action/damage/collapse, and Warden/Leaper world-height/airborne-death/pause/reward fixtures. Actors and state supplied; not normal campaign, native input or performance.'}}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
$deadline=[DateTime]::UtcNow.AddSeconds(210)
while(!$candidate.HasExited -and [DateTime]::UtcNow -lt $deadline){$candidate.WaitForExit(1000) | Out-Null}
if(!$candidate.HasExited){throw ('BT fixture still running: PID '+$candidate.Id+'; preserve and observe '+$folder)}
$candidate.WaitForExit();$proof.exitCode=$candidate.ExitCode;$proof.endedAtUtc=[DateTime]::UtcNow.ToString('o')
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
$proof | ConvertTo-Json
Get-Content -LiteralPath $log -Encoding UTF8 | Select-Object -Last 10
if($candidate.ExitCode -ne 0){throw 'BT fixture failed; preserve process/log evidence.'}
$candidate.Dispose()
if($Mode -eq 'ui'){
    $nativeLog=Get-Content -LiteralPath $log -Encoding UTF8 -Raw
    if(!$nativeLog.Contains('[Warden Patterns Windows] PASS')){throw 'Warden real close-range AI selection did not pass.'}
    if(([regex]::Matches($nativeLog,'\[Enemy Leap Windows\] PASS')).Count -ne 2){throw 'Current boss and leaper height/death fixtures did not both pass.'}
    if(([regex]::Matches($nativeLog,'\[Tide Pool Placement Windows\] PASS')).Count -ne 3){throw 'New pool placement fixture did not pass all three positions.'}
    if(([regex]::Matches($nativeLog,'\[Boss Hazard Windows\] PASS')).Count -ne 2 -or !$nativeLog.Contains('[Tidekeeper Windows] PASS') -or !$nativeLog.Contains('[SealHeart Native Windows] PASS') -or $nativeLog -match '(?m)^Exception:'){throw 'Native boss test did not pass; process exit alone is insufficient.'}
    $python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';$oldTaskPythonPath=$env:PYTHONPATH
    try{$env:PYTHONPATH=Join-Path $projectRoot 'Tools/.python-deps';& $python (Join-Path $projectRoot 'Tools/Validation/VerifyCaptureContent20261002.py') $folder;if($LASTEXITCODE -ne 0){throw 'Blank frames are not visual evidence.'}}
    finally{$env:PYTHONPATH=$oldTaskPythonPath}
}



