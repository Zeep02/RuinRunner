# 마을·재능·현장 성장·경제 통합 체크포인트

2026-10-04. 목표 원문 `86c694d9-cc77-4d1e-b95d-3de44c51966c/goal-objective.md`의 D/E/F/G/J 작업. **구현 체크포인트이며 정상 캠페인 완료 판정이 아니다.** 현재 Runtime 쓰기는 고정했고 부모 작업자가 Unity 검증을 담당한다.

## 정식 경로

- `GameController.StartSlot → OpenProfileTown`: 서버 연결/프로필 성공 후 실제 `TownWorld`를 만든다. 로컬 저장으로 대신 시작하지 않는다. 기존 로컬 슬롯 삭제는 비활성이다.
- 마을 도착점 `(0,-2)`, 장비 담당 `(-5,2)`, 재능 담당 `(0,4)`, 훈련 석상 `(4,4)`, 원정 입구 `(8,-1)`. 이동과 공격이 가능하며 가까운 대상에서 상호작용한다. 메뉴가 닫히면 같은 공간에 남는다.
- `DepartTown`: 서버 원정 생성 성공 후 seed/version/loadout을 받아 생성 월드를 시작한다. `WeaponLootTable`과 `AccessoryLootTable`은 마을 진입과 출정 직전에 서버 카탈로그를 바인딩한다.
- 지역 생성 그래프의 안전 분기에 상인·형상 모루·서약석·교환 제단을 배치한다. 지역 사이에는 기존 연속 월드를 유지한다.
- 사망/승리 정산은 서버 경로 하나로 수행하고 `ReturnToTown`에서 귀환한다. 중단된 서버 원정은 마을 원정 입구에서 명시적으로 정리할 수 있다.
- `GameSessionPhase`는 MainMenu/Town/Expedition/BossIntro/Result/Paused/Transition을 구분한다. 마을에서 원정 플레이 시간은 증가하지 않는다.
- 보스 생성 직후 `BossEntrancePresentation.Begin`을 호출한다. 메뉴·귀환·사망·재시작에서는 연출을 취소한다.

## 성장과 거래

- `TalentLoadout.cs`: 12개 stable ID, 4계열, 계열당 1개/총 3개. 마을에서 서버 설정을 무료 변경하고 출정 사본은 고정한다. 해금 비용·성취 조건은 서버 카탈로그를 표시한다. 전투/기동 6개는 전투 담당 구현, 탐색/보상 6개는 지도·보급·상자·상점·레벨 보상에 연결했다.
- 기존 UpgradeLevel/enum은 호환을 위해 남겼지만 정식 +n 스케일링, 강화재료 지급, U 스탯 구매, R 수치 강화를 제거했다. 과거 지출 환급은 서버 `legacyRefundVersion` 경로의 책임이다.
- 형상 모루는 무기 사본 하나에 2개 중 하나의 배타적 변형을 한 번 선택한다. 장비 이동에도 같은 사본의 변형을 유지한다.
- 서약 4종: 직접 반격, 다른 무기 연계, 서로 다른 적에 콤보 마무리, 피격 없이 한 탄창에서 3회 명중 후 빈 탄창 장전. 훈련/연쇄 피해/중복 명중을 제외하고 진행 중 서약은 하나로 제한한다. UI에 조건과 각성 결과를 함께 표시한다. 실제 각성 전투 효과는 전투 담당 코드에 연결된다.
- 교환 3종은 정확한 장신구 2개를 소비해 신규 유물 1개를 받는다. 소모될 효과/세트 기여와 결과를 먼저 표시하고 확정한다. 장소별 한 번은 서버 objectId로 검증한다.
- 상점 구매/판매/현물교환/버리기/아이템 획득은 서버 확정 receipt 이후 로컬 소유 사본에 반영한다. 무료 시작 장비는 판매 불가이며 마지막 무기를 버릴 수 없다. 가방 공간을 미리 확인한다.
- `ApplyEconomyReceipt`: runId+revision 중복 방지, server instanceId로 추가/삭제, sourceEventId로 픽업 제거, operationId로 교환 제단 사용 상태 복구. BackendFlowCoordinator 확정 이벤트는 정상 응답과 재생 모두 같은 적용 경로를 사용한다. 지역 전환 중에도 같은 원정 receipt를 받는다.
- 골드는 원정 재화로만 표시하며 종료하면 이어지지 않는다고 출정/결과 화면에 명시한다.
- 독립 거래 검토 후 미확정 서버 거래 동안 장착·해제·신규 획득·교환을 막았다. 전투 중 기존 퀵슬롯 선택은 유지한다. 교환 화면을 닫아도 확정 receipt는 최초 장소를 갱신한다. 수령 효과와 세트 안내도 확정 receipt에 한 번 연결한다.
- 보물 감별은 원래 드롭의 개수와 등급을 유지하며 선택한 종류로 바꾼다. 서버 카탈로그의 현재 지역 허용 범위와 가중치를 사용한다. 무탄약 특수행동은 탄창 서약에 세지 않으며, 엇갈린 탄창이 빈 장전 중 준 한 발은 장전 시간 변경 없이 한 번 발사할 수 있다.

