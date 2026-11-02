"""Record verified stance/cross-cut candidate evidence; text only, UTF-8."""
from pathlib import Path
import json

root = Path(__file__).resolve().parents[2]
readme = root / 'README.md'
text = readme.read_text(encoding='utf-8')
old = 'Builds/CampaignCandidate-20261001-AU/RuinRunner.exe'
new = 'Builds/CampaignCandidate-20261001-AV/RuinRunner.exe'
if text.count(old) != 1:
    raise RuntimeError('Preserve current README; expected one AU candidate reference.')
text = text.replace(old, new)
anchor = '최근에는49장비의 아트·외곽선'
start = text.index(anchor)
end = text.index('\n\n## 실행', start)
text = text[:start] + '''최근에는 장비·전투 효과·등급별 전리품과 BGM을 보강했습니다. 승인된 Sscary 배낭 주인공의 얼굴·의상·발 접지를 유지하며 전용 근접·가드·패링·차지·원거리 본체를 사용합니다. 아래쪽 방향의 손을 몸통 옆으로 올리고, 대기 자세를 단검·한손 검·양손 근접·활·권총·양손 총기 계열로 나눴습니다. 활은 몸 옆에서 세워 들며, 이동·공격 후 각 계열의 대기로 짧게 복귀합니다. 카타나 마지막 베기의 큰 십자 별은 손에 연결된 두 번의 순차 대각 베기로 바꿨습니다.

현재 AV는 모든31무기×8방향 대기,88이동·공격·복귀/6호흡과96원거리 일반·특수 공격, 원본 화면 픽셀 검사를 통과한 소스로 빌드했습니다. 실제 Windows 격리 기동·리소스·충돌·장비·성장·저장과1280×720 표시 fixture도 확인했습니다. 이 검사는 정상 세 캠페인이나 성능·청취 검증을 대신하지 않습니다. 전용 피격/재장전, 적·보스·맵 마감, 최신 세 빌드 방향의 정상 캠페인·분량·1080p60fps/반복 메모리와 커진 후보 리소스의 최적화는 남습니다. 현재 근거와1배속 자료는 [WeaponStances-20261002](Docs/WeaponStances-20261002.md), 교차 베기의 실제 피해/자원 규칙은 [CrossCutEffects-20261002](Docs/CrossCutEffects-20261002.md)에 기록했습니다.''' + text[end:]
readme.write_text(text, encoding='utf-8')

status = root / 'WORK_STATUS.md'
previous = status.read_text(encoding='utf-8')
header = '## 2026-10-02 무기군별 대기·높은 그립·순차 교차 베기 AV'
if previous.startswith(header):
    raise RuntimeError('Preserve existing stance status rather than append duplicates.')
