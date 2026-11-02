$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$prune = Join-Path $project 'Tools/Prune-Builds.ps1'
$sandbox = Join-Path $project ('.work/retention-tests-' + [Guid]::NewGuid().ToString('N'))
$script:checks = 0
function Check([bool]$Condition, [string]$Name) {
    if (!$Condition) { throw "FAIL: $Name" }
    $script:checks++
    Write-Host "PASS: $Name"
}
function New-Fixture([string]$Name) {
    $directory = Join-Path $sandbox $Name
    foreach ($child in @('Assets','ProjectSettings','Builds','deliverables')) {
        New-Item -ItemType Directory -Path (Join-Path $directory $child) -Force | Out-Null
    }
    return $directory
}
function New-Player([string]$Root, [string]$Name, [int]$Age) {
    $directory = Join-Path $Root ('Builds/' + $Name)
    $data = Join-Path $directory 'Game_Data'
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'Game.exe'), 'fixture', [Text.Encoding]::UTF8)
    $manager = Join-Path $data 'globalgamemanagers'
    [IO.File]::WriteAllText($manager, 'fixture', [Text.Encoding]::UTF8)
    (Get-Item -LiteralPath $manager).LastWriteTimeUtc = [DateTime]::UtcNow.AddDays(-$Age)
    return $directory
}
try {
    $fixture = New-Fixture 'project with spaces and 한글'
    $players = @(1..5 | ForEach-Object { New-Player $fixture "candidate-$_" $_ })
    New-Item -ItemType Directory -Path (Join-Path $fixture 'Builds/unrelated-notes') | Out-Null
    $staged = New-Player $fixture '.clean-staging/Windows' 0
    $preview = & $prune -ProjectRoot $fixture
    Check ($preview.planned.Count -eq 2 -and $preview.removed.Count -eq 0) 'preview reports two old builds without deleting'
    Check (@($players | Where-Object {Test-Path -LiteralPath $_}).Count -eq 5) 'preview leaves all players intact'
    $result = & $prune -ProjectRoot $fixture -Apply
    Check ($result.removed.Count -eq 2 -and $result.kept.Count -eq 3) 'global retention keeps three newest successful players'
    Check ((Test-Path -LiteralPath $players[0]) -and !(Test-Path -LiteralPath $players[4])) 'order uses actual build data time'
    Check ((Test-Path -LiteralPath $staged) -and (Test-Path -LiteralPath (Join-Path $fixture 'Builds/unrelated-notes'))) 'staging and unrelated folders survive'
    $again = & $prune -ProjectRoot $fixture -Apply
    Check ($again.removed.Count -eq 0) 'second run is idempotent'

    $legacy = New-Player $fixture 'legacy/Player.exe' 100
    $result = & $prune -ProjectRoot $fixture -Apply
    Check ($result.removed.Count -eq 1 -and !(Test-Path -LiteralPath $legacy)) 'legacy nested executable directory is pruned'

    $completed = New-Player $fixture 'new-build-with-old-file-times' 99
    $result = & $prune -ProjectRoot $fixture -Kind Builds -CompletedPath $completed -Apply
    Check ($result.kept[0] -eq $completed -and $result.removed.Count -eq 1) 'just-completed build is always kept'
    Check (Test-Path -LiteralPath (Join-Path $completed '.build-retention.json')) 'completion marker persists sorting time'
    $failed = New-Player $fixture 'failed-build' 0
    @{status='Failed'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $failed 'build-result.json') -Encoding UTF8
    $rejected = $false
    try { & $prune -ProjectRoot $fixture -CompletedPath $failed -Apply | Out-Null } catch { $rejected = $true }
    Check $rejected 'failed completion cannot trigger pruning'
    Check (Test-Path -LiteralPath $completed) 'failed completion preserves previous successful build'
    $rejected = $false
    try { & $prune -ProjectRoot $fixture -CompletedPath (Join-Path $fixture 'Assets') -Apply | Out-Null } catch { $rejected = $true }
    Check $rejected 'non-build completed path is rejected'

    $empty = New-Fixture 'empty'
    $result = & $prune -ProjectRoot $empty -Apply
    Check ($result.removed.Count -eq 0 -and $result.kept.Count -eq 0) 'empty project is a safe no-op'

    $packages = New-Fixture 'packages'
    foreach ($age in 1..4) {
        $directory = Join-Path $packages "deliverables/package-$age"
        New-Item -ItemType Directory -Path (Join-Path $directory 'Server') -Force | Out-Null
        @{status='candidate';createdUtc=[DateTime]::UtcNow.AddDays(-$age).ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'manifest.json') -Encoding UTF8
        '{}' | Set-Content -LiteralPath (Join-Path $directory 'public-config.json') -Encoding UTF8
    }
    $manifest = Join-Path $packages 'deliverables/package-1/manifest.json'
    $hash = (Get-FileHash -LiteralPath $manifest).Hash
    $result = & $prune -ProjectRoot $packages -Kind Packages -Apply
    Check ($result.kept.Count -eq 3 -and $result.removed.Count -eq 1) 'packages have their own three-copy limit'
    Check ((Get-FileHash -LiteralPath $manifest).Hash -eq $hash) 'package manifest remains unchanged'

    $linked = New-Fixture 'linked'
    $players = @(1..4 | ForEach-Object {New-Player $linked "build-$_" $_})
    $external = Join-Path $sandbox 'external-preserved'
    New-Item -ItemType Directory -Path $external | Out-Null
    'keep' | Set-Content -LiteralPath (Join-Path $external 'keep.txt') -Encoding UTF8
    $junction = Join-Path $players[3] 'linked-data'
    New-Item -ItemType Junction -Path $junction -Target $external | Out-Null
    $result = & $prune -ProjectRoot $linked -Apply
    Check ($result.skippedInUseOrLinked.Count -eq 1 -and $result.removed.Count -eq 0) 'linked build tree is excluded from deletion'
    Check (Test-Path -LiteralPath (Join-Path $external 'keep.txt')) 'junction target is preserved'
    # Remove the junction itself, never recursively traverse its target.
    [IO.Directory]::Delete($junction)

    $active = New-Fixture 'active'
    $players = @(1..4 | ForEach-Object {New-Player $active "build-$_" $_})
    $runningExe = Join-Path $players[3] 'ping.exe'
    Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32/ping.exe') -Destination $runningExe
    $process = Start-Process -FilePath $runningExe -ArgumentList '-t','127.0.0.1' -WindowStyle Hidden -PassThru
    try {
        $result = & $prune -ProjectRoot $active -Apply
        Check ($result.skippedInUseOrLinked.Count -eq 1 -and (Test-Path -LiteralPath $players[3])) 'running executable protects its old build'
    } finally {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
        $process.Dispose()
    }
    $result = & $prune -ProjectRoot $active -Apply
    Check ($result.removed.Count -eq 1) 'old build is pruned after its process exits'
    Write-Host "Build retention: $script:checks checks passed."
} finally {
    $resolved = [IO.Path]::GetFullPath($sandbox)
    $boundary = [IO.Path]::GetFullPath((Join-Path $project '.work')) + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path' }
    if (Test-Path -LiteralPath $resolved) {
        $links = @(Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})
        if ($links.Count) { throw 'Test cleanup found a link; preserving sandbox' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
