param([Parameter(Mandatory=$true)][string]$BackupFile,[Parameter(Mandatory=$true)][string]$NewDatabase)
$ErrorActionPreference='Stop'
if($NewDatabase -notmatch '^rr_restore_[a-z0-9_]{1,40}$'){throw 'Use a new name starting rr_restore_. Existing databases are never overwritten.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $root 'Backend/.local'
$pg=Join-Path $local 'postgresql/pgsql/bin'
$backup=(Resolve-Path -LiteralPath $BackupFile).Path
$env:PGPASSFILE=Join-Path $local 'private/pgpass'
try{
    $existing=& (Join-Path $pg 'psql.exe') '-X' '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' '-d' 'postgres' '-Atc' "SELECT 1 FROM pg_database WHERE datname='$NewDatabase'"
    if($LASTEXITCODE -ne 0 -or $existing -eq '1'){throw 'Database check failed or destination already exists'}
    & (Join-Path $pg 'createdb.exe') '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' $NewDatabase
    if($LASTEXITCODE -ne 0){throw 'Restore database creation failed'}
    & (Join-Path $pg 'pg_restore.exe') '-h' '127.0.0.1' '-p' '55432' '-U' 'postgres' '-d' $NewDatabase '--exit-on-error' $backup
    if($LASTEXITCODE -ne 0){throw 'Restore failed; partial new database preserved for diagnosis'}
    Write-Output "Restored into isolated database $NewDatabase; active API database unchanged."
}finally{Remove-Item Env:PGPASSFILE -ErrorAction SilentlyContinue}
