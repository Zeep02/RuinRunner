$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261001/melee-fx-range'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261001-AT/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve existing AT candidate before another build.'}
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-AT.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json

