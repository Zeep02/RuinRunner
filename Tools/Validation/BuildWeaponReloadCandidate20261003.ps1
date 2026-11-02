$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/weapon-reload'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261003-AX/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve existing AX candidate before another build.'}
$import=Get-Content -LiteralPath (Join-Path $review 'editor-import-catalog.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($import.result -ne 'PASS' -or $import.sources -ne 10018){throw 'Reload source/import/catalog checks have not passed.'}
$sourcePaths=@('Feedback/IndividualArt.cs','Feedback/NativeHeroFrameCatalog.cs','Diagnostics/NativeHeroAtlasValidation.cs','Diagnostics/RuntimeSmokeTest.cs','Core/GameBootstrap.cs','Feedback/MeleeCrossCutEffect.cs','Feedback/MeleeThrustEffect.cs','Feedback/MeleeSweepGeometry.cs','Player/PlayerController.cs','Player/PlayerAnimationController.cs','Player/HeldWeaponView.cs','Player/SscaryArmedFrames.cs','Player/WeaponStandingFrames.cs','Player/WeaponReloadFrames.cs','Items/OwnedEquipment.cs','Player/MeleeMotionTiming.cs','UI/PrototypeHud.cs','Enemies/EnemyController.cs')
$paths=@($sourcePaths | ForEach-Object {'Assets/RuinRunner/Runtime/'+$_}) + @('Assets/RuinRunner/Editor/TerrainAtlasPrebuild.cs','Assets/RuinRunner/Editor/NativeHeroAtlasPrebuild.cs','Assets/RuinRunner/Resources/Art/NativeHero/ArmedGripV3.asset','Assets/RuinRunner/Resources/Art/NativeHero/WeaponStandingV4.asset','ArtDirection/Review-20261003/weapon-reload/source-identity-v2.json','Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV2.asset','Assets/StreamingAssets/ThirdPartyNotices/Sscary-Adventurer.txt')
$snapshot=@($paths | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $projectRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant()}})
$snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-source-AX.json') -Encoding utf8
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-AX.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json
