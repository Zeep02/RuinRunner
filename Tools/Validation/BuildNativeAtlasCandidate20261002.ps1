$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261002/native-frame-atlas'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261002-AW/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve existing AW candidate before another build.'}
$migration=Get-Content -LiteralPath (Join-Path $review 'migration-result.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($migration.result -ne 'PASS' -or $migration.individualPngs -ne 30851){throw 'Source/GUID migration has not passed.'}
$sourcePaths=@('Feedback/IndividualArt.cs','Feedback/NativeHeroFrameCatalog.cs','Diagnostics/NativeHeroAtlasValidation.cs','Diagnostics/RuntimeSmokeTest.cs','Core/GameBootstrap.cs','Feedback/MeleeCrossCutEffect.cs','Feedback/MeleeThrustEffect.cs','Feedback/MeleeSweepGeometry.cs','Player/PlayerController.cs','Player/PlayerAnimationController.cs','Player/HeldWeaponView.cs','Player/SscaryArmedFrames.cs','Player/WeaponStandingFrames.cs','Player/MeleeMotionTiming.cs','UI/PrototypeHud.cs','Enemies/EnemyController.cs')
$paths=@($sourcePaths | ForEach-Object {'Assets/RuinRunner/Runtime/'+$_}) + @('Assets/RuinRunner/Editor/TerrainAtlasPrebuild.cs','Assets/RuinRunner/Editor/NativeHeroAtlasPrebuild.cs','Assets/RuinRunner/Resources/Art/NativeHero/ArmedGripV3.asset','Assets/RuinRunner/Resources/Art/NativeHero/WeaponStandingV4.asset','ArtDirection/Review-20261002/native-frame-atlas/source-inventory.json')
$snapshot=@($paths | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant()}})
$snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-source-AW.json') -Encoding utf8
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-AW.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json
