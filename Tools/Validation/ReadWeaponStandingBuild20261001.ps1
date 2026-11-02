$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261001/weapon-carry'
$response=unity command build_status --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-AV.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$summary=$report | Select-Object status,buildId,result,platform,outputPath,totalSizeBytes,buildTimeMs,buildStartedAt,buildEndedAt,totalWarnings,totalErrors
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $review 'build-AV-summary.json') -Encoding utf8
$summary | ConvertTo-Json

