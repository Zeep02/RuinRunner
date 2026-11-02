# 전투·보스 잔여 작업 — 2026-10-09

현재 체크포인트는 실제 전투 기능 회귀 PASS와 새 봉인 핵 아트의 첫 공급 상태 검사 PASS다. 정상 캠페인·최종 보스 시각 승인·Windows·음향·성능의 완료 기록이 아니다. Unity Play/임포트/빌드는 메인 에이전트가 순서대로 담당한다.

## 현재 코드와 과거 증거의 구분

- `CombatExpansionRegression20261004`의 실제 Unity 공급 상태 131항목 PASS는 보존한다. 기존 장검 연계·유한 반격·무기별 능동 행동·개별 무기 사본 상태를 다시 구현하지 않았다.
- `Logs/Validation/2026-10-04-weapon-redesign/143625/result.txt`의 `Missing muzzle tether_carbine` 실패는 보존했다. 이번 `Logs/Validation/2026-10-09-combat/153812/result.txt`의 실제 GameAction 발사·총구 시작점·재장전 PASS로 해당 기능 회귀를 닫았다. 정상 자연 획득이나 실제 음향 검토와는 구분한다.
- Tidekeeper는 원본 Kronovi 고유 모션을 사용한다. 문제는 새 `TideFan`/`TideSurge`가 예전 lane/pool의 예고 시간으로 샘플링되는 연결이었다.
- 감사 시 `SealHeartArt.Death`가 stomp 4~9번을 사용했던 문제를 확인했다. 원본을 보존하고 새 봉인 핵의 전용 붕괴 8포즈로 교체했다. 기존 기사형을 새 이름으로 확정하지 않았다.

## 이번 실제 수정

| 범위 | 변경과 이유 |
| --- | --- |
| `TidekeeperArt`, `EnemyPixelAnimator` | 현재 행동 ID와 `CampaignPhaseDuration`을 받는다. 0.85초 fan/surge도 준비 4포즈와 발동 3포즈를 실제 시간 안에 재생한다. 예전 2패턴 검사용 overload는 보존했다. |
| `CampaignEnemyActions` | fan의 발동은 실제 `.35/.65`초, seal volley는 `.36/.66`초로 표시한다. 탄막은 예고 시 후반 여부를 고정하여 예고 도중 HP 경계 통과로 탄수가 바뀌지 않는다. 중앙 직선 대신 전체 발사 각도를 포함하는 전방 부채꼴을 예고한다. |
| `ArenaHazard`, `HazardGroundFootprint`, `EnemyController` | 돌진 접촉은 반경 `1.35`인데 기존 경고 전체 폭은 `1.4/1.6`이었다. 실제 경로와 양 끝 반원을 포함한 캡슐을 사용한다. Warden도 같은 돌진 예고를 쓴다. 지면 벽을 사이에 둔 접촉 피해를 차단한다. |
| `EnemyController`, `CampaignEnemyActions` | Warden 돌진 궤적에 기존 native `GroundDust`, 착지에 `SlamDust`/`StoneChip`을 연결한다. 이동 거리에 따라 잔상을 배치해 벽에 막힌 돌진이 같은 위치에 효과를 계속 쌓지 않는다. |
| `BossEntrancePresentation` | 스킵 안내를 논리 화면 y650에서 보스 제목 아래 y224로 이동한다. 동일 해상도 배율을 쓰는 하단 퀵슬롯과 분리하며 실제 키 설정을 계속 표시한다. |

수정 전 파일은 `Backups/2026-10-09-goal/combat/` 아래 원래 상대 경로로 보존했다. Player/Core/UI/World의 공유 파일은 수정하지 않았다. `CombatCatalogBinding` 및 장신구 수치 연결은 백엔드 담당자가 소유한다. 기존 PNG·GUID·원본 아트는 변경하지 않았다.

## 검증

