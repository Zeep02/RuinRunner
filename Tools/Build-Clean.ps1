[CmdletBinding()]
param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
    [ValidateRange(60, 3600)]
    [int]$BuildTimeoutSeconds = 300,
    [ValidateRange(10, 300)]
    [int]$SmokeTimeoutSeconds = 45
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot '..'))
$buildsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Builds'))
$stagingRoot = [IO.Path]::GetFullPath((Join-Path $buildsRoot '.clean-staging'))
$stagingWindows = [IO.Path]::GetFullPath((Join-Path $stagingRoot 'Windows'))
$finalWindows = [IO.Path]::GetFullPath((Join-Path $buildsRoot 'Windows'))
$previousWindows = [IO.Path]::GetFullPath((Join-Path $buildsRoot ('Windows-previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
$validationLogs = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Logs\Validation'))
$buildLog = Join-Path $validationLogs 'latest-clean-build.log'
$smokeLog = Join-Path $validationLogs 'latest-clean-smoke.log'
$stagingExe = Join-Path $stagingWindows 'RuinRunnerPrototype.exe'
$finalExe = Join-Path $finalWindows 'RuinRunnerPrototype.exe'

function Assert-BuildPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = [IO.Path]::GetFullPath($Path)
    $boundary = $buildsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing filesystem operation outside the project Builds folder: $resolved"
    }
}

function Remove-BuildDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-BuildPath -Path $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Invoke-ProcessWithTimeout {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds,
        [Parameter(Mandatory = $true)][string]$Label,
        [switch]$VisibleWindow
    )

    $style = if ($VisibleWindow) { 'Normal' } else { 'Hidden' }
    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -WindowStyle $style
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            throw "$Label timed out after $TimeoutSeconds seconds."
        }

        $process.WaitForExit()
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

function Test-UnityLicenseFailure {
    if (-not (Test-Path -LiteralPath $buildLog -PathType Leaf)) {
        return $false
    }

    $pattern = 'Licensing initialization failed|LicenseClient.*refused|LicensingClient.*timeout|Failed to connect to licensing client|connection with the Unity Licensing Client has been lost'
    return $null -ne (Select-String -LiteralPath $buildLog -Pattern $pattern -ErrorAction SilentlyContinue)
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable was not found: $UnityPath"
}

if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets') -PathType Container)) {
    throw "Unity project Assets folder was not found: $projectRoot"
}

$runningEditor = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue
if ($null -ne $runningEditor) {
    throw 'Close the Unity Editor before running the clean command-line build. Unity Hub may stay open.'
}

New-Item -ItemType Directory -Path $buildsRoot -Force | Out-Null
New-Item -ItemType Directory -Path $validationLogs -Force | Out-Null
Remove-BuildDirectory -Path $stagingRoot
New-Item -ItemType Directory -Path $stagingWindows -Force | Out-Null

$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-accept-apiupdate',
    '-projectPath', ('"{0}"' -f $projectRoot),
    '-buildTarget', 'StandaloneWindows64',
    '-executeMethod', 'RuinRunner.EditorTools.BuildMenu.BuildWindowsDemo',
    '-buildOutput', ('"{0}"' -f $stagingWindows),
    '-logFile', ('"{0}"' -f $buildLog)
)

Write-Host '[1/3] Building a clean Windows player in staging...'
try {
    $buildExitCode = Invoke-ProcessWithTimeout -FilePath $UnityPath -ArgumentList $unityArguments -TimeoutSeconds $BuildTimeoutSeconds -Label 'Unity clean build'
}
catch {
    if (Test-UnityLicenseFailure) {
        throw "Unity licensing is not ready. Open Unity Hub, sign in, activate a Personal license, then run this command again. Log: $buildLog"
    }

    throw
}

if (Test-UnityLicenseFailure) {
    throw "Unity licensing is not ready. Open Unity Hub, sign in, activate a Personal license, then run this command again. Log: $buildLog"
}

if ($buildExitCode -ne 0) {
    throw "Unity exited with code $buildExitCode. Log: $buildLog"
}

if (-not (Test-Path -LiteralPath $stagingExe -PathType Leaf)) {
    throw "Unity reported success but the staged executable is missing. Log: $buildLog"
}

if ($null -eq (Select-String -LiteralPath $buildLog -SimpleMatch '[Build] Success:' -ErrorAction SilentlyContinue)) {
    throw "The expected prototype success marker is missing. Log: $buildLog"
}

Write-Host '[2/3] Running the staged executable smoke test...'
$smokeArguments = @('--smoke-test', '--validation-profile', '--validation-profile-name', ('Smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss')), '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-logFile', ('"{0}"' -f $smokeLog))
$smokeExitCode = Invoke-ProcessWithTimeout -FilePath $stagingExe -ArgumentList $smokeArguments -TimeoutSeconds $SmokeTimeoutSeconds -Label 'Prototype smoke test' -VisibleWindow

if ($smokeExitCode -ne 0) {
    throw "The staged executable smoke test exited with code $smokeExitCode. Log: $smokeLog"
}

if ($null -eq (Select-String -LiteralPath $smokeLog -SimpleMatch '[Smoke Test] PASS' -ErrorAction SilentlyContinue)) {
    throw "The staged executable did not emit the PASS marker. The current build was preserved. Log: $smokeLog"
}

Write-Host '[3/3] Promoting the verified build...'
Assert-BuildPath -Path $previousWindows
Assert-BuildPath -Path $finalWindows
Assert-BuildPath -Path $stagingWindows
if (Test-Path -LiteralPath $previousWindows) { throw "Previous build destination already exists: $previousWindows" }

if (Test-Path -LiteralPath $finalWindows -PathType Container) {
    Move-Item -LiteralPath $finalWindows -Destination $previousWindows
}

try {
    Move-Item -LiteralPath $stagingWindows -Destination $finalWindows
}
catch {
    if (-not (Test-Path -LiteralPath $finalWindows) -and (Test-Path -LiteralPath $previousWindows)) {
        Move-Item -LiteralPath $previousWindows -Destination $finalWindows
    }

    throw
}

if (-not (Test-Path -LiteralPath $finalExe -PathType Leaf)) {
    throw "Build promotion completed without the expected executable: $finalExe"
}

if (Test-Path -LiteralPath $stagingRoot -PathType Container) {
    Remove-Item -LiteralPath $stagingRoot -Force
}

# Retention runs only after both the build and its smoke test have passed.
try {
    $retention = & (Join-Path $scriptRoot 'Prune-Builds.ps1') -ProjectRoot $projectRoot -Kind Builds -CompletedPath $finalWindows -Apply
    Write-Host ("Retention : latest + 2 previous; removed {0} older builds" -f $retention.removed.Count)
} catch {
    Write-Warning "Build succeeded but retention failed: $_"
}

Write-Host ''
Write-Host 'CLEAN BUILD PASS'
Write-Host "Executable : $finalExe"
Write-Host "Build log  : $buildLog"
Write-Host "Smoke log  : $smokeLog"
if (Test-Path -LiteralPath $previousWindows -PathType Container) {
    Write-Host "Previous   : $previousWindows"
}
