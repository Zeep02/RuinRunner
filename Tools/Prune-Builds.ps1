[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet('All', 'Builds', 'Packages')][string]$Kind = 'All',
    [string]$CompletedPath,
    [switch]$Apply
)

# Preview by default. A successful build plus its two predecessors are retained.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/')
if (!(Test-Path -LiteralPath (Join-Path $root 'Assets') -PathType Container) -or
    !(Test-Path -LiteralPath (Join-Path $root 'ProjectSettings') -PathType Container)) {
    throw "Not a Unity project: $root"
}

function Assert-SafePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (!$full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the project: $full"
    }
    $cursor = $full
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing linked path: $cursor"
            }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $full
}

function Get-PlayerData([string]$Directory) {
    # A legacy candidate accidentally used an .exe-named directory as its output.
    $locations = @($Directory) + @(Get-ChildItem -LiteralPath $Directory -Directory -Filter '*.exe' |
        Where-Object { !($_.Attributes -band [IO.FileAttributes]::ReparsePoint) } |
        ForEach-Object { $_.FullName })
    foreach ($location in $locations) {
        foreach ($exe in Get-ChildItem -LiteralPath $location -File -Filter '*.exe') {
            $data = Join-Path $location ($exe.BaseName + '_Data/globalgamemanagers')
            if (Test-Path -LiteralPath $data -PathType Leaf) { return Get-Item -LiteralPath $data }
        }
    }
    return $null
}

$groups = @()
if ($Kind -in 'All', 'Builds') { $groups += 'Builds' }
if ($Kind -in 'All', 'Packages') { $groups += 'deliverables' }
$completed = $null
if ($CompletedPath) {
    $completed = Assert-SafePath $CompletedPath
    $parent = [IO.Path]::GetDirectoryName($completed)
    if (!(@($groups | ForEach-Object { Join-Path $root $_ }) -contains $parent)) {
        throw 'CompletedPath must be a direct child of the selected Builds or deliverables folder.'
    }
}

$candidates = @()
foreach ($group in $groups) {
    $directory = Join-Path $root $group
    if (!(Test-Path -LiteralPath $directory)) { continue }
    $null = Assert-SafePath $directory
    foreach ($item in Get-ChildItem -LiteralPath $directory -Directory -Force) {
        if ($item.Name -eq '.clean-staging') { continue }
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
        $stamp = $null
        if ($group -eq 'Builds') {
            $data = Get-PlayerData $item.FullName
            if ($null -eq $data) { continue }
            $statePath = Join-Path $item.FullName 'build-result.json'
            if (Test-Path -LiteralPath $statePath) {
                $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($state.status -ne 'Succeeded') { continue }
            }
            $stamp = $data.LastWriteTimeUtc
            $marker = Join-Path $item.FullName '.build-retention.json'
            if (Test-Path -LiteralPath $marker) {
                $stamp = [DateTime]::Parse((Get-Content -LiteralPath $marker -Raw -Encoding UTF8 | ConvertFrom-Json).completedUtc).ToUniversalTime()
            }
        } else {
            $manifestPath = Join-Path $item.FullName 'manifest.json'
            if (!(Test-Path -LiteralPath $manifestPath) -or
                !(Test-Path -LiteralPath (Join-Path $item.FullName 'public-config.json')) -or
                !(Test-Path -LiteralPath (Join-Path $item.FullName 'Server') -PathType Container)) { continue }
            $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($manifest.status -ne 'candidate') { continue }
            $stamp = [DateTime]::Parse($manifest.createdUtc).ToUniversalTime()
        }
        if ($item.FullName -eq $completed) { $stamp = [DateTime]::UtcNow }
        $candidates += [pscustomobject]@{ group = $group; path = $item.FullName; completedUtc = $stamp }
    }
}
if ($completed -and !(@($candidates | ForEach-Object { $_.path }) -contains $completed)) {
    throw "Completed output is missing, incomplete, or not successful: $completed"
}

# Query failure aborts pruning. Running players and packaged servers are never removed.
$processPaths = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.ExecutablePath } | ForEach-Object { $_.ExecutablePath })
$kept = @(); $removed = @(); $skipped = @(); $planned = @()
foreach ($group in $groups) {
    $ordered = @($candidates | Where-Object { $_.group -eq $group } | Sort-Object @{Expression='completedUtc';Descending=$true}, @{Expression='path';Descending=$true})
    $kept += @($ordered | Select-Object -First 3 | ForEach-Object { $_.path })
    foreach ($candidate in @($ordered | Select-Object -Skip 3)) {
        $boundary = $candidate.path + [IO.Path]::DirectorySeparatorChar
        if (@($processPaths | Where-Object { $_.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) }).Count) {
            $skipped += $candidate.path
            continue
        }
        $path = Assert-SafePath $candidate.path
        # Validate the entire tree before recursive removal; do not follow junctions.
        $entries = @(Get-ChildItem -LiteralPath $path -Recurse -Force)
        if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
            $skipped += $path
            continue
        }
        $bytes = ($entries | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
        $planned += [pscustomobject]@{path=$path; bytes=[long]$bytes}
    }
}

if ($Apply) {
    if ($completed -and ([IO.Path]::GetDirectoryName($completed) -eq (Join-Path $root 'Builds'))) {
        @{completedUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $completed '.build-retention.json') -Encoding UTF8
    }
    foreach ($candidate in $planned) {
        $path = Assert-SafePath $candidate.path
        Remove-Item -LiteralPath $path -Recurse -Force
        $removed += $candidate
    }
}
$removedBytes = 0L
foreach ($entry in $removed) { $removedBytes += $entry.bytes }
$report = [pscustomobject]@{
    applied=[bool]$Apply; keepCount=3; kept=$kept; skippedInUseOrLinked=$skipped
    planned=$planned; removed=$removed; removedBytes=$removedBytes
}
if ($Apply) {
    $logDirectory = Join-Path $root 'Logs'
    $null = Assert-SafePath $logDirectory
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $logDirectory 'build-retention-latest.json') -Encoding UTF8
}
$report