- 전체 Runtime 독립 C# 컴파일: `Backend/.local/dotnet/dotnet.exe build Tools/Validation/ProceduralGeneration20261004/RuntimeCompile.csproj --no-restore` 성공, 오류 0, 기존 obsolete API 경고 4.
- 새 fixture까지 독립 컴파일: `Backend/.local/dotnet/dotnet.exe build Tools/Validation/GoalCombat20261009/CombatCompile.csproj` 성공, 오류 0, 같은 경고 4. 첫 시도에서 중복 UnityEditor 참조로 난 컴파일 오류는 검사 프로젝트 참조를 정리해 수정했다. 게임 Runtime 오류가 아니었다.
- 픽셀 지침을 적용했다. 저장된 GraphicsSettings/QualitySettings의 렌더 파이프라인은 Built-in이며 메인 담당자가 실제 Unity의 `GraphicsSettings.currentRenderPipeline`으로 Built-in을 확인했다. 기존 PPU·Point 필터·원본 배율을 바꾸지 않았다.
- 실제 Play 첫 실행 `Logs/Validation/2026-10-09-combat/153427/result.txt`: 캡슐 16경우와 tide 4행동×양 국면의 준비/발동 포즈·일시정지는 통과했다. 벽 제거 후 피해 검사는 실패하여 무기 검사까지 도달하지 않았다. 원인은 fixture의 `SetStageClearSafety(true)`가 남긴 2초 무적을 안전 상태 해제만으로 없어진다고 가정한 것이다. 정상 코드의 보호 시간은 보존하고 접촉 fixture를 처음부터 보호 없이 생성하도록 수정했다. 첫 실패와 씬/입력/시간 등 복구 성공 기록은 그대로 보존한다.
- 두 번째 `153627`은 벽 접촉까지 통과하고 총 발사에서 실패했다. 공급 보호 상태가 정상적으로 전투 입력도 막는다는 점을 fixture가 놓쳤다. 적이 없는 무기 검사에서도 보호를 공급하지 않도록 수정했다. 정상 안전 규칙은 완화하지 않았다.
- 실제 후속 `153812` 전체 PASS: tide 4행동×양 국면, 캡슐 16경우, 벽 유무 접촉, 신규 무기 4종의 8방향 손/아이콘, tether/bastion의 각 8방향 실제 발사·탄약 1소비·발사체 생성 위치·전용 재장전·씬과 입력 등 복구. Player 원본/코드 수정 없이 기존 총구 수정의 실제 성공을 확인했다.

새 검사는 8방향/정지 돌진 캡슐, 모든 tide 행동의 양 국면·실제 렌더 준비/발동 포즈·일시정지, 벽 유무에 따른 실제 돌진 접촉, 신규 무기 네 종류의 손/아이콘, tether/bastion 8방향 실제 GameAction 발사·발사체 시작점·native 재장전을 확인한다. 보호된 플레이어와 공급 무기, 강제 선택 행동, 직접 inventory clock을 사용한다. 입력·오디오·시간·RNG·AutoTick·원래 씬을 복구한다. 결과와 raw 화면은 `Logs/Validation/2026-10-09-combat/<실행시각>/`에 기록한다. 공급 검사를 정상 원정·모션 부드러움·청취·성능으로 확대 해석하지 않는다.

## 아트 재사용 감사와 남은 우선순위

초기 타이밍/접촉 수정의 효과는 이미 배포 고지된 Ninja Adventure/Pixel-Boy and AAA 계열 native 리소스를 재사용했다. 후속 봉인 핵은 적합한 기존/외부 에셋 부재를 확인한 뒤 내장 `image_gen`으로 생성했다. 기존 출처는 `ASSET_SOURCES.md`, `Assets/StreamingAssets/ThirdPartyNotices`를 유지한다.

보존 후보를 직접 확인했다. `seal-native-candidates/hero-native-comparison.png`의 Mono 무료 골렘은 체형은 간결하지만 공식 보존 페이지에 modification 금지 조건이 있고 단일 공격/방향 제한이 있다. Foozle Mecha는 패키지 CC0이지만 현재 프로젝트와 다른 횡스크롤 차량 실루엣이다. Chierit Crystal Mauler는 전용 피격/사망이 있으나 횡방향 인물형/다른 색 밀도다. 단지 사망 프레임이 있다는 이유로 교체하지 않았다. 후보 ZIP/PNG/공식 페이지는 그대로 보존한다.

