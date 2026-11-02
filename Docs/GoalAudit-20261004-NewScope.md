# 새 목표 구현 감사 — 2026-10-04

기준: `C:/Users/Hyeon/.codex/attachments/86c694d9-cc77-4d1e-b95d-3de44c51966c/goal-objective.md`. 최신 C#과 Resources를 읽은 정적 감사다. Unity 실행·기존 검증 재실행·런타임 수정은 하지 않았다. 과거 빌드 PASS는 새 목표 완료 증거가 아니다. 아래 경로의 시작점은 프로젝트 루트다.

| 범위 | 실제 구현과 판정 | 직접 근거 |
|---|---|---|
| 시작·귀환 | 슬롯을 열면 바로 던전 시작, 기본 녹슨 검 고정. 실제 마을·시작 장비 선택·훈련 NPC·귀환 동선은 미구현. 결과 화면은 다시 시작/메인 메뉴. | `Assets/RuinRunner/Runtime/Core/GameController.cs:183`, `:239`, `:858`, `:894`; `Assets/RuinRunner/Runtime/UI/PrototypeHud.cs:1028` |
| 게임 상태 | 여러 bool과 `IsRunActive`가 전체 정지를 결정한다. 마을에서 움직이려면 상태 경계부터 분리해야 한다. 보스 등장 전용 상태도 현재 없음. | `Assets/RuinRunner/Runtime/Core/GameController.cs:48`, `:55`, `:278`, `:916` |
| 영구 재능 | 7개 반복 랭크 구매로 체력·마나·피해·운·속도·방어·가방을 증가시킴. 던전 중 U 구매가 살아 있다. 네 계열/12개 행동 재능·활성 제한·마을 재배치·이관 환급은 미구현. 기존 enum 0~6은 유지 필요. | `Assets/RuinRunner/Runtime/Data/MetaUpgradeDefinition.cs:3`, `:39`; `Assets/RuinRunner/Runtime/Progression/MetaProgressionService.cs:25`; `Assets/RuinRunner/Runtime/Progression/PlayerStats.cs:179`; `Assets/RuinRunner/Runtime/Core/GameController.cs:780` |
| 현장 성장 | +0~+3, 강화석, 인벤토리 R 강화/분해가 정식 경로다. 형상 모루·서약석·교환 제단·사본별 변형/각성 조건은 미구현. 현행 +2/+3 효과를 기본 지급하는 교체는 피해야 한다. | `Assets/RuinRunner/Runtime/Items/OwnedEquipment.cs:9`, `:61`; `Assets/RuinRunner/Runtime/Items/InventorySystem.cs:189`, `:194`, `:209`; `Assets/RuinRunner/Runtime/Core/GameController.Equipment.cs:13`, `:48`; `Assets/RuinRunner/Runtime/UI/PrototypeHud.cs:643`, `:719` |
| 소유 장비 기반 | 사본별 ID·탄약·진행 중 재장전·인벤토리 변경 이벤트·소유 목록 재계산은 재사용 가능. ID는 프로세스 내 증가 long이라 서버/재접속용 안정 ID와 별도 연결 필요. | `Assets/RuinRunner/Runtime/Items/OwnedEquipment.cs:8`, `:21`; `Assets/RuinRunner/Runtime/Items/InventorySystem.cs:87`; `Assets/RuinRunner/Runtime/Progression/PlayerStats.cs:34` |
| 골드·상인 | Runtime 검색상 런 골드, 실제 필드 상인, 품절 재고, 구매/판매 API와 거래 기록 없음. 강화석 보상/분해를 골드 거래로 이름만 바꿔 보존하지 말 것. | `Assets/RuinRunner/Runtime/Items/InventorySystem.cs:35`, `:189`; Runtime 전체 `Gold/Merchant/Town/Talent/Anvil/Oath/Awaken/Exchange` 검색 |
| 지형 생성 | 실제 게임은 지역별 고정 JSON. 4구역/3연결을 강제하며 시드는 적·보상만 변경. 7개 Rooms 에셋 추첨은 실제 모듈 지형 조합이 아니다. | `Assets/RuinRunner/Runtime/Data/StageLayoutDefinition.cs:34`, `:40`, `:51`; `Assets/RuinRunner/Runtime/World/DungeonMap.cs:94`; `Assets/RuinRunner/Runtime/Data/RoomTemplateCatalog.cs:19`; `Assets/RuinRunner/Resources/Stages/` |
| 그래프·재방문 | 연속 지형·탐색 상태·구역별 해제 상태는 존재. 연결은 i→i+1, 다음 순번만 진입, 직전 입구 하나 봉쇄. 안정된 구역/연결 ID·인접 목록·분기 역할·여러 출입구 봉쇄는 필요. | `Assets/RuinRunner/Runtime/Core/GameController.cs:661`; `Assets/RuinRunner/Runtime/World/DungeonMap.cs:111`, `:148`, `:155`, `:162` |
| 지형/장식 재사용 | 보행 마스크·타일 스냅·실제 발 크기 경로 검사는 사용 가능. 환경 장식 일부는 벽 지지/겹침 검사가 있지만 출입구 장벽은 양옆 랜턴을 무조건 생성한다. 비보행 외곽 지형과 모듈 예약 지점 통합은 별도 필요. | `Assets/RuinRunner/Runtime/World/StageTerrainLayout.cs:78`; `Assets/RuinRunner/Runtime/World/StageNavigation.cs:19`; `Assets/RuinRunner/Runtime/World/CampaignEnvironmentArt.cs:58`, `:85`; `Assets/RuinRunner/Runtime/World/EncounterBarrier.cs:34` |
| 난수 | 스트림/인덱스로 Unity 전역 난수 상태를 보존하는 기반은 있음. 생성 버전·지형 전용 생성기·최근 구조 서명·생성 재시도/대체율 기록 없음. | `Assets/RuinRunner/Runtime/Core/RunRandom.cs:9`, `:20` |
| 정산·저장 | 단일 메모리 세션 중 결과 중복 정산 방지는 있음. 로컬 JSON 임시 파일→교체/백업만 있으며 서버 인증·소유권·영속 요청 중복 방지·DB 충돌 처리는 미구현. 기존 소비 유산/랭크를 이관 원본으로 보존해야 함. | `Assets/RuinRunner/Runtime/Core/GameController.cs:694`; `Assets/RuinRunner/Runtime/Save/SaveSlotService.cs:95`, `:110`, `:133`; `Assets/RuinRunner/Runtime/Save/SaveSlotData.cs:8` |

