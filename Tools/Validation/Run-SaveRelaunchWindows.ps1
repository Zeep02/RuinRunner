param([Parameter(Mandatory=$true)][string]$Executable,[Parameter(Mandatory=$true)][string]$OutputRoot,[Parameter(Mandatory=$true)][string]$Profile)
$ErrorActionPreference='Stop'
$candidate=(Resolve-Path -LiteralPath $Executable).Path
$output=[IO.Path]::GetFullPath($OutputRoot)
if(Test-Path -LiteralPath $output){throw 'Preserve existing relaunch evidence; choose a fresh directory.'}
New-Item -ItemType Directory -Path $output | Out-Null
$runArgs=@('--save-relaunch-validation','--validation-profile','--validation-profile-name',$Profile,'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile',(Join-Path $output 'player.log'))
$quoted=$runArgs | ForEach-Object {if($_ -match '\s'){'"'+$_+'"'}else{$_}}
$process=Start-Process -FilePath $candidate -ArgumentList $quoted -WindowStyle Normal -PassThru
$runtime=Join-Path ([IO.Path]::GetDirectoryName($candidate)) 'RuinRunner_Data/Managed/RuinRunner.Runtime.dll'
$proof=[ordered]@{pid=$process.Id;executable=$candidate;runtimeSha256=(Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash;profile=$Profile;startedAt=(Get-Date).ToString('o');scope='Second process loads isolated post-victory profile; menu cleanup and persisted state fixture, not normal combat.'}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json') -Encoding UTF8
Write-Output ('START relaunch PID '+$process.Id)
if(!$process.WaitForExit(60000)){throw ('Relaunch still running; observe same PID '+$process.Id+' and preserve evidence.')}
$process.WaitForExit();$proof.exitCode=$process.ExitCode;$proof.endedAt=(Get-Date).ToString('o')
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json') -Encoding UTF8
$log=Get-Content -LiteralPath (Join-Path $output 'player.log') -Raw -Encoding UTF8
if($process.ExitCode -ne 0 -or !$log.Contains('[Save Lifecycle] PASS phase=relaunch')){throw 'Save relaunch did not pass; preserve log.'}
Write-Output ('PASS relaunch PID '+$process.Id+' exit '+$process.ExitCode)
$process.Dispose()