해결이 남은 실제 범위:

1. Warden 본체의 최종 아트와 실제 모션 검토, Tidekeeper의 기존 authored idle/move·공격·피격 반응·전용 사망 및 appearance 연결 검토. SealCore는 아래와 같이 구현했지만 실제 해상도/1배속 시각 승인은 대기 중이다.
2. SealSweep의 복사 초승달과 SealRift의 주황 높은 파편은 제거했다. 첫 낮은 해상도 캡처에서 중앙 native 먼지의 흰 대비가 영웅 주변을 복잡하게 만들므로 1280 재촬영 후 강도 조정을 검토한다.
3. 좁은 실제 생성 지형에서 새로운 경고와 효과의 경계·회피 여유·정상 성장 보스 압박 검토. 단순 원형 접촉 경고 외의 모든 원거리/지면 효과가 벽에서 잘리는지는 별도 확인이 필요하다.
4. 1배속 영상·음향 검토, 정상 자연 획득 캠페인 및 새 Windows 실행 파일 검증. 신규 무기 기능 회귀는 위 실제 PASS로 완료했다.

## SealCore V4 본체와 지면 공격

- 경로: `ArtDirection/Generated/SealCore-20261009/`. 원 생성 시트, 거절 후보, 전체 정확한 호출/프롬프트 `imagegen-calls.json`, 명시적 최신 피격 프롬프트, SHA256 manifest, contact sheet, 픽셀 audit를 보존했다. 외부 후보 및 라이선스 판단은 `sources.json`에 기록한다.
- 아이보리 세 관석/두 봉인석·보라색 석체·청록색 핵으로 이루어진 비인간형이다. 승인 주인공을 입력으로 보내거나 변경하지 않았다. 자체 생성 v2를 참조한 편집만 수행했으며 제3자 에셋을 생성기에 넣지 않았다.
- 개별 PNG 60개: idle 4, move/cast/sweep/charge/hurt/death/entrance 각 8. 파일 해시도 60개 모두 고유하지만 이 숫자만으로 자연스러운 모션을 주장하지 않는다. 피격 v2는 약해 v3로 보강했고 최종 export는 `frames-v4`다. 전용 사망은 핵 분리→관석 낙하→돌무더기→소등이며 공격을 재사용하지 않는다.
- 처리: 각 셀을 공통 64px 점 샘플링 그리드로 정리해 80px 투명 캔버스에 저장, 14색 공통 팔레트, 알파 0/255, 고립 1px 찌꺼기 제거, 투명 RGB 0, 고정 바닥 피벗(40,8). 원 시트는 불변이다. `Tools/Art/PrepareSealCore20261009.ps1`와 `ReviewSealCore20261009.ps1`로 재현한다. 이는 프레임 분리/크기·팔레트·경계 정리이며 복제 중간 포즈 생성이 아니다.
- idle 불투명 영역 44–54×43–45px, 32PPU에서 약 1.38–1.69×1.34–1.41 world units. 최종 rubble 높이18px. 캔버스 크기와 보이는 크기를 혼동하지 않는다. 원본 영웅 약12×26px와의 실제 Game View 비교를 별도로 수행한다.
- 메인 에이전트가 `ImportSealCore20261009.Main()`을 Edit Mode에서 실행했다. CLI는 30초 타임아웃이었지만 재실행하지 않고 `unity-import-v4.json` 60개/AssetDatabase sprite60개/실제 피벗을 확인했다. 새 `Campaign/SealCoreV4`에 Point/32PPU/무압축으로 임포트했다. 기존 파일과 GUID는 보존했다.
- Runtime은 행동별 실제 예고/발동 시계에 준비0–3, 발동4–5, 회복6–7을 연결한다. 돌진은 접촉 중4번을 유지하고 제동5–7은 접촉 해제 후에 재생한다. 피격은 공격의 약속된 포즈를 가리지 않는다. 1.05초 등장 조립·전용 사망을 연결하고 사망의 마지막10% 전까지 불투명 본체를 유지한다.
- `SealGroundAttack`은 기존 피해 반경/시간에 맞춘 32px 세계 그리드의 낮은 연속 파동과 균열이다. body와 같은 청록/아이보리/보라색을 쓴다. 실제 sweep ±.48 피해띠와 동일한 시계, WorldFloor9, 객체 제거 시 mesh 해제. 파편 이미지를 길게 복사하는 대신 지면 정보 형태를 사용하며 새 본체 프레임 수에 포함하지 않는다. Cross에는 기존 native SlamDust 1회만 재사용한다.
- 실제 첫 Play `Logs/Validation/2026-10-09-sealcore/160815/result.txt` 기능 PASS: 60임포트, 4행동×양국면의 모든 준비/발동/회복, 실제 hit가 약속된 공격을 가리지 않음, 보이는 지면 픽셀 중심과 실제 피해 판정 일치, 등장/피격/사망8포즈, 사망 pause, 콜백1회, 정리. 캡처는549×360이고 PixelPerfect 경고가 덮여 **최종 시각 검토는 불합격**이다.
- 이를 숨기지 않고 `MainSealArt`에1280×720 preset 저장/설정/실제 Screen 단언/복구를 추가했다. `MainSealMotion`도1280×720 실제 end-of-frame 비동기 GPU raw capture, 방향 픽셀 비교, clip별 실제 wall/simulation timestamp CSV, 구간 후 PNG 압축을 수행한다. 촬영 중 timeScale1/captureFramerate0을 단언한다. 실제FPS<50 또는 p95간격>30ms 또는 최대간격>100ms이면 `LOW_RATE_NO_SMOOTHNESS_PASS`로 기록한다. ffconcat은 실제 timestamp 간격을 사용한다. 충분한 촬영률 자체도 자연스러움 PASS가 아니며 실제1배속 재생 검토가 필요하다.
- `161049` 재촬영 시도는 fixture reflection의 `FlattenHierarchy` 누락으로 상속된 `ScriptableSingleton<GameViewSizes>.instance`를 못 찾아 실패했다. 복구 PASS와 실패 원본을 보존했고 플래그/정확한 null 진단을 수정했다. 이 실패는 게임 Runtime 예외가 아니며 새 촬영 성공으로 대체하지 않았다.
- `161315` 후속 실제1280×720 기능 재검사 PASS와 해상도/preset 복구 PASS를 확인했다. PNG를 직접 검토했다. 봉인 핵과 연속 파동은 읽히지만 Cross 중앙의 흰 먼지 대비가 여전히 크다. 소스 픽셀/크기/피해는 보존하고 해당 dust 색/알파만 (0.46,0.74,0.77,0.45)로 낮추어 Runtime에 반영했다. 후속 시각 검증은 별도 필요하다.

