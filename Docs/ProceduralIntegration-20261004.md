# 정상 원정의 생성 맵 연결 — 2026-10-04

코드를 정상 시작/지역 전환에 연결하고 독립 컴파일까지 완료했다. 이후 부모 담당자가 실제 Unity에서 지형 일치 9개, 제공 상태 토폴로지 30항목, 초기화 최적화 일치 9개를 통과했다. 이 작업자는 결과 로그를 검토했으며 Unity를 직접 실행하지 않았다. **정상 캠페인·Windows 빌드 통과를 뜻하지 않는다.**

## 바뀐 동작

- `GameController.StartRun`은 `RoomTemplateCatalog.SelectNormalRunSeed`로 시드를 정하고, `DungeonMap.BuildStage`가 모듈 생성기를 호출한다. 다음 지역도 동일 원정 시드와 생성 버전을 사용한다. 명시 시드는 최근 기록과 상관없이 그대로 재현한다. 일반 새 런은 세 지역 구조를 함께 검사해 최근 16개 구조와의 반복을 최대 8후보까지 피한다. 이 기록은 현재 프로세스 세션 범위이며 DB 원정 기록 연동은 후속 작업이다.
- 입구는 안전한 시작 구역이다. 필수 전투/보스 역할에만 웨이브가 생긴다. 상인·현장 성장 역할은 위치와 왕복 가능한 공간만 연결했으며 **아직 NPC·거래·성장 기능은 없다**.
- 구역과 연결은 생성된 안정 ID/인접 목록을 따른다. 양쪽 입장 트리거와 실제 구역 위치 확인으로 이동한다. 공간/카메라는 바뀌지 않고 연속으로 걷는다. `CurrentRoom+1` 규칙은 정상 경로에서 제거했다.
- 전투 시작 시 해당 구역의 모든 연결 양쪽 봉쇄를 닫는다. 종료 후 전투 상태와 보스 선행 조건에 따라 연다. 필수 일반 전투를 모두 끝내기 전에는 보스 연결이 봉쇄된다.
- 선택 분기와 클리어 구역을 왕복할 수 있다. 방문·클리어·상자 상태를 보존하고 같은 방문/클리어로 보상·적을 다시 생성하지 않는다. 결과의 구역 진행은 고정 `지역×4+인덱스` 대신 실제 방문 수를 사용한다.
- 지역 출구와 보스 경험치 탐색은 `BossRoomIndex`를 따른다. 보스가 배열 마지막에 있어야 한다는 가정을 제거했다.
- 생성된 장벽은 실재 바닥이 받치지 못하는 위치에 랜턴을 자동 생성하지 않는다. 기존 봉쇄 이펙트와 충돌 폭은 유지한다.

## UI/환경 통합용 API

`DungeonMap.Graph`, `ConnectionCount`, `GetConnection(i)`, `BossRoomIndex`, `EntranceRoomIndex`, `StageExitPosition`, `GetRegionId(i)`, `GetRegionRole(i)`, `GetNeighbors(i)`, `AreAdjacent(a,b)`, `GetConnectionBetween(a,b)`, `GetEntryPosition(a,b)`를 제공한다.

`RoomExit`에는 `ConnectionId`, `SourceRoom`, `TargetRoom`, `GetEntryPoint(region)`, `IsGraphConnection`이 있다. 배열 i번째 연결이 i번째 구역의 다음 방이라고 가정하지 않는다. 생성 연결의 `IsConsumed`는 재방문 차단에 사용하지 않는다.

부모 담당 범위 및 이후 상태:

- `DungeonMapView`: 실제 연결 양 끝 구역과 `ConnectionCount`를 사용하도록 부모가 수정했다. 읽기 검토에서 방향 오류는 발견하지 못했다. 연결 공개는 기존 정책인 양 끝 중 하나의 클리어 상태를 따른다.
- `CainosRuinsArt`, `CampaignEnvironmentArt`: 부모가 마지막 인덱스 대신 `terrain.Rooms[region].bossRoom`으로 보스 장식을 판별하도록 수정했다.
- UI/보스 관련 과거 fixture의 마지막 방/인덱스 3 가정. 역사적 고정 지형 검사는 `BuildAuthoredStageForValidation`로 명시해 사용.
- 마을·재능·경제·DB·신규 보스/환경 아트는 이번 코드 통합 범위에 포함하지 않았다.

