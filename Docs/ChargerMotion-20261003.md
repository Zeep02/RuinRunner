# 돌진 몬스터 원본 모션 교체 — 2026-10-03

전체 목표는 ACTIVE/미완료다. 이 단위는 전용 피격·사망이 없던 Dotmancer 멧돼지의 표시를 CraftPix Orc3 갑옷 오크로 바꾼다. Charger 역할·ID·등장 규칙·이동 충돌·피해·돌진 시간은 유지한다. 승인된 Sscary 주인공은 변경하지 않는다.

## 선택과 원본

- [Free Orc Pixel Characters Sprites](https://free-game-assets.itch.io/free-top-down-orc-game-character-pixel-art), 기존 슬라임/흡혈귀와 같은 CraftPix 제작자. 공식 무료 다운로드의 Orc3를 사용한다. [Freebie license](https://craftpix.net/file-licenses/)에 따른 게임 사용이며 독립 에셋 배포를 하지 않는다. 생성 도구에 이 팩을 입력하지 않았다.
- `ArtDirection/ExternalCandidates/20261003/craftpix-orcs`: 원본 ZIP, 라이선스 페이지, 원본 시트, acquisition.json. ZIP SHA-256 `a1b1e87494277198293b21f9bc507bf1a8664246d8f4a0ddf18fc798ed6ca1db`.
- 방향마다 대기4·달리기8·공격8·달리는 공격8·피격6·사망8, 총168개 PNG/24클립.64×64 공통 캔버스, Unity 발 피벗(32,24),32PPU,Point/무압축/밉맵 없음. Unity Sprite Data Provider의 EditPivot 지원을 확인하고 원본 RGBA 셀을 그대로 분리했다. 프레임 복제·보간·확대·재채색 없음.
- 기존 멧돼지는 원본/메타를 보존하고 현재 art selector에서 제외했다. LPC Wild Boar는 원본을 검토했으나 긴 체형/4포즈로 개선 폭이 작아 미채택. 유료 후보는 구매하지 않았다. 생성한 멧돼지 사망 시안은 눈·엄니·몸 비율/픽셀 밀도 불일치로 거절했고 런타임에 넣지 않았다. 실패 시안은 `ArtDirection/Review-20261003/boar-motion`에 보존한다.

## 실제 연결

- 대기는1.2초4포즈, 이동은기존12fps의8포즈. 피격은 기존0.18초에6포즈이며 위험한 공격 중에는 공격을 가리지 않는다.
- 기존0.38초 준비→0.40초 돌진→0.35초 회복을 유지한다. 준비는 정지공격0~1, 실제 돌진은 이동공격2~5, 회복은 정지공격6~7이다. 16장의 공격 소스를 모두 한 번에 재생한다고 세지 않는다.
- 사망은0.8초8포즈로 실제로 주저앉아 쓰러진다. 충돌/물리는 즉시 해제, 일반 적 처치 콜백은 기존처럼 즉시 한 번만 호출한다. 본체 크기에 맞춰 피해 대상 범위를0.70×0.98로 바꾸고 최대 원본 높이36픽셀 기준의 고정 체력바 높이를 사용한다. 발 이동 충돌은 변경하지 않는다.
- 본체/무기/그림자는 제작자의 원본 full 레이어를 사용하므로 손에서 무기가 분리되거나 매 프레임 별도 캔버스 잘림으로 발이 이동하지 않는다.

## Editor 검증

- `Logs/Validation/2026-10-03-charger/motion-171254`: 네 방향×대기/달리기/피격/돌진/사망20구간,1,056개 실제1280×720 화면.73검사 PASS. 모든 대기4·이동8·피격6·사망8포즈, 돌진중 이동공격4포즈가 실제 SpriteRenderer에 나타나는지 확인했다. 세 공격 단계와 회복·한 번의 정산·사망 제거·메뉴 정리를 확인했다.
- 검증은 실제 EnemyController 이동/ChargeAttack/TakeDamage를 호출하지만 적/접촉을 공급한 격리 장면이다. 정상 캠페인 완주나 키보드 조작·성능 자료로 세지 않는다. 모션 시간과 화면은 실제 시계를 기록했다.
- 각 구간 `*-1x.mp4`는 H26460fps 미리보기이며 실제 기록 시각까지 프레임을 유지한다. 보간·시간 압축 없음. 원본 PNG/recording.csv/모든 sprite 경로와 tint의 body-telemetry.csv를 보존한다.
- 사망 없는 원본을 여전히12~16포즈 보강으로 계산하지 않는다. 새 실제8포즈는 기존 이동4장보다 낫지만 프로젝트 전체 모션 목표를 끝냈다는 의미가 아니다.

## Windows BC

- `Builds/CampaignCandidate-20261003-BC/RuinRunner.exe`, `build_24d8d0551741`. 2026-10-03 17:20:27 KST 완료,658,113,922바이트/197.538초,오류0·기존경고3. 15,782개 소스 SHA 변경0/보관 객체·raw native 중복·퇴역V2 포함0. 기존 BB와 사용자 저장은 보존한다.
- `Logs/Validation/2026-10-03-charger/windows-smoke-BC-1280x720-172136`, PID40192/exit0. 실제 후보의 기동·리소스·캠페인 데이터·충돌·장비·성장·저장 서비스 PASS. 정상 완주로 세지 않는다.
- `windows-ui-BC-1280x720-172218`: 실제 네 방향 돌진/피격/사망·공통 발 피벗·피해 범위·기존 회복 시간·일시정지·정산1회 검사 PASS. 이전 피격/보호막/보스 사망 검사도 PASS.57PNG/빈 화면0,96설명 배치 PASS. 수집 스크립트의 기존45초 제한으로 먼저 반환됐지만 실제 앱은 모든 검사와 최종 CAPTURED/정상 shutdown 로그까지 완료했다. 당시 exit code는 회수하지 못했으므로 exit0이라고 기록하지 않는다. 원본 process.json과 completion-followup.json 보존.
- 대기 수집을1초 단위/최대120초로 조정한 `windows-ui-BC-1920x1080-172403`: PID37484/exit0,같은 새·기존 검사 PASS,57PNG/빈 화면0 및96설명 배치 PASS. 두 해상도의 실제 새 돌진·가방/게이지 화면과 Editor 사망 화면을 직접 확인했다.
- 외부1,624개 원본 PNG/7,626,965packed pixels,주인공21,978원본/67,516,416packed pixels 무손실 검사 PASS. 새 네 방향 fixture는 `--charger-motion-fixtures`에서만 실행하며 기본 실행은 정식 메뉴다. 이번 검증도 정상 캠페인/수동 입력/음향 청취/성능 측정과 구분한다.

## 주요 파일과 남은 범위

`ExternalCreatureArt.cs`, `EnemyPixelAnimator.cs`, `UiValidation.cs`, `ImportChargerOrc20261003.cs`, `ChargerMotionReview20261003.cs`. 가져오기 source rect/SHA/피벗은 `ArtDirection/Review-20261003/boar-motion/native-orc-import.json`에 기록한다.

주인공 전용 피격 본체, 후반 보스의 부족한 실제 포즈, 지형 반복/모서리와 환경 마감, 최신 세 정상 캠페인·분량·1080p 전경60fps/반복 메모리/실제 청취는 여전히 남는다.
