$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/boss-hazard-art'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261003-BJ/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve existing BJ candidate before another build.'}
$icons=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/item-expansion/icon-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($icons.result -ne 'PASS' -or $icons.icons.Count -ne 6){throw 'Six native icons have not passed import.'}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV2.asset')){throw 'Unverified deferred reload catalog must stay outside Resources.'}
$paths=@(rg --files (Join-Path $projectRoot 'Assets/RuinRunner/Runtime') | Where-Object {$_ -match '\.cs$'})
$paths+=@('Assets/RuinRunner/Editor/TerrainAtlasPrebuild.cs','Assets/RuinRunner/Editor/NativeHeroAtlasPrebuild.cs','Assets/RuinRunner/Resources/Art/NativeHero/ArmedGripV3.asset','Assets/RuinRunner/Resources/Art/NativeHero/WeaponStandingV4.asset','ArtDirection/Review-20261003/item-expansion/icon-import.json','Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV3.asset','ArtDirection/Review-20261003/firearm-reload/editor-import-catalog.json') | ForEach-Object {Join-Path $projectRoot $_}
foreach($icon in $icons.icons){$paths+=Join-Path $projectRoot $icon.path;$paths+=Join-Path $projectRoot ($icon.path+'.meta')}
$catalog=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/firearm-reload/editor-import-catalog.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($catalog.result -ne 'PASS' -or $catalog.sources -ne 7629 -or $catalog.samples -ne 20480){throw 'Current five-family native reload catalog has not passed.'}
$native=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/firearm-reload/source-map.json') -Encoding UTF8 -Raw | ConvertFrom-Json
foreach($row in $native.sources){$paths+=Join-Path $projectRoot $row.destination;$paths+=Join-Path $projectRoot ($row.destination+'.meta')}
$hit=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/hero-hurt/native-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($hit.images.Count -ne 5 -or @($hit.images | Where-Object {!$_.unchangedSourcePixels}).Count -ne 0){throw 'Native player hit source import is not verified.'}
foreach($frame in $hit.images){$paths+=Join-Path $projectRoot $frame.path;$paths+=Join-Path $projectRoot ($frame.path+'.meta')}
$paths+=Join-Path $projectRoot 'ArtDirection/Review-20261003/hero-hurt/native-import.json'
$paths+=Join-Path $projectRoot 'Assets/StreamingAssets/ThirdPartyNotices/IceMaan-VFX-Essentials.txt'
$orc=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/boar-motion/native-orc-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($orc.images.Count -ne 168 -or @($orc.images | Where-Object {!$_.unchangedSourcePixels}).Count -ne 0){throw 'Native Orc3 source import is not verified.'}
foreach($frame in $orc.images){$paths+=Join-Path $projectRoot $frame.path;$paths+=Join-Path $projectRoot ($frame.path+'.meta')}
$paths+=Join-Path $projectRoot 'ArtDirection/Review-20261003/boar-motion/native-orc-import.json'
$paths+=Join-Path $projectRoot 'Assets/StreamingAssets/ThirdPartyNotices/NativeActors.txt'
$golem=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/tidekeeper-motion/native-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($golem.images.Count -ne 51 -or @($golem.images | Where-Object {!$_.unchangedSourcePixels -or !$_.visibleImportedPixelsUnchanged}).Count -ne 0){throw 'Native guardian pixel import has not passed.'}
$motion=Get-Content -LiteralPath (Join-Path $projectRoot 'Logs/Validation/2026-10-03-tidekeeper/motion-183452/result.txt') -Encoding UTF8 -Raw
if(!$motion.StartsWith('PASS')){throw 'Actual native boss motion has not passed.'}
foreach($frame in $golem.images){$paths+=Join-Path $projectRoot $frame.path;$paths+=Join-Path $projectRoot ($frame.path+'.meta')}
$paths+=Join-Path $review 'native-import.json'
$hazard=Get-Content -LiteralPath (Join-Path $review 'native-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($hazard.images.Count -ne 30 -or @($hazard.images | Where-Object {!$_.unchangedSourcePixels}).Count -ne 0){throw 'Native hazard source check has not passed.'}
if(!(Get-Content -LiteralPath (Join-Path $projectRoot 'Logs/Validation/2026-10-03-boss-hazards/motion-191839/result.txt') -Raw -Encoding UTF8).StartsWith('PASS')){throw 'Native hazard actual motion has not passed.'}
foreach($frame in $hazard.images){$paths+=Join-Path $projectRoot $frame.path;$paths+=Join-Path $projectRoot ($frame.path+'.meta')}
$paths+=Join-Path $projectRoot 'Assets/RuinRunner/Resources/Audio/Combat/hazard-water.wav'
$paths+=Join-Path $projectRoot 'Assets/RuinRunner/Resources/Audio/Combat/hazard-water.wav.meta'
$paths+=Join-Path $projectRoot 'Assets/StreamingAssets/ThirdPartyNotices/NinjaAdventure-SOURCE.txt'
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/sealheart-ancient'
$ancient=Get-Content -LiteralPath (Join-Path $review 'native-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($ancient.images.Count -ne 192 -or @($ancient.images | Where-Object {!$_.unchangedSourcePixels}).Count){throw 'Native Ancient source import is incomplete.'}
$accepted=Get-Content -LiteralPath (Join-Path $review 'editor-motion.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($accepted.result -ne 'PASS' -or !(Get-Content -LiteralPath (Join-Path $projectRoot ($accepted.folder+'/result.txt')) -Encoding UTF8 -Raw).StartsWith('PASS')){throw 'Current native motion is not verified.'}
foreach($frame in $ancient.images){$paths+=Join-Path $projectRoot $frame.path;$paths+=Join-Path $projectRoot ($frame.path+'.meta')}
$paths+=Join-Path $review 'native-import.json'
$paths+=Join-Path $projectRoot 'Assets/StreamingAssets/ThirdPartyNotices/Foozle-Ancient-Boss.txt'
$snapshot=@($paths | ForEach-Object {[pscustomobject]@{path=[IO.Path]::GetFullPath($_);sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()}})
$snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-source-BJ.json') -Encoding utf8
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-BJ.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json
