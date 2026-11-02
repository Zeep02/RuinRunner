# 보스 지면 공격 효과 — 2026-10-03

전체 목표는 ACTIVE/미완료. 최신 대시 잔상·장전 게이지 하단 배치·퀵슬롯 중앙 정렬/작은 글꼴은 그대로 유지한다.

## 변경

- `ArenaHazardArt`: Ninja Adventure / Pixel-Boy, AAA CC0 원본 WaterPillar9(30×41), Water11(40×33), RockSpike10(54×48), 총30개 실제 서로 다른 셀. 모든 소스는32PPU/Point/무압축/고정 피벗이며 원본 셀의 픽셀을 재도색하거나 늘리지 않았다.
- Tidecaller/Tidekeeper의 원형 공격은 중앙 물기둥과 양옆 분출·앞 물보라로 구성한다. 최종 보스 십자/대각선 공격은 고정 균열 위의 결정 분출을 사용한다. 인접 결정의 원본 프레임 진행과 좌우 표시를 달리하지만 이것을 새 원본 포즈로 계산하지 않는다. 두 균열이 교차하는 중심 효과는 하나만 둔다.
- 모두 WorldFloor9에서 캐릭터 뒤로 표시한다. FX에 콜라이더나 피해 콜백은 없다. 원형 공격12개(세 원)/십자17개의 스프라이트 상한이 고정되어 있다. 큰 TideLane 직사각형과 SealCharge는 이번 새 원본 FX 적용 범위에서 제외했다.
- `ArenaHazard`의 원래 경고 시간/위치·크기/Contains/활성 시간·피해량은 유지했다. 0.24초의 소멸 꼬리는 활성 구간 밖이며 경계선도 지운다. `IsActive`는 활성 시작뿐 아니라 종료도 검사한다. 일시정지/소유자 사망/메뉴에서 함께 정리한다.
- CallTide/TideLanes 발동에 Ninja Adventure Water9.wav(0.540068초,원본 바이트 동일), SealCross 발동에 기존 heavy-impact 녹음을 연결했다. 하나의 패턴은 발동음 한 번만 재생한다. 기존16음원 제한/Monster 음량/일시정지를 사용한다. 재생 경로 검사는 실제 청취 평가를 대신하지 않는다.

## 조사와 채택하지 않은 자료

주인공 전용 hurt 본체는 보유 Sscary 원본에 없어 이번에도 해결된 것으로 세지 않는다. 최종 보스 후보 Bringer Of Death 무료 원본은 선명하고 여러 동작이 있으나 옆걸음과 수평 낫 베기가 현재 상하 돌진에 맞지 않아 런타임에 넣지 않았다. 원본 ZIP SHA256:56f76076156a834580b01ce41db7e77c93ba8a412ffd738a6a0202dbeb2640f5. 공식 출처 https://clembod.itch.io/bringer-of-death-free . 원본과 라이선스는 ArtDirection/ExternalCandidates/20261003/clembod-bringer, 실제 심부/승인된 주인공과 비교는 ArtDirection/Review-20261003/sealheart-motion/native-left-current-right.png . 기존 보스/주인공 원본은 보존했다.

## 검증

- ImportBossHazardFx20261003: 정확한 셀 추출, PNG RGBA 원본 동일성,30원본/30고유/30가시 프레임 확인. 임포트 메타는 Editor SpriteDataProvider/EditPivot capability 검사로 작성. 처음 생성 도구의 지역변수명 충돌은 실제 임포트 전 컴파일 단계에서 수정했다.
- `Logs/Validation/2026-10-03-boss-hazards/motion-191318`: 기능 검사는 통과했으나 사각 물보라/동일 결정의 반복 배치가 어색하여 시각적으로 미채택. 증거를 보존했다.
- `motion-191839`: 실제2·3지역의 세 구간351화면/세1배속 영상. 물기둥9/물보라11/결정10 원본 전체 관찰,경고/활성/꼬리,32PPU·upright·정렬,교차 중심 중복0,일시정지,소유자 사망,메뉴정리 검사 PASS. Console404→406 새 오류0. 녹화는 실제 wall timestamp로 각약1.99초,1280×720,60fps H264(보간/가속/자른 카메라 없음). 녹화부하를 성능으로 보고하지 않는다.
- 장비·보스·맵·보호 상태를 공급한 검사이며 정상 캠페인/수동 조작/음향 청취 증거가 아니다.
- Windows BG 실패와 후속 BH 결과는 아래에 구분한다. 세 해상도의 개별 프로세스·시드·로그를 `ArtDirection/Review-20261003/boss-hazard-art/windows-results-BH.json`에 연결했다.

