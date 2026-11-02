# 근접 횡베기의 원본 분리·방향·손 연결 — 2026-10-01

추가 교정 AT: 전방 끝과 피해 범위 측정을 마쳤으며 [별도 기록](MeleeForwardRange-20261001.md)에 최종 변경/근거/남은 범위를 정리했다. 아래 AS 검사는 당시 구현의 기록이다.

기존 횡베기 효과는 피해 판정 순간에 상자 중앙에서 시작하고 독립 타이머로 재생했다. 이동·대시·교체 중 손이나 본체 동작과 어긋날 수 있었다. MeleeSlashEffect.CreateBound는 공격 시작에 준비하고 실제 AttackAnimationSequence/Progress/ContactsResolved를 읽는다. 준비→접촉→잔상 키를 재생하며 실제 .42 접촉 프레임에는 원본의 밝은 키를 표시한다. LateUpdate에서 표시 그립 HeldWeaponView.BladePoint(0)를 따르고, 본체 완료·죽음·시퀀스 변경·대시/교체 취소에서 사라진다. 판정·피해 수치·콤보·이동·입력/MP/탄약 속도는 변경하지 않았다.

원본 점검에서 카타나 Slash03의 잘못된 분리를 발견했다. 실제228×42 스트립은57×42의 완전한4칸인데 기존에는38×42의6칸으로 잘라 곡선 중간에 직선 절단면이 생겼다. 연결 Editor에서 정확한4칸을 새 개별 PNG/Sprite로 가져왔고, Unity 및 독립 PNG 판독으로 RGBA 원본 일치를 확인했다. 새57PPU/중앙 피벗/Point/비압축/밉맵 없음이며 리사이즈·재색칠·생성 프레임·보간을 하지 않았다. 기존6조각은 GUID와 함께 `Assets/RuinRunner/ArtReview/IncorrectKatanaSlices20261001`에 보존한다. 원본 수를 늘려 보이게 세지 않는다.

단검 Slash01 5키, 장검 Arc 6키, 철퇴 Circular 6키, 카타나 Slash03 4키, 대검 첫 횡베기 Slash02 6키의 서로 다른 원본을 유지한다. 대검 내려찍기 Frostwindz9키는 별도다. Circular의 접촉 원본은 위쪽을 향해 다른 아래쪽 곡선과180도 방향이 달랐다. 철퇴의 원본 회전을 교정해 밝은 접촉이 공격 쪽에 나타나도록 했다. 카타나의 밝은 접촉 키는 완전한 원본1, 다른 곡선은2를 사용한다.

손잡이/가드가 회복 동작에서 얼굴·소매를 덮던 방향별 앞 순서를 수정했다. native 손은 그립 앞에 남고 본체가 가드의 겹친 부분을 가린다. 접촉 불꽃과 철퇴/대검의 낙하 파편도 공격자 아래 순서를 따른다. 적중마다 추가되던 고정 순서 RuntimeSquare 노란/빨간 버스트는 제거했다. 이미 존재하는 무기별/아이템별 native 적중 표시, 적의 피격 본체 색과 타격 소리는 유지한다. 적 사망·등장·플레이어 성장 등 다른 legacy 버스트까지 교체했다고 주장하지 않는다.

## 실제 검사

- `Logs/Validation/2026-10-01-melee-fx-binding/presentation-182420`: 224개 단언, 실제1280×720 PNG4254장, 45개 녹화 단계, 실제 표시 원본4~6키. 얼굴/상체657443픽셀 비교 가림0.
- `Logs/Validation/2026-10-01-melee-fx-binding/catalog-182909`: 406개 단언, 실제1280×720 PNG7614장, 80개 녹화 단계, 실제 표시 원본4~6키. 얼굴/상체1182940픽셀 비교 가림0.
- 기본5종×8방향40사례 및5종 이동 콤보, 일시정지·대시·무기 교체·메뉴 정리를 확인했다. 나머지10종×8방향80사례로15종의 표시 손/그립·좌우·원본·접촉을 확인했다. 피해 발생 횟수/표적 피해, .42 이전 궤적 시작, 실제 접촉 키, 완료/취소 정리를 검사한다. 최소 표시4키 기준은 낮추지 않았다.
- 합계 실제 화면11868장과 원본 얼굴/상체 불투명1840383픽셀을 좌표 그대로 비교했다. 좌표 맞춤이나 화면 보정 없이 허용 RGB 오차3 기준이다. 원본 PNG/CSV와1배속 무음 H264를 보존한다. 같은 프레임을 실제 CSV 시간만큼 유지하며 보간·가짜 프레임·가속을 하지 않는다.
- `Logs/Validation/2026-10-01-weapon-identity/catalog-183821`:31무기/18장신구의 실제 GameAction·공격/특수·MP/탄약·가드/패링·대시·스탯201단언 PASS.
- 장비·표적을 공급하고 스포너를 정지한 Editor 검사다. 정상 캠페인·게임 음향 청취·1080p60fps·메모리/분량 검사가 아니다. native 효과4~6키와 본체12~16포즈는 별개로 센다. 정지 이미지 검토/픽셀 검사가 모든 모션의 주관적 품질 수용을 대신하지 않는다.

