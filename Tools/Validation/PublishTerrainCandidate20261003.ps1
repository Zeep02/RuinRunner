$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$review=Join-Path $projectRoot 'ArtDirection/Review-20261003/terrain-composition'
$build=Get-Content -LiteralPath (Join-Path $review 'build-BD-summary.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($build.buildId -ne 'build_1742c94cca06' -or $build.result -ne 'Succeeded'){throw 'Unexpected BD candidate.'}
foreach($fixture in @('windows-smoke-BD-1280x720-175140','windows-ui-BD-1280x720-175216','windows-ui-BD-1920x1080-175325')){
    $proof=Get-Content -LiteralPath (Join-Path $projectRoot "Logs/Validation/2026-10-03-terrain-composition/$fixture/process.json") -Encoding UTF8 -Raw | ConvertFrom-Json
    if($proof.buildId -ne $build.buildId -or $proof.exitCode -ne 0){throw "Fixture failed: $fixture"}
}
$readmePath=Join-Path $projectRoot 'README.md'
$readme=Get-Content -LiteralPath $readmePath -Encoding UTF8 -Raw
$old='현재 Windows 후보는 `Builds/CampaignCandidate-20261003-BC/RuinRunner.exe`입니다.'
$new='현재 Windows 후보는 `Builds/CampaignCandidate-20261003-BD/RuinRunner.exe`입니다. 숲의 포석을 좁은 길과 어긋난 줄눈으로 정리하고, 회랑의 이끼를 벽가에 모으며 심부의 큰 석판 광장과 통로를 구분했습니다. 같은18지점 전후 화면·이동 마스크 보존·Windows 검증과 남은 시각 문제는 [TerrainComposition-20261003](Docs/TerrainComposition-20261003.md)에 기록합니다.'
if(!$readme.Contains($old)){throw 'Current README candidate changed; preserve and inspect.'}
[IO.File]::WriteAllText($readmePath,$readme.Replace($old,$new),[Text.UTF8Encoding]::new($false))
$statusPath=Join-Path $projectRoot 'WORK_STATUS.md'
$status=Get-Content -LiteralPath $statusPath -Encoding UTF8 -Raw
$entry=@'
## 2026-10-03 포석·석판 배치 정리 BD

- 전체 목표 ACTIVE/미완료. 외곽 포석을 반 칸 어긋난 열과 좁은 길/작은 광장으로 구성하고 native32개 파손 모양 분산을 개선했다. 회랑 내부 이끼를 줄이고 벽가에 집중시켰다. 심부의 임의 크기 전환을 중앙 석판 광장/주변 통로로 정리했다. PNG/PPU/이동 마스크/벽과 전투 판정/대시·장전·퀵슬롯·오크 개선은 보존한다.
- review-173429 이전/174150 중간/174515 채택 각18장,seed9100·동일카메라. 세 지역 전체 이동 SHA/열린 셀/충돌/바닥 수 동일. 외곽 포석1408→809,원본32종 모두 사용/표시 픽셀중심207104개가 열린 바닥 안/원본격자 이탈0. 세 시드는 같은 고정 지형을 사용하므로 세 레이아웃으로 확대하지 않는다. 고정 카메라/공급 배우 검사이며 정상이동·캠페인 증거는 아니다.
- BD build_1742c94cca06,2026-10-03 17:50:38 KST,658113922B/170.059초/오류0·기존경고3.15782소스SHA변경0/원본·보관·퇴역V2 중복포함0. Windows smoke PID25532/exit0,720p UI PID32068/exit0,1080p UI PID39180/exit0,각57PNG/blank0/96설명 및 기존피격·사망·오크/게이지·퀵슬롯 검사 PASS. 실제화면검토 포함. Docs/TerrainComposition-20261003.md에범위기록.
- 현재 후보 Builds/CampaignCandidate-20261003-BD/RuinRunner.exe. 후반 바닥의 강한 정사각 줄눈/넓은 빈 공간/소품 밀도차/단순 받침대는 남는다. 전용 주인공피격·후반보스 모션/최신세정상빌드캠페인·분량/성능·반복메모리·실제청취는 미완료. 기존 정상 입력 pilot은 근접/원거리 장비 선택은 하지만 개별강화·분해/세효과빌드 검증을 아직 대신하지 못한다. 이전빌드·원본·실패자료·사용자저장보존. Editor out of Play, 검증앱 종료.

'@
[IO.File]::WriteAllText($statusPath,$entry+"`r`n"+$status,[Text.UTF8Encoding]::new($false))
Write-Output 'README and WORK_STATUS now identify validated BD candidate; prior evidence preserved.'
