param([switch]$ResumeAfterImport)
$ErrorActionPreference = 'Stop'
$project = 'F:/codex/LoglikeCap'
$output = Join-Path $project ('Logs/Validation/2026-09-21-external-slash/setup-' + (Get-Date -Format 'HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
function Invoke-ReviewUnity([string]$label, [string[]]$arguments) {
    $response = & unity command @arguments --project-path $project --format json
    $exit = $LASTEXITCODE
    $response | Set-Content -LiteralPath (Join-Path $output ($label + '.json')) -Encoding utf8
    $data = ($response -join "`n") | ConvertFrom-Json
    if ($exit -ne 0 -or -not $data.success) { throw ($response -join "`n") }
    if ($data.data.result.success -eq $false) { throw ($response -join "`n") }
    return $data
}
function Wait-ReviewCompilation {
    for($attempt=0;$attempt -lt 12;$attempt++) {
        $state=Invoke-ReviewUnity ('compile-status-'+$attempt) @('recompile_status')
        if($state.data.result.status -ne 'compiling') { return }
        Start-Sleep -Milliseconds 500
    }
    throw 'Compilation is still running; inspect recorded status before continuing.'
}
if(-not $ResumeAfterImport) {
    $compile = Invoke-ReviewUnity 'compile' @('recompile')
    Wait-ReviewCompilation
    $import = Invoke-ReviewUnity 'import' @('run_script','--file','Tools/Validation/ImportNinjaSlashes.cs')
}
Wait-ReviewCompilation
$play = Invoke-ReviewUnity 'play' @('editor_play')
# Entering Play Mode reloads the Pipeline assembly after the command response.
# Give that documented mode transition time to release and reopen its listener.
Start-Sleep -Milliseconds 1800
$launch = Invoke-ReviewUnity 'launch' @('run_script','--file','Tools/Validation/ExternalSlashFixture.cs')
[pscustomobject]@{ setupLogs=$output; import=$import.data.result.result; capture=$launch.data.result.result } | ConvertTo-Json
