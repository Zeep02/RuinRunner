# 월드·지도 연결 작업 — 2026-10-09

현재 C#과 실제 새 캡처를 기준으로 남은 연결만 수정했다. 정상 캠페인 완주, 최종 시각 합격, Windows 빌드 합격을 뜻하지 않는다. Unity Editor 조작은 메인 담당자가 전담한다.

## 확인된 기존 완성 기반

- `DungeonMap`/생성 그래프의 연속 보행, 여러 출입구 봉쇄, 보스 선행 조건, 왕복·방문 상태는 구현되어 있다.
- 마을 NPC·훈련 대상·출정, 실제 원정 시설, 사본별 형상/서약 및 서버 상인은 이미 연결되어 있다. 과거 `ProceduralIntegration-20261004.md`의 “시설 기능 없음”은 최신 코드와 다르다.
- 장벽 랜턴의 지면 지지 검사는 이미 존재하여 보존했다.
- Built-in 렌더 파이프라인 설정과 독립 PixelPerfectCamera 6.0.0을 사용한다. `CameraFollow.Create`의 32 PPU/640×360 구성, Point 아트, AA/HDR 비활성화, 승인 주인공은 변경하지 않았다.

## 이번 변경

- `DungeonTilesetRenderer`가 생성 그래프를 `EnvironmentPlacementRules`에 전달한다. 생성기의 `DecorationSupports`를 실제 소품 배치에서 소비하며, 소품 밑면 전체의 실제 바닥·패드 포함 여부를 확인한다. 입구/보상/서비스/출구/적 등장 예약 위치와 연결부 및 구역 안 이동 폭을 비운다. 자리가 없는 패드는 비워두며 통행을 좁히려고 지형을 바꾸지 않는다. 수락한 실제 렌더 소품에는 검증 가능한 발밑 영역을 남긴다.
- 기존 벽 지지 검사를 유지하며 벽 조명·석상·유물도 예약 영역을 피한다. 바닥 수풀은 실제 밑면 지지와 예약 영역을 확인한다. 장애물 기반 기단·본체는 기존 충돌 지형에 계속 맞춘다.
- 외곽 유적의 보스 포석 뜰이 마지막 배열 구역을 보스로 가정하던 두 군데를 실제 `bossRoom` 판별로 고쳤다.
- `DungeonMap.GetObjectiveText`가 안전 입구/이동 통로/전투/클리어/상인/시설/보스 선행/출구·보상 상황을 구분한다. 메인 담당자가 HUD 연결을 수행했다.
- 미니맵·전체 지도는 발견한 상인/형상 모루/서약석/교환 제단/보급함/입구/출구에 같은 기존 PNG를 표시한다. 소비 시설은 회색과 “사용 완료”, 상인은 서버 재고와 구매/판매 가능 상태를 표시한다. 사용·재고는 매 Draw에서 읽으므로 지형 MapRevision 갱신을 기다리지 않는다. 발견 전 시설은 숨긴다.
- 지도의 지형 텍스처는 같은 `StageTerrainLayout.IsOpen`을 샘플링하되 표시 해상도를 월드 유닛당 8→2로 줄였다. 동일 범위의 텍스처 픽셀/메모리는 기존의 1/16이다. 실제 FPS 개선 수치로 주장하지 않는다.
- 첫 실제 720p 도착 캡처에서 NPC가 HUD에 가리고 윗부분 수련 공간이 화면 밖인 점을 확인했다. 실제 보행 영역을 28×18→18×10의 작은 불규칙 쉼터로 줄이고, 무기지기·수련지기·훈련·출정 문을 하나의 포석길에 모았다. 카메라 줌/픽셀 크기는 유지한다. `TownWorld.Size`와 `TrainingPosition`을 Controller가 사용한다.

마을 좌표: 도착 `(0,-1.75)`, 무기지기 `(-4.25,1.15)`, 수련지기 `(-.25,2.1)`, 훈련 `(3.6,1.6)`, 원정 입구 `(6.2,-1.25)`. 부가 소품도 실제 바닥 및 접근로 거리 검사 후 배치한다.

