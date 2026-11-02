사용자 2026-09-22 후속 지시: Tiny Swords Pawn의 외관을 거절하고 다른 후보 목록을 제시해 선택받을 것. 해당 후보의 활성 런타임 연결은 이전 구현으로 복구했다. 아래 주인공 적용 기록은 거절된 후보의 검토 이력이며 채택 상태가 아니다. 원본/추출/후보 코드는 보존했고 새 빌드에는 채택하지 않는다. 일반 적6종 교체는 별개이며 002458 AI/일시정지/사망 검사와437실화면 검토를 완료했다.

# 원본 캐릭터 에셋 교체 — Sscary 남성 모험가 방향 선택

2026-09-22 00:58 최신 선택: 사용자가 후보1번 Sscary The Adventurer Male의 느낌을 선호했다. 공식 무료판을 확보하고 193개 원본 프레임을 검토용 임포트했다. `Logs/Validation/2026-09-22-sscary/005704/sscary-source-review-1x.mp4`는 실제30.292142초,288화면으로 현재 맵 위 6방향 대기/걷기/대시/사망을 재생한 결과다. 고정48×64캔버스/발(24,22)/32PPU. 아직 PlayerController에는 연결하지 않았고 전체판$3 구매도 미승인. 무료 공격 없음, 유료도 검 콤보는 별도 보완 필요. 아래 Pawn 기록은 과거 거절 후보 이력이다.

2026-09-22 00:45: 후속 Mattz/PixiVan 두 외형도 사용자 거절. 애니메이션의 부드러움은 해당 미리보기 수준을 기준으로 삼는다. 일반 적 체력바와 주인공 피격 표시 수정 결과는 `Logs/Validation/2026-09-22-external-creatures/004218`에 보존했다. 원본 PNG의 불투명 픽셀로 독립 검사한 체력바 여백, 점프 추적, 플레이어/무기 가시성, 무적·일시정지·슬로모션·색상 복원을 검증했다. 434개 원본 화면과 실제43.161477초 영상 `creature-feedback-1x.mp4`. Windows 빌드는 아직 S이며 이 수정은 미포함.

사용자의 최신 기준은 세피리아·던그리드처럼 귀여운 주인공, 프레임 사이 얼굴 일관성, 원본 외부 에셋 우선이다. 무료라는 이유만으로 작은 구형 RPG 캐릭터나 광택 나는 중갑 기사를 채택하지 않는다. 원본과 이전 구현은 보존한다.

## 이번 적용

