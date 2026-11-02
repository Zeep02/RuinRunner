# 생성 지형 초기화 중복 제거 — 2026-10-04

생성 맵은 .125유닛 전체 래스터를 계산한 직후 동일 배열을 정수 타일 스냅으로 전부 덮고 있었다. `StageTerrainLayout`의 마지막 선택 인자 `snapToDungeonTiles=false`를 추가하고, 생성 Adapter만 `true`를 전달해 기존 `SnapToDungeonTiles()`를 바로 실행한다. 기존 생성자 호출은 세밀한 바닥·흙길 계산을 그대로 유지한다. 스냅 함수와 충돌/내비게이션 소비 코드는 변경하지 않았다.

기존 소스 두 개와 SHA256은 `ArtDirection/Review-20261004/procedural-terrain-initialization/before/`에 보존했다. 테스트의 `StageTerrainLayoutBefore.cs`는 그 소스에서 타입 이름만 바꾼 사본이다.

## 완료한 검증

- `Tools/Validation/ProceduralGeneration20261004/TerrainMask/TerrainMask.csproj`: 실제 변경 전·후 지형 소스를 같은 테스트용 managed `Vector2/Rect/Mathf` 연산으로 실행했다. Unity 런타임을 실행한 검사는 아니다.
- 생성 지형 3지역×3시드, 기존 곡선 지형 3개, 총 12개. 24개 모듈과 동서남북 연결을 모두 포함한다. 장애물 소수 크기·소수 좌표·기본 연결·명시 경로·흙길·탐색 분기도 포함한다.
- 정수 스냅 후 `Walkable`과 `Path` 각각 **16,139,392개 셀을 전부 비트 비교해 동일**했다. 재스냅도 동일하다. 기본 생성자 보존은 생성 지형 3개와 기존 지형 3개의 스냅 이전 마스크까지 비교했다.
- 생성 지형의 각 타일은 별도 `GeneratedWalkMask`와도 일치했다. 결과: `Logs/Validation/2026-10-04-procedural-generation/terrain-initialization-managed.json`.
- 전체 Runtime와 Editor 검사, 세 개 네이티브 fixture의 독립 C# 컴파일 오류 0개. 기존 폐기 API 경고 4개.

같은 managed 실행의 생성 지형 9개 합계는 기존 약 5.38초, 직접 스냅 약 0.148초였다. 단일 실행이며 Unity 실제 로딩 시간이나 프레임 성능을 측정한 값은 아니다.

## 실제 Unity 검증 — 부모 담당자 실행

부모 담당자가 `Tools/Validation/ProceduralGeneration20261004TerrainInitialization.cs`의 `ProceduralGeneration20261004TerrainInitialization.Main`을 실제 Unity에서 실행했다. 2026-10-04에 `Logs/Validation/2026-10-04-procedural-generation/terrain-initialization-native.txt` 결과를 읽어 PASS를 확인했다.

- 3지역×3시드(2026100400~2026100402), 9개 생성 지형에서 기존 일반 생성자+스냅과 최적화 Adapter의 `Walkable`/`Path`를 **각각 15,420,544셀 전부 비교해 동일**했다. 실제 Unity 내비게이션 검사도 통과했다. 위 managed 12개 검사와 셀 수가 다른 이유는 네이티브 검사가 생성 지형 9개만 포함하고 기존 곡선 지형 3개는 포함하지 않기 때문이다.
- 같은 네이티브 단일 실행에서 초기화 9개 합계는 기존 **37,480.08ms → 978.06ms**였다. 지형 초기화 메서드 단독 시간이며, 전체 게임 로딩 시간·렌더링·메모리·프레임 성능 개선 수치로 확대하지 않는다.
- 별도 `unity-adapter-parity.txt`의 네이티브 지형 일치 9개와 `runtime-topology.txt`의 제공 상태 토폴로지 30항목도 PASS다. 상세 범위는 `Docs/ProceduralIntegration-20261004.md`.

이 작업자는 Unity를 직접 실행하지 않았다. 위 결과는 부모 담당자의 네이티브 실행 증거이며, 정상 입력 이동/전투/캠페인이나 최종 환경 렌더의 시각 검증은 아니다. 런타임 소스는 이 문서 갱신 과정에서 변경하지 않았다.
