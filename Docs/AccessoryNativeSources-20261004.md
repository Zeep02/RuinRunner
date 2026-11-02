# 장신구 원본 픽셀 아트 정리 — 2026-10-04

활성 장신구 24종 중 **23종 교체, 주사위 1종 유지**를 Unity에 적용했다. 최종 import는 `item-style/before/20261004-042156-980`에 교체 전 PNG/meta를 보존했고 24개 모두 원본 바이트·크기·GUID·Sprite ID·local file ID 검사를 통과했다. 720p/1080p 시각 검수 결과는 아래에 별도로 기록한다.

최종 파일 대응의 기준은 `ArtDirection/Review-20261004/item-style/accepted-selection.json`이다. 원본 PNG와 기존 meta를 백업한 뒤 원본 바이트를 복사하고, 기존 asset GUID·Sprite ID·local file ID를 유지한다. 원본 보존 여부는 파일명이나 육안이 아니라 SHA256와 실제 픽셀 크기로 확인한다. 시트에서 가져오는 16픽셀 셀은 원래 RGBA 픽셀을 그대로 잘라서 사용한다. 리샘플링·재색칠·생성 이미지 축소는 하지 않는다.

## 선택 기준과 예외

사용자가 지정한 기준은 총 에셋처럼 명확한 픽셀 덩어리와 검거나 아주 짙은 윤곽, 단계가 읽히는 명암이다. Snoopeth Things와 Alex DB32 아이템을 주요 기준으로 삼았다. 모든 아이템을 비슷한 문장이나 원형 장식으로 바꾸지 않고 깃털·반지·병·렌즈·실·장화 등 원래 물체가 읽히는지도 검토했다.

- `lucky_dice`는 기존 그림의 검은 윤곽과 명암이 충분히 명확하고 더 나은 무료 원본을 확보하지 못해 유지한다. 기존 36×36 canvas이며 이번 교체의 native16~32 범위에 맞췄다고 주장하지 않는다.
- `mana_thread`는 적합한 검정 윤곽 실타래 원본을 확보하지 못했다. 가는 선 후보 대신 Alex2의 푸른 잔을 선택하고 표시명을 **마나를 담는 잔**으로 맞췄다. 공격 적중으로 마나를 회수하는 효과·ID·enum·저장 대응은 유지했다.
- `rush_spur`는 정확한 무료 박차 원본을 찾지 못했다. 갈색 군화 원본과 뜻을 맞추도록 표시 이름을 **돌진자의 군화**로 정리하는 결정이다. ID, enum, 효과, 저장 데이터 대응은 유지한다.
- `ruby_heart`는 Snoopeth Things 36 원본(16×18)을 선택했다. 같은 그림을 사용하는 회복 파편도 native scale 1로 표시하며 예전36px용 .42 축소를 제거했다. 회복 수치·생성 조건·수명은 유지한다.
- `four_leaf_clover`는 최종적으로 Snoopeth Things 61 원본을 선택했다. Pixeltime 초록 잎·AntumDeluge 은색 펜던트는 조사 후보로만 남는다.

## 24종 최종 선택안

아래는 최종 의사결정 기준이며 실제 반영 여부는 manifest/import 결과가 우선한다. 숫자는 원본 canvas 픽셀 크기다.

