param([string]$DataRoot,[int]$ApiPort=0)
$ErrorActionPreference='Stop'
$package=[IO.Path]::GetFullPath($PSScriptRoot)
$config=Get-Content -LiteralPath (Join-Path $package 'public-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$DataRoot){$DataRoot=Join-Path $env:LOCALAPPDATA 'RuinRunner/LocalBackend'}
if(!$ApiPort){$ApiPort=[int]$config.apiPort}
$tokenPath=Join-Path ([IO.Path]::GetFullPath($DataRoot)) 'private/admin-token.txt'
if(!(Test-Path -LiteralPath $tokenPath)){throw 'Start the backend first (Start Local Game.cmd or Start Server.cmd); it creates the admin token.'}
try{$ready=(Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/health/ready" -TimeoutSec 3).status -eq 'ready'}catch{$ready=$false}
if(!$ready){throw 'The backend is not running on this PC. Start it first, then open the admin tool again.'}
# The page works only from this PC (loopback). The token goes to the clipboard, never into the URL.
Set-Clipboard -Value ([IO.File]::ReadAllText($tokenPath)).Trim()
Start-Process "http://127.0.0.1:$ApiPort/admin"
Write-Output 'Admin page opened. The admin token is on the clipboard: paste it into the page.'
