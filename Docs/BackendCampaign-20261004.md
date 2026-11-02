# 정상 캠페인 저장·경제 연결 — 2026-10-04

ASP.NET Core + PostgreSQL의 기존 서버를 정상 프로필·마을 구성·원정 생성·정산 경계에 연결했다. `BackendFlowCoordinator`와 `GameController.Backend.cs`가 비동기 명령을 조정하며, `SaveSlotService`는 서버 응답을 읽는 메모리 투영으로 사용한다. 원본 `saveN.json`, `.bak`, `meta-progression.json`을 서버 상태로 덮어쓰지 않는다.

실제 HTTP/PostgreSQL 검증은 **56개 PASS**다. 최신 `Backend/Validation/campaign-20261004-053744.json`에 계정 격리, 이관·환급, 동시 재전송, 거래·재고·아이템 소유권, 재능 조건, API와 PostgreSQL 재시작 결과를 기록했다. 실제 UnityWebRequest와 정상 `BackendFlowCoordinator`를 사용한 `BackendCampaignFlow20261004`도 **26개 PASS**다: `Backend/Validation/unity-campaign-flow-20261004-052741-6b94c8.json`. create/reward/purchase/settle 응답 유실 후 재시도·coordinator 재생·중복 지급 방지·원본 저장 bytes 보존을 확인했다. 이 검사는 `gameSceneInteraction=false`이며 정상 마을에서 출정해 실제 전투·정산·귀환하는 화면 검증 및 Windows 플레이어 검증은 별도다.

## 서버 소유권

| 데이터 | 단일 소유자 |
|---|---|
| 계정, 프로필/슬롯, 유산, 선택·해금, 누적 기록 | PostgreSQL `players`, `profiles`, `currency_ledger` |
| 원정 ID·시드·지형 버전·콘텐츠 버전·시작 구성 | `runs`의 출정 스냅샷 |
| 최근 원정의 지역별 구조 서명·후보 횟수·명시 시드 여부 | `run_generation`; 같은 프로필·생성 버전의 최근 16개 원정 |
| 원정 골드, 소유 장비 인스턴스, 가격·재고·상점 교환 여부 | `run_economy`, `run_items`, `run_shops`, `run_shop_offers`, `run_gold_ledger` |
| 같은 요청 재전송과 동일 월드 보상 중복 차단 | `operations`, `run_reward_events`, `run_used_objects` |
| 콘텐츠 편집·출판 | `Backend/Content/catalog.campaign-v1.json` → 불변 `content_catalogs` |
| 이미지·애니메이션·효과의 행동 코드와 실시간 전투 | Unity. 전투 결과의 신뢰 표시는 `client-reported-singleplayer` |
| 기기 설정 | 로컬 `GameSettingsService` |
| 서버에 아직 확인하지 못한 명령과 인증 자료 | Windows CurrentUser DPAPI 복구 저장소. 서버 저장 성공으로 간주하지 않음 |

공식 게스트 자격은 무작위 installation UUID와 별도 무작위 secret이다. UUID만으로 로그인할 수 없다. 프로필·원정·장비의 모든 쿼리는 인증된 player를 확인한다. access token은 메모리에만 있으며 만료/401이면 같은 보호된 게스트 자격으로 갱신한다. 데이터베이스 비밀번호는 `Backend/.local/private`에만 있고 Assets나 클라이언트 카탈로그에 포함하지 않는다.

## 정상 흐름

- `ConnectBackendAsync` → `SelectBackendSlotAsync`는 서버 슬롯을 조회하거나 만들고, 필요한 경우 기존 저장 원본과 `.bak`을 DPAPI로 백업한 뒤 이관한다. 이미 진행한 서버 슬롯에는 로컬 파일을 덮어씌우지 않는다.
- `SaveBackendConfigurationAsync` / `UnlockBackendAsync`는 서버 revision·해금·가격·성취 조건을 통과한 뒤 투영을 바꾼다. 전체 재능 3개, 계열당 1개만 선택할 수 있다. 기존 rank 구매는 정상 흐름에서 사용하지 않는다.
- `StartBackendRunAsync`는 서버가 만든 run UUID/seed와 `modules-v1.0.0`, `campaign-v1`을 고정한다. 원정 시작에 0골드, 시작 무기 인스턴스 1개를 만든다. 시작 무기는 무료이고 판매/교환 재료가 되지 않는다.
- 서버는 실제 Unity 독립 생성기 네 소스를 `RuinRunner.Generation` 프로젝트로 링크한다. 일반 출정은 최근 16개 원정과 세 지역 각각의 구조 서명을 비교하고 최대 8개 후보에서 다른 구성을 선택한다. 모두 겹치면 최종 검증된 후보를 사용하며 `repeatedRecentStructure`에 남긴다. 명시한 seed는 재추첨하지 않는다. 서버에 먼저 존재하던 같은 생성 버전의 원정은 seed로 서명을 계산해 이력에 포함하며 seed/기존 처리 영수증은 바꾸지 않는다.
- 클라이언트는 원정에 고정한 카탈로그를 `WeaponLootTable.BindPinnedCatalog`와 `AccessoryLootTable.BindPinnedCatalog`로 적용한다. 연결이 끊겨도 이미 고정한 캐시로 진행 중인 전투는 계속할 수 있다.
- `ClaimBackendRewardAsync`와 `TradeBackendAsync`는 확인 뒤 골드·인벤토리를 반영한다. 미확정 보상은 DPAPI outbox에 남고, `TickBackendRecovery`가 3초 간격으로 재시도한다. 장비·회복 영수증은 `EconomyCommitted`를 통해 정상 응답/재생 모두 같은 경로로 전달된다. 게임 쪽은 원정 ID와 영수증 revision으로 중복 적용을 막는다.
- `SettleBackendRunAsync`는 원정 골드와 별도로 영구 유산·누적 기록을 한 번만 바꾼다. zero-progress/abandoned는 보상을 주지 않는다. 다음 원정에는 이전 골드·장비가 이전되지 않는다.
- 강제 종료 후 진행 중인 서버 원정을 찾을 수 있다. 현재 전체 전투 상태·적 위치·탄약·방문 지도 스냅샷 재개는 구현 범위가 아니다. 마을의 중단 원정 정리 경로가 그 원정을 abandoned로 정산한 후 새 원정을 허용한다.

