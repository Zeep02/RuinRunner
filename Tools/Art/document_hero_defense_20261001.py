"""Current-status documentation; run only after AP startup smoke succeeds."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[2]
handle=json.loads((root/'ArtDirection/Review-20261001/hero-defense/smoke-handle-AP.json').read_text(encoding='utf-8-sig'))
folder=Path(handle['folder']);assert (folder/'exit.txt').read_text(encoding='utf-8-sig').strip()=='0'
assert '[Prototype Smoke Test] PASS' in (folder/'player.log').read_text(encoding='utf-8')
readme=root/'README.md';text=readme.read_text(encoding='utf-8')
a=text.index('현재 코드는');b=text.index('\n\n## 실행',a)
text=text[:a]+'''현재 세 스테이지·세 보스·최종 엔딩, 복사본별 +0~+3 강화·분해, 장신구·세트의 전투 발동과 저장·재도전 흐름이 연결되어 있습니다. **전체 목표와 배포 완료 판정은 아직 미완료입니다.**

최근에는49장비의 아트·외곽선, 무기별 발사·궤적·적중·잔여 효과와 소리, 등급별 상자·전리품, 스테이지 BGM을 보강했습니다. 승인된 배낭 주인공은 원본 얼굴·옷·배낭을 유지하며 전용 찌르기·횡베기·되베기·단검 손목 베기·양손 내려찍기와 가드·패링·차지/해제 본체를 사용합니다. 부분 차지와 공격→가드에서 손·검이 튀는 연결, 내려찍기의 반대 회전과 머리 뒤 가림을 수정했습니다.

최신 Windows 후보의 기동/새 native 리소스 검사와 장비·표적을 공급한 Editor 동작·실제 화면 검사를 통과했습니다. 이 격리 검사는 정상 캠페인 완주와 성능 검사를 대신하지 않습니다. 원거리/피격 전용 본체, 적·보스 모션과 후반 맵·크기, 최신 세 빌드 방향의 정상 캠페인·25~40분 분량·1080p60fps/반복 런 메모리 검증은 남아 있습니다. 현재 작업과 실패·재검사 범위는 `WORK_STATUS.md`, 본체의 최신 상세는 `Docs/HeroDefense-20261001.md`에 기록합니다.''' +text[b:]
text=text.replace('Builds/CampaignCandidate-20261001-AN/RuinRunner.exe','Builds/CampaignCandidate-20261001-AP/RuinRunner.exe')
text=text.replace('철퇴 지면 분진·낙하 파편이 포함됩니다.','철퇴 지면 분진·낙하 파편과 가드·패링·차지/해제·공격→가드의 native 연결이 포함됩니다.')
readme.write_text(text,encoding='utf-8')
arch=root/'ARCHITECTURE.md';text=arch.read_text(encoding='utf-8')
text=text.replace('가드·차지·패링·원거리·피격 전용 본체는 아직 pending 자세가 남으며','가드·패링·차지/해제는 SscaryDefenseFrames의 native 관절 포즈를 사용한다. 원거리·피격 전용 본체는 아직 pending 자세가 남으며')
text+='''

## 2026-10-01 방어·차지/해제와 전환

SscaryDefenseFrames의4클립×6시점×대기8/보행8×16키는6144샘플을2857고유PNG로 공유한다. 가드/패링/차지/해제의 손·본체·무기 회전은 같은 실제 상태 시계를 따른다. Charge end/ChargeCut start의 Sprite·grip·behind는 동일한 native 샘플이며 부분 해제는 실제 직전 키를 캡처한다. 실제 피해 접촉.42,방어window/마나/공격·이동 속도는 유지한다.

가드의0.22초 진입과0.14초 복귀는 표시만 처리한다. 공격→가드의 잔여 native 회복은0.065초 안에서 끝내며 방어 bool/입력에 지연을 추가하지 않는다. 대시·교체·사망은 준비 cache를 즉시 제거한다. 패링 성공은 기존0.22초 회복을 직전 native 키에서 이어간다. HasAuthoredCombatPoses는 원거리/피격 등이 남아 false다.

HeldWeaponView는 본체의 authored 각도와 grip을 즉시 함께 사용한다. 내려찍기는 위로 준비→접촉0도→아래 후속→중립이다. 위쪽을 향하는 검은 몸 뒤, 낮은 검은 몸 앞에 정렬하며 가려진 손은 항상 뒤다. 대시는 모든 무기를 낮춰 얼굴을 피한다. BladePoint는 실제 sprite 전방 축과 flipY를 반영한다. 차지 빛/방어 금속 접촉은 이 위치를 사용하고 큰 효과는 몸 아래에 둔다. NativeCombatEffect의 선택적 loop FPS 설정은 차지의 원본 포즈에만18fps를 지정하며 다른 효과의 기존 속도/피해/수명은 유지한다.

최종 증거/실패/수정 전 후보와 AP 실행 경로: Docs/HeroDefense-20261001.md. 원본 source/native identity와 현재 SpriteEditor API 임포트 기록은 ArtDirection/Review-20261001/hero-defense에 있다.
'''
arch.write_text(text,encoding='utf-8')
sources=root/'ASSET_SOURCES.md';text=sources.read_text(encoding='utf-8')
text=text.replace('가드/차지/패링/원거리/피격 본체는 미완료.','이어 가드/패링/차지/해제2857native PNG/6144대기·보행 조합을 추가했다(Docs/HeroDefense-20261001.md). 원거리/피격 본체는 미완료.')
sources.write_text(text,encoding='utf-8')
print('README/ARCHITECTURE/ASSET_SOURCES updated to verified AP smoke candidate, whole goal remains incomplete.')
