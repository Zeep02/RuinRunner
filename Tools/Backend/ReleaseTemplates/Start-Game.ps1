param([string]$ApiUrl,[string]$DataRoot,[string]$ValidationProfile,[int]$ApiPort=0,[int]$DatabasePort=0)
$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'public-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$ApiUrl){
    & (Join-Path $PSScriptRoot 'Start-LocalBackend.ps1') -DataRoot $DataRoot -ApiPort $ApiPort -DatabasePort $DatabasePort
    if(!$ApiPort){$ApiPort=[int]$config.apiPort}
    $ApiUrl="http://127.0.0.1:$ApiPort/"
}
# Plain HTTP is accepted on loopback and private networks only, the same rule as RuinBackendClient.
function Test-PrivateAddress([string]$Address){
    $ip=$null
    if(![Net.IPAddress]::TryParse($Address,[ref]$ip) -or $ip.AddressFamily -ne 'InterNetwork'){return $false}
    $b=$ip.GetAddressBytes()
    return $b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168) -or ($b[0] -eq 169 -and $b[1] -eq 254) -or ($b[0] -eq 100 -and $b[1] -ge 64 -and $b[1] -le 127)
}
$endpoint=[Uri]$ApiUrl
if(!$endpoint.IsAbsoluteUri -or $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or ($endpoint.Scheme -ne 'https' -and !($endpoint.Scheme -eq 'http' -and ($endpoint.IsLoopback -or (Test-PrivateAddress $endpoint.Host))))){throw 'Server address must be a private network IP over http, or any address over https, with no credentials or query.'}
$player=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('Game/'+$config.playerExecutable)))
$gameRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Game'))+[IO.Path]::DirectorySeparatorChar
if(!$player.StartsWith($gameRoot,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $player)){throw 'The configured Windows player was not found in this package.'}
$arguments=@()
if($ValidationProfile){
    if($ValidationProfile -notmatch '^[A-Za-z0-9_-]{1,80}$'){throw 'Invalid isolated validation profile name'}
    $arguments=@('--validation-profile','--validation-profile-name',$ValidationProfile)
}
$env:RUINRUNNER_API_URL=$endpoint.AbsoluteUri
try{
    if($arguments.Count){Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory (Split-Path $player) | Out-Null}
    else{Start-Process -FilePath $player -WorkingDirectory (Split-Path $player) | Out-Null}
}finally{Remove-Item Env:RUINRUNNER_API_URL -ErrorAction SilentlyContinue}
