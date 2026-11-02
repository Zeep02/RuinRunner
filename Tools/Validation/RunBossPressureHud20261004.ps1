param([Parameter(Mandatory=$true)][string]$BuildId,[int]$Width=1280,[int]$Height=720)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $projectRoot 'Builds/CampaignCandidate-20261004-BU/RuinRunner.exe'
$build=Get-Content (Join-Path $projectRoot 'ArtDirection/Review-20261004/boss-pressure/build-BU-summary.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($build.buildId -ne $BuildId -or $build.result -ne 'Succeeded'){throw 'Wrong candidate identity'}
$stamp=Get-Date -Format HHmmss
$folder=Join-Path $projectRoot ('Logs/Validation/2026-10-04-boss-pressure/windows-hud-BU-'+$Width+'x'+$Height+'-'+$stamp)
if(Test-Path -LiteralPath $folder){throw 'Preserve prior evidence'}
New-Item -ItemType Directory -Path $folder | Out-Null
$log=Join-Path $folder 'player.log'
$argsList=@('--validation-profile','--validation-profile-name',('BossPressureBU-Hud-'+$Width+'x'+$Height+'-'+$stamp),'--ui-validation','--validation-capture-dir',$folder,'-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,'-logFile',$log)
$process=Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Normal -PassThru
$proof=[ordered]@{buildId=$BuildId;executable=$exe;processId=$process.Id;args=$argsList;startedAtUtc=[DateTime]::UtcNow.ToString('o');scope='Existing UI fixture only: supplied state and stages. Native motion regressions and normal gameplay are separate.'}
$proof | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $folder 'process.json') -Encoding UTF8
Write-Output ('START HUD '+$Width+'x'+$Height+' PID '+$process.Id)
$deadline=[DateTime]::UtcNow.AddSeconds(90)
while(!$process.HasExited -and [DateTime]::UtcNow -lt $deadline){$process.WaitForExit(1000) | Out-Null}
if(!$process.HasExited){throw ('Fixture still running; inspect PID '+$process.Id)}
$process.WaitForExit();$proof.exitCode=$process.ExitCode;$proof.endedAtUtc=[DateTime]::UtcNow.ToString('o')
$proof | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $folder 'process.json') -Encoding UTF8
if($process.ExitCode -ne 0){throw 'UI fixture failed'}
$process.Dispose()
$env:PYTHONPATH=Join-Path $projectRoot 'Tools/.python-deps'
& (Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe') (Join-Path $projectRoot 'Tools/Validation/VerifyCaptureContent20261002.py') $folder
if($LASTEXITCODE -ne 0){throw 'Blank frames do not pass visual review'}
$proof | ConvertTo-Json -Depth 4
