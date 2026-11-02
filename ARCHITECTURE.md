# Ruin Runner 구조

## 2026-10-04 작업본 경계

현재 목표는 실제 마을·재능·현장 변형·경제·보스 교체·DB 저장을 포함하며 진행 중이다. 아래 +n 강화·가방 정비·로컬 정산 설명은 아직 남아 있는 레거시 경로다. 새 설계가 일반 플레이에 모두 통합되었다는 의미가 아니다.

정상 지역은 `World/Generation`의 24개 모듈과 독립 난수 스트림으로 생성한다. `GeneratedStageUnityAdapter`가 연결 그래프를 기존 바닥·충돌·StageNavigation·지도에 전달한다. `DungeonMap.Graph`의 안정적인 구역/연결 ID, 역할, 인접 관계가 출입·봉쇄·재방문·보스 선행 조건의 기준이며 배열의 다음 인덱스를 출구로 간주하지 않는다. Merchant/FieldUpgrade 역할은 공간 예약까지 구현했고 실제 거래 오브젝트는 남아 있다. 자세한 구현과 공급 상태 검증은 `Docs/ProceduralIntegration-20261004.md`에 있다.

`Runtime/Backend`는 비동기 UnityWebRequest, 보호된 설치 자격 정보와 미완료 요청 저널, 예상 revision·operationId를 제공한다. 실제 API/PostgreSQL과 응답 유실·중복·인증 갱신·이관을 검사했으나 정상 SaveSlots/GameController 연결은 아직 없다. 서버 integration-v1 카탈로그는 경계 검증용 일부 데이터이며 전체 게임 카탈로그의 단일 원본 통합은 남아 있다.

무기 30개의 원본 PNG와 손잡이 좌표를 새로 연결했고 현재 보상 카탈로그는 42종이다. `LongswordActionRules`는 일반 검의 세 번 베기와 유한 받아내기→반격을 정의한다. 전체 무기군·고등급 전용 행동까지 완료한 것은 아니다. 최신 에디터 변경이 기존 BV Windows에 포함된 것으로 간주하지 않는다.

## 실행과 소유권

가방 정비 키는 기존 `GameSettingsService`/`GameAction` 입력을 사용한다. 이동 키로 가방과 위쪽 퀵슬롯을 선택하고 R로 선택한 소유 복사본을 강화한다. X는 분해 요청, E는 명시적 확정, Escape는 확인 단계만 취소한다. `InventoryFocusQuickSlot`은 정비 선택 위치이며 전투의 활성 슬롯을 바꾸지 않는다. `Dismantle=23`은 직렬화 enum 끝에 추가되어 기존 설정 번호를 유지하며 키 충돌 시 새 기본 키를 강제로 덮어쓰지 않는다. 거래는 기존 소유 ID·강화 단계·재료·Revision·입력 프레임/시간 검사를 통과해야 한다.

`AutomatedStageValidation`은 정상 획득한 장비를 읽어 고른 뒤 이 정비 키만 입력한다. 근접/원거리/무기 행동의 선호와 시드를 구분하고, 첫 지역 강화·분해 없이 엔딩만 도달하면 새 검사를 PASS 처리하지 않는다. 전리품 획득 뒤 남는 E 입력 간격 때문에 입력이 생략됐을 때는 거래 확인 단계로 진행하지 않는다. 공급 상태 회귀 검사와 정상 캠페인은 `Docs/NormalGrowth-20261004.md`에서 구분한다.

`Scenes/Demo.unity`의 정상 실행은 `GameBootstrap` → `GameController` → 메인 메뉴입니다. 검증 컴포넌트는 명시적인 명령줄 플래그에서만 생성합니다. Runtime과 Editor는 별도 asmdef이며 Runtime은 UnityEditor에 의존하지 않습니다.

`GameController`는 메뉴·런·모달·보상·정산을 조정합니다. `CampaignProgression` partial은 스테이지 진행과 전환을 담당합니다. 하나의 런에서 PlayerController, PlayerStats, InventorySystem, PlayerProgression, RunStats 인스턴스를 유지합니다. 스테이지 전환은 지형·적·투사체·장벽·지역 상태를 교체하고 동일 플레이어를 새 스테이지 시작점으로 옮깁니다.

`stageEpoch`를 캡처한 적 사망·상자·출입구 콜백은 이전 스테이지에서 실행되지 않습니다. 전환 전에 epoch 증가/코루틴 취소/투사체 정리/기존 지형 비활성화를 수행합니다. `RuntimeSceneObjects`는 런타임 세션 개체의 정리 범위를 관리하고 `StageRuntimeOwnership`은 복제한 RoomTemplate을 해제합니다.

중간 보스 처치 → 상자 → 경험치/선택 → 봉인석 상호작용 → 다음 스테이지 순서입니다. 최종 봉인석만 승리를 정산합니다. `metaRewardGranted`와 게임 상태가 중복 사망/결과 호출을 차단합니다. 남은 장비는 `RequiresStageDecision`으로 집계하며, 가방이 가득 차도 봉인석에서 명시적으로 남길 수 있습니다.

## 지형과 전투 구역

2026-10-03 배치 정리: `CainosRuinsArt`의 포석은 좁은 길/광장과 반 칸 어긋난 열로 구성하고 전체 표시 면적이 열린 바닥 안에 있을 때만 둡니다. 후반 바닥은 회랑의 습기를 벽가에 집중시키고, 심부는 Champi 원본 포석과 이어진 마모 영역을 사용합니다. 심부의 이전 큰 석판 광장과 장애물 둘레의 작은 타일 띠는 제거했습니다. 원본 이미지/PPU와 이동 마스크/벽 충돌은 바뀌지 않으며 전후·빌드 근거 및 남은 한계는 `Docs/TerrainComposition-20261003.md`에 기록합니다.

`ContinuousWallRibbon`은 열린 바닥 경계에서 검은 영역 쪽으로 폭0.25칸의 연결된 벽 윗면을 구성합니다. 이 윗면의 아래쪽 외곽에만 높이0.5칸의 벽면을 붙이므로 직선/볼록/오목 모서리가 같은 폭과 높이로 연결됩니다. 기존 직선 벽에서 추출한 개별 재질 Sprite를 참조하고 아틀라스의 실제 UV로 메시를 만듭니다. 이동 마스크와 콜라이더 위치는 유지하며, 생성한 메시/재질은 맵 제거 시 해제합니다.

`StageTerrainLayout.SnapToDungeonTiles`는 제작 지형을 정확한1칸 타일 중심에서 평가합니다. 세밀한0.125칸 배열의 다음 중심을 읽어 생기던0.0625좌표 편향을 제거했습니다. 이 공통 마스크로 렌더링·물리·지도·경로를 일치시킵니다. `DungeonTilesetRenderer`의 수조/받침대도 실제 막힌 셀에 맞추며 장애물 내부에 외벽 이미지를 중복해서 그리지 않습니다.