## 정상 원정 보스 관측기 오류

메인의 첫 정상 Windows 원정 `Logs/Validation/2026-10-09-goal/windows-melee-01/boss-pressure.json`에서 SealHeart 33.298초/11회 공격 시작 기록을 읽었다. `SealCharge` 반복 표시는 AI 반복 선택 증거가 아니다. 기존 `BossCombatObservation.cs`는 `IsSealCrossCast`만 보아 Cross 이외 모든 패턴을 Charge로, `IsTideLaneCast`만 보아 Lanes 이외 모든 패턴을 Pools로 기록했다. `CampaignBossBrain`은 직전 인덱스 재선택을 금지한다. 기존 로그는 실제 action ID를 복원할 정보가 없으므로 그대로 보존하고 별도의 주석으로 제한을 밝힌다. Tools 원본과 Runtime 관측기를 모두 `CurrentBossAction.ToString()`으로 수정했고 독립 컴파일을 통과했다. 이후 Windows 빌드의 새 관측 기록을 기준으로 행동 분포를 평가한다. 이 관측 오류를 맞추기 위해 정상 AI 선택을 변경하지 않는다.

## 실제 시간 봉인 핵 영상

- 실제 촬영 `Logs/Validation/2026-10-09-sealcore-motion/161816`은 구간별 평균58.6–59.8fps였으나 Sweep/Volley/hurt/death의 p95 간격이30–32.6ms라 `LOW_RATE_NO_SMOOTHNESS_PASS`다. Charge/Cross는 촬영률 조건만 통과했다. 전체 시각/부드러움 합격을 주장하지 않는다. 이 촬영은 Cross 먼지 완화 전의 기준이다.
- `vfr-review-v4`는 원본 PNG/CSV를 변경하지 않은1280×720 H264 VFR 재생용 파일6개다. source frame 수+명시적 마지막 이미지 hold1개, 실제CSV와 PTS 차이1–6µs, PTS 엄격 증가, 실제 시간축 passthrough decode 오류0을 검사했다. 저빈도 이미지를60fps로 채우지 않았다.
- v1은 시간 오차 허용치를3µs로 너무 엄격하게 둔 검사 실패, v2는 FFmpeg의 -r/passthrough 상충으로 실패했다. 모두 보존했다. v3의 null-muxer 기본 출력 clock 경고는 microsecond passthrough decode로 해소했다. v4는 가변률 codec timing metadata만 정상화했고 remux의 AVCC trailing4bytes notice와 성공 검증을 `encoding-verification.json`에 함께 기록했다.
- 재현 `Tools/Art/EncodeSealMotion20261009.ps1`은 모든 timestamped 보스 clip 폴더를 처리하며 촬영 품질 판정을 바꾸지 않는다. 실제1배속 화면 검토 완료를 인코딩 성공으로 대신하지 않는다.