## API 계약

기본 주소는 `http://127.0.0.1:5186/`다. Unity의 `RUINRUNNER_API_URL`은 공개 주소 재정의만 허용한다. 원격 주소에는 HTTPS를 강제하며, URL 자격정보와 redirect를 허용하지 않는다. 각 변경에는 UUID `operationId`가 필요하다.

| API | 핵심 계약 |
|---|---|
| `POST /v1/auth/guest` | 안정된 player ID 및 메모리 세션 |
| `GET/POST /v1/profiles`, `GET /v1/profiles/{id}` | owner 별 슬롯 1~3, profile revision |
| `PUT /v1/profiles/{id}/configuration` | expectedRevision, startingWeaponId, activeTalentIds |
| `POST /v1/profiles/{id}/unlocks` | expectedRevision, kind, contentId, contentVersion |
| `POST /v1/profiles/{id}/legacy-import` | pristine server slot, exact source hash, migrationVersion 1 |
| `POST /v1/runs`, `GET /v1/profiles/{id}/active-run` | 서버 원정 생성/조회, profile당 active 원정 하나 |
| `GET /v1/runs/{id}/economy` | 일관된 snapshot: revision, gold, items[], shops[] |
| `POST /v1/runs/{id}/trades` | expectedRevision, buy/sell/barter/discard/exchange, stable instance UUID |
| `POST /v1/runs/{id}/rewards` | eventId, source, stageIndex, roomId, optional contentId |
| `POST /v1/runs/{id}/settle` | expectedRunRevision, outcome, bounded stats |
| `GET /v1/catalogs/current`, `GET /v1/catalogs/{version}` | 현재/고정 출판 카탈로그 |

보상 source는 enemy/combat/chest/boss/pickup이다. enemy/combat는 골드만 지급하며, pickup은 카탈로그에 있는 아이템만 지급하고 골드가 없다. 동일 `(runId,eventId)`는 다른 operationId로 보내도 다시 받을 수 없다. 이 검증은 조작된 클라이언트의 새로운 허위 전투 event 생성을 막는 안티치트가 아니다.

상점 재고는 원정 seed와 카탈로그의 개별 weight로 결정해 DB에 한 번 저장한다. 첫 상점은 해당 지역의 최저가 품목을 한 칸 보장하며 동가 품목과 나머지 진열은 seed로 선택한다. `campaign-v1`의 첫 필수 전투 6마리와 구역 완료 보상은 합계 18골드이고 보장 품목은 14골드다. 기존 원정의 이미 저장한 상점 재고는 바꾸지 않는다. buy/sell/회복/barter와 교환 제단의 입력 소비·출력 지급·영수증은 동일 DB transaction에 있다. 물물교환은 해당 재능 활성 시 상인당 한 번, 상품 가격 ≤ 판매 장비의 sellPrice × 2다. 서버는 총 소유 23개/장신구 20개를 검증하고, 클라이언트는 실제 장착 칸과 가방 크기로 더 정확한 preflight를 한다.

교환식은 정확한 입력 ID 2개를 소비한다: `mana_thread+sapphire_flask → mana_aegis`, `guard_bell+guardian_seal → prism_cage`, `rush_spur+storm_feather → trail_spindle`. 시작 장비·소유하지 않은 인스턴스·같은 인스턴스 두 번은 사용할 수 없다. `(runId,objectId)`로 제단당 한 번만 허용한다.

## 카탈로그 편집 기준