- 주인공: [Pixel Frog Tiny Swords Free Pack](https://pixelfrog-assets.itch.io/tiny-swords)의 Pawn. 192px 원본 캔버스, 실제 몸체 약52×64px, 64PPU. 원본 Body/Head/Hands를 합성하고, Object 레이어의 원래 장갑 색과 인접한 원래 윤곽만 분리한다. 재칠·축소·AI 보간 없음. `hero-extraction.json`에 출처/소스 프레임/픽셀 SHA/고정 발 기준/손 좌표 기록. 별도 장비 이미지를 원본 손 위치에 연결한다.
- 실제 원본: 대기8, 달리기6, 도끼형 스윙6, 칼 동작4, 곡괭이 동작6, 망치3. 총33개의 몸체 파일이며 일부 클립의 몸체가 공유되는 것은 작가 원본이다. 좌우 반전을 별도 포즈로 세지 않는다. 대기 원본 그림자는 별도 Sprite로 표시한다.
- 8방향 입력은 유지되지만 몸체 시점은 오른쪽/왼쪽 반전 2종이다. 전용 뒷모습·대시·피격·사망 원본은 없다. 대시는 원본 달리기6장을 사용하고, 피격은 기존 피격 표시, 사망은 실제 마지막 자세를 소멸시킨다. 목표의 이동12~16/공격12~18/강공격16~24 프레임을 충족했다고 주장하지 않는다.
- 일반 적6종: CraftPix Slime1/3/2 → Grunt/Shooter/Leaper, Dotmancer Boar → Charger, CraftPix Vampires2/3 → Tidecaller/Sentinel. 기본32PPU와 앞/뒤/좌/우 원본 동작을 사용한다. 공격 준비/타격/회수는 실제 AI 상태와 일치시킨다. 기존 비균일 크기 확대를 시각 자식에서 보정하고, 실제 몸체에 맞춰 피격 범위를 조정한다. 기존 공격 수치/타이밍은 유지한다.
- Boar에는 피격/사망 원본이 없으므로 사망 시 마지막 자세를 소멸시킨다. 다른 다섯 종은 원본 피격/사망을 사용한다. 보스3종은 아직 기존 아트이며 이번 교체 완료 범위에 포함되지 않는다.

## 보존 및 출처

원본 ZIP/공식 페이지/해시: `ArtDirection/ExternalCandidates/20260921/{tiny-swords,craftpix-slimes,craftpix-vampires,dotmancer-diamond}`. 이전 주인공 코드: `ArtDirection/Backups/20260922-native-hero`. 이전 이미지 파일도 그대로 보존하며 활성 주인공은 외부 에셋만 참조한다.

- Pixel Frog 현재 무료판: 개인/상업 게임 사용·수정 허용, 독립 재배포/재판매/재포장 금지. 과거 CC0 판과 구분한다. 유료 Enemy Pack 미구매.
- [CraftPix Slimes](https://free-game-assets.itch.io/free-slime-mobs-pixel-art-top-down-sprite-pack), [Vampires](https://free-game-assets.itch.io/free-vampire-4-direction-pixel-character-sprite-pack), [라이선스](https://craftpix.net/file-licenses/): 게임 내 사용/수정 허용, 독립 재배포 금지. AI 학습/개발 입력으로 사용하지 않는다.
- [Dotmancer Diamond](https://dotmancer.itch.io/diamond-top-down-pixel-art): 공식 무료 full_version 다운로드. 원본 내부 `Paid` 폴더명은 구매를 뜻하지 않는다. 게임 내 사용/수정 조건을 따르며 Godot 코드/프로젝트는 통합하지 않는다.

## 검증 상태

원본 PNG/레이어/접촉시트 직접 검토 완료. Unity 임포트, 실제 게임 화면/입력/공격/무기 연결, 패킹 픽셀 보존, Windows 빌드는 후속 결과를 여기에 기록한다. 이 문서의 코드 적용은 검증 PASS나 전체 목표 완료를 뜻하지 않는다.

이전 S 빌드의 표시 상태 Windows 검증은95장의 비검은 화면으로 완료되었다. 숨긴 창의 검은95장은 시각 증거에서 제외한다. S 정상 근접 런은 약314초 뒤 stage3 room1/44 kills에서 끝났으며 최종 보스·엔딩 완주가 확인되지 않았다. 후반 고FPS만으로 성능 문제가 해결됐다고 판정하지 않는다.

## 최신 교정: 구매 대기 해제 및 실제 플레이 교체
사용자 지시에 따라 유료 구매 질문을 철회. 선택한 무료 Sscary 프레임을 PlayerController 실제 경로에 우선 적용하며 이전 생성 주인공 fallback은 제거한다. 공격 프레임 생성본 v1/v2는 원본과의 비례/픽셀 불일치로 불채택. 전투 중 원본 대기/이동 임시 자세는 미완성이며 향후 원본과 일치하는 공격 포즈 제작을 계속한다.

사용자 정정: 원본의 등짐은 배낭이며 스카프가 아니다. AI의 잘못된 scarf 지시가 들어간 생성 v1/v2는 미채택/재사용 금지. 런타임193장에는 해당 생성본이 없고 공식 원본 픽셀이 사용된다.

## 2026-09-22 01:50 후속 수정과 실제 검사
- 이전 V 정상 Windows 입력 런은477.8초/60킬/3보스/최종엔딩,exit0. Logs/Validation/2026-09-22-hero-sockets/windows-V. 녹화로 성능 측정 아님.25–40분 목표미달. 아래 최신걷기/소환/대시 변경 이전 빌드다.
- 네이티브 주인공 손144좌표/원본193픽셀일치/총구동기화. 013032 격리검사280관찰/10발/총구오차0. 그러나 손 좌표 일치만으로 얼굴을 가리는 총 자세를 놓쳤고 사용자가 지적했다. 해당 이전화면을 시각합격으로 취급하지 않는다.
- 후속 걷기28→12fps(기본속도), 대시중총구아래, 모든몬스터공통소환마법진. 014504 격리검사305관찰/10발/총구오차0,모든종동일프레임/정지/완성/잔광정리PASS. 실제26.522339초영상259표본. Logs/Validation/2026-09-22-gait-summon/014504. 아직 정상Windows최신검사 및 모든방향시각검수별도.
- 소환출처 https://srzolda.itch.io/magic-summons, CC BY4.0; 크레딧StreamingAssets에 추가.36원본프레임 PNG External/Srzolda/Summon. Native 64px로 정확한8배 확대를복구했고 재그림/AI생성없음.
- 장비31장착/61아이콘 전체재작업 사용자요청 추가. 무료후보 NinjaAdventure/LimeZu/Shikashi/7Soul/Scrython/FartFish비교중; 출처 ArtDirection/ExternalCandidates/20260922. 그림이 맞지 않는것은 구매/자동채택하지 않음. 현재권총1개생성후실화면검수예정. 전체교체완료아님.
