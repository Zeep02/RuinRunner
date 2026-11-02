param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $root 'Backend/.local'
$private=Join-Path $local 'private'
$dest=Join-Path $private ('backups/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $dest -Force | Out-Null
$env:PGPASSFILE=Join-Path $private 'pgpass'
try{
    & (Join-Path $local 'postgresql/pgsql/bin/pg_dump.exe') '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' '-d' 'ruinrunner' '-Fc' '-f' (Join-Path $dest 'ruinrunner.dump')
    if($LASTEXITCODE -ne 0){throw 'Database backup failed'}
    @{utc=[DateTime]::UtcNow.ToString('O');format='PostgreSQL custom archive';sha256=(Get-FileHash -LiteralPath (Join-Path $dest 'ruinrunner.dump') -Algorithm SHA256).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dest 'receipt.json') -Encoding UTF8
    Write-Output "Database backup: $dest"
}finally{Remove-Item Env:PGPASSFILE -ErrorAction SilentlyContinue}