## 현재 검증과 한계

- 최신 전체 Runtime 및 `Tools/Validation/GoalWorld20261009.cs` 독립 C# 컴파일: 오류 0, 기존 폐기 API 경고 4. 로그 `Logs/Validation/2026-10-09-goal/world/runtime-compile.txt`. Unity의 네이티브 실행을 대신하지 않는다.
- 기존 순수 생성기 검사 재실행: 3지역×100시드, 24모듈, 오류/재시도/대체/구조 중복 0. 기록 `Logs/Validation/2026-10-09-goal/world/generator/generation-validation.json`. 보존된 2026-10-04 기록의 동일 300개 시드 구조 서명과 모두 일치했다(`history-parity.json`). 생성기·모듈·생성 버전·서버 계약 소스는 변경하지 않았다.
- 첫 실제 마을 화면: `Logs/Validation/2026-10-09-goal/session-153329-e49bf2/1280x720-town-arrival.png`. 이 화면은 공간 축소 전이며 새 마을 최종 합격 증거가 아니다.
- 메인이 네이티브 `GoalWorld20261009.Main`을 실행했다. 3지역×10시드 실제 생성 소품의 바닥/예약/패드/원본 PNG와 지형 불변성 결과는 `world/native-placement.txt`다. 공급 상태 목표 분기와 지도 PNG는 `world/native-objectives.txt`에서 PASS이며 정상 플레이가 아니다.
- `Tools/Validation/GoalTownWalk20261009.cs` 실제 실행은 `world/town-walk-072332.txt` PASS다. 격리 검증 프로필의 열린 마을에서 정상 `GameAction` 이동·상호작용·닫기만 사용해 세 서비스·훈련 접근·도착점 왕복을 완료했다. 위치/장비/체력/재화/배속을 바꾸지 않았다. 첫 실행의 Esc 이중 처리 실패 `town-walk-065240.txt`를 보존하고 메인이 modalCloseFrame 가드를 수정했다.
- 최신 마을과 패널의 1280×720, 1920×1080, 1600×1000 캡처는 `Logs/Validation/2026-10-09-goal/session-162246-949916/`에 있다. 후속 전체 캠페인, 시설 사용 상태, 좁은 구역 큰 적, 지역 2/3 배경, Windows 및 성능 합격은 메인 종합 보고의 실제 결과로 판단한다. 이 문서의 생성기 300회는 정상 플레이 횟수가 아니다.

## 보존과 출처

원본 여섯 파일은 `Backups/2026-10-09-goal/world/Assets/...`와 SHA-256 `manifest.json`으로 보존했다. 기존 PNG·GUID·원본 시트·사용자 저장·이전 빌드는 바꾸지 않았다. 새로 외부 다운로드하거나 생성한 아트는 없다. 재사용 자산은 기존 Cainos 원본, Tiny Questers NPC, Ninja Adventure 모루, 기존 캠페인 소품/월드 UI다. 출처·사용 허가는 `Docs/ExternalArtReplacement-20260921.md`, `Docs/TownGrowthIntegration20261004.md`, `ASSET_SOURCES.md` 및 `Assets/StreamingAssets/ThirdPartyNotices`의 기존 기록을 유지한다.

## 쉼터 소품 후속 체크포인트

