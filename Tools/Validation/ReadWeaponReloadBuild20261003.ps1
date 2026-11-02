$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/weapon-reload'
$started=Get-Content -LiteralPath (Join-Path $review 'build-start-AX.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$expected=if($started.data.result -is [string]){$started.data.result | ConvertFrom-Json}else{$started.data.result}
$response=unity command build_status --project-path $projectRoot --format json
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
if($report.buildId -ne $expected.buildId){throw 'The Editor reports a different build. Preserve the completed AX evidence.'}
$response | Set-Content -LiteralPath (Join-Path $review 'build-AX.json') -Encoding utf8
$summary=$report | Select-Object status,buildId,result,platform,outputPath,totalSizeBytes,buildTimeMs,buildStartedAt,buildEndedAt,totalWarnings,totalErrors
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-AX-summary.json') -Encoding utf8
$summary | ConvertTo-Json
if($report.status -ne 'completed' -or $report.result -ne 'Succeeded'){return}
$rows=@($report.packedAssets | ForEach-Object {$_.contents})
$rawNative=@($rows | Where-Object {$_.type -eq 'Texture2D' -and $_.assetPath -match '/ArtSources/(Hero|Archive)/'})
if($rawNative.Count -ne 0){throw 'Unpacked native source textures were still shipped. Inspect preserved build report.'}
$archived=@($rows | Where-Object {$_.assetPath -match '/ArtSources/Archive/'})
if($archived.Count -ne 0){throw 'Retired art was still bundled. Inspect preserved build report.'}
$baseline=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261002/native-frame-atlas/build-AW-summary.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$textures=@($rows | Where-Object {$_.type -eq 'Texture2D'})
$packedProof=[ordered]@{buildId=$report.buildId;baselineBuildId=$baseline.buildId;baselineBytes=$baseline.totalSizeBytes;candidateBytes=$report.totalSizeBytes;sizeDeltaBytes=($report.totalSizeBytes-$baseline.totalSizeBytes);rawNativeTextures=$rawNative.Count;retiredObjects=$archived.Count;textureObjects=$textures.Count;textureBytes=($textures | Measure-Object sizeBytes -Sum).Sum;scope='Actual clean Windows build report; no quality compression, source/metadata preserved.'}
$packedProof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-footprint-AX.json') -Encoding utf8
$packedProof | ConvertTo-Json
$source=Get-Content -LiteralPath (Join-Path $review 'build-source-AX.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$changes=@($source | Where-Object {(Get-FileHash -LiteralPath (Join-Path $projectRoot $_.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256})
if($changes.Count -ne 0){$changes | Select-Object path | ConvertTo-Json;throw 'Build sources changed during build.'}
