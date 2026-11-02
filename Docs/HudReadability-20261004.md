# HUD 명패 가독성 — 2026-10-04

전체 목표는 ACTIVE/미완료다. 이 단위는 강한 월드 이펙트와 겹치는 무기명·보스명을 읽을 수 있게 개선한다.

## 변경

`PrototypeHud.DrawHudName`은 기존 64px Slot 이미지/6px 테두리와 불투명 내부를 재사용한다. 실제 Galmuri 텍스트 폭에 좌우 14씩 더한 만큼만 명패를 그리고 화면 픽셀 경계로 정렬한다. 보스명은 체력바 위 2px 간격, 무기명은 퀵슬롯 위 2px 간격을 확보한다. 별도 이미지 생성·캐릭터·전투 판정·저장·입력 수정은 없다.

사용자가 최근 요청한 AZ 대시의 실제 본체/손무기 잔상, 대시 아래 탄약/장전, 보이는 경계 기준 아이콘 중앙 정렬, 작은 퀵슬롯 글꼴을 보존했다. 이번 변경은 제목 대비만 보강하며 기존 잔상의 새 애니메이션 검증으로 세지 않는다.

## Editor 확인

- Unity 6000.6.0f1 PID21424, Built-in, Demo. 이전 Editor는 정상 종료돼 있었다. 지정 버전을 CLI로 열었다. beta.6에는 스킬의 --caller/--skill 인자가 없으므로 지원되는 명령으로 연결했다.
- 컴파일 성공, Console 9→10 새 오류0. Play 직후 첫 네트워크 요청 실패는 같은 PID/ready 상태를 확인한 뒤 재시도했다. 강제 종료·캐시 삭제는 없었다.
- `Tools/Validation/HudReadabilityReview20261004.cs`: transient 저장과 공급한 심부/보스/장비로 실제 SealCross를 실행한 뒤 표시를 멈춰 비교한다. 정상 캠페인 또는 연속 모션 검증이 아니다.
- `Logs/Validation/2026-10-04-hud-readability/editor-051839`: 1280×720, 1920×1080, 1440×900에서 12PNG, 빈 화면0. 27무기 이름의 글리프/폭 검사. 가장 긴 표시 이름은 ‘별을 부수는 자’, 폭136/사용가능432. 실제 자원 게이지 중심·위아래 분리도 확인했다.
- 세 해상도의 보스/가방/재장전/긴 이름 표본6장을 실제 크기로 검토했다. 원래 Game View 프리셋/자동 틱 설정 복구, 테스트에서 추가한 프리셋 제거, 메뉴 복귀와 Play 종료.
- 감사: `ArtDirection/Review-20261004/hud-readability/editor-review.json`, `before/PrototypeHud.cs`.

## Windows 후보

- 실행 후보: `Builds/CampaignCandidate-20261004-BO/RuinRunner.exe`. 기존 BN 및 실패 후보는 보존한다.
- `build_b4fbbcf3b453`, 2026-10-04 05:23:19 KST 완료, CleanBuildCache/DetailedBuildReport, 680,662,408B/166.638초. BuildReport 오류0·기존경고3(Pipeline Player 설정 및 Diagnostics obsolete API).
- 16,382개 소스의 빌드 전후 SHA 변경0, 보관 원본/퇴역 장전V2 중복 포함0. AZ의 잔상·게이지·아이콘 배치 소스 SHA도 동일하다. `dash-hud-preserved.json`.
- 격리 기동/장비/성장/저장 smoke PID50860, exit0/PASS. `windows-smoke-BO-1280x720-052356`.
- UI 1280×720 PID20040(seed2104966204), 1920×1080 PID55264(seed2101862997), 1440×900 PID55476(seed1448359794) 모두 exit0/PASS. 각각83PNG/빈화면0/예외0, 기존96장신구 설명 배치와 보스 회귀/게이지 분리 검사 통과. 각 해상도의 게이지·가방·강한 보스 효과 표본9장을 실제 크기로 확인했다. 전체249장 연속 검토·정상 캠페인·성능·오디오 청취로 확대하지 않는다.
- 결과: `ArtDirection/Review-20261004/hud-readability/windows-results-BO.json`. 현재 실행 파일의 식별값·시드·프로세스·로그를 연결했다.
- 새 Editor 세션의 원시 UDS `Failed to free block` 진단71줄은 BuildReport와 별개로 발생했다. 이전 세션 누적736과 혼합하지 않는다. 원인 미해결, 서비스 PID41604 유지. 원시 진단/서비스 로그와 프로세스 정보는 `editor-allocator-BO.json`, `UDS-Service-BO.log`, `datastore-process-BO.json`에 보존했다. 강제 종료/캐시 삭제/버전 교체는 하지 않았다.
- 종료 상태: Demo/out-of-Play/dirty=false/컴파일 종료, managed Console10→13 새 오류0, 검증 Windows 모두 정상 종료. 일반 사용자 저장은 사용하지 않았다.

## 남은 범위

다른 배경 없는 안내 문구의 대비, Warden·전용 주인공 피격 본체, 생성 소품의 접지/밀도, 최신 세 정상 캠페인·첫 지역 강화/분해·분량·1080p 전경 성능·반복 메모리·실제 청취는 남는다. 이전 Editor UDS allocator 진단의 원인도 미해결이며 새 세션의 오류 수와 혼합하지 않는다.