- 실제 축소 후에도 작업장·수련장이 비어 보인다는 화면 검토에 따라 Cainos 원본의 남향 돌 벤치, 상자, 나무통, 동향 표지판을 개별 PNG로 정확히 추출했다. 벤치 위 기존 Ninja Adventure 모루, 수련 기둥·등, 출정 문 양옆 기둥, 비보행 외곽 나무를 배치했다. 서비스·도착·훈련 좌표와 생성기 계약은 유지했다. 소품 밑면은 실제 바닥 및 접근로 거리 검사를 받는다.
- 원본은 `ArtDirection/ExternalCandidates/20260921/cainos-basic/original/Texture/TX Props.png`, SHA-256 `bc100749fcf3cc9008a0078454f704bc63f9000d6f3e4a91574b83da098fa71e`이며 무수정 보존했다. `Tools/Art/GoalTownRefuge20261009.cs`가 원본 메타데이터의 사각형·피벗으로 정확한 픽셀을 추출하고 Sprite Editor pivot API, 32 PPU, Point, 무압축으로 임포트한다. 사용 허가는 `Assets/StreamingAssets/ThirdPartyNotices/Cainos-SOURCE.txt`에 있다.
- 첫 임포트는 동적 실행 타입의 List를 JsonUtility가 누락하여 0개를 잘못 PASS로 반환했다. `prepared-empty-attempt.json`/`imported-empty-attempt.json`을 보존하고 Newtonsoft 및 생성·역직렬화·실제 임포트 각 4개 불변식을 추가했다. 재실행 결과 `ArtDirection/Review-20261009/town-refuge/imported.json`에서 4개 실제 임포트를 확인했다.
- 실제 화면 `Logs/Validation/2026-10-09-goal/session-160934-0f5f0e/1280x720-town-arrival.png`에서 Court의 `Stone/quiet` 연속 타일이 큰 단색 회색 사각형으로 드러나 채택을 보류했다. `Court`만 기존 32종 닳은 포석의 0.5유닛 격자·잔디 틈·중복 회피로 교체했다. 원본/해시는 `Backups/2026-10-09-goal/world/pre-court-paving/`에 보존했다. 메인이 최신 `session-162246-949916` 1280 화면에서 큰 회색 Court가 제거된 점을 확인했고 후속 실제 보행 `town-walk-072332.txt`도 PASS다.

## 주인공 피격 본체 — 정적 감사와 실제 연결

- 승인 주인공의 `WeaponStandingV4` 및 기존 빈손 idle/walk 개별 PNG와 2026-10-04 보정된 하단을 기준으로 `Tools/Art/GoalHeroHurt20261009.cs`에서 본체 관절을 저작했다. 원본 해시·얼굴·배낭·발을 보존했고 신규 정체성 생성이나 원본 덮어쓰기를 하지 않았다.
- 메인이 실행한 `hurt-native-v2-preview`는 6 무기 자세×8 방향×9 키=432 레코드, 346 고유 PNG였다. `ArtDirection/Review-20261009/hero-hurt-native/contact-{up,right,down}.png`에서 정지 비교했다. 24 고유 프레임의 분리된 검은 팔 외곽선을 발견하여 이 후보는 채택 보류다. `outline-audit.json`에 실패를 보존했다.
- 기존 저작기의 `y<33` 보호 직사각형이 높이 든 옛 손을 다시 복사하고 4방향 피부 이웃만 제거해 대각선 윤곽이 남는 원인을 수정했다. v3 preview, v4 full을 거쳐 v5 full은 7 자세×8 방향×16 idle/gait 변형×9 키=8,064 레코드, 5,906 고유 PNG다. 시작/끝 키는 해당 원본 자세와 같으므로 9개 모두 다른 자세라고 세지 않는다. 빈손은 실제 마지막 무기를 가방으로 해제하는 경로가 있어 포함했으며, 기존 6 무기 자세 6,912개 키의 v4 픽셀 해시는 모두 유지했다.
- 첫 v5 감사는 NW 빈손 54키에서 손 피부를 찾지 못해 실패했다. `v5/png-audit-occlusion-failed.json`을 보존했다. 실제 원본 배낭 영역/불투명색과 일치하는 54개 가림만 `manifest-reviewed.json`에 `handOccludedByBackpack` 증거와 behind 메타데이터를 추가했다. PNG 수정은 0이며 다른 무기 레코드도 바꾸지 않았다. 메인이 정지 contact에서 해당 가림을 확인했다.
- `ArtDirection/Review-20261009/hero-hurt-native/v5/png-audit.json`은 773 원본 해시, 전체 8,064 레코드/5,906 PNG, 얼굴·배낭·발·알파·팔 연결 감사 PASS다. 메인이 실제 Unity에서 5,906개 PNG를 임포트한 결과는 같은 폴더 `imported.json`이다. 최초 느린 순차 importer는 메인이 단계별 batch 방식으로 수정했다. 정적/임포트 PASS를 실제 재생 합격으로 세지 않는다.
- Runtime 연결은 `SscaryHurtFrames.cs`, `PlayerAnimationController.cs`, `PlayerController.cs`, `NativeHeroFrameCatalog.cs` 4개다. 원본과 미세 diff는 `Backups/2026-10-09-goal/world/pre-hero-hurt-runtime/`에 있다. 실제 .18초 damage timer의 읽기 전용 진행률을 9키로 샘플하고, 현재 gait/idle 변형 및 손 소켓을 유지한다. dash/기존 공격/방어/발사/재장전 우선순위와 공격 커밋은 바꾸지 않는다. 기존 NativeHero atlas/catalog prebuild 경로에 `WeaponHurtV1`만 추가했다.
- 실제 검증 도구 `Tools/Validation/GoalHeroHurtMotion20261009.cs`: `MainSmoke()`는 대표 20클립, `Main()`은 11무기군+2 추가 총기+빈손의 8방향·정지/이동 224조합과 우선순위 기능검사, `MainDirectionTransitions()`는 7본체 자세의 8방향 실제 전환 56 짧은 클립이다. 격리 공급 fixture이며 실제 피해·GameAction·1배속을 쓴다. PNG 압축은 클립 밖, raw 메모리는 클립마다 해제한다. 실제 타임스탬프/ffconcat/캡처 품질을 남긴다. 최초 작성 시 독립 컴파일 오류 0, 기존 경고 4이며 실제 결과가 생기기 전에는 PASS로 세지 않는다.

