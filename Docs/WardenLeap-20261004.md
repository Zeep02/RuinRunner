# 도약 높이·공중 사망 연결 — 2026-10-04

전체 목표는 ACTIVE/미완료다. 이번 단위는 실제 재현한 Warden 및 Leaper의 도약·사망 연결 문제를 고친다.

- 도약의 1.6/1.1 높이는 월드 단위인데 기존에는 부모의 세로 배율을 다시 받았다. 부모 배율로 나눠 정확한 높이를 유지한다. 실제 보스 본체는 이미 자식 배율로 상쇄돼 균일한 크기였으므로 본체 크기와 픽셀 원본은 수정하지 않았다.
- 도약 중 사망하면 코루틴이 중단돼 공중 위치가 남았다. 치명타 순간의 자세를 유지한 뒤 0.22 게임 초 동안 가속 낙하해 기존 지면 피벗에서 원래 사망 모션을 마친다. 이동 루트·발 충돌체·피해·착지 판정 시간·보스 사망 1.05초 및 보상 정책은 유지한다. 일시정지 시 낙하·사망·지연 보상도 정지한다.
- 코드 변경: `EnemyController.cs`. Windows 회귀 검사는 `UiValidation.cs`의 명시적 `--enemy-leap-fixtures`에만 추가한다.

## 근거

`Logs/Validation/2026-10-04-warden-leap/before-054538`에서 보스 높이 3.358, 공중 사망 후 3.316이 남는 문제와 Leaper의 공중 사망 높이 1.151 잔존을 재현했다.

`after-054925`는 30검사·275실제 프레임을 기록했다. 보스 최고점 1.599, Leaper 1.098, 사망 후 높이는 둘 다 0. 사망 시작 시 순간이동 없음, 이동 충돌 크기 유지, 일시정지·재개, 보상 1회, 메뉴 정리를 검사했다. 공급한 배우·치명타·보호 상태를 사용하는 실제 코루틴 검사이며 정상 캠페인이나 성능 검사가 아니다. `after-054830`의 프레임 종료 전 캡처 실패는 보존했고 도구의 캡처 시점만 수정해 다시 실행했다.

동일 폴더의 네 `*-1x.mp4`는 실제 타임스탬프를 유지한다. 원본 1280×720, 프레임 보간·속도 변경 없음. 브라우저에서 1배속 재생 완료 및 시작·착지 화면을 확인했고 별도 PNG 표본도 검토했다. 모든 프레임을 연속으로 주관 평가했다는 뜻은 아니다. 영상 서버와 검토 탭은 종료했다.

`ArtDirection/Review-20261004/warden-leap/editor-motion.json`과 `combat-source-preservation.json`에 검사 범위를 남겼다. 공격 세 종류의 소스와 도약 대기 시간은 동일하다. 변경 전 소스·문서는 `before/`에 보존한다.

## Windows BP 후보 검증

현재 실행 파일은 `Builds/CampaignCandidate-20261004-BP/RuinRunner.exe`다. `build_9690d6b610fe`, 2026-10-04 05:52:59 KST 생성, 680,668,040바이트/141.138초. BuildReport 오류0·기존 경고3, 16,382개 소스 SHA 변경0, 퇴역·raw 중복·미채택 장전V2 포함0. 이전 정상 BO와 실패 기록은 보존했다.

- `windows-smoke-BP-1280x720-055340`: PID39972, seed1520850598, exit0. 기동·리소스·충돌·장비·성장·저장 격리 검사 PASS.
- `windows-ui-BP-1920x1080-055635`: PID33124, seed305804298, exit0. 85PNG/빈 화면0/예외0. 새 보스·Leaper 최고점1.597/1.097, 행동 시간1.300/1.096초, 착지·공중 사망·일시정지·보상1회 PASS. 기존96개 설명 배치·게이지/퀵슬롯·세 보스/물기둥 배치 검사도 PASS.
- 실제1080p 화면32·33·49-Boss·49-Leaper를 검토해 게이지 두 행, 아이콘 중심, 작은 슬롯 글꼴, 지면에 닿은 사망 표시를 확인했다. 공급 상태 검사이며 정상 캠페인·실제 입력·성능·청취 검증은 아니다. BO의 세 해상도 결과를 BP 결과로 재사용하지 않는다.

연결된 증거는 `ArtDirection/Review-20261004/warden-leap/{build-BP-summary,build-integrity-BP,windows-results-BP}.json`이다. 검증 앱은 정상 종료했다. Editor는 Demo/Play 종료/dirty=false, Console cursor31 이후 새 관리 오류0이다. 이전 캡처 도구 실패2건은 지우지 않았다. 원시 Editor UDS allocator 진단은71→78줄(+7)로 원인 미해결이며 `editor-allocator-BP.json`과 원본 로그에 별도로 남겼다. BuildReport와 런타임 예외가0이라는 결과로 이를 해결됐다고 표시하지 않는다.

## 원본 후보 비교와 남은 범위

[Foozle Skeleton King](https://foozlecc.itch.io/lucifer-skeleton-king-boss)와 [Goblin Beast](https://foozlecc.itch.io/lucifer-goblin-beast-boss)는 CC0지만, 원래 픽셀 밀도에서 현재 첫 보스의 크기·실루엣을 충분히 대체하지 못해 채택하지 않았다. 실제 맵 비교는 `ArtDirection/Review-20261004/warden-native/native-left-current-right.png`; 원본은 Assets 밖에 보존한다. [Rock Boss](https://10kstudios.itch.io/top-down-boss-enemy-animated)는 공개된 동작 목록에 피격·사망이 없어 채택하지 않았다.

이 변경으로 Warden의 픽셀 밀도·추가 방향·8프레임 본체나 주인공 전용 피격 포즈까지 해결됐다고 세지 않는다. 지형·소품 마감, 최신 세 정상 빌드 캠페인, 첫 지역 강화·분해, 분량·1080p 전경 성능·반복 메모리·오디오 청취도 남아 있다.
