"""Update UTF-8 development records for the verified native ranged unit."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=root/'README.md'
s=p.read_text(encoding='utf-8')
s=s.replace('원거리/피격 전용 본체, 적·보스 모션', '피격/재장전 전용 본체, 적·보스 모션')
s=s.replace('본체의 최신 상세는 `Docs/HeroDefense-20261001.md`', '본체의 최신 상세는 `Docs/HeroRanged-20261001.md`와 `Docs/HeroDefense-20261001.md`')
s=s.replace('가드·차지·패링·원거리·피격 전용 본체는 아직 미완료입니다.', '가드·패링·차지/해제2857native PNG/6144조합과 활·쇠뇌·권총·소총·산탄총·대포4964native PNG/9216조합도 추가했습니다. 원거리의 실제 정지 고유 포즈는7~15장/방향이며 대기·보행 조합을 신규 저작 포즈로 세지 않습니다. 피격/재장전 전용 본체는 남아 있습니다.')
s=s.replace('부분 차지와 공격→가드에서 손·검이 튀는 연결, 내려찍기의 반대 회전과 머리 뒤 가림을 수정했습니다.', '부분 차지와 공격→가드에서 손·검이 튀는 연결, 내려찍기의 반대 회전과 머리 뒤 가림을 수정했습니다. 이어 원거리 여섯 무기군의 별도 지원 팔·시위 해제·반동/복귀와 실제 발사 순간 손·총구 연결, 연사마다 반동 시작을 추가하고 개머리판/탄환/잔상이 얼굴을 덮는 문제를 고쳤습니다.')
# Only the current executable paragraph changes; older per-build histories stay intact.
lines=s.splitlines()
for i,line in enumerate(lines):
    if line.startswith('현재 Windows 후보는 `Builds/CampaignCandidate-20261001-AP/'):
        lines[i]=line.replace('CampaignCandidate-20261001-AP','CampaignCandidate-20261001-AQ').replace('이 포함됩니다.', '과 원거리 여섯 무기군의 native 팔/지원 손·반동/복귀·실제 총구 및 얼굴 가림 수정이 포함됩니다.')
p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
p=root/'ARCHITECTURE.md'
s=p.read_text(encoding='utf-8').replace('원거리·피격 전용 본체는 아직 pending 자세가 남으며 옛 생성 주인공을 재사용하지 않습니다.', '원거리는 SscaryRangedFrames의6클립/6시점/대기8·보행8/16키 조합으로 별도의 팔·시위·지원 손·반동/복귀를 사용한다. PrepareRangedShot은 실제 발사마다 표시 시계/sequence를 다시 시작하고 key0 손의 native 총구를 샘플한다. 이 시계는 실제 attackPoseTimer/쿨다운/마나/탄약/이동 판정을 변경하지 않는다. 개머리판과 발사 효과/탄환/잔상은 주인공 아래 그림 순서를 따라 원본 얼굴을 보존한다. 피격·재장전 전용 본체는 남으며 옛 생성 주인공을 재사용하지 않습니다.')
s=s.replace('대시 때 원거리 무기는 총구를 아래로 운반합니다.', '대시 때 손무기는 전방 축을 낮춰 운반합니다. 원거리 held Sprite는 본체 아래, native 손은 그립 앞에 보이며 활의0.9배 표시는 유지합니다.')
p.write_text(s,encoding='utf-8')
p=root/'ASSET_SOURCES.md'
s=p.read_text(encoding='utf-8').replace('원거리/피격 본체는 미완료.', '활/쇠뇌/권총/소총/산탄총/대포4964native PNG/9216대기·보행 조합을 추가했다(정지7~15실제 포즈/방향, Docs/HeroRanged-20261001.md). 원본 얼굴/배낭/부츠/팔레트/캔버스 보존. 피격/재장전 전용 본체는 남는다.')
p.write_text(s,encoding='utf-8')
print('Updated README/ARCHITECTURE/ASSET_SOURCES current ranged scope; preserved build histories.')
