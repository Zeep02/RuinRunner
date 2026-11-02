# 후반 스테이지 원본 바닥 교체

전체 게임 목표의 일부 개선이다. 활 전용 팔 동작, 보스 아트·모션, 후반 소품, 공간 구성, 25–40분 런과 1080p60fps는 완료되지 않았다.

## 채택한 구성

- 침수 회랑: RafaelMatos Ancient Ruins 무료판의 석재/이끼 10개 원본 셀. 작가의 두 벽돌 열을 유지하고 벽·수조 주변의 습기와 보행 경로를 기준으로 이끼 밀도를 결정한다. 처음의 5×5 사각 패치 배치는 폐기했다.
- 봉인 심부: Foozle Lucifer Dungeon의 9개 원본 셀. 작은 가장자리 포장, 보행로 석재, 큰 내부 석판을 구분하고 파손을 드문 무리로 배치한다. 전체에 균일한 자주색 명도 조정을 적용한다.
- 모든 채택 PNG는 원본의 정확한 32×32 RGBA 크롭이다. 리샘플링·다시 그리기 없이 32PPU, Point, 무압축, 밉맵 없음. 19개 모두 불투명하여 바닥의 검은 구멍을 메우는 별도 지오메트리가 필요 없다. 벽 접촉 아래 깔리는 바닥도 같은 색을 유지한다.
- 원본 PNG: `Assets/RuinRunner/Resources/Art/Pixel/Separated/External/RafaelRuins/Floor`, `External/FoozleDungeon/Floor`.
- 제작 도구와 원본 비교: `Tools/Art/export_native_floors.py`; 실제 크롭 좌표·SHA256은 `ArtDirection/ExternalCandidates/20260922/native-floor-selection/extraction.json`에 기록한다. 이 manifest의 최종 채택 목록은 2026-09-30의 19개이며, 앞선 Champi 후보 목록을 대체한다.

## 후보 비교와 사용 조건

| 원본 | 판단 |
| --- | --- |
| [RafaelMatos Ancient Ruins](https://rafaelmatos.itch.io/epic-rpg-world-pack-free-demo-ancient-ruins-asset-tileset) | 무료 상업 프로젝트 사용·수정 허용. 독립 에셋 재배포, NFT, AI 학습 데이터 사용 금지. 10개 바닥만 채택, 생성 모델 입력으로 사용하지 않았다. |
| [Champi Dungeon](https://champi-s.itch.io/dungeon-pixel-art-x32) | CC0. 실제 맵의 촘촘한 둥근 타일 반복과 사각 버튼 같은 깨끗한 타일 때문에 두 차례 배치 후 제외. 원본과 시험 화면 보존. |
| [Foozle Lucifer Dungeon](https://foozlecc.itch.io/lucifer-dungeon-tileset) | CC0. 세 크기와 파손 바닥 9개 채택. 현재 페이지는 David, 패키지 Readme는 Baldur로 작가 표기가 달라 두 기록을 배포 고지에 함께 보존. |
| [Foozle Lava Dungeon](https://foozlecc.itch.io/lucifer-lava-dungeon-tileset) | CC0. 원본 다운로드·실제 타일 검토. 벽돌 무늬가 촘촘하여 이번 바닥에는 채택하지 않음. |
| [Lisanise Temple Ruins](https://lisanise.itch.io/temple-ruins) | 유료 AUD3로 무료 대안을 우선했다. 구매·다운로드 없음. |

Kao Dungeon과 Stealthix Dungeon은 9월22일 검토한 원본 후보이며 이번 활성 바닥에는 포함하지 않는다. 배포 고지 두 파일은 `Assets/StreamingAssets/ThirdPartyNotices`에 포함했다.

## 보존과 빌드 구성

사용하지 않는 생성 바닥 3,072개와 탈락한 Champi 후보 6개를 Unity AssetDatabase로 `Assets/RuinRunner/ArtReview/RetiredGeneratedTerrain`에 옮겼다. 개별 PNG와 GUID는 유지된다. `Resources`와 새 아틀라스의 packable 목록에서는 제외했다. 원본 생성 시트와 사용자 제공 파일은 변경하지 않았다.

기존 smoke의 생성 Field 1024개 필수 조건을 폐기된 경로가 비어 있어야 하는 조건으로 바꿨다. 남은 기존 지형 133개 원본 검증은 유지하고, 새 바닥 19개의 개수·32px/32PPU·불투명도·필터 검사와 External Atlas의 원본 대비 패킹 픽셀 검사를 연결했다. 기대치를 낮춰 누락을 통과시키는 방식이 아니다.

## 검증과 한계

- `Logs/Validation/2026-09-30-native-floors/125952`: Champi 최초 반복 배치. 데이터 검사 통과와 별개로 시각 후보 탈락.
- `131057`: Champi 깨끗한 타일 혼합. 데이터 검사 통과, 시각 후보 탈락.
- `132712`: 최종 Rafael/Foozle, 실제 production renderer에서 두 스테이지 각5장. 지면/접촉부 각각3,617·3,740개, 원본10·9종, Point/32PPU/색상 일치/불투명 검사 통과. 정지 배치 검토이며 정상 캠페인·이동·성능 증거가 아니다.
- 이 정지 fixture는 일부 방 중앙의 장애물 안에 배우를 강제 배치한 화면이 있다. 정상 이동 충돌의 증거로 사용하지 않는다. 실제 Windows 이동 검사는 별도로 수행한다.
- 벽·분수·수조·석조 장애물·보스는 여전히 기존 아트가 남아 있다. 현재 바닥 교체를 후반 스테이지 전체 시각 완성으로 보고하지 않는다.

## AD Windows 실행 검증

- `Builds/CampaignCandidate-20260930-AD/RuinRunner.exe`: Unity 6000.6.0f1 클린 빌드 `build_7e133dc3957f`, 25.524초, 오류 0, 기존 경고 3. 세 테마 바닥의 개별 원본·아틀라스 픽셀 검사가 포함된 smoke, 지도, 1920×1080 UI 실행 모두 exit 0. 35장 화면의 크기·디코드 검사도 통과했다. 검증 폴더는 `Logs/Validation/2026-09-30-native-floors/windows-AD`.
- 첫 정상 입력 실행 `windows-AD-normal-ranged`는 3스테이지 2구역까지 진행했으나 프로세스가 외부에서 종료되었고 결과 파일이 없다. 이를 완주로 세지 않는다.
- 별도 새 프로필의 `windows-AD-normal-ranged-02`는 seed 824942335, 정상 GameAction 원거리 입력, 무영구 강화, 1920×1080에서 3보스·60킬·엔딩 PASS, 게임 시간 391.2초, 프로세스 exit 0. `launch.json`, `player.log`, `exit.json`, `recording.csv`, 3,040장 원본 화면을 보존했다. 녹화 오버헤드가 있어 60fps 성능 증거는 아니다. 목표 25~40분 분량에도 못 미친다.
- 두 번째 실행의 `frames/frame-001500.png`와 `frame-002000.png`를 직접 보았다. 벽 밑 검은 구멍은 보이지 않았으나 침수 회랑의 규칙적인 줄눈과 심부의 큰 정사각형 반복은 여전히 어색하다. 현재 소스에서 심부의 16·32픽셀 판석을 넓은 수선 구역으로 섞는 변화를 시작했으며, 이는 AD 빌드에 들어 있지 않다. 새 렌더·빌드 검증 전에 개선 완료로 판단하지 않는다.