`StageTerrainArt`는 Outer/Flooded/Core의 다른 소재를 선택합니다. 외곽 바닥은 Cainos 원본, 회랑은 RafaelMatos Ancient Ruins 10개 원본 32px 셀, 심부는 Champi_S의 16px 포석16종과32px 바탕입니다(이전 Foozle 원본 보존). 회랑의 `NativeStageGround`는 벽까지 거리·통행·마모도를 사용하고, 심부의 `NativeCoreGround`는 넓게 이어진 포석과 공간별 마모를 구성합니다. 원본은32PPU·Point·개별 PNG이며 심부 표시는 아래 CoreStone 설명을 따릅니다. 사용하지 않는 이전 생성 Field 3,072개는 GUID를 보존한 채 `ArtReview`로 이동해 Resources에서 제외했습니다. 바닥 접점은 같은 Sprite와 색상을 벽 아래0.125칸까지 연장하고 장애물 아래도 동일한 좌표로 이어 그립니다. `TerrainAtlasPrebuild`는 기존3개 Ribbon 아틀라스와 원본 외부 Sprite 아틀라스를 빌드 전에 구성합니다. Point/RGBA32/4px패딩/회전·트리밍없음과 원본 픽셀 비교를 유지합니다. 화면 품질 및 출처·후보 구분은 `Docs/NativeFloors-20260930.md`를 참조합니다.

`CampaignEnvironmentArt`는 보스 구역의 문양1개와 입구/보스 벽의 기념물2개, 지지되는 벽 구간의 램프·유물을 배치합니다. 모든 환경 Sprite의 여백을 포함한 경계를 예약해 서로 겹치지 않게 합니다. 기존 장애물은 하나의 잔해 또는 일체형 수조로 표현하며 추가 충돌체는 만들지 않습니다. 원본·추출·피벗 기록은 `ArtDirection/Generated/Environment-20260920`과 `StageThemes-20260920`에 있습니다.

`Resources/Stages/outer_ruins.json`, `flooded_halls.json`, `seal_core.json`은 이전 제작 지형과 호환 검사의 자료로 보존합니다. 정상 새 원정/지역 전환은 `GeneratedStageUnityAdapter.Create`를 사용하므로 고정 4구역·3연결 구조에 의존하지 않습니다. `CampaignStage`는 표시명·보스·지역 소재와 보스 상자 회복30% 규칙을 계속 연결합니다.

`RoomTemplateCatalog`의 기존 템플릿은 기반 데이터로 사용하며, 정상 공간의 최종 원본은 생성된 그래프와 변환된 `StageLayoutDefinition`입니다. 적·보상 선택은 `RunContent`가 담당합니다. `dungeon_demo`와 `stage_one` 등 이전 데이터는 정상 캠페인 선택 경로가 아닙니다. Room Designer는 기존 템플릿 편집용이며 캠페인 전체 공간을 대신하지 않습니다.

`DungeonMap.BuildStage`가 하나의 큰 월드를 만들고, 같은 Terrain/walkable 자료를 렌더링·물리 경계·내비게이션·미니맵/전체 지도에서 사용합니다. `EncounterRegion`은 발견·전투·클리어와 장벽을 관리합니다. `RoomExit`는 실제 출입구 통과를 감지하며 스테이지 내부 플레이어 순간이동이나 카메라 스냅을 수행하지 않습니다. 선택 보급함은 소속 구역 클리어 후 회수할 수 있습니다.

`WaveDirector`는 소환 예고 후 현재 무리만 생성합니다. `EnemyRoles`로 보스를 분류하고 EnemyType 기존0..4에 Tidekeeper/SealHeart/Tidecaller/Sentinel을 추가했습니다. `CampaignEnemyActions`는 위치 고정 장판, 측면 절단, 안전 통로, 돌진·교차 공격을 구현합니다. `ArenaHazard`의 크기/회전/원·사각 형태가 경고 그림과 실제 판정의 공통 원본입니다. 같은 활성 공격은 플레이어에게 한 번만 피해를 시도합니다.

`ArenaHazardArt`는 원형 물기둥과 십자·대각선 균열에 Ninja Adventure의 실제 원본30프레임을 연결합니다. 피해 판정과 원래 예고/활성 시간을 유지하며 같은 경과 시간으로 원본 포즈를 선택합니다. 활성 종료 뒤0.24초는 피해 없는 소멸 구간이고 경계선도 숨깁니다. FX는 고정32PPU/WorldFloor9, 회전 없는 지면 포즈로 표시하고 별도 콜라이더를 만들지 않습니다. 물기둥은 한 패턴12개, 교차 균열은 중복 중심을 뺀17개가 상한입니다. `CampaignEnemyActions`가 패턴 발동 시점에 한 번만 물/충격음을 재생하며 기존 Monster 채널을 사용합니다. 일시정지·소유자 사망·메뉴 정리와 실제 Windows 근거는 `Docs/BossHazards-20261003.md`에 기록합니다. 큰 수로 장판과 돌진은 기존 표시를 유지합니다. 원형 공격의 배치는 `TidePoolPattern`이 같은 지형 마스크·Ground 충돌체·구역 경계를 검사하고 원 전체가 들어갈 위치만 선택합니다. 보스 세 원은 중심 간격4.75/경계 간격0.95를 확보하고, 막힌 후보를 이웃 원 위로 clamp하지 않습니다. 예고 시작 때 위치를 고정하며 원의 크기·피해·예고/활성 시간은 유지합니다. 실제 배치·모션 근거와 한계는 `Docs/TidePoolPlacement-20261003.md`에 기록합니다.

## 입력과 전투 시간

입력은 `GameSettingsService`의 GameAction을 거칩니다. 자동 입력도 `StageValidationInput`에서 같은 행동 조회 경로에 값을 공급합니다. 실제 실행에서 기본값은 비활성입니다. GameAction 값은 저장 호환을 위해 append-only이며 20..22는 성장4·5·6입니다. 무기1·2·3 키는 성장 모달에서 앞3선택을 겸합니다. 모달 처리 후 return하므로 선택 입력이 무기 교체로 새지 않습니다.

전체 지도와 가방은 일시정지 모달입니다. GameController가 PlayerController보다 먼저 입력을 처리하고, 공통 modalCloseFrame으로 닫는 프레임의 공격·대시·교체를 차단한 뒤 다음 프레임에 시뮬레이션 시간을 재개합니다. 지도 표시 중에는 일반 HUD와 첫 조작 안내를 숨깁니다.