## Warden V3와 Tide 등장 연결

- 기존 Warden 사용자 제공 원본, 이전 추출 PNG와 GUID를 모두 보존했다. 기존/온라인 호환 에셋 검토를 `ArtDirection/Generated/Warden-20261009/source-review.txt`에 기록한 뒤 내장 image_gen으로 동일한 청동뿔·작은 청록눈/가슴보석·검은 갑옷·붉은 망토 정체성을 단순화했다. 제3자 에셋이나 승인 영웅을 생성 입력으로 넣지 않았다.
- 대기/이동/방사/조준탄/돌진/도약/피격/사망/등장 각8개, 총72개 개별 PNG를 `frames-v3`에 보존했다. 공격에는 실제 팔·무릎·몸통 준비/발동/회복 자세가 있고, 사망은 무릎→전방 붕괴→꺼진 갑옷 더미, 등장은 별도 무릎→기상 포즈다. 파일해시72개 고유는 motion PASS를 의미하지 않는다.
- 원 시트 셀 경계 밖 팔/망토가 섞인 v1과 패딩이 모자란 v2 실패를 보존했다. 본체의 연결 알파 성분을 추출하여 이웃 조각을 제외하고 완전한 동일 크기 본체를112px 캔버스에 담았다. 원 시트 끝에서 잘린 radial03을 재생성했고, 화면 정면으로 돌아가던 walk06만 수정했다. 기존 다른15개의 idle/walk 원본은 그대로 사용했다.
- 공통18색,0/255알파,투명RGB0,Point32PPU,피벗(64,8). idle 불투명61–63×59–63px, 최대 radial 폭81px, 마지막 사망 높이31px. `pixel-audit-v3.csv`, `export-v3.json`, `sources.json`, `contact-v3.png`, `imagegen-calls.json`에 전체 정확한 프롬프트·출처·해시·크기·실패 이력을 기록한다.
- 메인은 contact-v3의 본체/동작 방향을 실제 Game View 통합용으로 승인했다. `ImportWarden20261009.Main()`이 새 Campaign/WardenV3에72개를 가져오고 Sprite data provider의 EditPivot 권한, 전체PNG해시/색/알파/크기/피벗을 검증한다. 기존 자산을 덮어쓰지 않는다. 후속 `unity-import-v3.json`이72개 리소스/고유GUID/원본해시 일치를 확인했다. CLI30초 응답 제한 뒤 실제 import가 완료된 것으로, 중복 실행하지 않았다. 실제 재생 검증은 대기다.
- `BossSpriteAnimator.FrameRoot`와 전용 Entrance를 연결했다. 기존 실제 공격 이벤트/판정/국면 시간은 보존했다. `EnemyController.SampleEntrance`가 세 보스 모두를 선택하며, Tide는 Kronovi가 공식적으로 defeat/appearance 용도로 저작한14포즈를 역순 재생한다. 이를 새로 만든 등장14포즈로 보고하지 않는다.
- `MainAllBossArt()`가 한 번의 실제1280 해상도 설정/복구 안에서 Seal 전체, Warden 양국면/전 포즈/발사 수/방향/접지/피격/사망/등장, Tide 전 행동의 준비/발동/회복 및 등장/피격/사망을 검사한다. 기능 캡처 때는 명시적으로 pause하므로 모션 증거가 아니다. `MainWardenMotion()`9clip, `MainTideMotion()`8clip은 독립 실제시간 raw end-of-frame 촬영으로 분리했다.
- 해당 Runtime과 Tools 검사 소스 독립 컴파일 오류0, 기존 obsolete 경고4. Unity Editor 실제 임포트/컴파일/기능/모션 및 최종 Windows 재검증은 메인 담당자가 순서대로 수행한다. Runtime 변경 백업은 `Backups/2026-10-09-goal/combat/warden-v3-install`이다.
## 기존 진단과 행동 설명의 현재 연결

