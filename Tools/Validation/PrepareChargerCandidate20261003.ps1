$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$encoding=[Text.UTF8Encoding]::new($false)
foreach($prefix in @('Build','Read','Run')){
    $oldName=if($prefix -eq 'Read'){'ReadEnemyReactionBuild20261003.ps1'}else{$prefix+'EnemyReactionCandidate20261003.ps1'}
    $newName=if($prefix -eq 'Read'){'ReadChargerBuild20261003.ps1'}else{$prefix+'ChargerCandidate20261003.ps1'}
    if(Test-Path -LiteralPath (Join-Path $PSScriptRoot $newName)){throw 'One-time preparation only; preserve the existing candidate scripts and subsequent fixes.'}
    $text=[IO.File]::ReadAllText((Join-Path $PSScriptRoot $oldName),$encoding)
    $text=$text.Replace('enemy-reactions','boar-motion').Replace('2026-10-03-boar-motion','2026-10-03-charger').Replace('BB','BC').Replace('EnemyReaction','ChargerMotion')
    if($prefix -eq 'Build'){
        $insert=@'
$orc=Get-Content -LiteralPath (Join-Path $projectRoot 'ArtDirection/Review-20261003/boar-motion/native-orc-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($orc.images.Count -ne 168 -or @($orc.images | Where-Object {!$_.unchangedSourcePixels}).Count -ne 0){throw 'Native Orc3 source import is not verified.'}
foreach($frame in $orc.images){$paths+=Join-Path $projectRoot $frame.path;$paths+=Join-Path $projectRoot ($frame.path+'.meta')}
$paths+=Join-Path $projectRoot 'ArtDirection/Review-20261003/boar-motion/native-orc-import.json'
$paths+=Join-Path $projectRoot 'Assets/StreamingAssets/ThirdPartyNotices/NativeActors.txt'
'@
        $text=$text.Replace('$snapshot=@(',($insert+"`n"+'$snapshot=@('))
    }
    if($prefix -eq 'Run'){$text=$text.Replace("'--enemy-reaction-fixtures'","'--enemy-reaction-fixtures','--charger-motion-fixtures'").Replace('enemy anticipation/death/pause fixtures','enemy anticipation/death/pause and four-direction native charger fixtures')}
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot $newName),$text,$encoding)
}
