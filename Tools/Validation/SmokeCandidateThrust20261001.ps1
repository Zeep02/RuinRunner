[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildId)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$exe=Join-Path $projectRoot 'Builds/CampaignCandidate-20261001-AU/RuinRunner.exe'
if(!(Test-Path -LiteralPath $exe -PathType Leaf)){throw 'AU executable is missing.'}
$stamp=Get-Date -Format HHmmss
$profile='ThrustAU-'+$stamp
$folder=Join-Path $projectRoot ('Logs/Validation/2026-10-01-thrust-fx/windows-smoke-AU-'+$stamp)
if(Test-Path -LiteralPath $folder){throw 'Use a fresh validation directory.'}
New-Item -ItemType Directory -Path $folder | Out-Null
$log=Join-Path $folder 'player.log'
$startUtc=[DateTime]::UtcNow.ToString('o')
$smokeProcess=Start-Process -FilePath $exe -ArgumentList @('-batchmode','-nographics','--smoke-test','--validation-profile','--validation-profile-name',$profile,'-logFile',$log) -WindowStyle Hidden -PassThru
$proof=[ordered]@{buildId=$BuildId;executable=$exe;processId=$smokeProcess.Id;profile=$profile;startedAtUtc=$startUtc;log=$log}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
if(!$smokeProcess.WaitForExit(45000)){throw ('Isolated AU smoke still running: PID '+$smokeProcess.Id+'; see '+$folder)}
$smokeProcess.WaitForExit()
$proof.exitCode=$smokeProcess.ExitCode;$proof.endedAtUtc=[DateTime]::UtcNow.ToString('o')
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $folder 'process.json') -Encoding utf8
$proof | ConvertTo-Json
Get-Content -LiteralPath $log -Encoding UTF8 | Select-Object -Last 9
if($smokeProcess.ExitCode -ne 0){throw 'AU smoke failed; preserved process/log evidence.'}
$smokeProcess.Dispose()