## 파일과 원본 보존

주요 구현은 `Runtime/Core/GameController.Town.cs`, `.Economy.cs`, `.FieldServices.cs`, `Runtime/World/TownWorld.cs`, `Runtime/Progression/TalentLoadout.cs`, `Runtime/Items/EquipmentMutation.cs`, `InventorySystem.Transactions.cs`, `Runtime/UI/PrototypeHud.Town.cs`에 분리했다. 기존 진입점/장비/UI/지도 연결은 승인받은 파일만 변경했다.

원본 백업은 `ArtDirection/Review-20261004/town-growth-integration/before/`에 있으며 9개 파일 SHA256을 manifest에 기록했다. Town NPC 10프레임은 기존 Tiny Questers 무료 사용 허용 원본, 모루는 Ninja Adventure CC0 원본을 바이트 그대로 복사했다. 각 출처/라이선스 경로/해시는 `town-art-source.json`, `anvil-source.json`에 기록했다. 별도 리샘플링/재색칠은 하지 않았다.

## 현재 증거와 남은 검증

- 전체 Runtime와 `Tools/Validation/TownGrowth20261004.cs` 독립 컴파일: 오류 0, 기존 FindObjectsSortMode 사용중단 경고 4. 로그 `ArtDirection/Review-20261004/town-growth-integration/runtime-compile.log`.
- `TownGrowth20261004.Main()` 실제 Unity 실행 PASS: `Logs/Validation/2026-10-04-town-growth/rules-143212/result.txt`. 재능 제약/출정 사본, 구강화 비활성, 동일 종류 사본 간 변형 독립성, 서약 4종 조건/중복/훈련·무탄약 특수 제외/부분 장전 초기화, 장전 중 허용된 한 발 소비, 서버 사본 중복/가방/마지막 무기 규칙을 실제 상태 모델로 검사했다. 공급 상태 규칙 검증이며 정상 획득/전투/캠페인 판정이 아니다.
- 부모가 별도로 전달한 Town/Boss 원본 171개 Unity import 검증은 import 증거이며, 마을 UI·이동·NPC 겹침·전투 품질 또는 정상 원정의 증거가 아니다.
- 추가 `Tools/Validation/TownFlowUi20261004.cs`는 독립 게스트/DPAPI 폴더를 사용해 실제 GameController와 API의 마을 설정·출정·정산·귀환을 검사하고 720p/1080p UI를 캡처한다. 패널 위치/오픈과 사망은 공급하므로 실제 보행·전투 증거가 아니다. 이미지 자동 판정은 하지 않고 CAPTURED로 보고한다. 작성 시 독립 컴파일 0오류이며 아직 Unity 실행/직접 이미지 판정 전이다.
- 반드시 남은 작업: 실제 마을 진입/보행/훈련/장비·재능 설정/출정/상인 구매·판매·재방문/세 성장 시설/실패·정산·귀환·재출정, 통신 재시도와 아이템 재생, 720p/1080p 카드·툴팁 직접 시각 확인. 모든 서약의 실제 입력과 각성 효과, 여러 seed/4빌드 정상 완주는 별도로 해야 한다.
- 공급 장비·텔레포트·무적·기존 `BeginRunForActiveSlot` legacy fixture 결과를 정상 캠페인 통과로 합산하지 않는다. 서버 PostgreSQL 검사도 Unity 통합 플레이를 대체하지 않는다.