status.write_text(header + '''

- 전체 목표 ACTIVE/미완료. 승인된 얼굴/배낭/발 접지를 유지하면서 남쪽 손을 몸통 옆으로 올렸다. 모든 armed 본체가 이동 중에도 조준 방향을 유지하고, 별도 보행·호흡을 계속한다. 단검/한손 검/양손 근접/활/권총/양손 총기의 여섯 대기·0.14초 복귀를 분리했다. 활은 몸 옆에서 세워 들며0.9크기를 유지한다. 입력·이동에 복귀 지연을 넣지 않았다.
- ArmedGripV3의12,266고유 PNG/24,096조합과 WeaponStandingV4의2,083PNG/6,912조합을 실제 native Sprite API로 임포트/확인했다. 조합을 새 저작 포즈 수로 세지 않는다. V1당김 팔/V2머리·발 차이/V3실루엣 시안은 거절·보존했다. V4 원본 전체 머리RGBA/얼굴/발 차이0. 실제31무기×8대기248/499단언,88이동·공격·복귀/6호흡639단언·7111화면,96원거리 일반/특수 및 이동·취소742단언·3010화면 PASS. 원본 화면 머리/상부39,333+1,112,249+475,454픽셀의 가림/변색0.
- 카타나 큰 십자 별은 unchanged57×42 네 원본 셀의 순차 대각 베기로 교체했다. 두 실제 접촉에 총 피해를 나누고 치명타/MP2/원본 아이템 발동은 공격 기준으로 중복 방지한다. 24방향/3이동/3전체콤보와 일시정지·취소212단언/1663화면, 물리 벽/중복 콜라이더/강화·MP·발동17검사 PASS. 당시 V1대기 검토를 현재 V4품질 증거로 사용하지 않는다. 실제 action 본체/손/FX 코드는 그대로며 최신 V4대기·복귀는 별도로 확인했다.
- 실제 Game View 전면 검토와 원본 CSV의1배속 H264를 사용했다. 숨긴 Editor의10~20fps 키 누락 때문에 두 연속성 fixture가 실패했고 기준을 낮추지 않고 전면 기존 Game View에서 재검증했다. 과거 부분/실패 자료와 Console 예외를 지우지 않았다. 자세한 범위: Docs/WeaponStances-20261002.md, Docs/CrossCutEffects-20261002.md.
- AV build_dcf6593e36d2,1,163,897,055바이트/148.624초/오류0·경고3. 현재 후보 Builds/CampaignCandidate-20261001-AV/RuinRunner.exe. 소스12SHA일치. 실제 process24484/WeaponStandingAV-154357 startup/native 리소스/충돌·장비·성장·저장 PASS·exit0/20.49초. 별도 process20888의1280×720 GPU fixture30사진·CAPTURED/정상 종료 로그 확인; 메뉴/최초HUD를 직접 검토했고 native exit코드 핸들은 유지하지 않아 관측exit0을 주장하지 않는다. 정상 Windows 입력/캠페인/성능/청취 증거가 아니다.
- 조합/Resources 시안으로 후보 용량이 AU약649MB→약1.16GB다. 원본/개별PNG/GUID를 유지하는 리소스 최적화와 전용 피격·재장전, 적·보스/맵·이음새·크기·FX 마감, 최신 세 빌드 방향의 정상 완주·분량/1080p60/반복 메모리 검증은 남는다. 이전 AU와 아래 당시 기록은 보존한다.

''' + previous, encoding='utf-8')

architecture = root / 'ARCHITECTURE.md'
previous = architecture.read_text(encoding='utf-8')
header = '## 2026-10-02 armed grip and family standing profiles'
if header in previous:
    raise RuntimeError('Architecture entry already exists.')
architecture.write_text(previous + '\n\n' + header + '''

PlayerAnimationController selects native SscaryArmedFrames for real melee/defense/ranged action clocks and WeaponStandingFrames for six family-specific resting/short-return profiles. Armed facing follows committed/current aim while the original gait advances independently (reversed for backpedaling). HeldWeaponView binds the selected closed-hand socket and authored angle; the .14s visual return never gates gameplay input. Instant ranged emission uses the native action key0 muzzle/body clock. Point/no-mip/uncompressed48×64 PNGs retain the original rigid face/backpack/foot pivot.

MeleeCrossCutEffect follows two distinct native wrist actions and original57×42 katana cells. Actual contacts at .22/.52 split one action's damage and share crit/action-target proc rules; +3MP consumption is once per action. Cancellation and slow-frame contact presentation do not leave delayed second damage or a simultaneous giant cross/star. Native standing combinations and actual authored action poses are separate metrics. Dedicated hurt/reload remain pending. Current source/build/visual evidence and limits: Docs/WeaponStances-20261002.md, Docs/CrossCutEffects-20261002.md.
''', encoding='utf-8')

render_folder = root / 'Logs/Validation/2026-10-01-weapon-carry/windows-render-AV-154948'
render_report = dict(buildId='build_dcf6593e36d2', processId=20888,
    verdict='CAPTURED', nativeSize=[1280,720], screenshots=len(list(render_folder.glob('*.png'))),
    directlyReviewed=['01-main-menu.png','06-run-hud.png'], observedNativeExitCode=None,
    terminalEvidence='CAPTURED completion and shutdown in player.log; original PID no longer exists',
    scope='Actual Windows GPU UI/world fixtures using an isolated profile, transient player and supplied stage states. Not normal input/campaign, full UI review, performance or audio proof.')
(render_folder / 'assessment.json').write_text(json.dumps(render_report, indent=2), encoding='utf-8')
print('Verified AV stance/cross-cut, source/license and current Windows render scope documented.')