`GameTick`은60Hz 물리 기준이고, 플레이어 피격 무적은60틱입니다. `PlayerController`는 입력 버퍼·대시·가드·패링·무기 행동을 담당합니다. `MeleeMotionTiming`의 준비/타격/회수 진행률을 본체·손무기·판정이 공유합니다. 공격 시작 방향/피해를 확정하고 타격은 한 번만 처리합니다. 대시·교체·사망은 대기 판정을 취소하되 쿨다운과 탄약 소유권을 초기화하지 않습니다.

근접 공격은 실제 공격 상자와 겹친 몸체의 가장 가까운 표면까지 시야를 검사합니다. 북쪽 벽에서 큰 몸체의 중앙이 벽 뒤로 투영되어도 노출된 부분은 타격할 수 있고, 벽이 몸체 전체를 가리면 피해가 차단됩니다. `MeleeWallContactFixture`는 실제 최종 보스 지형의 재현 좌표와 완전 가림 대조군을 검사합니다.

현재 영웅은 `External/SscaryMale/{idle,walk,dash,death}/{6시점}/00..07`의 원본192포즈와 그림자1장입니다. 48×64/32PPU/발(24,22),VisualRoot offset(0,-.28)을 고정합니다. `SscaryHandSockets`의 제작 시 정렬한144좌표가 손 위치를 결정합니다. `AdvancePoseClock`을 Update와LateUpdate가 같은 프레임에 공유하고 보행은 기본1.5루프/초(8장12fps), 최대14.4fps입니다. 대기는8장1.6초이며 일시정지 시간을 따릅니다. 원거리 장비는 사격 사이에도 몸체가 조준을 유지하고, 후진은 보행 위상을 유지한 역방향 재생입니다. 대시는 이동 방향, 비공격 근접 이동은 이동 방향을 따릅니다. 전용 찌르기는 SscaryThrustFrames의 정지/보행 조합을 사용합니다. 횡베기·되베기·내려찍기는 SscaryMeleeFrames의 별도 native 관절 포즈를 사용합니다. 가드·패링·차지/해제는 SscaryDefenseFrames의 native 관절 포즈를 사용한다. 원거리는 SscaryRangedFrames의6클립/6시점/대기8·보행8/16키 조합으로 별도의 팔·시위·지원 손·반동/복귀를 사용한다. PrepareRangedShot은 실제 발사마다 표시 시계/sequence를 다시 시작하고 key0 손의 native 총구를 샘플한다. 이 시계는 실제 attackPoseTimer/쿨다운/마나/탄약/이동 판정을 변경하지 않는다. 개머리판과 발사 효과/탄환/잔상은 주인공 아래 그림 순서를 따라 원본 얼굴을 보존한다. 피격·재장전 전용 본체는 남으며 옛 생성 주인공을 재사용하지 않습니다.

`NativeWeaponArt`는 활을 제거하기 전31개 개별 원본 무기의 그립·총구·발사축을 보관합니다. `HeldWeaponView`와 `ShotMuzzlePosition`이 동일한 저작 좌표를 사용합니다. 대시 때 손무기는 전방 축을 낮춰 운반합니다. 원거리 held Sprite는 본체 아래, native 손은 그립 앞에 보이며 활의 과거 저작 좌표는 보존하지만 현재 장비 풀에서는 활4종을 제공하지 않습니다. `ItemIconLibrary`는24장신구와10과거패시브별칭을 같은 원본으로 연결하고 정수 화면 픽셀 확대를 담당합니다. `InventoryIconFactory`의 필드 전리품도 같은 텍스처를 사용합니다. 이전 장비92PNG는ArtReview로 이동해 Resources에서 제외했습니다.

2026-09-30 재검수에서 권총/산탄총5종과 장신구5종의 원본을 교체하고 GUID를 유지했습니다. `NativeCombatEffect`는 개별 원본 PNG를 공유 캐시에서 읽고 공통 캔버스로 재생합니다. 표시 크기는 실제 보이는 픽셀 폭으로 정하며 투사체 공격 상자와 분리합니다. `CombatPresentation`이 무기군/무기ID별 발사·탄환·마무리·충돌 효과를 선택합니다. 활성 효과가80개 이상이면 일회 효과의 추가 생성을 생략합니다. 반복 탄환 코어는 표시를 유지하고 실제 투사체와 함께 해제하며, 반복 장신구 효과는 각 행동의 별도 개수/시간 상한을 따릅니다. `PlayerProjectile`은 실제 BoxCast로 벽을 차단하고 대상 집합/종료 플래그로 중복 피격·폭발을 막습니다. 대포/플린트록의 주변 폭발은 벽 시야를 확인하고 직격 대상을 제외합니다. `PixelTrail.shader`는 잔상의 정점색을 직접 렌더링하며 Resources 참조로 빌드 포함을 보장합니다.

`ItemCombatEffects`는 동일 런의 PlayerController에 붙어 실제 소유 장신구·고유 ID 세트 수를 조회합니다. 원본 적중은 적/공격 시퀀스로 중복을 제거하며, 산탄·지연 연사·직격/폭발이 같은 행동을 여러 번 세지 않습니다. 마나 소비는 성공한 유료 행동에만, 대시 잔류는 실제 Rigidbody 이동 위치에만, 방어 반응은 실제 가드·패링 성공에만 연결합니다. 공유 쿨다운은 소유자 수명과 함께 유지하고 일시정지/전환 안전 상태에서는 시간을 멈춥니다. 효과의 중복 강도는3사본까지이며 기존 능력치 보너스와 별개입니다.

출혈·회복 파편·연쇄 번개·메아리·유한 마력구·대시 바람·보호막·방어 반격·표식 확산의 대상을 각각 제한합니다. 실제 DamageCollider, 대상 중복 제거와 Ground 시야를 사용하고 발동 피해는 `EnemyController.TakeItemDamage`로 구분합니다. 이 경로는 원본 적중을 만들지 않고 기본 공격의 카메라 피드백 쿨다운을 점유하지 않습니다. 처치는 기존 콜백으로1회 보상하며 발동 피해 처치는 출혈 전파를 만들지 않습니다. 보호막이 모두 흡수한 피해는 PlayerStats의 최소1 피해 경로를 건너뜁니다. 세션 정리/사망/전환은 일시 효과를 제거하지만 런 내부 공유 쿨다운과 행동 누적 횟수를 초기화하지 않습니다. 런 종료 시 소유자와 함께 해제됩니다.

