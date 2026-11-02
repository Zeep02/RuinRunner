$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/boss-hazard-art'
$started=Get-Content -LiteralPath (Join-Path $review 'build-start-BG.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$expected=if($started.data.result -is [string]){$started.data.result | ConvertFrom-Json}else{$started.data.result}
$response=unity command build_status --project-path $projectRoot --format json
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
if($report.buildId -ne $expected.buildId){throw 'The Editor reports a different build. Preserve BG evidence.'}
$response | Set-Content -LiteralPath (Join-Path $review 'build-BG.json') -Encoding utf8
$summary=$report | Select-Object status,buildId,result,platform,outputPath,totalSizeBytes,buildTimeMs,buildStartedAt,buildEndedAt,totalWarnings,totalErrors
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-BG-summary.json') -Encoding utf8
$summary | ConvertTo-Json
if($report.status -ne 'completed' -or $report.result -ne 'Succeeded'){return}
$rows=@($report.packedAssets | ForEach-Object {$_.contents})
$rawNative=@($rows | Where-Object {$_.type -eq 'Texture2D' -and $_.assetPath -match '/ArtSources/(Hero|Archive)/'})
$archived=@($rows | Where-Object {$_.assetPath -match '/ArtSources/Archive/'})
$deferred=@($rows | Where-Object {$_.assetPath -match 'WeaponReloadV2'})
if($rawNative.Count -ne 0 -or $archived.Count -ne 0 -or $deferred.Count -ne 0){throw 'Raw source, retired art or unverified reload was bundled. Preserve detailed report.'}
$source=Get-Content -LiteralPath (Join-Path $review 'build-source-BG.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$changes=@($source | Where-Object {(Get-FileHash -LiteralPath $_.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $_.sha256})
if($changes.Count -ne 0){$changes | Select-Object path | ConvertTo-Json;throw 'Build sources changed during build.'}
$proof=[ordered]@{buildId=$report.buildId;sourceFiles=$source.Count;sourceChanges=$changes.Count;retiredObjects=$archived.Count;rawNativeTextures=$rawNative.Count;deferredReloadObjects=$deferred.Count;scope='Actual clean Windows report/source identity; not gameplay or visual approval.'}
$proof | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-integrity-BG.json') -Encoding utf8
$proof | ConvertTo-Json
