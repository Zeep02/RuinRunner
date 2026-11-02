# 팔 아래 잔여 픽셀 제거 — 2026-10-04

사용자 화면의 팔 아래 피부색·검은 도트는 이전 낮은 손 자세가 새 팔 아래에 남은 것이었다. 화면 본체와 `WeaponStandingV4/01947.png`의 불투명 픽셀이 정확히 일치했다. 팔 교체 범위 아래에 남은 이전 손과 떨어진 검은 외곽선을 제거했다.

현재 카탈로그 21,978개 본체 PNG를 원래 기록된 발 동작과 대조했다. 8,988개 파일의 44,498개 가시 픽셀을 수정했다. 발은 각 포즈에 실제 사용한 legSource로 복구했으며 일괄 대기 다리로 덮지 않았다. 얼굴·머리·배낭·팔의 색상, 캔버스·PPU·피벗·GUID·메타·프레임 시계는 보존한다. 원본 192개 대기/보행/대시/사망 이미지와 활성 27개 무기도 따로 검토했다.

증거는 `ArtDirection/Review-20261004/hero-pixel-cleanup`에 있다. `before`는 기존 파일과 메타, `applied.json`은 변경 SHA와 픽셀 수, `source-verification.json`은 독립 검증 PASS다. `VerifyHeroPixelCleanup20261004.py`는 카탈로그 누락, 정확한 원본 발, 잔여 검은 팔 조각, 머리/채색 팔 변경, 메타 변경을 검사한다. 2026-10-04 최종 재검사 PASS.

## 실제 출력

- Windows BV: `Builds/CampaignCandidate-20261004-BV/RuinRunner.exe`, build_ee7e4d636667, 10:22:48 KST, 680,701,915 bytes, 138.003초. BuildReport 오류0/기존 경고3.
- 기동 PID33408/exit0/PASS. 별도 실제 GPU PID24892/exit0: 21,978 SpriteRenderer 프레임, 67,516,416 RGBA 픽셀, 173페이지 불일치0. raw 본체 Texture2D 중복 및 퇴역/ReloadV2 포함0.
- BV와 BU의 RuinRunner.Runtime.dll SHA256은 동일하다: `1b3e59e39cb2cef7c1bb8b79c22f7282ba62617e5f5951d99a954830ae02b2a4`. 이번 변경은 본체 원본 PNG만이며 정상 세 캠페인을 BV에서 다시 했다고 주장하지 않는다.
- Editor `motion-102411`: 12대표 무기×8방향=96사례, 대기 전체 호흡·이동 공격·원거리 실제 장전/복귀, 3,901실제 PNG·시간 CSV·본체/손 텔레메트리. 모든 사례와 손 연결 검사 PASS. 공급 장비/격리 저장/적 제거 fixture로 정상 캠페인이나 성능 측정이 아니다.
- `reported-dot-before-after.png`: 사용자 화면과 실제 Game View의 같은 본체01947 비교. 정확한 후 화면은 frame-000114.png이며 불투명 본체 픽셀 불일치0. 같은 native 배율로 제시했다. 12무기×8방향 정지 표본을 검토했다. 선택한 쇠뇌 대기/장전 영상은1배속 재생과 시작/끝 화면을 확인했으며 모든 프레임을 연속 관찰한 것은 아니다.

Prepare 요청은 시간 초과 후 실제 완료됐으며 생성 영수증으로 확인했다. Apply는 포커스 이후 실행됐고 import 중 autotick 명령의 시간 초과도 최종 영수증으로 대조해 중복 적용하지 않았다. 도구용 eval 문법/Count 컴파일 실수는 수정했고 Assets 컴파일 실패와 구분한다. 최종 Console103 이후 새 관리 오류0, 컴파일 종료. 원시 Editor UDSFileSystem::Open 기록141은 여전히 미해결이며 Windows/managed 성공으로 덮지 않는다.

미채택 `ArtDirection/Generated/SscaryMotion-20261004/hurt-native-v1`은 수정 전 자세를 기반으로 만들어졌으므로 현재 사용 금지다. 이후 피격 본체는 수정된 원본으로 다시 제작해야 한다. 전체 게임 목표와 전용 피격·보스/환경·사람 기준 균형·성능/메모리/청취 검증은 미완료다.
