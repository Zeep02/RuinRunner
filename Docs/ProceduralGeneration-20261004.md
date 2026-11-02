# 모듈 조합 생성기 — 2026-10-04

`Assets/RuinRunner/Runtime/World/Generation/`에 Unity와 독립적인 생성기와 Unity 변환기를 추가했다. 후속 통합으로 **정상 새 런과 지역 전환이 생성 맵을 사용하도록 코드에 연결했다.** 기존 씬·아트 원본·실제 저장 파일은 변경하지 않았다. 부모 담당자가 실제 Unity에서 지형 일치 9개, 제공 상태 토폴로지 30항목, 초기화 최적화 일치 9개 검사를 통과했다. **이 결과는 정상 캠페인 통과를 뜻하지 않는다.** 아래 API 계약과 함께 `Docs/ProceduralIntegration-20261004.md`에서 통합 상태와 남은 검사를 확인한다.

## 생성 계약

```csharp
GeneratedStage graph = ProceduralStageGenerator.Generate(seed, stageIndex);
GeneratedUnityStage authored = GeneratedStageUnityAdapter.Convert(graph);
StageTerrainLayout terrain = authored.BuildTerrain();
```

- 버전은 `ProceduralStageGenerator.Version == "modules-v1.0.0"`. 같은 버전/지역/명시 시드는 같은 구역·좌표·윤곽·장애물·연결·역할을 생성한다.
- `GeneratedStage`: `Seed`, `StageIndex`, `GeneratorVersion`, `StructureSignature`, `EntranceId`, `BossId`, `Regions`, `Connections`, `Diagnostics`.
- 구역: `Id`와 `Index`, `ModuleId`, `Role`, `Service`, `MainPathOrder`, `Center`, 양방향 `Neighbors`/`Connections`. ID는 현재 생성 결과 내부의 안정 ID다. 서버 전역 구역 ID는 **원정 ID + 지역 ID + 구역 ID**로 묶는다.
- 연결: `Id`, `From`/`To`, 해당 인덱스, 양쪽 출입 방향, 폭, 지형 polyline, 두 `DepartureGate`/`ArrivalGate`. 직전 인덱스/다음 인덱스로 연결을 추론하지 않는다.
- 역할: 입구, 필수 전투, 보스, 상인, 현장 성장, 보급. 주 경로 5~6구역과 네 가지 짧은 선택 분기다. 상인은 첫 필수 전투의 분기에 보장된다.
- 현장 서비스: 첫 지역 `form-anvil`/`oath-stone`, 둘째 `oath-stone`/`exchange-altar`, 셋째 `exchange-altar`/`form-anvil`. NPC/거래/성장 규칙은 이 모듈이 구현하지 않는다.
- 모듈: 지역별 8개, 총 24개. 윤곽은 별개 직교 바닥 조각들의 연결된 외곽선이며, 장애물은 실제 통합 보행 마스크를 막는다. 엇갈린 정원, 긴 행렬길, 기단, 계단 수로, 쌍둑, 열주실 등 폭·윤곽·막힌 공간 배치가 다르다. 색 변경을 별도 모듈로 세지 않았다. 건물 이미지를 회전/반사하지 않는다.
- `Module`의 `SpawnCandidates`, `RewardAnchor`, `ServiceAnchor`, `PlayerAnchor`, `ExitAnchor`, `DecorationSupports`는 로컬 좌표. 구역의 `RewardPosition` 등은 월드 좌표. 예약한 NPC/성장 자리에 임의 장식을 덮지 않는다.

지형 RNG는 `GenerationRandom(seed, stage, GenerationStream.Layout, attempt)`이다. `Encounter`, `Reward`, `Merchant`, `FieldUpgrade`, `Decoration` 스트림은 별도이며, Unity 전역 Random을 소비하지 않는다. 현재 Unity 어댑터의 전투 구성은 기존 독립 스트림 `RunContent.Encounters`를 사용한다. 상점/강화/장식 소비자는 지정 스트림과 안정된 구역 인덱스를 사용해야 한다.

일반 새 원정에서만 `ChooseFreshSeed(nextSeed, stage, recentSignatures)`를 사용한다. 후보 8회(설정 가능, 상한 32회) 안에 최근 구조와 다른 결과를 찾고, 모두 반복이면 그 사실을 진단에 표시한다. 명시 시드 `Generate`는 최근 기록의 영향을 받지 않는다. 생성 자체는 12회 상한과 유효성이 검사되는 별도 배치 대체 경로를 갖는다. 대체도 해당 시드의 모듈 조합을 사용한다.

## 통합 계약과 연결 상태

