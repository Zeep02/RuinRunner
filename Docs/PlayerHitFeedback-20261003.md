# 피격·보호막 반응 정리 (2026-10-03)

이번 단위는 접촉 이펙트와 피격 구분이다. **주인공의 전용 피격 본체 모션은 아직 미완료**이며, 전체 목표 완료로 세지 않는다.

- 실제 체력 피해에는 보유한 IceMaan `Hit Spark`의 서로 다른 원본 5프레임을 사용한다. 32×32/PPU32, 공통 중앙 피벗, Point/무압축, 0.18초. 공격자가 있는 쪽 몸 가장자리에 생성하고 현재 몸체 뒤에 정렬한다. 이전 붉은 사각 파편은 제거했다.
- 보호막이 피해를 전부 흡수하면 0.06초 푸른 색조와 방어음을 사용하며 피격 타이머·추가 밀침·상처 효과를 발생시키지 않는다. 보호막 회전광도 몸체 뒤로 보낸다. 일부만 흡수하면 남은 피해와 기존 방어력/밀침을 유지한다.
- 정면 가드는 호박색 표시·방어음과 기존 피해 감소/약한 밀침을 유지한다. 피해 무적 시간·입력·공격 시계·이동 속도·보호막 수치/쿨다운은 바꾸지 않았다. 0 이하 접촉은 가짜 피격/무적을 만들지 않는다.

## 현재 단위 검증

- 연결된 Unity 6000.6.0f1 컴파일 오류 0.
- `Logs/Validation/2026-10-03-player-hit/motion-155656`: 일반 피해/수호자 보호막/마지막 불빛 각각 8방향, 부분 흡수 2종, 정상 가드 입력, 동시 재피격 차단, 0 이하 피해 무시, 사격 유지, 일시정지·대시 복귀·사망·메뉴 정리. 211 단언 PASS, 실제 화면 653개/27개 기록 구간. 원본 접촉 5프레임을 실제 재생에서 모두 관찰했다.
- 장비·체력·접촉을 공급한 검사다. 실제 `TakeDamage`·아이템 보호막과 일반 공격/대시 입력 경로를 사용했다. 정상 캠페인·수동 키보드·성능 검사로 주장하지 않는다.
- 실제 화면에서 효과의 얼굴 가림과 손/무기 연결을 확인했다. `actual-hit-1x.mp4`, `shield-1x.mp4`는 각각 25표본/원래 0.412·0.415초, H264 60fps 컨테이너로 실제 타임스탬프를 유지한다. 보간·시간 압축은 없다. 브라우저에서 1배속 재생을 확인했다.

## 미채택 피격 시안과 출처

Sscary의 보유 남성 팩·공식 제공 동작은 idle/walk/dash/death/jump다. 전용 hurt는 없으며, 죽음 초반의 주저앉는 포즈는 손/무기 연결과 의미가 달라 전용 피격 대체로 쓰지 않았다. 생성 시안 역시 얼굴/몸 크기·윤곽과 알파 품질이 원본을 유지하지 못해 거절했다. `ArtDirection/Review-20261003/hero-hurt/rejected-hurt-sheet-v1.png`와 `assessment.json`에 보존하며 Resources/모션 카탈로그에 넣지 않았다. 파일 수나 접촉 FX를 새 본체 포즈로 세지 않는다.

채택한 접촉 효과: [IceMaan Pixel VFX Essentials](https://icemaan.itch.io/pixel-vfx-essentials). 이미 보유한 무료 팩의 정확한 셀 분리만 수행했다. 개인/상업 게임 사용·수정 허용, 에셋 팩 재배포/재판매 및 AI 학습 세트 업로드 금지. 원본·2026-10-01 공식 페이지·게임 동봉 고지는 보존한다. 원본 픽셀 동일성은 `ArtDirection/Review-20261003/hero-hurt/native-import.json`에 기록한다.

중요 파일: `Runtime/Player/PlayerController.cs`, `Runtime/Items/ItemCombatEffects.cs`, `Runtime/Feedback/NativeCombatEffect.cs`, `Runtime/Diagnostics/UiValidation.cs` 및 `Resources/Art/Pixel/Separated/CombatFX/PlayerHit/00..04.png`. 경로 앞에는 `Assets/RuinRunner/`가 붙는다. 이전 소스는 검토 폴더의 `before/`에 보존한다.

## Windows 후보 BA

- 실행: `Builds/CampaignCandidate-20261003-BA/RuinRunner.exe` (옆 Data/DLL과 함께 실행).
- `build_c9b148c20308`, 2026-10-03 16:04:16 KST 완료, CleanBuildCache/DetailedBuildReport, 650,883,891 bytes / 153.226초 / 오류 0·기존 경고 3.
- 15,444개 소스 SHA의 빌드 전후 변경 0. 신규 5PNG와 각 .meta·출처 고지도 포함한다. raw native 중복·보관 시안·퇴역 장전 V2의 빌드 포함 0. `ArtDirection/Review-20261003/hero-hurt/build-BA*.json` 및 `build-integrity-BA.json`.
- 실제 Windows 격리 기동/자원/충돌/장비/성장/저장 smoke: `Logs/Validation/2026-10-03-hero-hurt/windows-smoke-BA-1280x720-160537`, PID33976, exit0/PASS.
- 720p Windows UI/접촉 fixture: `windows-ui-BA-1280x720-160558`, PID35096, exit0/PASS. 실제 PNG45장/빈화면0, 기존96설명 배치 통과. 신규34번 3화면을 직접 확인했고 실제 피해·수호자·마지막 불빛의 HP/무적·색조·AudioSource 재생·렌더 순서·밀침·효과 소멸을 확인했다. 오디오 재생 여부이며 주관적인 실제 청취 평가는 아니다.
- 1080p Windows UI/접촉 fixture: `windows-ui-BA-1920x1080-160706`, PID28708, exit0/PASS, PNG45장/빈화면0·96설명 배치. 새로운 일반 피해/마지막 불빛 화면을 실제로 확인했다. 원래 픽셀 밀도와 몸체 뒤 정렬을 유지했다. 이번 단위에서는 UI 레이아웃을 변경하지 않았으며 비표준1340×820 UI 검증은 보존된 AZ 기록이다.
- 이전 AZ의 잔상·게이지·퀵슬롯 및 AY의 재장전은 유지한다. 이번 빌드의 진단은 공급된 fixture이며 정상 캠페인 완주·성능 합격으로 확대하지 않는다. 이전 빌드·원본·사용자 저장·거절 시안은 보존했다.

전용 피격 본체, 적/보스·환경/맵 마감, 최신 세 가지 빌드 방향의 정상 캠페인·사람의 플레이 분량·1080p 성능/반복 메모리·청취는 남는다.


BB 후속 단위에서 단일 자세로 다시 생성한 hurt v2도 얼굴·눈·윤곽이 원본과 달라 거절했다. `ArtDirection/Review-20261003/hero-hurt/hurt-v2-assessment.json` 및 `rejected-hurt-single-v2.png`에 보존하며 런타임 채택은 없다. 이후 적 피격/사망 수정과 현재 BB 후보는 [EnemyReactions-20261003](EnemyReactions-20261003.md)를 참조한다.
