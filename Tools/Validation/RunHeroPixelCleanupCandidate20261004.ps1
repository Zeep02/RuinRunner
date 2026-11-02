param([ValidateSet('smoke','gpu')][string]$Mode='smoke')
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261004/hero-pixel-cleanup'
$build=Get-Content -LiteralPath (Join-Path $review 'build-BV-summary.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$exe=Join-Path $projectRoot 'Builds/CampaignCandidate-20261004-BV/RuinRunner.exe'
if($build.result -ne 'Succeeded' -or [IO.Path]::GetFullPath($build.outputPath) -ne $exe){throw 'BV identity not confirmed.'}
$stamp=Get-Date -Format HHmmss
$folder=Join-Path $projectRoot ('Logs/Validation/2026-10-04-hero-pixel-cleanup/windows-'+$Mode+'-BV-'+$stamp)
New-Item -ItemType Directory -Path $folder | Out-Null
$profile='HeroPixelCleanupBV-'+$Mode+'-'+$stamp
$arguments=@('--validation-profile','--validation-profile-name',$profile,'-logFile',(Join-Path $folder 'player.log'))
if($Mode -eq 'smoke'){$arguments+=@('-batchmode','-nographics','--smoke-test')}
else{$arguments+=@('--native-hero-atlas-validation','--validation-capture-dir',$folder,'-screen-fullscreen','0','-screen-width','1280','-screen-height','720')}
$process=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$proof=[ordered]@{buildId=$build.buildId;executable=$exe;mode=$Mode;processId=$process.Id;profile=$profile;startedAtUtc=[DateTime]::UtcNow.ToString('o');scope='Isolated startup/GPU PNG identity checks; not normal campaign or performance.'}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
if(!$process.WaitForExit(45000)){throw ('Fixture still running; preserve PID '+$process.Id+' and '+$folder)}
$process.WaitForExit();$proof.exitCode=$process.ExitCode;$proof.endedAtUtc=[DateTime]::UtcNow.ToString('o')
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
$proof | ConvertTo-Json
Get-Content -LiteralPath (Join-Path $folder 'player.log') -Encoding UTF8 -Tail 7
if($process.ExitCode -ne 0){throw 'BV fixture failed.'}
$process.Dispose()
