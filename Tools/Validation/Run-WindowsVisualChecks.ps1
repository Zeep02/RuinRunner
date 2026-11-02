param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [ValidateSet('all','terrain')][string]$Scope='all'
)
$ErrorActionPreference='Stop'
$candidatePath=(Resolve-Path -LiteralPath $Executable).Path
$outputPath=[IO.Path]::GetFullPath($OutputRoot)
$checks=@(
    @{Name='smoke'; Flag='--smoke-test'; Width=1280; Height=720},
    @{Name='map'; Flag='--map-validation'; Width=1920; Height=1080},
    @{Name='ui-1280x720'; Flag='--ui-validation'; Width=1280; Height=720},
    @{Name='ui-1920x1080'; Flag='--ui-validation'; Width=1920; Height=1080},
    @{Name='ui-1460x900'; Flag='--ui-validation'; Width=1460; Height=900}
)
if($Scope -eq 'terrain') { $checks=$checks | Where-Object { $_.Name -in @('smoke','map','ui-1920x1080') } }
foreach($check in $checks) {
    $checkFolder=Join-Path $outputPath $check.Name
    New-Item -ItemType Directory -Path $checkFolder -Force | Out-Null
    $checkArgs=@($check.Flag,'--validation-profile','--validation-profile-name',('Visual-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$check.Name),
        '--validation-seed','824942335','--validation-capture-dir',('"'+$checkFolder+'"'),
        '-screen-fullscreen','0','-screen-width',[string]$check.Width,'-screen-height',[string]$check.Height,
        '-logFile',('"'+(Join-Path $checkFolder 'player.log')+'"'))
    # Render checks need the game window visible; SW_HIDE produces black captures.
    $checkProcess=Start-Process -FilePath $candidatePath -ArgumentList $checkArgs -WindowStyle Normal -PassThru
    $checkProcess.Id | Set-Content -LiteralPath (Join-Path $checkFolder 'process.pid') -Encoding UTF8
    Write-Output ('START '+$check.Name+' PID '+$checkProcess.Id)
    if(-not $checkProcess.WaitForExit(120000)) { throw ('Timeout '+$check.Name+'; process left open for diagnosis.') }
    $checkProcess.Refresh()
    $code=$checkProcess.ExitCode
    [pscustomobject]@{name=$check.Name;exitCode=$code;executable=$candidatePath;completed=(Get-Date).ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $checkFolder 'process-result.json') -Encoding UTF8
    Write-Output ('EXIT '+$check.Name+' '+$code)
    if($code -ne 0) { throw ('Validation failed: '+$check.Name) }
}