- 메인 승인 아래 기존 `BossSpriteValidation`을 Warden112px/32PPU/피벗(64,8)와 낮은 전용 사망의 실제 영역에 맞췄다. `AlphaValidation`은 새 리소스 경로와 별도 등장8포즈도 읽는다. 기존 원본은 `combat/diagnostic-catalog-followup`에 보존했다.
- 새 보스 텍스처를 읽기 가능으로 바꾸어 CPU메모리를 계속 유지시키지 않는다. `SpriteVisibleBounds.ReadPixels`는 명시적 검증 경로에서만 비동기 GPU readback 결과를 대기하여 조사한다. 평상시 전투 호출은 없고 geometry는 캐시한다. Alpha PNG 출력은 임시 CPU복사본을 생성·해제한다. `MainAllBossArt`는 실제GPU 불투명 경계가 모든72포즈의 고정 바닥 피벗과 맞는지도 검사한다. 독립 컴파일 통과; 실제GPU 검사 결과는 대기다.
- `WeaponActionCatalog` Bow fallback은 실제 `TryRangedWeaponAction`의 부채꼴 관통 화살을 설명하도록 고쳤다. 현행 loot/campaign-v1/v2에는 Bow 무기가 없으며 새 무기를 추가하지 않았다. 기존 fallback 마나20/재사용2.2초와 카탈로그 override는 유지했다.
- flintlock/signal_pistol의 Nova형 권총 우클릭은 실제로 강한 한 발인데 모든 권총을3연발로 설명하던 분기도 구분했다. 마나12/재사용1.25초 및 실제 전투 코드는 불변이다. 메인 HUD가 동일 카탈로그 본문을 읽도록 연결하므로 별도 고정 설명을 만들지 않았다.
메인의 후속 정상 Windows 원거리 캠페인 `windows-ranged-02/boss-pressure.json`은 수정된 실제 action ID를 기록했다. Warden11.830초/공격 시작6회·발동6회, Tide39.840초/시작13회·발동12회, Seal35.294초/시작12회·발동11회. 각각 네 패턴이 모두 나타났고 동일 ID 연속 선택은0회다. 마지막 시전 취소는 사망과 구분하여 발동 수를 그대로 보고한다. 이 기록은 관측기의 정상 동작과 해당 원정의 패턴 기회를 확인하며 전체 빌드·시각·음향·밸런스 합격으로 확대하지 않는다. 이전 잘못 붙은 이름만 근거로 AI를 수정하지 않았다.
- 후속 발사체 충돌 경로까지 확인하여 flintlock은 추가 착탄 폭발, signal_pistol은 강한 한 발로 짧은 설명을 구분했다. 실제 폭발 반경/피해를 바꾸지 않았다.
