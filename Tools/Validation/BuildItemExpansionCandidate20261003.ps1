$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/item-expansion'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261003-AX/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve existing AX candidate before another build.'}
$icons=Get-Content -LiteralPath (Join-Path $review 'icon-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($icons.result -ne 'PASS' -or $icons.icons.Count -ne 6){throw 'Six native icons have not passed import.'}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV2.asset')){throw 'Unverified deferred reload catalog must stay outside Resources.'}
$paths=@(rg --files (Join-Path $projectRoot 'Assets/RuinRunner/Runtime') | Where-Object {$_ -match '\.cs$'})
$paths+=@('Assets/RuinRunner/Editor/TerrainAtlasPrebuild.cs','Assets/RuinRunner/Editor/NativeHeroAtlasPrebuild.cs','Assets/RuinRunner/Resources/Art/NativeHero/ArmedGripV3.asset','Assets/RuinRunner/Resources/Art/NativeHero/WeaponStandingV4.asset','ArtDirection/Review-20261003/item-expansion/icon-import.json') | ForEach-Object {Join-Path $projectRoot $_}
foreach($icon in $icons.icons){$paths+=Join-Path $projectRoot $icon.path;$paths+=Join-Path $projectRoot ($icon.path+'.meta')}
$snapshot=@($paths | ForEach-Object {[pscustomobject]@{path=[IO.Path]::GetFullPath($_);sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()}})
$snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-source-AX.json') -Encoding utf8
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-AX.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json