## 실패 보존

173740: 기존 가드/접촉의 얼굴·상체8485픽셀 가림 FAIL. 175027: 가드 순서 교정 후 접촉/파편328픽셀 가림 FAIL. 181050: 원본 분리·철퇴 방향·native 파편 교정 후 legacy 노란/빨간 사각형38픽셀 가림 FAIL. 실패 CSV/PNG/색 진단과 원본 코드를 보존했다. 해당 버스트의 RGB와 화면 위치를 확인해 제거했으며 픽셀 검사 범위를 줄이지 않았다. catalog-181532는 이 잔여 결함을 고치기 위해 중지하고 실제7051샘플/부분 CSV/PNG와 복구한 View 상태를 보존해 FAIL로 기록했다. 마지막 두 PASS만 채택한다.

카타나 import의 CLI30초 응답 시간 초과 시점에는2칸만 관찰됐다. 임의로 다른 import를 시작하지 않고 공식 focus 후 완료된4칸/manifest와 실제 Sprite 속성, 독립 RGBA 일치를 확인했다. 시간 초과 응답만으로 성공을 선언하지 않았다.

## Windows 후보와 한계

최신 `Builds/CampaignCandidate-20261001-AS/RuinRunner.exe`, ID `build_d32ec2a78972`. CleanBuildCache/DetailedBuildReport/StandaloneWindows64: 649362422바이트, 47.481초, 오류0·경고3개. AR는 수정 전 후보로 보존한다. 기본 실행은 정상 메인 메뉴다.

실제 AS 프로세스17648, 격리 프로필 `SweepAS-184846`의 startup/native 리소스·충돌·장비·성장·저장 smoke PASS/exit0. 근거 `Logs/Validation/2026-10-01-melee-fx-binding/windows-smoke-AS-184846/process.json` 및 `player.log`. 숨겨진 batchmode/nographics 실행이며 화면·소리·정상 캠페인/성능 검사를 대체하지 않는다. 빌드와 변경 소스 SHA는 unit-summary.json에 기록한다.

[Ninja Adventure Asset Pack](https://pixel-boy.itch.io/ninja-adventure-asset-pack), Pixel-Boy/AAA, CC0-1.0. 기존 무료 원본과 라이선스를 사용하며 유료 구매를 하지 않았다. source/native/Sprite 검증은 `ArtDirection/Review-20261001/melee-fx-binding`, 배포 고지는 NinjaAdventure-SOURCE.txt/CC0.txt에 있다.

전체 목표 ACTIVE/미완료. 이번 범위는 근접 Sweep과 그것에 겹치는 손잡이/적중 표시다. 손/접촉 시계 일치가 피해 상자의 바깥 범위까지 시각적으로 일치함을 뜻하지 않는다. 그립 중심으로 옮긴 곡선의 전방 끝과 실제 피해 상자의 reach+half-width 범위를 별도로 측정해야 한다. 이 캡처의 표적은1.05단위에 공급했으므로 최대 사거리 끝의 시각적 일치를 검증한 것으로 확대하지 않는다. 다음 효과 점검에 이를 포함한다. CrossCut/찌르기/특수/방어/장신구와 legacy 등장·사망·성장 효과의 추가 화면 마감, 피격/재장전 본체, 적·보스 모션, 맵의 밀도·크기·연결, 최신 정상3캠페인·분량·1080p60fps/반복 런 메모리 검증은 남는다.