## 기존 검사에서 교체할 가정

- `Assets/RuinRunner/Runtime/Diagnostics/RuntimeSmokeTest.cs:92`: 4~5방, `:111`: 출입구=방 수−1, `:109`: 기존 스탯 목록이 충분하다는 검사는 새 그래프/재능 검증으로 교체한다. `:171`의 기존 enum 값 보존 검사는 유지한다.
- `Assets/RuinRunner/Editor/StageLayoutValidation.cs:16`: 같은 고정 지형에 10개 적 시드, `:47`: i↔i+1 봉쇄, `:61`: centers[3] 출구. 지역당 실제 지형 100시드·다중 출입구·선택 분기 왕복·구조 중복률 검사로 대체해야 한다.
- `Assets/RuinRunner/Runtime/Diagnostics/AutomatedStageValidation.cs:342`, `:361`: 다음 순번으로 이동. `:748` 이후 유지 보수 및 `:920` 성장 PASS는 가방 +n강/분해 횟수다. 실제 마을·구매·현장 성장·실패 귀환·재출정 시나리오가 필요하다.
- `Tools/Validation/InventoryKeys20261004.cs:63`, `Tools/Validation/OwnedEquipmentCore20261001.cs:28`, `Tools/Validation/OwnedEquipmentRuntime20261001.cs:168`: R 강화·강화석 지불 전제를 제거한다. 사본 독립, 이중 요청, 장비 제거 효과 정리, 탄약/장전 보존 검사는 새 거래/변형 경로로 이식한다.
- `Tools/Validation/SaveCompatibilityFixture.cs:21`: 기존 랭크가 계속 적용된다는 전제 대신 원본 백업·가치 보존·한 번만 환급/해금 전환·두 번 이관·서버 진행 충돌을 검사한다.

## 충돌을 피하는 다음 소유 범위

1. **통합 담당**: 기존 `GameController*`, `CampaignProgression`, `PlayerController*`, `PrototypeHud`, 입력/메뉴 상태와 실제 Unity 조작·빌드. 다른 담당은 이 파일을 동시에 수정하지 않는다.
2. **맵 담당**: 새 `Runtime/World/Generation/**`의 순수 생성 DTO/24개 모듈/그래프/검증기와 어댑터, 전용 검사. `DungeonMap`/기존 마스크/입장 흐름은 통합 담당이 계약에 맞춰 연결한다.
3. **성장 담당**: 새 재능·현장 성장 규칙/선택/사본 상태 계약. 기존 OwnedEquipment/InventorySystem/PlayerStats 수정은 한 명에게 묶고 전투 담당과 행동 훅을 먼저 합의한다. 마을 UI를 먼저 U 메뉴로 복제하지 않는다.
4. **백엔드 담당**: 별도 서버/API/PostgreSQL·버전 카탈로그·멱등 거래·이관. Unity 연동 DTO·인증/소유권·run/content/generator version을 먼저 통합 담당과 합의한다. 카탈로그 편집 원본을 한 곳으로 정한 뒤 기존 정적 목록을 생성/캐시 경로로 전환한다.
5. **마을/환경 담당**: 새 마을 공간·상호작용 구현과 NPC 자료, 생성기의 역할별 예약 지점 소비. 기존 HUD/게임 상태를 소유하지 않으며 통합 요청으로 연결한다.

이 문서는 구현 완료 보고가 아니다. 보스·전투·아트의 시각적 합격은 별도 실제 화면/1배속 관찰이 필요하다.