## 남음

최종 보스와 Warden 본체 모션, 주인공 전용 피격 본체, 다른 공격/지형 마감, 정상 세 가지 아이템 빌드의 전체 캠페인·첫 지역 강화/분해 흐름,분량,1080p 성능/반복 메모리/실제 음향 청취는 미완료다.

## 보존한 Windows 실패와 진단 수정

BG build_b6e83321ca56,664771505B/178.420초/오류0·경고5. 15950파일 빌드 전후 SHA변경0. smoke PID40660 exit0 PASS. UI720p PID11400은 기존 EnemyReactionFixtures의예고색 복원 검사에서 실패하여 새 효과 검사에 도달하지 못했다. 실제 원인 값이 최초 예외에 없어 구체적인 실패 조건을 단정하지 않는다. 소스에서 coroutine 재개가 LateUpdate 색 갱신보다 앞에 있음을 확인해, 검증을 WaitForEndOfFrame 뒤로 옮겼고 초기 렌더러 준비·상세 진단을 추가했다. 기존.055초 피격/공격 예고/기대색은 변경하지 않았다. 직접 추가했던 오디오 조회의deprecated 오버로드도 교정했다. BG 앱은 경로/격리프로필/PID를 확인한 뒤 CloseMainWindow로 정상 종료했고 관측exit0과 별개로 검사 결과는FAIL이다. 실패로그/빌드를 보존하고 배포 후보로 지정하지 않았다. 후속 BH에서 재검증한다.

## BH Windows 결과

현재 후보는 `Builds/CampaignCandidate-20261003-BH/RuinRunner.exe`다. `build_9a44bf4e34b1`, 2026-10-03 19:33:26 KST 생성, 664,772,017바이트/179.504초/오류0·기존경고3. 15,950개 소스의 SHA 변경0, 퇴역 원본·raw native 텍스처·미채택 장전 V2의 빌드 포함0을 확인했다.

| 검사 | 프로세스 | 시드 | 결과 |
| --- | --- | --- | --- |
| startup/resource/function smoke | 41256 | 해당 없음 | PASS, exit0 |
| 1280×720 | 42632 | 543039900 | PASS, 64PNG, 빈 화면0, exit0 |
| 1920×1080 | 41196 | 1220567679 | PASS, 64PNG, 빈 화면0, exit0 |
| 1440×900 | 41404 | 2134207600 | PASS, 64PNG, 빈 화면0, exit0 |

각 UI 실행에서 두 지역의 새 효과, 원래 예고/활성 시간, 한 개의 교차 중심, 캐릭터 뒤 정렬, FX 충돌체 없음, 실제 AudioSource 재생, 일시정지와 소멸 후 진입 시 무피해/정리를 검사했다. 기존 96설명 배치·주인공 피격/보호막·공격 예고색 복원·세 보스 사망 일시정지·오크 네 방향·Tidekeeper 두 주문/피격/붕괴 검사도 모두 통과했다. 예외0, 네 검증 프로세스 모두 종료를 확인했다.

기록 경로는 `Logs/Validation/2026-10-03-boss-hazard-art/windows-smoke-BH-1280x720-193402`, `windows-ui-BH-1280x720-193444`, `windows-ui-BH-1920x1080-193634`, `windows-ui-BH-1440x900-193838`이다. 720p의 장전/대시와 물기둥·대각선, 1080p의 가방 퀵슬롯과 십자, 1440×900의 장전/대시·가방 퀵슬롯·대각선을 직접 확인했다. `32-stacked-reload-gauge.png`, `33-centered-inventory-quickbar.png`, `43-native-hazard-*.png`에 해당 장면을 남겼다.

Editor는 Demo 씬에서 Play/컴파일 종료 상태이며 Console406→418 새 오류0이다. 이전 오류 기록은 지우지 않았다. 공급한 배우·장비·보호 상태로 검사했으며 정상 캠페인·사람의 조작·실제 음향 청취·1080p 성능 결과로 사용하지 않는다. Windows fixture의 기존 보스 돌진 속도가 남아 오른쪽에 표시된 장면은 새 본체 이동 품질 증거가 아니다. 원형 공격의 기존 가장자리 clamp 때문에 측면 원이 겹칠 수 있는 문제도 이번 범위에서 해결하지 않았다.
