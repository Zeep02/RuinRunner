# Claude Code 용 pixelfx·PixelLab 연동 설치. 프로젝트 루트에서 실행: powershell -ExecutionPolicy Bypass -File Tools\AgentSetup\Install-ClaudeCode.ps1
# 원격 도구는 .claude/ 와 .mcp.json 을 쓸 수 없어서 이 스크립트로 복사한다. 기존 파일은 덮어쓰기 전에 .bak 으로 남긴다.
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$src = $PSScriptRoot

foreach ($skill in Get-ChildItem (Join-Path $src "claude-skills") -Directory) {
    $dest = Join-Path $root ".claude\skills\$($skill.Name)"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $skill.FullName "SKILL.md") (Join-Path $dest "SKILL.md") -Force
    Write-Host "skill: $dest"
}

$mcp = Join-Path $root ".mcp.json"
$new = Get-Content (Join-Path $src "mcp.json") -Raw -Encoding UTF8 | ConvertFrom-Json
if (Test-Path $mcp) {
    Copy-Item $mcp "$mcp.bak" -Force
    $cur = Get-Content $mcp -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $cur.mcpServers) { $cur | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) }
    $cur.mcpServers | Add-Member -NotePropertyName pixellab -NotePropertyValue $new.mcpServers.pixellab -Force
    $new = $cur
}
$json = $new | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($mcp, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "mcp: $mcp (pixellab)"

if (-not [Environment]::GetEnvironmentVariable("PIXELLAB_API_TOKEN", "User")) {
    Write-Host ""
    Write-Host "PIXELLAB_API_TOKEN 이 없습니다. pixellab.ai 에서 토큰을 받아 다음을 실행한 뒤 터미널을 다시 여세요:"
    Write-Host '  setx PIXELLAB_API_TOKEN "<토큰>"'
}
