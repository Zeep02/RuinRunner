[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildId,[ValidateSet('smoke','gpu','ui')][string]$Mode='smoke')
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $projectRoot 'Builds/CampaignCandidate-20261003-AX/RuinRunner.exe'
if(!(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'AX executable is missing.'}
$build=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/weapon-reload/build-AX-summary.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($build.buildId -ne $BuildId -or $build.result -ne 'Succeeded' -or [IO.Path]::GetFullPath($build.outputPath) -ne $exe){throw 'Build identity/output does not match the protected AX candidate.'}
$stamp=Get-Date -Format HHmmss
$profile='WeaponReloadAX-'+$Mode+'-'+$stamp
$folder=Join-Path $projectRoot ('Logs/Validation/2026-10-03-weapon-reload/windows-'+$Mode+'-AX-'+$stamp)
if(Test-Path -LiteralPath $folder){throw 'Use a fresh validation directory.'}
New-Item -ItemType Directory -Path $folder | Out-Null
$log=Join-Path $folder 'player.log'
$arguments=@('--validation-profile','--validation-profile-name',$profile,'-logFile',$log)
if($Mode -eq 'smoke'){$arguments+=@('-batchmode','-nographics','--smoke-test')}
else {
    $flag=if($Mode -eq 'gpu'){'--native-hero-atlas-validation'}else{'--ui-validation'}
    $arguments+=@($flag,'--validation-capture-dir',$folder,'-screen-fullscreen','0','-screen-width','1280','-screen-height','720')
}
$startUtc=[DateTime]::UtcNow.ToString('o')
$style=if($Mode -eq 'ui'){'Normal'}else{'Hidden'}
$candidate=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle $style -PassThru
$proof=[ordered]@{buildId=$BuildId;mode=$Mode;executable=$exe;processId=$candidate.Id;profile=$profile;startedAtUtc=$startUtc;log=$log;scope=if($Mode -eq 'smoke'){'Isolated startup/resource/function checks, not normal campaign.'}elseif($Mode -eq 'gpu'){'Every current body frame rendered with actual GPU SpriteRenderer, not motion/gameplay/performance.'}else{'Staged UI/region/boss/ending fixtures, not normal campaign.'}}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
if(!$candidate.WaitForExit(45000)){throw ('AX fixture still running: PID '+$candidate.Id+'; preserved process/log at '+$folder)}
$candidate.WaitForExit()
$proof.exitCode=$candidate.ExitCode;$proof.endedAtUtc=[DateTime]::UtcNow.ToString('o')
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
$proof | ConvertTo-Json
Get-Content -LiteralPath $log -Encoding UTF8 | Select-Object -Last 9
if($candidate.ExitCode -ne 0){throw 'AX fixture failed; preserved process/log evidence.'}
$candidate.Dispose()
if($Mode -eq 'ui'){
    $python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    $oldTaskPythonPath=$env:PYTHONPATH
    try {
        $env:PYTHONPATH=Join-Path $projectRoot 'Tools/.python-deps'
        & $python (Join-Path $projectRoot 'Tools/Validation/VerifyCaptureContent20261002.py') $folder
        if($LASTEXITCODE -ne 0){throw 'The captured UI contains blank frames. Process exit/CAPTURED alone is not visual evidence.'}
    }finally{$env:PYTHONPATH=$oldTaskPythonPath}
}