NativeCombatEffect는 부모의 실제 크기를 상쇄해 축소된 적·회복 파편에 붙어도 요청한 월드 크기와 원본 비율을 유지합니다. Thunder는 짧은 원본 셀을 실제 확인된 연결 경로에 놓고 주인공 뒤에 그리며, 긴 이미지를 비균일하게 늘리지 않습니다. 툴팁은 기존 Galmuri/IMGUI의 실제 줄바꿈 계산을 사용합니다. 이벤트 경로·효과 규칙·최신 검사 범위는 `Docs/ItemCombatEffects-20261001.md`를 참조합니다.

일반 적6종은 `ExternalCreatureArt`가 CraftPix의 원본 방향별 포즈를 선택합니다. Charger는 Orc3의 대기4·이동8·피격6·사망8장을 사용하고, 실제 돌진의 예고/이동/회복 단계에 정지공격→이동공격→정지공격의 원본 포즈를 연결합니다. 공통64px 캔버스/발 피벗/32PPU를 유지하며 이전 멧돼지 원본은 보존합니다. Tidekeeper는 `TidekeeperArt`의 Kronovi 원본 부유/충전/두 주문/부분 접기 피격/붕괴 포즈를 사용하며 `IsTideLaneCast`와 실제 AttackVisualPhase 시계로 구별합니다. 공통100px 캔버스/지면 피벗(49,6)/32PPU를 유지하고 떠 있는 본체의 고정 피격 영역을 사용합니다. 보스 사망은 물리와 이동/피격 콜라이더를 함께 끕니다. 실제 고유 포즈 수·원본 출처·실패/통과 자료는 `Docs/TidekeeperMotion-20261003.md`에 기록합니다. SealHeart는 `SealHeartArt`의 Foozle Ancient 네 방향 원본192셀(클립별 고유 RGBA 합166)을 사용합니다. 대기·이동·단방향 피격·두 공격·사망을 실제 행동 시계에 연결하며, 공격 방향은 시작 시 고정합니다. 공통96px 캔버스/피벗(48,32)/32PPU를 사용하고 원본80px는 투명 여백만 추가합니다. 본체 피격 상자는 칼 끝을 제외한0.95×1.45이며 기존 발 충돌과 공격 시간을 유지합니다. 출처·임포트 실패 보존·현재 검증 범위는 `Docs/SealHeartNative-20261003.md`에 기록합니다. Warden·주인공 전용 피격 본체와 전체 아트 최종 검토는 남아 있습니다. `EnemySpawnTelegraph`는 모든 적/보스에 SrZolda36프레임 공통 마법진을 사용합니다. 기존0.85초 예고 내에서 문양을 완성한 뒤 적을 생성하고0.16초 동안 잔광을 제거합니다.

`CameraFollow`는 Built-in PixelPerfectCamera의640×360 구도/32PPU/ortho5.625를 유지하되 upscaleRT=false로 창 해상도에 직접 그립니다. 고정640×360 중간 버퍼의 확대가 회전 무기와 이동을 굵은 픽셀에 묶던 문제를 줄입니다. 물리 좌표와 회전은 연속값, 에셋은 Point/무압축이며 AA·HDR·비균등 stretch는 사용하지 않습니다.

치명피해는 즉시 이동/공격을 차단하고 정산합니다. 죽음8포즈는 일시정지 상태에서도 unscaled0.85초 동안 진행하며, HUD는 완료 후 결과를 표시합니다. 손무기와 자원 게이지는 죽으면 숨깁니다.

## 성장과 저장

`RunRandom`은 시드·스트림·인덱스로 임시 RNG를 만들고 전역 상태를 복원합니다. `RunContent`가42무기/24장신구 추첨, 운/희귀도/추가 드롭, 서로 다른6개 성장 선택, 스테이지별 적 조합을 제공합니다. 효과 수치는 WeaponLootTable/AccessoryLootTable/PlayerStats, 세트는 ItemSetCatalog가 원본입니다.

`RewardPresentation`은 희귀도별 상자/등장/수집의 색·타이밍·원본 효과·음향을 정의합니다. `RewardChest`는 각 등급의6고유 개방 포즈를 사용하고 세 번째 포즈에서 콜백을 한 번 호출합니다. GameController와 탐색 보상은 닫힌 상자의 현재 Luck/시드 추첨을 캐시하고 개방 직전에 결과를 확정해 외형과 실제 내용물을 일치시킵니다. `FieldLootPickup`은 지형/벽/경로/다른 드롭/표시 아이콘 영역을 고려해 착지점을 고릅니다. 잘못된 생성점은 접근 가능한 바닥으로 복구합니다. 지면 마커와 떠 있는 아이콘을 별도 Transform으로 두고 원본 아이콘의 중앙을 보정합니다. 정착 후 가장 가까운 아이템만 입력을 받으며 한 프레임의 E를 하나만 소비합니다. 세션 종료에서 정적 active 집합과 원본 효과를 정리합니다.

`InventorySystem`의 가방 셀은 위치를 소유하고 목록은 데이터 조회용입니다. 신규 획득은 첫 빈칸, 교환은 지정된 칸만 변경합니다. 장착3슬롯은 가방 용량에서 제외합니다. `OwnedWeapon` / `OwnedAccessory`가 복사본 식별·+0~+3 단계와 무기별 탄약/장전 진행을 소유하고 `WeaponSlotState`는 그 객체의 위치를 보관합니다. 이동/장착은 객체를 복제하지 않으며 강화 효과 정의는 공유 드롭 원본과 분리합니다. 동일 장신구 사본은 각각 능력치가 있지만 세트 조각은 고유 아이템 ID로 집계합니다.

`PlayerStats.BindInventory`는 소유 목록 변경을 구독하고 기본값·유산·특성·현재 장신구/세트를 재계산합니다. 획득의 최대 자원 증가분만1회 회복하고 이동/강화/분해/재계산은 절대 현재 자원을 유지하거나 새 최대값으로 제한합니다. 기존 획득 콜백의 ApplyAccessory/ApplySetBonus는 bound 상태에서 재누적하지 않습니다. 소유자 파괴에서 구독을 해제합니다.

런 전용 강화석은 구역별 토큰을 사용하는 전투 클리어와 수동 분해로 지급합니다. `GameController.Equipment`가 비전투 강화·소유 복사본 선택·분해 확인·기대 단계/가방 버전·입력 연타를 검증합니다. `EquipmentUpgradeRules`는 구매 전 비교 및 공유 발동 단계 설명을 제공하고 기존 HUD에 장비 정비/세트 탭을 표시합니다. 강화는 실제 무기군/장신구 운용에도 적용합니다. 제거 후 더 강한 지속 발동은 정리하되 공유 쿨다운은 유지합니다. 저장 슬롯 필드는 변경하지 않았고 런 중간 저장은 추가하지 않았습니다. 세부 검사 범위는 `Docs/OwnedEquipment-20261001.md`입니다.

