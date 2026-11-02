$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261004/hero-pixel-cleanup'
$destination=Join-Path $projectRoot 'Builds/CampaignCandidate-20261004-BV/RuinRunner.exe'
if(Test-Path -LiteralPath $destination){throw 'Preserve the existing BV build.'}
$python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$previousTaskPythonPath=$env:PYTHONPATH
try {
    $env:PYTHONPATH=Join-Path $projectRoot 'Tools/.python-deps'
    & $python (Join-Path $PSScriptRoot 'VerifyHeroPixelCleanup20261004.py')
    if($LASTEXITCODE -ne 0){throw 'Current body pixel regression gate failed.'}
} finally {$env:PYTHONPATH=$previousTaskPythonPath}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'Assets/RuinRunner/Resources/Art/NativeHero/WeaponReloadV2.asset')){throw 'Retired reload catalog must not be bundled.'}
$response=unity command build --target StandaloneWindows64 --outputPath $destination --options '["CleanBuildCache","DetailedBuildReport"]' --confirm true --project-path $projectRoot --format json
$response | Set-Content -LiteralPath (Join-Path $review 'build-start-BV.json') -Encoding utf8
$envelope=$response | ConvertFrom-Json
if(!$envelope.success){throw ($envelope.errors | ConvertTo-Json -Depth 8)}
$report=if($envelope.data.result -is [string]){$envelope.data.result | ConvertFrom-Json}else{$envelope.data.result}
$report | Select-Object status,buildId,outputPath,message | ConvertTo-Json