`campaign-v1`은 무기 46개, 장신구 30개, 재능 12개, 시작 무기 6개와 가격·드롭 weight·해금 조건·정산 규칙을 포함한다. 초기 이관 자료는 `ArtDirection/Review-20261004/combat-expansion/catalog-items.initial-migration.json`이며, `PublishInitialCampaignCatalog.py`는 이를 한 번 서버 JSON으로 옮긴 도구다. 이후 해당 이관 파일을 두 번째 게임 편집 원본으로 사용하지 않는다.

출판한 같은 version의 bytes를 바꾸면 시작 시 실패한다. 변경은 새 JSON/version을 추가하고 `RR_CONTENT_VERSION`을 바꾸는 방식이다. 진행 중인 원정은 이전 카탈로그를 계속 참조한다. 기존 `integration-v1`은 역사적 테스트 스냅샷으로 보존한다.

아이템 개별 dropWeight는 rarity 0~4에 100/62/28/9/3을 사용한다. 클라이언트 pinned 경로는 기존 RollRarity를 중복 적용하지 않는다. 현재 아이템 stage 범위는 0~2이며 향후 범위 변경은 새 콘텐츠 버전과 대응 클라이언트 검증이 필요하다.

서버 카탈로그가 소유한 전투 수치는 무기 공격력·속도·탄창·장전·탄속·관통·강타·가드·특수 mana/CD다. 행동 ID/무기군, 이미지, 액션 판정, 장신구별 조건부 효과의 C# 구현은 클라이언트에 있다. 현재 장신구 `stats`는 비어 있으며 효과 동작 숫자 전체가 서버 데이터로 전환된 상태라고 주장하지 않는다. **미완료:** 장신구 기본 능력치·발동 확률·쿨다운 같은 조정 수치를 버전 카탈로그의 tuning 데이터로 옮기고 원정 고정 버전에 연결해야 한다. 행동 자체의 C# 구현은 유지할 수 있다.

## 이관·복구와 운영

마이그레이션 `001_initial.sql`은 보존하고, `002_run_economy.sql`, `003_profile_progression_v2.sql`, `004_run_generation_history.sql`을 추가했다. 003은 기존 서버 프로필에 무료 기본 재능만 추가하며 balance/누적 기록은 보존하고 revision을 증가시킨다. 이미 환급한 legacy import에는 refundVersion만 표시하며 재환급하지 않는다. 004는 원정별 세 지역 서명과 선택 진단, 최근 이력 조회 인덱스를 추가한다.

원본 로컬 파일에는 쓰기·삭제를 하지 않는다. 이관은 migrationVersion 1, exact source checksum, 원본 백업 ID와 환급 receipt를 보존하며 `legacy-local-untrusted`로 표시한다. 서버 기록이 있으면 원본 local보다 우선한다. 정상 메뉴에 기존 local 삭제 기능을 재사용하지 않는다.

서비스는 portable SDK/PostgreSQL을 쓰며 전역 설치를 바꾸지 않는다. `Tools/Backend/Start-Backend.ps1` / `Stop-Backend.ps1`은 자기 PID/경로를 확인한다. `Backup-Backend.ps1`로 적용 전 실제 DB 백업을 만들었다: `Backend/.local/private/backups/20261004-051237`, 004 이전 `20261004-053354`. 백업과 credentials는 비공개 디렉토리에 유지한다.

일반 검증 명령은 `python Tools/Backend/Test-Campaign.py --restart`다. 이 실행은 새 테스트 게스트만 만들고 API/DB 둘 다 재시작한다. live Unity 검증 중에는 실행하지 않는다. 오래된 `Test-Backend.py`와 `BackendClientIntegration20261004.cs`는 integration-v1의 역사적 fixture이며 campaign-v1의 기본 가격·재능을 검증하는 도구가 아니다.

실제 Unity 정상 coordinator 검증은 부모가 `Tools/Validation/BackendClientProxy.ps1 -Campaign`을 시작하고 `BackendCampaignFlow20261004.Main`을 호출한다. 결과 JSON이 PASS인지 확인한 뒤 같은 스크립트 `-Stop`으로 소유 proxy만 정리했다. API/DB는 유지한다. start 메시지는 PASS가 아니다. proxy는 첫 create/reward/trade/settle 응답을 upstream commit 뒤 유실시키며 본문/토큰은 기록하지 않는다. 이때의 Unity `Curl error 52`는 의도한 빈 응답 사례이며 별도 production 오류로 집계하지 않는다.

생성/첫 구매 검증은 `Backend/Validation/GenerationChecks`의 실제 서버 코드 및 HTTP/DB 검사다. `generation-economy-20261004-053734.json`의 15개 PASS는 세 번째 지역 단독 중복의 후보 거부, 최대 8회, 명시 seed 유지, 16개 이력 범위, 100개 상점의 보장/다양성, 실제 첫 전투 보상만으로 구매, 18개 일반 원정의 서명 일치, 과거 metadata 추가를 포함한다. API+PostgreSQL 재시작 후 `generation-economy-20261004-053906.json`에서 20개 이력 보존과 최신 16개 seed/서명 재현을 다시 확인했다. 이들은 게임 화면을 조작한 완주 검사가 아니다.