가방은 클릭·드래그와 이동/슬롯 키를 함께 지원합니다. InventoryFocusCell이 선택 칸을 보관하고 BagColumn/BagRow/BagCell이 기본5×4 및 확장 열의 물리 배치를 HUD와 입력에 공유합니다. 무기 칸에서 슬롯 키를 누르면 기존 AssignWeaponToSlot 경로를 사용해 탄약과 장전 시간을 유지합니다. 자동 검증도 이 일반 키 경로로 가방의 새 무기를 장착하며 직접 장비를 주입하지 않습니다.

`SaveSlotService`는3슬롯의 요약/프로필/원자적 저장과 백업 복구를 담당합니다. `MetaProgressionService`는 유산 규칙만 알고 저장 경로를 알지 못합니다. 기존 victories를 보존하고 campaignVictories/bestCampaignStage를 추가했습니다. 미등록/중복/범위 밖 강화 값은 정규화합니다. 런 중간 저장은 구현하지 않습니다.

`GameSettingsService`는 공용 음량·화면·기본/보조 키를 같은 원자적 교체/백업 방식으로 저장합니다. 손상된 기본 파일→백업→기본값 순서로 복구합니다. JsonUtility의 null 배열 객체 처리 차이를 보정하고 유효한 사용자 키를 유지합니다. 새 행동 기본키가 기존 커스텀 키를 가져가지 않습니다. 검증 프로필 실행에서는 슬롯과 설정 모두 분리 경로를 사용합니다.

## 표시·오디오·검증

`PrototypeHud`, `PixelUiSkin`, `PixelHudSkin`, `DungeonMapView`는 IMGUI 기반 표시와 입력만 담당합니다. Galmuri는 Resources에서 읽고 메뉴 로고/프레임/슬롯은 독립 이미지입니다. 월드는 Built-in+Pixel Perfect 640×360구도/32PPU이며 창 해상도에 직접 그립니다. UI 정수 배율은 독립적입니다. 비정형 창의 최종 UI 검수는 진행 중입니다.

`PrototypeAudioController.SetStage`는 스테이지별 음악/환경음을 연결합니다. 사용자 음량은 Master/Music/Interface/Effects/Ambience로 표시하며 기존 Player/Monster/Inventory 저장 채널은 Effects 호환 경로로 유지합니다. 음원 원본·합성 스크립트와 외부 아트 출처는 ASSET_SOURCES/ArtDirection에 기록합니다.

`CombatAudio`는 원본 전투/보상 WAV24개와 최대16개2D AudioSource를 사용합니다. 원본 음량 보정은 사용자 채널/마스터 설정을 적용하기 전에 수행하며 UI음과 게임 진행 중 보상음을 구분해 일시정지/재개/런 정리를 따릅니다. 변동 pitch는 별도 System.Random으로 생성하여 드롭/전투 RNG를 바꾸지 않습니다. `RuntimeSceneObjects.ClearSession`은 투사체·효과·전리품·전투 음원을 함께 정리합니다. 검증 범위와 원본 기록은 `Docs/EquipmentAndEffects-20260930.md`에 있습니다.

`RuntimeSmokeTest`와 `DemoContractValidation`은 최신 빌드 시작/리소스/카탈로그/활성 원본 주인공192포즈와 그림자/기본 상태 계약입니다. `Tools/Validation`의 전투·전환·저장·사망·입력 fixture는 상태 주입 격리 검사입니다. `AutomatedStageValidation`만 정상 GameAction으로 전투·보상·이동을 수행하며 RunEvidenceRecorder의 화면/CSV를 남깁니다. 자동 런도 메뉴 클릭 검증을 대신하지 않습니다.

`RunEvidenceRecorder`는 실제 화면과 실제 시간을 저장하고 영상 변환은 `Tools/Art/encode_run_recording.py`가 수행합니다. 이 기록의 프레임 간격은 성능 지표로 쓰지 않습니다. 성능/메모리는 기록 오버헤드 없는 별도 실행에서 측정해야 합니다. 최신 판정과 아직 남은 사항은 WORK_STATUS.md를 기준으로 합니다.


2026-10-01: CombatPresentation을 NativeCombatEffect에서 분리해 무기별 발사/궤적/적중/잔여 연출을 정의한다. 공통 ImpactRing 경로와 총기 Fireball 기본 표시를 제거하고 실탄/볼트/화살/포탄/공허 탄을 구분했다. NativeCombatEffect는 고정 원본 포즈·지연 표시·부모 크기 보정만 소유하며 피해 콜백이 없다. WeaponSecondaryParticles는64원본 파편 상한과 지형·일시정지·수명·세션 정리를 따른다. WeaponPreparationEffect는 실제 대검 충전 상태만 읽고 해제/대시/교체/사망에서 정리된다. 폭발 표시를 실제 Blast의 한 번 판정으로 옮겨 강화 반경과 일치시켰다. 무기별 기록과 한계는 Docs/WeaponEffects-20261001.md 참조.

## 2026-10-01 주인공 찌르기 본체
PlayerAnimationController는 IsThrustActive의 실제 AttackAnimationProgress로 SscaryThrustFrames의16시간 키를 선택한다. 6시점×정지/8원본 보행 조합864샘플은503고유48×64 PNG를 공유한다. 스트라이드 시계는 공격 동안 계속 진행하고, 손 좌표와 단일 본체 Sprite가 같은 샘플을 사용한다. 캐릭터 identity/발 피벗/32PPU/Point 유지, 런타임 변형·추가 데미지 콜백 없음. 대시/일시정지/취소는 기존 PlayerController 상태를 따른다. MeleeThrustEffect의 잔류 빛은 DefaultExecutionOrder300에서 시전자 몸 아래로 높이 정렬을 따라가며 얼굴/그립을 가리지 않는다. 전체 전투 본체 완료 플래그는false, 찌르기·횡베기·내려찍기 플래그는true. 네이티브 저작·해시·시간·실제 입력 검수 범위와 실패 기록은 Docs/HeroThrust-20261001.md에 있다.

## 횡베기·되베기·양손 내려찍기

