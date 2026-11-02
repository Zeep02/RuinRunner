# 배낭 주인공의 실제 찌르기 본체 포즈

현재 전체 게임 목표는 미완료다. 이번 변경은 선택된 Sscary 남성의 찌르기이며, 횡베기·차지·가드·패링·총기/활·피격 본체까지 마감했다는 뜻이 아니다.

## 실제 변경

무료 원본의 얼굴·머리·배낭·옷·팔레트·48×64 캔버스와 발(24,22)/32PPU를 유지한다. 단검 우클릭, 단검/카타나의 찌르기 연계와 롱소드 3타 등 실제 `IsThrustActive` 경로에서 전용 팔·손·회수와 짧은 발 디딤을 재생한다. 입력·데미지·판정 시점·이동 속도·후딜·자원·쿨다운은 바꾸지 않았다. 움직일 때 원본 보행 위상은 별도로 계속 진행하고, 새 손 좌표로 장착 무기를 연결한다. 대시/취소/일시정지는 원래 상태를 따른다.

16개 **시점**을 저작했으며, 방향별 실제 고유 정지 포즈는 위10/오른위13/오른아래11/아래11/왼아래11/왼위12장이다. 8개 보행 조합을 더한864슬롯은503고유 PNG를 공유한다. 864를 새로운 공격 포즈 수로 세지 않는다. 같은 그림의 시간 구간은 동일 PNG를 사용하며, 런타임 이미지 보간·투명도 겹침·크기 변화로 프레임을 만들지 않는다. 주요 동작 전체12~16포즈와 적·보스 모션 요구가 모두 해결된 상태가 아니다.

후속 화면 검수에서 이동 중 롱소드의 잔류 빛이 캐릭터 앞을 덮었다. 효과를 실제 시전자 몸 아래에 그리고, 높이 정렬이 변해도 몸 아래에 남도록 `MeleeThrustEffect`를 조정했다. 판정·크기·원본 효과 프레임은 유지한다.

## 출처와 제작

