# Ruin Runner — 사용 에셋과 제작 기록

현재 자료와 보존 이력을 구분한다. 활성 주인공은 사용자 선택 Sscary의 배낭 캐릭터이며 이전 생성 캐릭터와 거절된 Pawn은 런타임에 없다. 일반 적6종, 공통 소환진과 장비를 외부 원본으로 교체했다. 현재 활성 장비는 활4종을 제외한27무기·24장신구이며 이전31무기/18장신구 원본과 저작 이력은 보존한다. 구체적인 장비 선택/변경/라이선스 기록은 Docs/EquipmentArt-20260922.md와 배포 ThirdPartyNotices를 따른다.

| 범위 | 원본 / 제작자 | 사용·수정 기록 |
|---|---|---|

| 주인공 / 배낭 모험가 | [Sscary The Adventurer Male](https://sscary.itch.io/the-adventurer-male) | 공식 무료 원본6시점,4동작×8장+그림자193PNG. 원본 픽셀/배낭 유지, 손144좌표 저작. 2026-10-01 원본 얼굴·배낭/팔레트를 보존한 네이티브 찌르기와 보행 조합503고유PNG 추가.16시간 키의 실제 정지 포즈10~13장/방향이며864조합을 신규 포즈로 세지 않음. 무료 게임 사용/수정 허용, 독립 재배포/재판매 금지; Premium 구매/생성 픽셀 도입 없음. Docs/HeroThrust-20261001.md에 수정/검수/원본·시안 기록. 이어 native 횡베기·되베기·양손 내려찍기2910고유PNG/4608대기·보행 조합을 추가했고 방향별 정지 고유 포즈12~15장이다. 단검에는 별도 compact 손목 베기/되베기1169고유PNG/3072조합(정지8~14실제 포즈/방향)을 추가했다. 상세 Docs/HeroMelee-20261001.md. 이어 가드/패링/차지/해제2857native PNG/6144대기·보행 조합을 추가했다(Docs/HeroDefense-20261001.md). 활/쇠뇌/권총/소총/산탄총/대포4964native PNG/9216대기·보행 조합을 추가했다(정지7~15실제 포즈/방향, Docs/HeroRanged-20261001.md). 원본 얼굴/배낭/부츠/팔레트/캔버스 보존. 피격/재장전 전용 본체는 남는다. |
| 공통 등장 마법진 | [Magic Summons / SrZolda](https://srzolda.itch.io/magic-summons) | CC BY4.0,36프레임. 원본 exact8x 확대를64px 원본 격자로 무손실 복원,32PPU. |
| 기존31무기·18장신구 출처(활4종은 비활성 보존) | 7Soul / SnoopethDuckDuck / CraftPix / Shikashi / Kyrise | CC0, CC BY4.0, CraftPix Freebie 조건에 따라 선별. 개별 출처/셀/SHA/패딩/피벗은 native-item-selection의 검토 확정manifest와 Docs/EquipmentArt-20260922.md. 미채택 후보와 구분. |
| 신규 장신구6종 | 7Soul1 / Henrique Lazarini, 기존 496 Pixel Art Icons 팩 · CC0 | I_Fabric / Ac_Ring02 / A_Shoes03 / Ac_Necklace01 / I_Ink / Ac_Necklace06. 원본34×34 픽셀을 보존하고 투명 여백2픽셀만 추가해38×38 개별PNG로 임포트. Point/무압축/밉맵 없음/32PPU/중앙 피벗. ID별 SHA·GUID·출처는 Review-20261003/item-expansion/icon-import.json, 배포 고지는 StreamingAssets/ThirdPartyNotices/7Soul-Icons.txt. |
| 거절된 주인공 후보 / Pawn | [Tiny Swords Free Pack / Pixel Frog](https://pixelfrog-assets.itch.io/tiny-swords) | 현재 무료판 개인/상업 게임 사용·수정 허용, 독립 재배포/재판매/재포장 금지. 33개 원본 몸체 PNG와 그림자1개,192px canvas/64PPU. 원본 레이어 합성과 Object 장갑 분리, 손 좌표 연결. 8대기/6달리기/6스윙/4칼/6곡괭이/3망치. 좌우 반전2시점, 전용 뒷모습/피격/사망/대시 미제공. 사용자가 외관 거절. 후보 런타임 연결 제거, 이전 영웅으로 임시 복구. 자세한 제한과 검증은 Docs/NativeActors-20260922.md. |
| 일반 적5종 | [CraftPix Slimes](https://free-game-assets.itch.io/free-slime-mobs-pixel-art-top-down-sprite-pack), [Vampires](https://free-game-assets.itch.io/free-vampire-4-direction-pixel-character-sprite-pack) | 무료 공식 원본과 라이선스 사본 보존. Slime1/3/2, Vampires2/3의 원본4방향/대기/이동/공격/피격/사망을64px 단일PNG로 분리,32PPU/Point/무압축. 게임 내 사용 허용, 독립 재배포 금지. AI 학습에 사용하지 않음. |
| Charger / 갑옷 오크 | [Free Orc Pixel Characters Sprites / CraftPix](https://free-game-assets.itch.io/free-top-down-orc-game-character-pixel-art) | 공식 무료 Orc3 원본. 4방향×대기4·이동8·공격8·이동공격8·피격6·사망8=168개 개별 PNG.64px 공통 캔버스/32PPU/발 피벗(32,24), 원본 RGBA 셀 그대로 분리. 게임 사용·수정 허용, 독립 에셋 재배포 금지. ZIP SHA/원본/라이선스는 ExternalCandidates/20261003/craftpix-orcs, Unity 가져오기 기록은 Review-20261003/boar-motion/native-orc-import.json. |
| 보존 이력: 기존 Charger / 멧돼지 | [Diamond Top Down Pixel Art / Dotmancer](https://dotmancer.itch.io/diamond-top-down-pixel-art) | 공식 무료 full_version 원본.128px canvas/32PPU,4방향 대기/달리기/공격. 전용 피격/사망 클립 없어 2026-10-03 Orc3로 교체. 원본 PNG와 메타 보존. Paid 폴더는 구매를 뜻하지 않으며 Godot 소스 미통합. |
| 외곽 유적 환경·봉인 석제 장치 | [Pixel Art Top Down - Basic v1.2.3 / Cainos](https://cainos.itch.io/pixel-art-top-down-basic) | 공식 무료 원본, 상업 게임 이용/수정 허용·독립 에셋 재배포 금지. 원본 crop116개와 source metadata/read-only 추출 기록은 ExternalCandidates/20260921/cainos-basic. 게임 경로 External/Cainos,32PPU/Point/무압축. 풀·석길·벽·기둥·석상·등·나무를 연결하며 원본 색/픽셀은 재칠하지 않음. 평면 배경 잔디의 런타임 색조만 어둡게 함. |
| 근접 참격·무기군 마무리·전투 봉인 애니메이션 | [Ninja Adventure / Pixel-Boy and AAA](https://pixel-boy.itch.io/ninja-adventure-asset-pack) | 공식 원본94MB, CC0-1.0. 기존 참격38 PNG 중29장 사용, 추가 전투/봉인33장 사용. crop/공통 투명 canvas padding만 수행. External/NinjaAdventure. LICENSE 및 출처를 StreamingAssets/ThirdPartyNotices에 포함. 변경/실행 검증 범위는 Docs/ExternalArtReplacement-20260921.md. |
| 보존 이력: 영웅8방향 이동96장 | 사용자 영웅 기준 머리와 내장 imagegen 제작용 몸통·관절·목도리 부품 | Generated/HeroWalk-20260921에 실제 참조·정확한 프롬프트·제외 후보·공통 추출 격자·관절/손 좌표 기록. Unity 저작용 관절 회전으로48px/32PPU 완성 PNG96장을 베이크해 HeroWalk/move/00..07에 연결. 런타임은 단일Sprite이고 머리 불변 검사/실제입력 검수 기록. 공격·대시 확장·상태 연결·전체 마감은 진행 중. |
| 보존 이력: 초기 잡몹 얼굴 일관성 교정24장 | 기존 네 잡몹의 실제 기준 PNG를 참조한 내장 imagegen | Generated/CreatureConsistency-20260921에 생성 원본/전체 프롬프트/제외본과 추출 기준 보존. 몸·잎·갓의 포즈와 동일한 전체 머리를 저작 단계에서 조합하고 Unity로64px 개별 PNG에 베이크. 런타임 CreatureMotion/<종>/frames 사용,32PPU/발(32,8). 기존 원본은 유지. 네 종 눈 형상/프레임 간 머리 픽셀 동일성 검사 기록. 프레임 수 증가·전체 모션 완료를 뜻하지 않음. |
| 연속 바닥3종·연결 벽 재질 | 기존 스테이지 재질을 실제 참조한 내장 imagegen 및 기존 벽의 기술적 추출 | 최초 생성 원본/정확한 프롬프트는 Generated/ContinuousFloors-20260921에 보존. 현재 바닥은 Generated/QuietFloors-20260921의 별도 수정본3장으로1024px/32색을3072개의32px PNG로 분리하고32PPU로 연결한다. 수정 참조·프롬프트 요약·제외본·추출 기록 보존. 기존 벽의 불투명 재질24개와 세 SpriteAtlas V2는 개별 원본/참조를 보존한다. |
| 보존 이력: 영웅 스탠딩48장 | 기존 영웅 이동 포즈와 원본을 실제 참조한 내장 imagegen | Generated/HeroIdle-20260921에 원본3장·제외 사유·최초 프롬프트/후속 지시·추출 기록.6실제 포즈×8방향,48px/32PPU/발(24,10),1.8초 호흡. 남쪽 방향은 원래 앞모습을 실제 입력으로 따로 제작했다. 런타임에 크기 변경/알파 추정을 하지 않는다. |
| 영웅, Warden, 상자, 보스 투사체 | 사용자 제공 ArtDirection/UserProvided/*-20260910/original.png | 원본 보존. 기존 분리 프레임과 추출 CSV 유지. |
| 보존 이력: 영웅의 연결 동작 | 사용자 영웅을 실제 참조한 내장 imagegen 수정본 | ArtDirection/Generated/HeroMotion-20260916에 공격/방어 원본·정확한 프롬프트·분리 좌표·비교 시트 보존. 기존 이동 32장은 동일 비율 축소본, 공격 48장·대시 16장·가드/피격 각8장은 별도 포즈. Runtime은 HeroMotion의 48×48 PNG112장과 고정 발/손 소켓 사용. 방향별 동작 검증 진행 중. |
| 보존 이력: 영웅 사망 | 같은 사용자 영웅/방어 포즈를 실제 참조한 내장 imagegen | HeroMotion-20260916/death-original.png, death-prompt.txt, death-extraction.json. 무릎 굽힘→무릎 꿇기→손 짚기→넘어짐→누움의 8방향×5포즈, 개별48px PNG40장. 원본 무수정, 전체 시트에 동일한 축소 비율. |
| 보존 이력: 영웅 원거리 자세 24장 | 기존 영웅을 실제 참조한 내장 imagegen | Generated/HeroRanged-20260920 원본·프롬프트·분리 기록. 8방향의 준비/발사/회수 포즈,48px/32PPU/공통 발 피벗. 16개 원거리 손무기는 기존 픽셀을 보존하고 손잡이·총구·발사축을 제작 수치로 정렬. |
| 보스 예고 외곽 | 기능성 원·사각형의 편집기 PNG 제작 | Tools/Validation/BakeHazardSprites.cs. 원형/회전 사각 판정과 같은 정규화 경계를 제작 단계에서 저장. Campaign/Hazards 개별 PNG/Sprite. 광택/불투명 생성 시안은 Hazards-20260916에 제외 사유와 보존. |
| 손에 든 무기 31종 | 기존 아이콘의 편집기 베이크 | Tools/Validation/BakeHeldWeapons.cs, Separated/HeldWeapons/*.png. 기존 실루엣·그립·크기 유지, 런타임 불투명 영역 검색과 Sprite.Create 분할 제거. 베이크 수치는 HeroMotion-20260916/held-weapon-bake.csv. |
| 일반 투사체 6종 | 실제 게임 화면과 사용자 보스 투사체를 참조한 내장 imagegen | ArtDirection/Generated/Projectiles-20260916의 원본·prompt.txt·extraction.json 보존. 동일 1:16 축소, 공통32px 캔버스, 투명도 경계 정리 후 Campaign/Projectiles의 Arrow/Bolt/Bullet/Cannon/EnemyOrb/Arc 개별 PNG로 분리. ImportCampaignProjectiles.cs로32PPU/Point/무압축/중앙피벗 임포트. 런타임 연결 후 장비·실제 화면 재검증 진행 중. |
| 석재 바닥·벽·수조·횃불·항아리 | Dungeon Gathering, Jose Javier / SnowHex | 제공 팩에서 개별 PNG 분리. 2026-09-20 Set 1.0의 오목 코너4개 추가 분리, 방향 선택 교정, 벽12타일의 빈 공간 색만 투명화해 카메라 배경과 연결. 원본과 벽돌 팔레트 보존. Docs/DungeonGatheringLicense.txt는 게임 사용·수정을 허용하며 독립 에셋 재배포를 금지한다. 게임용 Sprite로만 포함한다. [제작자 페이지](https://snowhex.itch.io/dungeon-gathering) |
| 스테이지 바닥 문양·벽 기념물·돌/식생 장식9종 | 실제 게임 화면과 세피리아 형태 참고를 전달한 내장imagegen | ArtDirection/Generated/Environment-20260920에 원본·정확한prompt·추출좌표·비교기록 보존. 공통4:1축소/128px개별PNG/32PPU. 기존 사용자 배우는 보존. Campaign/Environment에 개별Sprite, 실제3스테이지1080p정지 비교 확인. 전체아트마감/이동시각검수 완료 아님. |
| 스테이지별 지형 3세트·일체형 수조 | 승인된 타일 연결 형태와 실제 각 보스 장면을 참조한 내장 imagegen | Generated/StageThemes-20260920에 원본·정확한 프롬프트·추출·검증 기록. 석회암/이끼, 청록 수몰 석재, 자주색 봉인석을 별도 제작. Campaign/Terrain의 개별 PNG109장(기본60/바닥 사분면48/수조1). 타일 형태 참조의 Dungeon Gathering 라이선스도 유지. |
| 글꼴 | Galmuri — Lee Minseo (quiple) | 수정하지 않은 Galmuri11, Galmuri11-Bold, Galmuri9. SIL OFL 1.1 전문/저작권은 Assets/StreamingAssets/ThirdPartyNotices/Galmuri.txt. 정확한 커밋은 Resources/Fonts/SOURCE.md. [공식 정보](https://quiple.dev/font/galmuri) |
| 보존 이력: 기존 초반 몬스터 | 기존 내장 imagegen 생성물 | ArtDirection/Drafts/MeadowCohesion/README.md에 실제 참조와 프롬프트 보존. 게임은 개별 PNG를 읽는다. |
| 로고·메뉴·UI 프레임 | 기존 내장 imagegen 생성물 및 편집본 | ArtDirection/Generated/UI/2026-09-10-prompts.md와 원본 보존. 수치/설명은 실제 Galmuri 텍스트다. |
| 신규 보스 2종, 신규 일반 적 2종 | 이번 작업의 내장 imagegen 생성물 | ArtDirection/Generated/Campaign-20260916에 원본, 정확한 프롬프트, 채택/제외 및 좌표 기록. 실제 게임 화면·사용자 Warden을 참조 입력으로 전달. PNG63장, 고정 피벗, 32PPU. |
| 스테이지 음악 3곡 | Cleyton Kauffman · CC0 | Forest Whisper / Exploration Theme / Ruined City Theme. OpenGameArt 원작자 게시물·ZIP·WAV·readme를 ArtDirection/ExternalCandidates/20261001-music에 보존. 음량을 약 -20 LUFS로 맞추고 Forest Whisper 반복 경계 5ms만 클릭 보정. 원곡의 길이·템포·음정·편곡 유지. 정확한 출처·SHA256·처리: Review-20261001/campaign-music-import.json. |
| 스테이지 환경음 3개 | Ninja Adventure · Pixel-Boy and AAA · CC0 | Wind / WaveFar / Wind2 원본 WAV 그대로 사용. Review-20261001/campaign-ambience-import.json. 이전 합성 음악·환경음은 before-music에 보관하며 레거시 생성기도 활성 Resources를 덮어쓰지 않는다. |
| 기타 기존 무기/아이템/VFX | 기존 프로젝트의 개별 이미지 | Resources/Art/Pixel/Items/README.md, Resources/Art/Pixel/Separated/README.md와 Editor/ArtMigration 기록을 유지한다. |

세피리아 참고 화면/GIF는 시각·행동 연구 자료다. 원본 게임의 캐릭터·타일·로고·UI를 추출해 게임 에셋으로 사용하지 않았다. 참고 화면과 생성 시트는 배포 Resources에 추가하지 않는다.

2026-09-16의 신규 에셋 기록은 당시 상태이며, 일부 생성 이미지와 후보는 이후 원본 에셋으로 교체되었다. 이 문서는 게임 전체가 시각 검증을 통과했다는 선언이 아니다.

## 2026-09-30~10-01 장비·전투·보상 연출 추가

| 사용 | 공식 원본과 조건 | 최종 기록 |
| --- | --- | --- |
| 권총·산탄총 5종 재교체 | [Snoopeth Guns](https://snoopethduckduck.itch.io/guns), CC0 | Pack1의 2px 외곽선 원본, 투명 여백만 추가. 기존 원본·GUID와 교체 전 파일 보존. |
| 붉은 반지·루비 펜던트 | [7Soul CC0 Jewelry](https://opengameart.org/content/cc0-jewelry-icons), CC0 | native32 ring_03_red/necklace_01. soul_capacitor/guardian_seal/sapphire_flask는 기존 허용된 7Soul496 원본으로 재교체. |
| 무기별 마무리·발사·충돌 효과 | [CodeManu Free Pixel Effects](https://codemanu.itch.io/pixelart-effect-pack) | 본문·패키지 README는 퍼블릭 도메인, 페이지 메타데이터는 CC BY4.0으로 표기가 달라 작가/링크/변경 고지를 제공. 원본 픽셀은 변경하지 않고 빈 셀·중복 hold를 런타임 밖으로 보존. |
| 투사체·연기·보상 별빛과 전투/보상 WAV24개 | [Ninja Adventure / Pixel-Boy and AAA](https://pixel-boy.itch.io/ninja-adventure-asset-pack), CC0 | 기존 보유 원본에서 정확한 셀 크롭/공통 캔버스와 WAV 바이트 그대로 채택. 개별 PNG와 출처 해시 보존. |
| 5등급 상자 개방 | [Somi Essential Animated Chest Pack](https://pyrrhhic.itch.io/the-essential-animated-chest-pack-6-unique-themes) | 무료 PNG Chests.zip만 사용. 상업 게임 이용/수정 허용, 독립 에셋 재판매·재배포·저작 주장 금지. 5테마×6고유 개방 포즈,32px/발 피벗/24PPU. packaged LICENSE의 작가 placeholder와 공식 페이지의 Somi 표기를 함께 보존. |
| 투사체 잔상 셰이더 | 프로젝트 코드 `Resources/Shaders/PixelTrail.shader` | 정점색 기반 발광, 원본 이미지 수정 없음. |

최종 sourceCell·SHA256·PPU·선택 목록은 `ArtDirection/Review-20260930`의 equipment-adopted/combat-presentation-import/reward-presentation-import.json이다. 공식 페이지와 다운로드 원본은 ExternalCandidates에 보존하고 사용 조건은 StreamingAssets/ThirdPartyNotices에 동봉한다. 후보만 확보한 Scrython·Admurin·BDragon·NYKNCK 등을 이번 채택으로 계산하지 않는다. 전용 주인공 전투 포즈와 전체 아트 마감은 계속 미완료다. 자세한 범위는 `Docs/EquipmentAndEffects-20260930.md`를 참조한다.

## 2026-10-01 장신구·세트 발동 효과

기존 보유 [Ninja Adventure / Pixel-Boy and AAA](https://pixel-boy.itch.io/ninja-adventure-asset-pack), CC0 원본에서 Thunder 8포즈(20×28), ShieldYellow 6포즈(24×26)를 추가로 채택했다. 14개 개별 PNG는 원본 셀과 RGBA 전 픽셀·고유 해시가 일치하며,32PPU·고정 캔버스/중앙 피벗·Point·무압축·밉맵 없음으로 임포트했다. 원본을 그리거나 재표본화하지 않았다. 기록은 `ArtDirection/Review-20261001/item-effects-native-import.json`, `native-item-effects-identity-qa.json`이며 배포 출처는 `Assets/StreamingAssets/ThirdPartyNotices/NinjaAdventure-SOURCE.txt`에 추가했다.

그 외 출혈·회복 파편·마력구·메아리·바람은 기존에 채택한 같은 팩 및 CodeManu의 개별 원본 PNG와 기존 전투/보상 WAV를 재사용한다. 소유자 크기에 따른 이펙트 왜곡 방지, 재생 시간·회전·색조·그림 순서 변경은 런타임 표시 설정이다. 이번에는 추가 구매·새 이미지 생성·신규 음원 채택이 없다. 실제 동작과 검증 한계는 `Docs/ItemCombatEffects-20261001.md`에 기록한다.

## 2026-10-01 무기별 고유 연출

- [Ansimuz Warped Shooting Fx](https://ansimuz.itch.io/warped-shooting-fx): 공식 페이지와 원본 public-license.pdf 모두 CC0. spark/waveform/charged/Hits-2/Hits-3/Hits-6의 원본34포즈를 채택했다. 무료 원본 ZIP·공식 HTML·SHA256은 ExternalCandidates/20261001/ansimuz-shooting에 보존하고 배포 Ansimuz-Shooting.txt에 기록한다. 이 팩의 음악은 사용하지 않는다.
- [Ninja Adventure](https://pixel-boy.itch.io/ninja-adventure-asset-pack): CC0. 불꽃·폭발·원형 지면 먼지·이중 초승달·베기와 금속/돌/잎 파편 원본을 추가 채택했다. 파편 변형은 애니메이션 포즈로 계산하지 않는다.
- [Pixel Frog Tiny Swords Free Pack](https://pixelfrog-assets.itch.io/tiny-swords): 게임 사용/수정 허용, 독립 재배포·재판매 금지. 무료 팩에 이미 보유한 Explosion_02(192×192)와 Dust_02(64×64) 각10고유 포즈를 대포 폭발·철퇴 지면 먼지로 채택했다. 승인 주인공은 교체하지 않았다. 현재 팩을 CC0라고 표기하지 않는다.
- [Scrython Pixelart Guns](https://scrython.itch.io/pixelart-guns): 게임 사용/수정 허용, 독립 팩 재판매·재배포 금지. 이전 후보 원본의 Bullet.png에서 탄두 셀1개만 추가 채택했다. 무기 모델 교체는 아니다.

108원본 셀은 무재표본화/무재도색으로 분할하고 Unity Sprite Editor API로 공통 피벗·32PPU·Point·무압축을 설정했다. 원본 픽셀과 해시가 일치하며87고유 애니메이션 포즈/21파편 변형·탄두로 구분한다. 원본/범위/라이선스와 실제 게임 화면 검사 한계는 Docs/WeaponEffects-20261001.md 및 ArtDirection/Review-20261001/weapon-fx에 기록한다. 기존 CodeManu·NinjaAdventure 일부 클립은 실제 무기 성격에 맞춰 재사용하며 같은 링을 모든 적중에 붙이던 경로는 제거했다.

## 2026-10-01 대검 궤적

Frostwindz 공식 무료 Pixel Art VFX - Slashes의 원본128×128 Slash3/color5 9장을 unchanged PNG로 사용한다. 상업 게임/수정 허용, 독립 재배포/재판매/AI 학습 금지. Slash2/color4 Y형7장은 사용자 거절로 런타임에서 제외했다.64px첫시안·원본 ZIP·라이선스·SHA·실패 화면은 개발 자료에 보존하며 Docs/GreatswordEffects-20261001.md와 배포 Frostwindz-Slashes.txt에 기록한다. 다른 무료 후보 IceMaan/JasonTomLee는 이 수정에 채택하지 않았다.

## 2026-10-01 카타나 원본 분리 교정

Ninja Adventure CC0 Slash03의228×42 스트립은57×42의 완전한4칸이다.38×42의6조각으로 분리되었던 기존 시안은 ArtReview/IncorrectKatanaSlices20261001에 GUID와 함께 보존하며 새4칸은 Unity exact RGBA crop/Point/57PPU/중앙 피벗으로 가져왔다. 다른 횡베기 원본은 재색칠/리사이즈하지 않았으며 원본4~6키와 본체12~16포즈를 구분한다. 출처/원본 RGBA/실제 Sprite와 화면 검증은 ArtDirection/Review-20261001/melee-fx-binding 및 Docs/MeleeEffects-20261001.md.


## 2026-10-01 physical thrust sources AU

- SHEARLINE free sampler — godboyhappy: https://godboyhappy.itch.io/shearline-pixel-melee-slash-vfx . Original native32 thrust-stab.png, eight exact cells (six visible poses plus empty boundaries), fixed pivot(7,16)/32PPU. Commercial game use/modification allowed; no standalone asset redistribution or authorship claim. Full supplied license: StreamingAssets/ThirdPartyNotices/SHEARLINE-Thrust.txt. No downloaded engine code executed.
- FREE PIXEL VFX ESSENTIALS — IceMaan: https://icemaan.itch.io/pixel-vfx-essentials . Original Spritesheets/Slashes/Thrust.png, five exact native32 cells, fixed pivot(2,16)/32PPU. Personal/commercial use and modification allowed, attribution optional; no asset pack/store/AI-training redistribution. Official-page license text in StreamingAssets/ThirdPartyNotices/IceMaan-VFX-Essentials.txt; original acquisition page/RAR retained outside runtime.
- Exact RGBA crop only: no repaint/resampling/generated poses. Fixed canvas, uniform native display. Independent PNG/import checks and actual-size action/camera proof: ArtDirection/Review-20261001/thrust-fx and Docs/ThrustEffects-20261001.md. These replace gun muzzle/bullet-hit graphics used by physical stabs; existing firearm sources remain available for firearm effects.

## 2026-10-02 Sscary armed grip and separate weapon stances

The previously licensed official Sscary Adventurer Male Free Demo remains the selected backpack hero. Native arm/closed-hand joints were raised and retargeted across melee/ranged actions. `ArmedGripV3` has12,266 unique PNGs/24,096 breathing/gait/action combinations; `WeaponStandingV4` has2,083 unique PNGs/6,912 combinations across six separate family standing/short-return profiles. Combinations and return keys are not new authored action-pose counts. Original rigid head/face/backpack, native palette/canvas/footfall, Point and uncompressed import are preserved. Failed V1/V2/V3 stance candidates and originals remain in development records. No Premium purchase, new generated character, raster resampling or standalone pack redistribution. Source license remains `Assets/StreamingAssets/ThirdPartyNotices/Sscary-Adventurer.txt`; source/edit/import/identity/current screen evidence: `ArtDirection/Generated/SscaryMotion-20261001`, `ArtDirection/Review-20261001/weapon-carry`, `Docs/WeaponStances-20261002.md`.

The sequential katana cross-cut reuses the previously verified four complete57×42 Ninja Adventure CC0 Slash03 cells. It changes runtime direction, timing, uniform display and grip binding, not the source pixels or license. Damage is split into two real contacts for one action; mana/refinement/proc deduplication rules are documented with actual tests in `Docs/CrossCutEffects-20261002.md`.


### Native frame atlas packaging — 2026-10-02

No new art or licensing terms. The approved Sscary source and existing native arm/stance edits remain individual PNGs under Assets/RuinRunner/ArtSources/Hero; earlier action/stance versions remain under ArtSources/Archive/SscaryMaleCombat with unchanged PNG bytes, metadata and GUIDs. Current14349frames reference two built-in lossless SpriteAtlases through source-derived catalogs; retired16502frames are not shipped. This packaging does not add independently authored poses or alter the approved face/backpack. Source/move/pixel/build records: ArtDirection/Review-20261002/native-frame-atlas and Docs/NativeFrameAtlases-20261002.md. Existing Sscary notices and originals are preserved.

## 2026-10-03 five remaining ranged reloads

WeaponReloadV3 reuses 7,629 unchanged individual PNGs from the preserved native WeaponReloadV2 authoring set, selecting only Crossbow/Pistol/Rifle/Shotgun/Cannon. The 20,480 runtime samples combine entry, direction, original breathing/gait and 16 time keys; they are not 20,480 newly authored poses. Fixed-gait effective source poses range from 5 to 15 depending on direction/occlusion. No new download, purchase, image generation or resampling was used. Original Sscary license/identity/backpack and source PNG/meta remain intact. New copies receive Unity GUIDs; build catalogs include V3 while V2 remains excluded. Import/hash/motion/build evidence and remaining limits: Docs/FirearmReload-20261003.md.

The dash now reuses existing Ninja Adventure / Pixel-Boy and AAA CC0 SmokeCircular frames (GroundDust 00..09), originally imported by Tools/Validation/ImportWeaponIdentityFx20261001.cs. PNGs/pivots/palette remain unchanged; native24px width,0.28s lifetime, below-body sorting. No additional source acquisition or license change.


## 2026-10-03 플레이어 접촉 효과 추가

- IceMaan, [FREE PIXEL VFX ESSENTIALS](https://icemaan.itch.io/pixel-vfx-essentials): 기존 보유본 `Spritesheets/Hit Sparks/Hit Spark.png`의5개32×32 원본셀을 PlayerHit/00..04.png로 정확하게 분리. 리페인트/재표본화 없음, PPU32/공통중앙피벗/Point/무압축. 각 실제 RGBA 동일성은 ArtDirection/Review-20261003/hero-hurt/native-import.json.
- 2026-10-01 보존 공식 라이선스: 개인/상업 게임 사용·수정 허용, 에셋팩 재배포·재판매 및 AI학습 세트 업로드 금지. 원본/공식페이지/StreamingAssets의 IceMaan 고지 보존. 에셋은 게임 내부에서만 사용한다.
- 피격 본체 생성 시안은 원본 Sscary 정체성과 픽셀 품질을 유지하지 못해 거절했다. Review-20261003/hero-hurt에만 보존하며 실제 게임 에셋으로 채택하지 않았다. 접촉 프레임과 본체 모션은 별개다.

## 2026-10-03 침수 회랑 수호자 원본 모션

Kronovi / Dark Pixel의 무료 Mecha-Stone Golem: https://darkpixel-kronovi.itch.io/mecha-golem-free . 공식 페이지의 상업·비상업 게임 사용/수정 허용 조건을 확인했다. 크레딧 권장, 에셋 팩 재판매·재배포 금지. 결제 없이 공식 다운로드를 사용했으며 ZIP과 원본 PNG/Aseprite는 ArtDirection/ExternalCandidates/20261003/kronovi-golem에 보존한다.

51개 100×100 원본 셀을 개별 PNG로 분리했다. RGBA 원본 동일성과 실제 Unity의 보이는 픽셀·알파 동일성은 Review-20261003/tidekeeper-motion/native-import.json에서 검증했다. Unity Alpha Is Transparency의 투명 RGB 확장은 원본 PNG 변경과 구분해 별도 기록한다. 공통 피벗 (49,6), 32 PPU, Point/무압축. idle 4/4, glow 8/5, immune 8/8, laser_cast 7/7, shield_cast 10/9, death 14/13 (파일/클립 내부 고유 RGBA). 중복·홀드 프레임을 새 포즈로 세지 않는다. 피해 반응에는 guard의 앞 4포즈만 짧게 접었다 펴는 데 사용한다. AI 생성·재도색·확대·유료 구매 없음. 배포 고지: StreamingAssets/ThirdPartyNotices/NativeActors.txt.
## 2026-10-03 보스 지면 공격 원본

기존 Ninja Adventure / Pixel-Boy and AAA CC0의 WaterPillar9,Water11,RockSpike10: 총30개 고유 원본 셀을 공통32PPU/Point/고정 피벗으로 임포트했다. 원본 PNG 픽셀을 재도색·재표본화하지 않았으며 물기둥 묶음과 봉인 균열의 실제 활성 시계에 연결했다. Water9.wav 원본을 그대로 복사해 Monster 채널 발동음으로 사용한다. 분리/해시/권한 기록은 ArtDirection/Review-20261003/boss-hazard-art/native-import.json 및 배포 NinjaAdventure-SOURCE.txt.

무료 Bringer Of Death 후보는 횡스크롤 자세·수평 베기가 최종 보스의 상하 돌진에 맞지 않아 미채택했다. 원본/공식 라이선스/실제 지형 크기 비교는 ArtDirection/ExternalCandidates/20261003/clembod-bringer 및 Review-20261003/sealheart-motion에 보존한다. Assets/배포에는 포함하지 않는다.

## 2026-10-03 — SealHeart native four-direction boss

- Official source: https://foozlecc.itch.io/lucifer-skeleton-ancient-boss ; Foozle / Lucifer Ancient Boss, CC0, no purchase. Official page credits David (chroma_dave); packaged Readme credits Baldur (the__baldur). Both source records are preserved; attribution is not required and this discrepancy is not silently resolved.
- Original ZIP and source files: `ArtDirection/ExternalCandidates/20261003/foozle-ancient`; SHA256 `7280f8955f64da3081976f0c6407dfa83d8cb959f267bca655aef3fc669e5452`. The separately acquired Skeleton King remains a candidate outside runtime.
- Selected native clips: idle7, walk8, hurt4, death9, attack02 13, attack03 7 in each of four authored directions. 192 files, 166 unique RGBA poses summed per clip; holds are not new poses. Left/right walk have 7 unique poses each. Source 80px cells are padded by a fixed 8px on each edge into a common96px canvas, while native96px side walk cells remain intact. No opaque pixels, colors, outlines or proportions change.
- Actual ground comparison: `ArtDirection/Review-20261003/sealheart-ancient/native-left-current-right.png`; native imports/first-attempt failures/source audit preserved in the same folder. Runtime playback acceptance and Windows verification are recorded separately, not inferred from this static comparison.
- Packaged notice: `Assets/StreamingAssets/ThirdPartyNotices/Foozle-Ancient-Boss.txt`.
# 2026-10-03 심부 포석

- Champi_S, [Dungeon Pixel Art x32](https://champi-s.itch.io/dungeon-pixel-art-x32), CC0 v1.0. 공식 페이지에서 상업 이용·수정 허용을 2026-10-03 재확인했다.
- 원본 `ArtDirection/ExternalCandidates/20260922/champi-dungeon/Asset-Tileset.png` 보존. SHA-256 `6fecffe24861b8984e3169177c6f86e7df32c8b35ad8f62bc8833d444ab6e295`.
- `External/ChampiDungeon/Paving/00..15`, `Ground`는 원본 사각 영역을 Unity API로 추출한 개별 PNG. 재샘플링·재도색 없이 원본 RGBA를 대조했다. `core-floor/native-import.json`에 18개 비교 추출 기록이 있다. `Seal`은 비교용 미사용 후보이며 기존 CoreMosaic는 유지한다.
- 표시만 `CoreStone` 공용 머티리얼로 대비와 색을 조정하고 포석 마모에 공간별 알파를 적용한다. 원본 픽셀·피벗·32 PPU·Point 설정은 유지한다.
- 배포 고지: `Assets/StreamingAssets/ThirdPartyNotices/ChampiSDungeon.txt`.