SscaryMeleeFrames는3클립×6시점×대기8/보행8×16시간 키의4608샘플을2910고유PNG로 공유한다. 원본 머리/얼굴/배낭은 rigid 픽셀 형태를 유지하고 새로운 관절/닫힌 손/보조 팔과 원본 디딤으로 구성한다. 최초/최종 키는 원본 source와 정확히 같다. 몸과 손은 실제 AttackAnimationProgress, independent idle/stride와 같은 샘플을 사용한다. 근접 접촉 .42는 유지한다. HandBehindBody는 미리 검토한 손의 앞뒤 관계이며 HeldWeaponView의 앞뒤 정렬에 반영한다. MeleeSlashEffect/ExternalCombatEffect는 시전자 몸 아래에 전체 큰 참격을 그려 얼굴/손을 보존한다. 내려찍기 무기 회전은 .18준비/.42접촉/.68후속/1회수로 끝나며 입력/피해/자원/쿨다운을 변경하지 않는다. CrossCut 본체는 좌우 베기를 공유하고 전용 교차 효과는 유지한다. 가드/차지/패링/원거리/피격 전용 본체와 전체 전투 완료는 아직 미완료다. Docs/HeroMelee-20261001.md에 실제 검증 범위와 보존 시안을 기록한다.

단검은 SscaryShortBladeFrames의 짧은 손목 베기/되베기1169고유PNG/3072원본 대기·보행 조합을 별도로 사용한다. 빠른0.15초 안팎의 실제 공격에서 큰 검과 같은 과한 팔 이동을 없앴다. 철퇴 Impact의4장짜리 흰 기둥을 제거하고10포즈SlamDust→지연GroundDust→중력StoneChip으로 재구성했다. MaceSlam의공통28사각형 Burst도 제거했다. NativeCombatEffect.BelowActor는 큰 지면 효과의 sorting을 시전자 몸 아래로 매LateUpdate 따라가게 하며 다른 효과의피해/입력 콜백은추가하지않는다.


## 2026-10-01 방어·차지/해제와 전환

SscaryDefenseFrames의4클립×6시점×대기8/보행8×16키는6144샘플을2857고유PNG로 공유한다. 가드/패링/차지/해제의 손·본체·무기 회전은 같은 실제 상태 시계를 따른다. Charge end/ChargeCut start의 Sprite·grip·behind는 동일한 native 샘플이며 부분 해제는 실제 직전 키를 캡처한다. 실제 피해 접촉.42,방어window/마나/공격·이동 속도는 유지한다.

가드의0.22초 진입과0.14초 복귀는 표시만 처리한다. 공격→가드의 잔여 native 회복은0.065초 안에서 끝내며 방어 bool/입력에 지연을 추가하지 않는다. 대시·교체·사망은 준비 cache를 즉시 제거한다. 패링 성공은 기존0.22초 회복을 직전 native 키에서 이어간다. HasAuthoredCombatPoses는 원거리/피격 등이 남아 false다.

HeldWeaponView는 본체의 authored 각도와 grip을 즉시 함께 사용한다. 내려찍기는 위로 준비→접촉0도→아래 후속→중립이다. 위쪽을 향하는 검은 몸 뒤, 낮은 검은 몸 앞에 정렬하며 가려진 손은 항상 뒤다. 대시는 모든 무기를 낮춰 얼굴을 피한다. BladePoint는 실제 sprite 전방 축과 flipY를 반영한다. 차지 빛/방어 금속 접촉은 이 위치를 사용하고 큰 효과는 몸 아래에 둔다. NativeCombatEffect의 선택적 loop FPS 설정은 차지의 원본 포즈에만18fps를 지정하며 다른 효과의 기존 속도/피해/수명은 유지한다.

최종 증거/실패/수정 전 후보와 AP 실행 경로: Docs/HeroDefense-20261001.md. 원본 source/native identity와 현재 SpriteEditor API 임포트 기록은 ArtDirection/Review-20261001/hero-defense에 있다.

2026-10-01 대검 내려찍기: NativeCombatEffect의 CreateCleave가 실제 AttackAnimationSequence/Progress/ContactsResolved를 읽는다. 피해를 예약하지 않으며 실제 접촉 프레임에 원본 bright key4를 보여준다. LateUpdate에서 HeldWeaponView.BladePoint(0)로 표시 그립을 따른다. CancelMeleeAttack은 CancelCleave로 즉시 숨긴다. broad crossguard 및 충전 완료 MetalSpark는 본체 아래이고, 준비 수명이 완료 불꽃까지 정리한다. ChargedCut Y형 시안은 ArtReview로 이동하여 Resources에서 제외한다. 자세한 실제 화면/실패/검증 범위는 Docs/GreatswordEffects-20261001.md.

2026-10-01 근접 Sweep: MeleeSlashEffect.CreateBound는 실제 공격 시계/접촉 카운터를 읽고 LateUpdate에서 표시 그립을 따른다. CancelMeleeAttack이 bound slash를 즉시 숨긴다. 카타나 원본57×42의4칸/피크1, 다른 native 곡선5~6칸/피크2이며 Circular 접촉 축은 위쪽이다. 피해를 예약하거나 재생하지 않는다. native 손 앞에 그립을 감싸고 가드·접촉/파편은 본체 아래에 그린다. Enemy.ApplyDamage의 중복 RuntimeSquare hit burst를 제외했다. 상세 실패·실제 캡처·검증 한계는 Docs/MeleeEffects-20261001.md.


## 2026-10-01 근접 전방 피해 범위

`MeleeSweepGeometry.TryEnvelope`은 손에 붙은 Sweep/Cleave의 원본 접촉 픽셀 끝을 기존 표시 크기로 계산하고 기존 판정의 가까운 경계/폭은 유지해 전방 길이를 제한한다. `PlayerAnimationController.MeleeHandPosition`은 Update 접촉과 LateUpdate 표시가 같은 native 포즈/보행 시계를 읽도록 한다. 이 과정은 피해·입력·애니메이션 시간을 재예약하지 않는다. 다른 모션은 기존 판정을 사용한다. 계약/304물리 경계/새 모션/AT 빌드 근거와 범위는 Docs/MeleeForwardRange-20261001.md다.


## 2026-10-01 native physical stab AU

MeleeThrustEffect binds fixed native tip/streak cells to HeldWeaponView.BladePoint and the committed action sequence/contact counter (.34). It owns no damage callbacks. MeleeHandPosition samples the current Sscary thrust socket before LateUpdate; MeleeSweepGeometry aligns the physical box to that socket and exact contact-cell forward/side extent. Uniform native display sizes are independent of old broad hitbox art stretching. Dash/swap/death/menu end owned presentation; early longsword guard retains its existing recovery rule. Enemy world HP bars now follow world depth below the hero instead of fixed32000 foreground. Current scope, failed fixtures and AU proof: Docs/ThrustEffects-20261001.md. Full campaign/art/performance goal remains unfinished.


## 2026-10-02 armed grip and family standing profiles

PlayerAnimationController selects native SscaryArmedFrames for real melee/defense/ranged action clocks and WeaponStandingFrames for six family-specific resting/short-return profiles. Armed facing follows committed/current aim while the original gait advances independently (reversed for backpedaling). HeldWeaponView binds the selected closed-hand socket and authored angle; the .14s visual return never gates gameplay input. Instant ranged emission uses the native action key0 muzzle/body clock. Point/no-mip/uncompressed48×64 PNGs retain the original rigid face/backpack/foot pivot.