| ID | 표시 이름 | 원본 | 픽셀 |
|---|---|---|---:|
| lucky_dice | 금 간 행운의 주사위 | 기존 Shikashi 주사위 유지 | 36×36 |
| storm_feather | 폭풍깃 | Alex RPG 2 / Item_70 | 16×16 |
| guardian_seal | 수호자의 인장 | Snoopeth Things / 2px/14 | 18×18 |
| crimson_ring | 진홍 반지 | Alex RPG 1 / Item__42 | 16×16 |
| mana_prism | 마나 프리즘 | Shade cave / c01 r01 | 16×16 |
| bulwark_brooch | 성벽 브로치 | Snoopeth Things / 2px/37 | 18×18 |
| berserker_emblem | 광전사의 문장 | Snoopeth Things / 2px/24 | 18×18 |
| sage_sigil | 대현자의 문양 | Snoopeth Things / 2px/40 | 18×18 |
| ruby_heart | 루비 심장 | Snoopeth Things / 2px/36 | 16×18 |
| sapphire_flask | 사파이어 플라스크 | Alex RPG 2 / Item_64 | 16×16 |
| guardian_plate | 수호자의 철판 | Alex RPG 1 / Item__57 | 16×16 |
| winged_boots | 날개 장화 | DCSS / urand_seven_league_boots | 32×32 |
| assassin_mark | 암살자의 표식 | Snoopeth Things / 2px/52 | 18×18 |
| executioners_coin | 처형자의 동전 | Snoopeth Things / 2px/30 | 18×18 |
| four_leaf_clover | 네잎클로버 | Snoopeth Things / 2px/61 | 18×18 |
| arcane_lens | 비전 렌즈 | Snoopeth Things / 2px/34 | 18×18 |
| vampire_fang | 흡혈귀의 송곳니 | Alex RPG 1 / Item__34 | 16×16 |
| soul_capacitor | 영혼 축전기 | Snoopeth Things / 2px/7 | 18×18 |
| mana_thread | 마나를 담는 잔 | Alex RPG 2 / Item_43 | 16×16 |
| guard_bell | 수호의 고리 | Alex RPG 1 / Item__40 | 16×16 |
| rush_spur | 돌진자의 군화 | Alex RPG 1 / Item__51 | 16×16 |
| steady_locket | 정조준 목걸이 | Alex RPG 1 / Item__32 | 16×16 |
| powder_satchel | 마지막 탄피의 화약병 | Shade potions / c00 r05 | 16×16 |
| last_light_pendant | 마지막 불빛 | Alex RPG 1 / Item__33 | 16×16 |

## 원본 출처와 사용 조건