1. 연결함: `DungeonMap.BuildStage`는 어댑터의 `Definition`/`Rooms`/`Centers`를 받는다. 기존 고정 JSON은 `BuildAuthoredStageForValidation`로만 유지한다. 생성 배열에서 **보스는 마지막 원소가 아니다**.
2. 연결함: `GameController.EnterRoom`은 양방향 인접 검사와 구역 상태를 사용한다. 입구/상인/성장/보급 구역에 `BeginWave`를 호출하지 않으며, 클리어 구역 재방문으로 적/보상을 다시 생성하지 않는다.
3. 연결함: 구역 전투는 연결된 **모든** 출입구를 봉쇄하며, 종료 후 인접 전투/보스 선행 조건을 재계산한다. 새 `RoomExit`은 양방향 트리거이며 재방문을 consumed로 차단하지 않는다.
4. 진행/보스/출구·자동 조작은 `BossId`/역할/인접 목록으로 연결했다. 부모가 환경의 보스 장식 조건과 지도의 연결 양 끝 구역/방향 표시도 연결했다. `Rooms.Length-1`, `centers[3]`, `CurrentStageIndex*4` 가정을 새 코드에 재도입하지 않는다.
5. 실제 필드 상인과 성장 오브젝트는 예약 위치에서 생성하고 해당 구역/원정 ID로 재고·이용 횟수·진행을 유지한다. 외곽 지형·벽 접합·그림자·실제 소품 지지는 통합 렌더러 단계에서 검수해야 한다. 생성기는 회색 진단 도형을 최종 환경 아트로 사용하지 않는다.
6. 임시 `RoomTemplate`은 `StageRuntimeOwnership.Rooms`에 넣어 지역 해제 때 정리한다. 새 asmdef/외부 패키지는 필요 없다.

## 수행한 검사

실행:

```powershell
& 'F:/codex/LoglikeCap/Backend/.local/dotnet/dotnet.exe' run --project Tools/Validation/ProceduralGeneration20261004/ProceduralGeneration20261004.csproj --configuration Release -- Logs/Validation/2026-10-04-procedural-generation 100
```

`generation-validation.json`: 세 지역 × 100시드, 300개 실제 서로 다른 스냅 지형. 24개 모듈 모두 사용, 겹침/고립/예약 위치 실패 0, 구조 중복 0, 재시도 0, 일반 생성 대체 0. 출입구 2,544개에 대해 양쪽 봉쇄와 전투 구역 다중 입구 봉쇄를 검사했고, 배우·보상·NPC/성장 예약 25,596곳을 검사했다. 별도로 9개 대체 경로, 잘못 이동한 게이트/겹친 모듈을 일부러 넣었을 때 검사기가 실패하는 경우도 검사했다.

마스크는 현재 `StageTerrainLayout.SnapToDungeonTiles`와 같은 1유닛 셀 중심, 다각형 포함 규칙, 4제곱 둥근 장애물 규칙을 사용한다. 경로는 .5유닛 간격과 실제 플레이어 발 오프셋/여유 크기를 반영한다. 적 예약 위치에는 .90×.70 크기 여유를 적용한다. 선택 분기는 같은 무방향 보행 성분에 있어 입구와 왕복 가능하다. 명시 시드 전체 DTO 재현, 독립 RNG 호출, 최근 구조 중복 회피의 상한도 검사했다.

증거:

- `Logs/Validation/2026-10-04-procedural-generation/generation-validation.json`
- `Logs/Validation/2026-10-04-procedural-generation/three-stage-geometry-review.png` — 직접 확인한 지형 진단도. 게임 화면이 아니다.
- 같은 폴더의 `stage-0/1/2-sample.json`, `.svg`, `.png` — 실제 생성 데이터와 미리보기.

부모 Unity 담당자가 다음 검사를 실행했고, 임시 방 데이터의 네이티브 마스크/경로와 독립 검사 사이의 일치를 확인했다.

```powershell
unity command run_script --file Tools/Validation/ProceduralGeneration20261004Unity.cs --entry ProceduralGeneration20261004Unity.Main --timeout_ms 60000 --timeout 60 --format json
```

실제 Unity 증거를 2026-10-04에 읽어 확인했다. 공통 위치는 `Logs/Validation/2026-10-04-procedural-generation/`이다.

- `unity-adapter-parity.txt`: 3지역×3시드(2026100400~2026100402), 9개 생성 지형의 정수 타일 마스크, `StageNavigation` 경로, 생성된 적 예약 위치 검사 PASS.
- `runtime-topology.txt`: 지역 0 / 시드 2026100400의 실제 생성 맵에 상태를 제공한 30항목 PASS. 다중 입구 봉쇄, 분기 접근과 반환 가능 상태, 방문 중복 방지, 보스 선행 조건/입구 봉쇄, 출구·양방향 트리거의 바닥 위치를 검사했다. 30개 서로 다른 맵이나 실제 입력 플레이 30회를 뜻하지 않는다.
- `terrain-initialization-native.txt`: 9개 생성 지형, `Walkable`/`Path` 각각 15,420,544개 셀의 기존 경로와 최적화 경로 비트 일치 및 내비게이션 PASS. 초기화 시간과 정확한 범위는 `Docs/ProceduralTerrainInitialization-20261004.md`.

이 작업자는 Unity를 직접 조작하지 않았으며 위 결과는 부모 담당자가 실행한 증거다. 전체 Runtime와 변경한 Editor 검사/세 Unity fixture의 실제 Unity DLL 참조 독립 컴파일은 오류 0, 기존 폐기 API 경고 4였다. 실제 입력으로 충돌·장벽 통과/봉쇄를 확인하는 플레이, 통합 환경 렌더의 시각 검수, 상인/강화 기능, 정상 캠페인, 전체 로딩 시간/메모리, 지역별 100개 시드의 네이티브 검사·화면 검수는 이 증거에 포함되지 않는다. 이 검사만으로 새 맵 기능 전체를 완료 처리하지 않는다.
