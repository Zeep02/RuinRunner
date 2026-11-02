param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$local=Join-Path $root 'Backend/.local'
$downloads=Join-Path $local 'downloads'
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
$sdkUrl='https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip'
$sdkHash='24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430'
$sdkZip=Join-Path $downloads 'dotnet-sdk-10.0.401-win-x64.zip'
if(!(Test-Path -LiteralPath $sdkZip)){Invoke-WebRequest -Uri $sdkUrl -OutFile $sdkZip}
if((Get-FileHash -LiteralPath $sdkZip -Algorithm SHA512).Hash -ne $sdkHash){throw 'SDK archive checksum mismatch'}
$sdkDir=Join-Path $local 'dotnet'
if(!(Test-Path -LiteralPath (Join-Path $sdkDir 'dotnet.exe'))){Expand-Archive -LiteralPath $sdkZip -DestinationPath $sdkDir}
$pgUrl='https://sbp.enterprisedb.com/getfile.jsp?fileid=1260609'
$pgZip=Join-Path $downloads 'postgresql-18.6-windows-x64.zip'
if(!(Test-Path -LiteralPath $pgZip)){Invoke-WebRequest -Uri $pgUrl -OutFile $pgZip}
$pgDir=Join-Path $local 'postgresql'
if(!(Test-Path -LiteralPath (Join-Path $pgDir 'pgsql/bin/pg_ctl.exe'))){Expand-Archive -LiteralPath $pgZip -DestinationPath $pgDir}
@{sdk='10.0.401';sdkUrl=$sdkUrl;sdkSha512=$sdkHash;postgres='18.6';postgresUrl=$pgUrl;postgresSha256=(Get-FileHash -LiteralPath $pgZip -Algorithm SHA256).Hash;retrievedUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $downloads 'receipt.json') -Encoding UTF8
& (Join-Path $sdkDir 'dotnet.exe') --version
& (Join-Path $pgDir 'pgsql/bin/pg_ctl.exe') --version