| 출처 | 선택 수 | 라이선스 / 근거 |
|---|---:|---|
| [SnoopethDuckDuck Things](https://snoopethduckduck.itch.io/things) | 10 | CC0. DB32 원본, 상업 이용·수정 허용. `2px`는 저자가 제공한 윤곽선 판본이며 임의 업스케일판이 아니다. |
| [Alex RPG Item Pack 1](https://alexs-assets.itch.io/16x16-rpg-item-pack), [Pack 2](https://alexs-assets.itch.io/16x16-rpg-item-pack-2) | 10 | CC0. 저자가 native16, DB32, 검은 윤곽선을 명시. |
| [Shade Assorted RPG Icons](https://merchant-shade.itch.io/16x16-mixed-rpg-icons) | 2 | CC0. cave (16,16,16,16), potions (0,80,16,16) 원본 셀. 원본 전체 SHA·crop SHA·RGBA 동일성은 `merchant-gap-crop-manifest.json` 및 선택 manifest 참조. |
| DCSS — CanOfWorms | 1 | 날개 장화 제작자와 공식 CC0 허가를 연결. 아래 상세 근거 참조. |
| 기존 Shikashi Icons | 유지 1 | Matt Firth (shikashipx), game-icons.net. CC-BY 4.0. 기존 `Assets/StreamingAssets/ThirdPartyNotices/Shikashi-Icons.txt`의 출처와 조건 유지. 새 원본을 확보하거나 신규 제작한 것으로 계산하지 않음. |

DCSS 날개 장화는 [도입 커밋 e33e995](https://github.com/crawl/crawl/commit/e33e9958401823c0a6fa3d7dc913435df9db3d03)이 CanOfWorms를 제작자로 명시한다. [공식 ARTISTS.md](https://github.com/crawl/tiles/blob/master/ARTISTS.md)에서 해당 작가의 과거·미래 제출작을 CC0로 허용한다. 저장소 전체를 일괄 CC0라고 가정하지 않았다. 고정 커밋 원본과 취득 PNG의 SHA256은 동일하다. 원문과 SHA는 `ArtDirection/item-gaps/source-license-manifest.json` 및 같은 폴더의 이력 JSON에 보존했다.

DCSS ruby, AntumDeluge Lucky Charm, Pixeltime clover, Idylwild thread의 개별 허가도 확인했지만 최종 선택에서는 제외되었다. 그 출처·원본·크레딧 초안은 `ArtDirection/item-gaps/README.md`와 manifest에 조사 이력으로 보존한다. 최종 사용 에셋 수에 더하지 않는다.

## 픽셀 표시와 PPU 검증 기준

장신구 importer는 Point, no mipmap, Uncompressed, readable, FullRect, center pivot, 32 PPU다. 화면 UI는 투명 여백을 제외한 `ContentBounds`를 중앙에 배치하고 가능한 경우 정수 **물리 픽셀** 배율로 그린다. 원본 canvas가 서로 달라도 같은 크기의 정사각형으로 늘려 붙이지 않는다. 퀵슬롯은 80×92, 아이콘 영역은 52×54이며 키·탄약 텍스트와 겹치는지는 캡처로 확인한다.

월드 드롭은 `InventoryIconFactory.GetWorldSprite`가 **해당 imported Sprite PPU를 보존**하고 `FieldLootPickup`이 scale 1로 표시한다. 결과 크기는 visible pixels / PPU다. 검증에서 모든 아이템을 최대 길이 0.78 world units로 정규화하던 기대값을 제거했다.

무기 표본에는 `iron_greatsword`, `scrap_pistol`, `void_revolver`가 포함된다. `void_revolver`는 부모가 **기존 48 PPU에서 32 PPU로 명시적으로 수정**했다. 원본 PNG 바이트·rect·pivot·GUID 보존 근거는 `ArtDirection/Review-20261004/item-pixel-presentation/void-revolver-before/result.txt`다. fixture는 기존 값 48을 `priorImporterPPU` 이력으로 남기고 현재 importer 및 world Sprite가 승인된 값 **32**와 일치하는지 엄격히 검사한다. 나머지 두 표본은 기존 PPU와 일치를 요구한다. 임의 허용 오차 확장이나 역사 기록 덮어쓰기는 하지 않았다.

월드 투영 픽셀 배율과 UI 배율은 별개다. world Sprite가 현재 imported PPU를 보존한다는 사실만으로 모든 카메라에서 정수 투영 배율이라고 주장하지 않고 실제 카메라 기준 수치를 결과에 남긴다.

## 실제 검증 결과

`ArtDirection/Review-20261004/item-style/unity-review-20261004-042721-642/result.json`에서 PASS했다. Unity6000.6.0f1에서 실제 production 렌더 경로를 사용한 공급 상태 검사다.

- 720p/1080p 각각 가방2페이지·툴팁4종·아이콘 영역3종·퀵슬롯·필드 드롭, 총22장.
- 모든24종의 원본SHA/GUID/PPU/실제아이콘경로 일치. 회복 파편의 크기 변경은 컴파일 확인 범위이고 이번 가방/드롭 fixture가 회복 효과 발동을 검증한 것은 아니다.
- 화면에 그려진150개 아이콘 검사: 경계 오차0px, 중앙 오차 최대0.5px, 비정수 축소0개.
- 실제 FieldLootPickup48개 검사: scale1, imported32PPU, 원본1픽셀당720p2픽셀/1080p3픽셀 투영, 중심 일치.
- 기존 사용자 저장 파일 해시 불변, 게임/HUD/인벤토리/카메라/Game View 상태 복원. 새 콘솔 오류0건(기존 Editor 진단과 구분).
- 부모 및 독립 검수에서 가방·툴팁 텍스트 겹침/패널 밖 잘림을 발견하지 않았다. 날개 장화32px와 기존 주사위36px는 주력16~18px보다 묘사가 촘촘하고 작은 아이콘 칸에서는 작게 보이는 차이가 남는다. 원본 품질을 해치는 축소나 과도한 확대 보정은 하지 않았다.

검증은 실제 InventoryIconFactory/ItemIconLibrary/PrototypeHud/FieldLootPickup 경로지만 공급한 인벤토리와 정착시킨 드롭을 검사한다. 정상 획득·드롭 비행/수집·좁은 지형의 다수 아이템 배치·전투·새 Windows 빌드를 완료했다고 보고하지 않는다. 특히 FieldLootPickup의 극단적 혼잡 때 기존 착지 fallback은 남아 있다. 전체 게임 목표는 계속 미완료다.

첫 두 fixture 실패도 보존했다. `042333-625`는 Editor 상속 속성 검색에 FlattenHierarchy가 빠진 도구 오류였고 `042545-703`은 해상도 전환 중 이미 제거한 드롭에 라벨을 그리던 도구 오류였다. 각각 수정한 뒤 최종22장 전부 재실행했으며 두 실패에서도 savesUnchanged/stateRestored=true였다.

정확한24px 생성 시도는 요구 크기를 지키지 못해 미채택했다. `item-style/rejected-generated`에 원본·요청·크기 판정을 보존했고 생성물을 축소해 Assets에 넣지 않았다. 이번23종 교체는 모두 무료 외부 원본이다.