MeleeCrossCutEffect follows two distinct native wrist actions and original57×42 katana cells. Actual contacts at .22/.52 split one action's damage and share crit/action-target proc rules; +3MP consumption is once per action. Cancellation and slow-frame contact presentation do not leave delayed second damage or a simultaneous giant cross/star. Native standing combinations and actual authored action poses are separate metrics. Dedicated hurt/reload remain pending. Current source/build/visual evidence and limits: Docs/WeaponStances-20261002.md, Docs/CrossCutEffects-20261002.md.


## Native frame catalog and built-in atlas sources — 2026-10-02

NativeHeroFrameCatalog preserves existing logical frame IDs while referencing individual PNG Sprites outside Resources. NativeHeroAtlasPrebuild (IPreprocessBuildWithReport) writes two source-derived catalogs and lossless V2 atlases; TerrainAtlasPrebuild retains the other external/terrain sources. No rotated/tight meshes, resampling, new action clocks or source file deletion. Retired action/stance PNGs remain available to Editor authoring through IndividualArt aliases and retain GUID/metadata. RuntimeSmoke compares every current full packed rectangle with its original visible-pixel SHA; opt-in NativeHeroAtlasValidation independently renders all14349 actual SpriteRenderer frames at native1:1 on the Windows GPU. Detailed build contents exclude raw native Texture2D duplicates and all archived sprites. CameraFollow retains its640x360 framing/32 worldPPU and runtime AA/MSAA/HDR/dynamic-resolution guards. Scope/failures/current AW evidence: Docs/NativeFrameAtlases-20261002.md. Whole-game completion and actual performance remain pending.

## 2026-10-03 아이템 확장과 활 제거

`WeaponFamily.Bow`의 직렬화 값과 원본은 보존하며 네 정의를 활성 `WeaponLootTable`에서 제외한다. `AccessoryType`은 기존0~17을 유지하고18~23에 여섯 신규 유형을 추가한다. 기존 패시브 별칭은 RubyHeart~SoulCapacitor 범위만 변환하여 신규 ID의 아이콘을 사용한다.

`ItemCombatEffects`의 직접 공격 ID, 공통 시계, 최고 소유 강화와 최대3복사본 규칙으로 자원 회복·방어 보상·대시 후 타격·정지 준비·빈 탄창 장전 충격파·저체력 보호막을 운용한다. `InventorySystem.ReloadCompleted`는 실제 소유 무기의 장전 전/후 상태를 비교해 완료를 한 번 알린다. 화약병은 현재 장착한 무기의 빈 탄창 완료만 받으며 이전 구독을 해제하고 파괴 시에도 구독을 정리한다. 장비 이동과 stage cleanup은 발동 쿨다운을 초기화하지 않는다.

`ItemTooltipLayout`은 실제 Galmuri `CalcHeight`로 제목·종류·본문·세트 영역을 분리하고 화면 여백에 제한한다. 본문은 별도 스크롤이며 아이템에 마우스를 둔 채 휠을 사용한다. 현재24종×4강화 단계의 실제 OnGUI 측정과 세 해상도 화면, 제한된 높이의 GameView 휠 이벤트를 검증한다. 세부 근거는 `Docs/ItemExpansion-20261003.md`에 있다.

## 2026-10-03 five-family reload and dash occlusion

WeaponReloadFrames V3 selects only Crossbow/Pistol/Rifle/Shotgun/Cannon from preserved V2 native PNGs: 7,629 unique images / 20,480 carry-or-shot/direction/gait/time samples. PlayerAnimationController keys the presentation to OwnedWeapon.Identity + ReloadSequence, completes real shot recoil before mapping the remaining reload timer, and preserves independent gait. Input, ammo timing and ownership rules are unchanged. NativeHeroFrameCatalog now exposes three catalogs (21,978 frames); prebuild and smoke enumerate their expected sizes. V2 stays archived and unpacked. Ranged dash stocks remain behind the body; existing Ninja Adventure GroundDust replaces foreground square dash sparks. Source identity, 80 reload cases, 40 eight-direction transitions, current screen pixels and AY Windows evidence: Docs/FirearmReload-20261003.md. Dedicated hurt and full campaign/art/performance completion remain pending.

## 2026-10-03 대시 잔상과 HUD

`DashAfterimageEffect`는 실행 순서300의 LateUpdate에서 실제 본체/무기 SpriteRenderer의 픽셀·피벗·변환을 함께 채취한다. 최대4쌍의 렌더러만 재사용하고 현재 캐릭터 뒤에서0.18초 동안 감쇠한다. 일시정지는 수명을 멈추고 `RuntimeSceneObjects.ClearSession`, 순간이동, 비활성화와 소유자 파괴는 효과를 정리한다. 대시 판정/이동 시계는 변경하지 않는다. `PlayerResourceDisplay`는 중앙에 고정된 두 행에 대시와 탄약/장전을 배치한다. `PrototypeHud`의 전용 퀵슬롯 글꼴은 다른 설명 UI와 분리하며 `ItemIconLibrary.ContentBounds`로 아이콘의 실제 보이는 경계를 중앙 정렬한다. 현재 Editor/Windows 근거: `Docs/DashHud-20261003.md`.

## 2026-10-04 보스 공격 선택과 피해 관찰

`EnemyController.UpdateBoss`는 Warden이 가까운 거리에서 시작하면 첫 패턴만 돌진으로 선택한 뒤 방사·연사·돌진·도약의 순환을 유지한다. 매번 근접 거리를 검사해 선택한 패턴을 돌진으로 덮어쓰지 않는다. 공격 피해·예고/활성/회복·도약 시계는 그대로다. 정상 성장 후 패턴이 생략되는 문제를 관찰해 기본 Tidekeeper HP를1800, SealHeart를2800으로 조정했다. Warden430과 기존 작성된 BalanceConfig 오버라이드는 유지한다. 이는 캠페인 분량을 맞추는 조정이나 전체 균형 합격이 아니다.

`BossCombatObservation`은 명시적 정상 자동 입력 검증의 `--combat-pressure-trace`에서만 생성된다. 방어력 적용 후 원본/아이템 피해를 남은HP까지 합산하고, 실제 AI의 시작·활성·회복과 첫 적중~사망의 게임 시각을 기록한다. 입력·시간·능력치·배우를 변경하지 않으며 일반 실행에는 관찰기가 없다. 장착 무기 이름은 관찰 시점의 상태라 교체 전 투사체의 원본을 뜻하지 않는다. `AssessBossPressure20261004.py`는 실행 DLL SHA, 정상 완료, CSV/JSON 기여도와 체력 합계를 대조한다. 공급 상태의 패턴 검사와 정상 캠페인·사람 기준 분량·전경 성능을 구분한다. 전후 근거와 남은 범위: `Docs/BossPressure-20261004.md`.