## 백업과 변경

정확한 기존 파일을 `ArtDirection/Review-20261004/procedural-integration/before/`에 상대 경로와 SHA-256 manifest로 보존했다. 기존 `.meta`, GUID, 에셋 PNG, 실제 사용자 저장과 정상 빌드를 덮어쓰지 않았다.

주요 변경: `DungeonMap`, `RoomExit`, `EncounterBarrier`, `StageLayoutDefinition`, `RoomTemplateCatalog`, `GameController`, `CampaignProgression`. 검사는 `RuntimeSmokeTest`, `DemoContractValidation`, `AutomatedStageValidation`, `Editor/StageLayoutValidation`의 고정 방 전제를 교체했다. 자동 조작은 선택 분기 방문/클리어 구역 재방문을 수행하고 해당 경로를 완료 조건에 포함한다. 옛 강화 횟수는 새 성장 시스템의 완료 조건으로 쓰지 않는다.

`StageRuntimeOwnership`과 `EncounterBarrier`의 임시 방/Material 정리는 Play/비 Play 상태를 구분하므로 Editor fixture 후 불필요한 지연 파괴 오류를 피한다.

## 검증 체크포인트

- 독립 생성기: 3지역×100시드 통과. 결과와 범위는 `Docs/ProceduralGeneration-20261004.md`.
- 실제 Unity DLL 참조 독립 컴파일: `Tools/Validation/ProceduralGeneration20261004/RuntimeCompile.csproj`. 전체 Runtime, 변경한 Editor 검사, 아래 세 fixture를 포함. 오류 0, 기존 `FindObjectsSortMode` 폐기 경고 4. 로그 `Logs/Validation/2026-10-04-procedural-generation/independent-runtime-compile.log`.
- 부모가 실행한 지형 일치 검사: `Tools/Validation/ProceduralGeneration20261004Unity.cs`, entry `ProceduralGeneration20261004Unity.Main`. 3지역×3시드(2026100400~2026100402), 9개 생성 지형의 네이티브 타일/경로/적 예약 위치 PASS. 증거 `Logs/Validation/2026-10-04-procedural-generation/unity-adapter-parity.txt`.
- 부모가 실행한 제공 상태 검사: `Tools/Validation/ProceduralGeneration20261004Topology.cs`, entry `ProceduralGeneration20261004Topology.Main`. 지역 0 / 시드 2026100400의 실제 생성 맵/게이트에 상태를 제공하여 다중 봉쇄·보스 선행 조건·분기 접근/반환 가능 상태·구역 방문 중복·트리거 바닥 위치 등 **30항목 PASS**. 증거 `Logs/Validation/2026-10-04-procedural-generation/runtime-topology.txt`. 단일 맵의 상태 검사이며 정상 입력 플레이 검사가 아니다.
- 부모가 실행한 초기화 일치 검사: `Tools/Validation/ProceduralGeneration20261004TerrainInitialization.cs`, entry `ProceduralGeneration20261004TerrainInitialization.Main`. 9개 지형, `Walkable`/`Path` 각각 **15,420,544셀 비트 동일** 및 내비게이션 PASS. 증거 `Logs/Validation/2026-10-04-procedural-generation/terrain-initialization-native.txt`.
- Editor의 `Prototype/Stage Designer/Validate Campaign`은 지역별 100개 네이티브 지형/경로/봉쇄를 검사할 수 있다. 해당 300개 네이티브 검사 실행 증거는 이번 결과에 포함되지 않는다. 고정 JSON 검사는 별도 `Validate Legacy Authored Fixtures` 메뉴로 남겼다.

초기 .125유닛 래스터의 중복 계산을 제거했고 managed 비교와 부모의 실제 Unity 비교에서 전체 셀의 동일성이 확인됐다. 초기화 메서드 단독 시간도 기록됐으며 상세 범위는 `Docs/ProceduralTerrainInitialization-20261004.md`에 있다. 실제 입력으로 첫 분기 왕복/필수 전투/보스/지역 전환, 동적 충돌 봉쇄, 정상 캠페인, 전체 로딩 시간/메모리, 최종 환경 시각 품질은 이번 검증 증거에 포함되지 않는다.
