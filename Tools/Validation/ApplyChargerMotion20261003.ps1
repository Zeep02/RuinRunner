$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/boar-motion'
$encoding=[Text.UTF8Encoding]::new($false)
$manifest=Get-Content -LiteralPath (Join-Path $review 'native-orc-import.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($manifest.images.Count -ne 168){throw 'Wait for the complete exact native import.'}
foreach($name in @('ExternalCreatureArt','EnemyPixelAnimator')){
    $relative=if($name -eq 'ExternalCreatureArt'){'Assets/RuinRunner/Runtime/Feedback/'}else{'Assets/RuinRunner/Runtime/Enemies/'}
    $destination=Join-Path $projectRoot ($relative+$name+'.cs')
    if((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath (Join-Path $review ('before/'+$name+'.cs'))).Hash){throw ('Source changed since backup: '+$name)}
}
foreach($name in @('ExternalCreatureArt','EnemyPixelAnimator')){
    $relative=if($name -eq 'ExternalCreatureArt'){'Assets/RuinRunner/Runtime/Feedback/'}else{'Assets/RuinRunner/Runtime/Enemies/'}
    [IO.File]::WriteAllText((Join-Path $projectRoot ($relative+$name+'.cs')),[IO.File]::ReadAllText((Join-Path $review ($name+'-proposed.cs')),$encoding),$encoding)
}
$ui=Join-Path $projectRoot 'Assets/RuinRunner/Runtime/Diagnostics/UiValidation.cs'
$text=[IO.File]::ReadAllText($ui,$encoding)
if($text.Contains('ChargerMotionFixtures')){throw 'Charger fixture is already applied.'}
$anchor='            Time.timeScale = 1;'+"`n"+'            for (var stage = 0; stage < CampaignStage.Count; stage++)'
$text=$text.Replace("`r`n","`n")
if(!$text.Contains($anchor)){throw 'Expected UI stage transition location is missing.'}
$insert=@'
            if (Array.IndexOf(args, "--charger-motion-fixtures") >= 0)
            {
                var charger = ChargerMotionFixtures();
                while (charger.MoveNext()) yield return charger.Current;
            }
'@
$text=$text.Replace($anchor,$insert+"`n"+$anchor)
$method=[IO.File]::ReadAllText((Join-Path $review 'windows-fixture-method.txt'),$encoding)
$text=$text.Replace('        private IEnumerator EnemyReactionFixtures()', $method+"`n"+'        private IEnumerator EnemyReactionFixtures()')
[IO.File]::WriteAllText($ui,$text,$encoding)
$controller=Join-Path $projectRoot 'Assets/RuinRunner/Runtime/Enemies/EnemyController.cs'
$text=[IO.File]::ReadAllText($controller,$encoding).Replace('The original boar pack has no death clip; fade its final pose without synthesizing art.','Only art without an authored death clip uses this fallback; do not synthesize a new pose.')
[IO.File]::WriteAllText($controller,$text,$encoding)
