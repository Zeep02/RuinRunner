$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($pair in @(@('BuildChargerCandidate20261003.ps1','BuildTerrainCandidate20261003.ps1'),@('ReadChargerBuild20261003.ps1','ReadTerrainBuild20261003.ps1'),@('RunChargerCandidate20261003.ps1','RunTerrainCandidate20261003.ps1'))){
    $source=Join-Path $PSScriptRoot $pair[0]
    $destination=Join-Path $PSScriptRoot $pair[1]
    if(Test-Path -LiteralPath $destination){throw "Preserve existing $destination"}
    $text=Get-Content -LiteralPath $source -Encoding UTF8 -Raw
    $text=$text.Replace("'ArtDirection/Review-20261003/boar-motion'","'ArtDirection/Review-20261003/terrain-composition'").Replace('boar-motion/build-BC-summary.json','terrain-composition/build-BD-summary.json')
    $text=$text.Replace('20261003-BC','20261003-BD').Replace('-BC.json','-BD.json').Replace('-BC-summary.json','-BD-summary.json').Replace(' BC ',' BD ').Replace('BC fixture','BD fixture')
    $text=$text.Replace('ChargerMotionBC-','TerrainCompositionBD-').Replace('2026-10-03-charger/windows-','2026-10-03-terrain-composition/windows-').Replace("'-BC-'","'-BD-'")
    [IO.File]::WriteAllText($destination,$text,[Text.UTF8Encoding]::new($false))
}
Write-Output 'Prepared guarded BD terrain build and Windows validation scripts; source/actor guards retained.'