- 원본: [Sscary The adventurer - Male](https://sscary.itch.io/the-adventurer-male). 2026-10-01 공식 페이지 재확인: 무료/상업 게임 사용·수정 허용, 독립 재배포/재판매 금지. 무료판에 공격/총기 모션이 없음을 확인했으며 Premium 구매 없음.
- 내장 imagegen을 사용한 팔 준비 시안: `ArtDirection/Generated/SscaryMotion-20261001/raised-arm-key-generated.png`, 전체 프롬프트 `raised-arm-key-prompt.txt`. 384×512/고정8픽셀 격자를 요구했으나 실제1086×1448/31352색으로 원본 얼굴·명암·가장자리가 달라서 **생성 픽셀 미채택**. 팔을 접는 위치의 참고로만 사용했다.
- `AuthorSscaryThrust20261001.cs`로 공식 원본 위에 동일 팔레트의 네이티브 관절·닫힌 손을 저작하고, 원본 바지/신발 프레임을 디딤에 조합했다. v1의 남쪽 손이 지면 가까이 내려오는 문제는 지면 축 투영과 손높이 제한으로 v2에서 수정, v3에서 디딤과 회수를 보강했다. v1/v2 원본은 별도 보존했다.
- 생성 시안 픽셀을 축소·픽셀화해 넣거나 다른 주인공을 섞지 않았다. 수정 원본/좌표/해시: `ArtDirection/Generated/SscaryMotion-20261001/thrust-v3/manifest.json`.
- SpriteEditor API로 Point/무압축/무밉/FullRect/공통 피벗을 설정하고 실제 Sprite를 확인했다. 같은 이미지 공유와 적용 기록: `ArtDirection/Review-20261001/hero-thrust/native-import.json`. 배포 기록: `Assets/StreamingAssets/ThirdPartyNotices/Sscary-Adventurer.txt`.

## 검증과 한계

- `native-qa-v3.json`:864원본 슬롯의48×64/RGBA·원본 해시, 모든 원본 머리/얼굴 픽셀, 디딤 원본/접지 높이, 공식 팔레트와 손 주변 피부 확인 PASS. 런타임503PNG가 각 후보와 정확한 RGBA로 일치하고 중복 PNG가 없는 것도 확인했다.
- `Logs/Validation/2026-10-01-hero-thrust/presentation-120257`: 실제1280×720 Game View 및 GameAction으로6방향 단검 적중, 정지/이동 단검 우클릭과 롱소드 연계, 실제 이동 유지, 찌르기 중 대시 취소, 메뉴 일시정지/재개, 회수·잔여 효과 정리를 확인했다. 장비와 고정 표적을 지급한 **격리 검수**이며 정상 캠페인/성능 증거가 아니다.
- 실제 시각/시계 기록: 원본PNG·`recording.csv`·`body-telemetry.csv`. 찌르기 관측0.244~0.261초/15~16샘플, 해당 공격의 실제 고유 화면10~11장, 손 변화 최대 원본2픽셀. 캡처 간격 일부30~35ms이며 실제1080p60fps를 통과했다는 뜻이 아니다.
- 실제 시간 H264: `worn_dagger-standing-1x.mp4`, `worn_dagger-moving-1x.mp4`, `rust_sword-standing-1x.mp4`, `rust_sword-moving-1x.mp4`, `thrust-dash-return-1x.mp4`. 1280×720/60fps 출력, 무음, 실제 시각으로 다음 표본까지 유지, 합성/보간/가속 없음. 네이티브 화면 연속 크롭: `ArtDirection/Review-20261001/hero-thrust/*actual-native-sequence.png`.
- 실패는 보존했다. 첫115801은 새 리소스 경로의 중복 슬래시로 로딩 실패하여 생산 코드/생성기를 수정했다.115955 이동 검수는 고정 적의 실제 충돌에 막혔으며 원본 좌표 기록에서0.756칸 이동 후 충돌을 확인하고, 이동 검수 표적만 옆으로 옮겨 경로를 비웠다.120115는 단검 돌진의38% 타격 대기보다 먼저 찌르기를 가정한 검증 오류였다. 실제 `IsThrustActive` 시작까지 기다린120257에서 통과했다. 구형 `GetInstanceID` 사용으로 첫 fixture 컴파일이 실패한 것도 `HashSet<Sprite>`로 수정했다.
- 초기 AJ build_a3d35e5e97be는308748900바이트/오류0/기존경고3으로 성공했지만, 잔류 빛의 몸 가림 수정 전이다. 최종 후보와 후속 시각/Windows 실행 결과는 아래에 별도 기록한다.

## 남은 작업

찌르기 이외 전용 주인공 포즈, 단검 돌진의 타격 전 본체, 모든 주요 동작의 상태 연결과 실제 속도 평가, 적·후반 보스·환경 크기/모션, 정상3빌드 캠페인·분량·1080p성능·연속 런 메모리 검증은 계속 필요하다. 현재 `HasAuthoredCombatPoses`는false를 유지하고 찌르기에만true를 제공한다.

## 최종 연출 및 Windows 후보
- 잔류 빛의 몸 가림 수정 후 presentation-121157에서21항목 PASS. 실제 프레임마다 효과가 몸 아래에 정렬되는 것, 롱소드 정지/이동 연계·접촉·손 연결·대시 취소·메뉴 일시정지·회수·정리를 확인했다. 네이티브 화면은 final-rust_sword-*-actual-native-sequence.png, 이 실행의 실제시간 H264는 presentation-121157/rust_sword-*-1x.mp4. 앞선 전체 presentation-120257은46항목 PASS.
- 최종 후보 Builds/CampaignCandidate-20261001-AK/RuinRunner.exe, build_a15dbd088674:308749412바이트/17487ms/오류0/기존경고3. 초기 AJ와 AI는 보존했다.
- 같은 AK 실행 파일의 windows-smoke-121557/player.log에 Prototype Smoke Test PASS·exit.txt0. 실제 새 런 초기화에서503네이티브 본체를 로드하고, 모든 방향·정지/보행·시간 키의 번들 Sprite/Point/피벗/크기·손 좌표를 확인한다. Headless/nographics 격리 startup/resource 검증이며, 새 최종 빌드의 정상 캠페인·눈으로 본 Windows 플레이·1080p60fps 검증을 대신하지 않는다.
