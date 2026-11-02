[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildId,[ValidateSet('smoke','ui')][string]$Mode='smoke',[int]$Width=1280,[int]$Height=720)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $projectRoot 'Builds/CampaignCandidate-20261003-AX/RuinRunner.exe'
$build=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/item-expansion/build-AX-summary.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($build.buildId -ne $BuildId -or $build.result -ne 'Succeeded' -or [IO.Path]::GetFullPath($build.outputPath) -ne $exe){throw 'Build identity/output does not match protected AX candidate.'}
$stamp=Get-Date -Format HHmmss
$profile='ItemExpansionAX-'+$Mode+'-'+$Width+'x'+$Height+'-'+$stamp
$folder=Join-Path $projectRoot ('Logs/Validation/2026-10-03-item-expansion/windows-'+$Mode+'-AX-'+$Width+'x'+$Height+'-'+$stamp)
if(Test-Path -LiteralPath $folder){throw 'Use a fresh validation directory.'}
New-Item -ItemType Directory -Path $folder | Out-Null
$log=Join-Path $folder 'player.log'
$arguments=@('--validation-profile','--validation-profile-name',$profile,'-logFile',$log)
if($Mode -eq 'smoke'){$arguments+=@('-batchmode','-nographics','--smoke-test')}
else{$arguments+=@('--ui-validation','--validation-capture-dir',$folder,'-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height)}
$style=if($Mode -eq 'ui'){'Normal'}else{'Hidden'}
$candidate=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle $style -PassThru
$proof=[ordered]@{buildId=$BuildId;mode=$Mode;executable=$exe;processId=$candidate.Id;profile=$profile;startedAtUtc=[DateTime]::UtcNow.ToString('o');log=$log;scope=if($Mode -eq 'smoke'){'Isolated startup/resource/function checks, not normal campaign.'}else{'Visible staged UI/region/boss/ending/new-item fixtures, not normal campaign or native input.'}}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
if(!$candidate.WaitForExit(45000)){throw ('AX fixture still running: PID '+$candidate.Id+'; preserve and observe '+$folder)}
$candidate.WaitForExit();$proof.exitCode=$candidate.ExitCode;$proof.endedAtUtc=[DateTime]::UtcNow.ToString('o')
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
$proof | ConvertTo-Json
Get-Content -LiteralPath $log -Encoding UTF8 | Select-Object -Last 10
if($candidate.ExitCode -ne 0){throw 'AX fixture failed; preserve process/log evidence.'}
$candidate.Dispose()
if($Mode -eq 'ui'){
    $python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';$oldTaskPythonPath=$env:PYTHONPATH
    try{$env:PYTHONPATH=Join-Path $projectRoot 'Tools/.python-deps';& $python (Join-Path $projectRoot 'Tools/Validation/VerifyCaptureContent20261002.py') $folder;if($LASTEXITCODE -ne 0){throw 'Blank frames are not visual evidence.'}}
    finally{$env:PYTHONPATH=$oldTaskPythonPath}
}
