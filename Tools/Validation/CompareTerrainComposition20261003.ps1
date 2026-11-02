$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$prefix=Join-Path $projectRoot 'Logs/Validation/2026-10-03-terrain-composition'
$before=Get-Content -LiteralPath (Join-Path $prefix 'review-173429/terrain.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$after=Get-Content -LiteralPath (Join-Path $prefix 'review-174515/terrain.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$checks=@()
for($stage=0;$stage -lt 3;$stage++){
    foreach($field in @('walkMaskSha256','walkableFineCells','colliders','ground')){
        if($before.stages[$stage].$field -ne $after.stages[$stage].$field){throw "Geometry changed: stage $stage $field"}
        $checks+="stage $stage identical $field"
    }
}
foreach($capture in @('review-173429','review-174150','review-174515')){
    $dir=Join-Path $prefix $capture
    if(Test-Path -LiteralPath (Join-Path $dir 'failure.txt')){throw "Review failed: $dir"}
    $files=@(Get-ChildItem -LiteralPath $dir -Filter '*.png' -File)
    if($files.Count -ne 18){throw "Incomplete capture: $dir"}
}
$report=[ordered]@{result='PASS';seed=9100;before='review-173429';intermediate='review-174150';after='review-174515';checks=$checks;paversBefore=$before.stages[0].paving;paversAfter=$after.stages[0].paving;scope='Three production maps, same camera/seed, geometry identity and complete frozen captures. Visual assessment recorded separately; no normal campaign or moving-camera claim.'}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/terrain-composition/geometry-comparison.json') -Encoding utf8
$report | ConvertTo-Json -Depth 5