## 2026-10-03 피격과 보호막 표시

`PlayerController.TakeDamage`는 가드 감쇠→아이템 흡수→남은 체력 피해의 기존 순서를 유지한다. 완전 흡수일 때 hurt 타이머/추가 knockback을 주지 않으며 짧은 푸른 표시와 방어음을 사용한다. 실제 피해는 `NativeCombatEffect`의 PlayerHit 원본5셀/0.18초와 짧은 붉은 표시를 사용한다. 가드 표시는 호박색이며 기존 약한 밀침을 유지한다. 무적 틱과 일반 입력/공격 시계는 변경하지 않는다. 접촉/보호막은 BodyRenderer를 따라 몸체 뒤로 정렬한다. 전용 hurt 본체 카탈로그는 추가하지 않았다. 관련 한계·BA 검증은 Docs/PlayerHitFeedback-20261003.md.


## 2026-10-03 적 피격 표시와 사망 시계

`EnemyController`의 presentationTint는 현재 행동의 기본 색이며 hitFlashRemaining은 마지막 피격의 0.055초 표시 시계다. LateUpdate가 마지막 피격 프레임 이후에만 시간을 차감하고 현재 행동색으로 돌아간다. 겹치는 코루틴이 서로 색을 지우지 않는다. 일반 적과 보스 사망은 AnimationPaused를 따르며 보스는 scaled deltaTime으로 HP 표시와 완료 콜백을 함께 진행한다. 일반 적의 즉시 사망 콜백과 보스의 사망 종료 콜백 규칙은 유지한다. 원본 픽셀 사망에는 공통 RuntimeSquare 버스트를 겹치지 않는다. 기존 프레임 수 부족은 Docs/EnemyReactions-20261003.md에 별도로 기록한다.
# 2026-10-03 심부 바닥 표시

`NativeStageGround`의 stage 2는 Champi_S 원본의 조용한 바탕을 사용한다. `NativeCoreGround`는 실제 통행 경로와 공간 마모를 기준으로 16종 포석을 배치하고, 충돌 섬도 포함한 별도 재질 셀 집합 위에 그림을 이어 놓는다. 원래 Walkable·콜라이더·내비게이션은 변경하지 않는다. 바닥과 접합부 복사본은 같은 `CoreStone` 머티리얼을 사용한다.

포석은 원본 Sprite의 vertices/UV/triangles 및 Color32를 묶어 텍스처당 한 MeshRenderer로 그린다. 아틀라스 패킹의 실제 UV를 사용하며, 컴포넌트 해제 때 생성 Mesh를 파괴한다. 임의 확대·런타임 이미지 생성·픽셀 재도색은 없다. 팔레트 조정은 Built-in `CoreStone.shader`, 공유 머티리얼은 Resources/Materials/CoreStone이다. 7,925포석/1메시와 이전 SpriteRenderer 방식의 네 정적 광장 전체 픽셀 동일성을 확인했다. 자세한 채택·실패·이동 검증 범위는 `Docs/CoreFloor-20261003.md`를 따른다.

## 2026-10-04 HUD 이름표 대비

`PrototypeHud.DrawHudName`은 캐시한 GUIContent와 실제 Galmuri 텍스트 폭으로 무기명·보스명의 명패 폭을 정한다. 기존 `gaugeFrameStyle`의 불투명 Slot 이미지와 픽셀 테두리를 재사용하고 시작점을 화면 픽셀에 정렬한다. 명패는 각각 퀵슬롯/보스 체력바 위에 2단위 여백을 두며, 슬롯의 키·탄약 폰트와 보이는 아이콘 중심 정렬은 유지한다. 변경 범위와 세 해상도 실제 화면 근거는 `Docs/HudReadability-20261004.md`에 기록한다.

## 2026-10-04 도약 높이와 공중 사망 연결

`EnemyController.LeapSlam`은 시각 루트의 기준 위치를 보관하고, 월드 높이를 부모 세로 배율로 나눠 로컬 위치에 적용한다. 본체의 기존 자식 배율은 보존한다. Warden의 의도된 높이1.6이 부모 배율 때문에3.36으로 커지던 문제를 수정한다. 도약 도중 사망하면 `SettleAirborneDeath`가 치명타 순간의 위치에서0.22게임 초 동안 지면 피벗으로 가속 낙하시키며 기존 사망 프레임·정산 시간을 따른다. 일시정지에는 낙하도 정지하고 이동 루트·발 충돌·공격 판정은 변경하지 않는다. `UiValidation`의 `--enemy-leap-fixtures`는 보스와 Leaper의 높이·착지·발 충돌·공중 사망·일시정지·보상1회를 따로 검사한다. 이 검사는 공급한 배우·보호 상태를 사용하므로 정상 캠페인 증거와 구분한다. 근거와 미해결 아트 범위는 `Docs/WardenLeap-20261004.md`에 기록한다.


## 2026-10-04 아이템 원본 표시 경로

활성 장신구24개는 기존Resources경로/GUID를 유지하며23개PNG만 무료 원본으로 교체한다. 최종 매핑은 `ArtDirection/Review-20261004/item-style/accepted-selection.json`, 검증·출처는 `Docs/AccessoryNativeSources-20261004.md`에 둔다. `mana_thread`와 `rush_spur`의 표시명만 바뀌며 타입/저장/효과 분기는 유지한다.

UI의 ItemIconLibrary는 불투명 영역을 정수 화면 픽셀 배율로 가운데에 그린다. InventoryIconFactory의 world Sprite는 동일PNG의 불투명영역/center pivot과 실제 importer PPU를 보존하며, FieldLootPickup은 scale1과 실제sprite.bounds로 착지 크기를 예약한다. 서로 다른 아이템의 최대변을 같은1칸으로 정규화하지 않는다. 현재66개장비PNG는32PPU다. 퀵슬롯80×92/icon52×54는720p의 긴 원본도 축소 없이 수용하고, 키·아이콘·탄약 영역을 분리한다.

근접 HeldWeaponView는body-2, MeleeEffectSorting은실제무기+1(body-1)이라 검광이 무기를 덮고 몸체 윤곽은 남는다. 바닥 효과/잔상은 기존뒤쪽정렬이고 PlayerResourceDisplay는독립전경SortingGroup이다. 이 정렬은 원거리 무기의 기존 앞뒤 처리를 바꾸지 않는다.
