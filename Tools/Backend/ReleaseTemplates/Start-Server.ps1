param([string]$DataRoot,[int]$ApiPort=0,[int]$DatabasePort=0,[switch]$SkipFirewall)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'public-config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$ApiPort){$ApiPort=[int]$config.apiPort}
& (Join-Path $PSScriptRoot 'Start-LocalBackend.ps1') -DataRoot $DataRoot -ApiPort $ApiPort -DatabasePort $DatabasePort -Network | Out-Null

# 다른 PC가 들어오려면 방화벽에서 이 포트를 열어야 한다. 같은 서브넷에서 오는 연결만 허용한다.
if(!$SkipFirewall){
    $ruleName="RuinRunner Server $ApiPort"
    if(!(Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)){
        Write-Host '다른 PC의 접속을 허용하려면 방화벽 규칙이 필요합니다. 관리자 권한 창이 뜨면 [예]를 눌러 주세요.'
        $command="New-NetFirewallRule -DisplayName '$ruleName' -Direction Inbound -Protocol TCP -LocalPort $ApiPort -RemoteAddress LocalSubnet -Action Allow -Profile Any | Out-Null"
        try{
            $elevated=Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-Command',$command)
            $null=$elevated.Handle
            $elevated.WaitForExit()
        }catch{Write-Warning '방화벽 규칙을 추가하지 못했습니다. 다른 PC에서 접속이 안 되면 Windows 방화벽에서 이 포트를 직접 허용하세요.'}
        if(!(Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)){Write-Warning '방화벽 규칙이 없습니다. 다른 PC에서 접속이 막힐 수 있습니다.'}
    }
}

# 인터넷에 연결된 어댑터의 주소를 먼저 보여 준다.
$addresses=@(Get-NetIPConfiguration -ErrorAction SilentlyContinue | Where-Object {$_.IPv4Address -and $_.NetAdapter.Status -eq 'Up'} |
    Sort-Object {if($_.IPv4DefaultGateway){0}else{1}} | ForEach-Object {$_.IPv4Address.IPAddress} | Where-Object {$_ -notlike '169.254.*'})
Write-Host ''
Write-Host '서버가 켜졌습니다.' -ForegroundColor Green
Write-Host '다른 PC에서 "Join Server.cmd"를 실행하고 아래 주소를 입력하세요.'
Write-Host ''
if($addresses.Count -eq 0){Write-Host '  네트워크 주소를 찾지 못했습니다. 네트워크 연결을 확인하세요.' -ForegroundColor Yellow}
function Test-PrivateAddress([string]$Text){
    $b=([Net.IPAddress]::Parse($Text)).GetAddressBytes()
    return $b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168) -or ($b[0] -eq 100 -and $b[1] -ge 64 -and $b[1] -le 127)
}
$usable=@($addresses | Where-Object {Test-PrivateAddress $_})
foreach($address in $usable){Write-Host ("  {0}:{1}" -f $address,$ApiPort) -ForegroundColor Cyan}
if($addresses.Count -gt 0 -and $usable.Count -eq 0){
    Write-Host '  이 PC는 공인 IP만 있어서 그 주소로는 접속할 수 없습니다.' -ForegroundColor Yellow
    Write-Host '  공유기에 연결하거나, 두 PC에 VPN(예: Tailscale)을 설치한 뒤 다시 실행하세요.' -ForegroundColor Yellow
}
Write-Host ''
Write-Host '이 PC에서 게임을 하려면 "Start Local Game.cmd"를 실행하면 됩니다.'
Write-Host '서버를 끄려면 "Stop Local Backend.cmd"를 실행하세요. 이 창은 닫아도 서버는 계속 켜져 있습니다.'