## 외곽 유적의 회색 기단 상판

- 메인의 실제 Windows 1지역 화면에서 `CainosRuinsArt.BuildPlinth`가 `Stone/quiet` 단색 타일을 반복하는 결함을 확인했다. 기존 닫힌 지형·충돌·시드·배치 계약은 그대로 두고 이 함수의 그리기만 수정했다. 해시 확인/백업은 `Backups/2026-10-09-goal/world/pre-plinth-finish/`에 있다.
- 기존 `Stone/worn` 기반, 같은 Cainos 포석의 불규칙 배치, native Ribbon cap의 뒤·양옆 연속 테두리를 쓴다. 앞면과 앞 립은 유지했다. 원본 Paving PNG의 잔디 배경을 그대로 겹친 첫 후보는 초록 격자 얼룩이 생겨 보류하고 비교를 보존했다. 최종 준비 PNG 32개는 정확한 잔디 6색만 투명화하고 나머지 원본 돌 RGBA·16×16 좌표를 유지한다. 새 색·확대·생성 아트는 없다.
- 메인이 `Tools/Art/GoalPlinthFinishImport20261009.cs::Main()`으로 32개를 실제 임포트했다. 결과는 `ArtDirection/Review-20261009/plinth-finish/v2-stone-only/imported.json`이다. 첫 실패는 `Sprite.texture`의 2048×2048 미리보기 atlas 전체를 원본 16×16과 비교한 검증 결함이었다. `import-pixel-count-failed.json`을 보존하고 실제 Texture2D asset을 검사하도록 수정했다. 256픽셀, 보존한 돌 RGBA, 삭제한 잔디 수, pivot/PPU 검사는 유지했다.
- 출처는 기존 Cainos v1.2.3, 재사용 허가는 `Assets/StreamingAssets/ThirdPartyNotices/Cainos-SOURCE.txt`다. 상세 수정 고지는 `Tools/Art/Staging/PlinthFinish/Cainos-SOURCE-addendum.txt`, PNG별 원본/후보 해시는 같은 폴더 `prepared.json`에 있다. 정적 1:1 비교 `v2-stone-only/native-comparison.png`는 실제 Unity 화면을 대신하지 않으며 기단 Play 시각/충돌 확인은 메인 담당이다.
