$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261001/thrust-fx'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261001-AU/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve existing AU candidate before another build.'}
$sourcePaths=@('Feedback/MeleeThrustEffect.cs','Feedback/MeleeSweepGeometry.cs','Player/PlayerController.cs','Player/PlayerAnimationController.cs','Enemies/EnemyController.cs','Diagnostics/RuntimeSmokeTest.cs')
$snapshot=@($sourcePaths | ForEach-Object {$path='Assets/RuinRunner/Runtime/'+$_; [pscustomobject]@{path=$path;sha256=(Get-FileHash -LiteralPath (Join-Path $projectRoot $path) -Algorithm SHA256).Hash.ToLowerInvariant()}})
$snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-source-AU.json') -Encoding utf8
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-AU.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json

