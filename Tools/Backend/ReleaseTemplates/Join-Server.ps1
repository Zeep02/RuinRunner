param([string]$Address)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'public-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$folder=Join-Path $env:LOCALAPPDATA 'RuinRunner'
$remembered=Join-Path $folder 'server-address.txt'
$last=if(Test-Path -LiteralPath $remembered){(Get-Content -LiteralPath $remembered -Raw -Encoding UTF8).Trim()}else{''}

function Test-PrivateAddress([string]$Text){
    $ip=$null
    if(![Net.IPAddress]::TryParse($Text,[ref]$ip) -or $ip.AddressFamily -ne 'InterNetwork'){return $false}
    if([Net.IPAddress]::IsLoopback($ip)){return $true}
    $b=$ip.GetAddressBytes()
    return $b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168) -or ($b[0] -eq 169 -and $b[1] -eq 254) -or ($b[0] -eq 100 -and $b[1] -ge 64 -and $b[1] -le 127)
}
function ConvertTo-ServerUrl([string]$Value){
    $value=$Value.Trim()
    if($value -notmatch '^[a-zA-Z][a-zA-Z0-9+.-]*://'){$value='http://'+$value}
    $uri=[Uri]$value
    if($uri.IsDefaultPort -and $uri.Scheme -eq 'http'){return "http://$($uri.Host):$($config.apiPort)/"}
    return $uri.GetLeftPart([UriPartial]::Authority)+'/'
}

while($true){
    if(!$Address){
        $prompt=if($last){"서버 주소를 입력하세요 (Enter: $last)"}else{'서버 주소를 입력하세요 (예: 192.168.0.10)'}
        $Address=Read-Host $prompt
        if(!$Address){$Address=$last}
        if(!$Address){continue}
    }
    try{$url=ConvertTo-ServerUrl $Address}catch{Write-Host '주소 형식이 올바르지 않습니다.' -ForegroundColor Yellow;$Address='';continue}
    $target=[Uri]$url
    if($target.Scheme -eq 'http' -and !(Test-PrivateAddress $target.Host)){
        Write-Host '공인 IP와 도메인은 암호화 없이 접속할 수 없습니다. 공유기 내부 주소, VPN(예: Tailscale) 주소, 또는 https 주소를 입력하세요.' -ForegroundColor Yellow
        $Address='';continue
    }
    Write-Host "연결 확인: $url"
    try{
        $health=Invoke-RestMethod -Uri ($url+'health/ready') -TimeoutSec 5
        if($health.status -eq 'ready'){break}
        Write-Host '서버는 응답했지만 아직 준비되지 않았습니다.' -ForegroundColor Yellow
    }catch{
        Write-Host '서버에 연결하지 못했습니다. 주소, 같은 네트워크인지, 서버 PC의 "Start Server.cmd" 실행과 방화벽을 확인하세요.' -ForegroundColor Yellow
    }
    $Address=''
}
New-Item -ItemType Directory -Force -Path $folder | Out-Null
[IO.File]::WriteAllText($remembered,$Address.Trim(),[Text.UTF8Encoding]::new($false))
Write-Host '연결되었습니다. 게임을 시작합니다.' -ForegroundColor Green
& (Join-Path $PSScriptRoot 'Start-Game.ps1') -ApiUrl $url
