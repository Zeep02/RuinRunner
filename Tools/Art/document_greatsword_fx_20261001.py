"""UTF-8 current-unit records, preserving prior source/capture/build history."""
from pathlib import Path
import json,csv,hashlib,sys
root=Path(__file__).resolve().parents[2]
review=root/'ArtDirection/Review-20261001/weapon-identity-more'
dataset=root/sys.argv[1]
result=(dataset/'result.txt').read_text(encoding='utf-8')
assert result.startswith('PASS ')
face=json.loads((review/dataset.name/'screen-face-qa.json').read_text(encoding='utf-8'))
assert face['result']=='PASS'
build=json.loads((review/'build-AR-summary.json').read_text(encoding='utf-8-sig'))
assert build['status']=='completed' and build['result']=='Succeeded' and build['totalErrors']==0
smoke=root/sys.argv[2]
process=json.loads((smoke/'process.json').read_text(encoding='utf-8-sig'))
assert process['buildId']==build['buildId'] and process['exitCode']==0
assert '[Prototype Smoke Test] PASS' in (smoke/'player.log').read_text(encoding='utf-8-sig')
manifest=json.loads((review/'native-import-before-rejection.json').read_text(encoding='utf-8'))
manifest['rejectedImages']=[p for p in manifest['images'] if p['clip']=='ChargedCut']
for p in manifest['rejectedImages']:
    p['retiredPath']=p['path'].replace('Resources/Art/Pixel/Separated/CombatFX/ChargedCut','ArtReview/RejectedChargedWedge20261001')
    assert (root/p['retiredPath']).is_file()
manifest['images']=[p for p in manifest['images'] if p['clip']=='GreatswordCut']
for p in manifest['images']:
    assert hashlib.sha256((root/p['path']).read_bytes()).hexdigest()==p['fileSha256']
manifest['currentRuntime']='Only Slash3/color5 nine unchanged original PNGs. Branching Slash2/color4 was rejected by user; preserved with GUID outside Resources. Runtime swing pivots on the rendered held grip, not the damage box center.'
manifest['activeNativePngCount']=9
manifest['rejectedPreservedPngCount']=7
if 'contactAlignment' in manifest:manifest['rejectedCandidateContactAlignment']=manifest.pop('contactAlignment')
(review/'native-import.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
assertions=sum(s.startswith('PASS ') for s in result.splitlines())-1
samples=len(list(csv.DictReader((dataset/'recording.csv').open(encoding='utf-8-sig'))))
source_keys=json.loads((review/dataset.name/'actual-source-key-review.json').read_text(encoding='utf-8'))
key_min=min(len(case['visibleSourceKeys']) for case in source_keys);key_max=max(len(case['visibleSourceKeys']) for case in source_keys)
body=f'''# 대검 궤적과 충전 표시 수정 — 2026-10-01

사용자가 지적한 주황색 갈래/Y형 내려찍기 시안을 제외했다. 검의 동작과 다른 모양이었으며 피벗 변경만으로 해결되지 않는 원본 선택 결함이었다. 새 참격도 처음에는 피해 상자 중앙을 회전 중심으로 삼아 검끝에서 떨어져 있었다. 현재는 실제 표시 손의 그립을 중심으로 회전하고, 이동 중에도 HeldWeaponView의 최신 손 위치를 따른다.

철제 대검·용 살해자·혈흔 대검의 내려찍기에는 Frostwindz 공식 무료 Slash3/color5의 128×128 원본 PNG 9장을 사용한다. 작은 이미지를 리사이즈하거나 생성/재색칠하지 않았다. 본체 준비→실제 .42 접촉→후속→복귀 시계를 따라 재생하고, 프레임 지연으로 접촉 키를 건너뛰어도 실제 판정이 난 프레임에 밝은 원본 키4를 표시한다. 피해 판정·수치·이동·마나·입력 속도는 변경하지 않는다. 일반/충전 해제는 같은 내려찍기 동작의 궤적을 공유하며 기존 충전 준비, 더 큰 판정 크기와 접촉 후 분진·금속 파편으로 무게를 표현한다. 다른 무기군을 이 궤적으로 통일하지 않았다.

넓은 대검의 손잡이가 일부 방향에서 원본 얼굴을 덮던 문제는 본체 아래에 그려 고쳤다. native 손은 그립 앞에 남는다. 완전 충전의 완료 불꽃도 얼굴 아래 순서를 따라가며 해제·대시·교체에서 준비 빛과 함께 사라진다. 참격은 취소 시 즉시 숨겨 무기 교체 후 이전 접촉 프레임이 다시 보이지 않는다.

## 실제 화면과 검사

- `{dataset.relative_to(root).as_posix()}`: {assertions}개 단언 PASS, 실제 1280×720 화면 {samples}장. 6시점×일반 2타 콤보/부분 충전/완전 충전의18사례, 대검3종 이동 콤보, 일시정지·대시·교체·완전 충전 취소·메뉴 정리. 접촉 횟수와 실제 표적 피해를 확인한다.
- 각 사례에서 실제 참격 원본{key_min}~{key_max}개의 키가 관찰됐다. 짧은 프레임 지연으로 개별 키를 건너뛸 수 있어 원본 수와 화면 관찰 수를 구분한다. 실제 CSV시간을 유지한 무음 H264이며 보간/배속/가짜 프레임이 없다.
- 원본 얼굴/상체 불투명 픽셀 {face['sampledUpperBodyPixels']}개를 실제 화면과 좌표 그대로 비교: 가려지거나 바뀐 픽셀 {face['occludedOrChangedPixels']}개. PNG 크롭은 실제 해상도이며 리사이즈하지 않았다.
- 장비·정지 표적과 자원을 공급하고 스포너를 정지한 아트/입력 검토다. 정상 캠페인·게임 음향 청취·1080p60fps 성능 증거가 아니다.

## 현재 Windows 후보

`Builds/CampaignCandidate-20261001-AR/RuinRunner.exe`, build ID `{build['buildId']}`. 연결된 Editor의 CleanBuildCache/DetailedBuildReport/StandaloneWindows64 빌드 성공: {build['totalSizeBytes']}바이트, {build['buildTimeMs']/1000:.3f}초, 오류0/기존 경고{build['totalWarnings']}개. 원거리 작업의 AQ는 수정 전 후보로 보존한다. 기본 실행은 정상 메인 메뉴다.

실제 AR 프로세스 {process['processId']}, 격리 프로필 `{process['profile']}`: startup/native 리소스·충돌·장비·성장·저장 smoke PASS/exit0. 근거 `{smoke.relative_to(root).as_posix()}/process.json`, `player.log`. 숨겨진 batchmode/nographics 검사이며 정상 캠페인·화면 품질·소리·성능 검사를 대신하지 않는다. 최종 Editor groundTruth는 컴파일 실패false/오류0/기존 경고3이며 과거 Console buffer의19오류를 지우지 않았다.

## 실패와 보존

162650: 실제 렌더 지연으로 접촉 키를 건너뛰는 결함 발견. 해당 실패의 화면 Flush도 미완료여서 모든 PNG/CSV가 남았다고 주장하지 않는다. 이후 실제 접촉 카운터를 읽고, 실패 시 CSV를 먼저 보존하도록 수정했다. 163148/163405: Editor 비활성 렌더50~100ms로 표시 키3장만 관찰, 최소5키 검사 FAIL. 공격 시간을 늘리거나 기준을 낮추지 않고 제한된 캡처 동안 공식 Editor focus/실제 GameView를 활성화했다.

163650: 대부분 동작은 통과했으나 무기 교체의 이전 참격 한 프레임이 남는 검사 FAIL, 얼굴/상체4166픽셀 가림 FAIL. 즉시 취소 숨김으로 고쳤다. 165816: 주황색 시안 제거 후 동작 검사는 PASS였지만 회전 중심은 아직 잘못되어 채택하지 않았다. 170335: 그립 중심과 대검 뒤 순서는 통과했으나 완전 충전 불꽃199픽셀 가림 FAIL. 불꽃도 본체 아래 순서/준비 수명으로 고쳤다. 171142는 마지막 충전 취소 fixture가 BeginFrame 없이 GetKeyDown을 유지해 매 프레임 충전을 다시 시작하여 미완료였다. 캡처/CSV를 보존하고 View/auto-tick을 복구한 뒤 실패로 기록했다. fixture의 입력 프레임 재설정과3초 기한을 추가하고 다시 실행했다. 게임 충전 시간을 바꾸지 않았다. 이전 결과와 실제 화면은 삭제하지 않았다.

거절된 주황128px7장은 `Assets/RuinRunner/ArtReview/RejectedChargedWedge20261001`에 GUID와 함께 이동하여 Resources에서 제외했다. 첫64px16장 시안도 `ArtReview/GreatswordFx64V1`에 보존한다. 변경 전 코드는 `ArtDirection/Review-20261001/weapon-identity-more/before`와 중간 시안 파일에 보존한다.

## 출처와 남은 범위

[Frostwindz Pixel Art VFX - Slashes](https://frostwindz.itch.io/pixel-art-slashes). 무료 공식 ZIP SHA256 `273b180edacf5685fcc699e34ab81834ff23d03662911cbb191d7e74d4b3b428`. 원본과 다운로드/라이선스는 `ArtDirection/ExternalCandidates/20261001/frostwindz-slashes`, 배포 고지는 `Assets/StreamingAssets/ThirdPartyNotices/Frostwindz-Slashes.txt`. 개인/상업 게임·수정 허용, 독립 재배포·재판매·AI 학습 금지. 유료 파일을 사용하지 않았다.

전체 목표는 ACTIVE/미완료. 이 검사는 대검 내려찍기와 준비/완료 표시의 범위다. 모든 무기/모든 피격·장신구 효과의 얼굴 가림을 통과했다고 확대하지 않는다. 기존 첫 횡베기 및 다른 무기 효과의 추가 품질 검토, 피격/재장전 본체, 적·보스 모션과 맵 밀도·크기·연결, 최신 정상 캠페인3빌드 방향·분량·1080p60fps·반복 런 메모리는 남아 있다.
'''
(root/'Docs/GreatswordEffects-20261001.md').write_text(body,encoding='utf-8')
for name in ('README.md','ARCHITECTURE.md','ASSET_SOURCES.md','WORK_STATUS.md'):
    p=root/name;s=p.read_text(encoding='utf-8')
    if name=='README.md':
        s=s.replace('본체의 최신 상세는 `Docs/HeroRanged-20261001.md`','대검 궤적/충전의 수정은 `Docs/GreatswordEffects-20261001.md`, 본체의 최신 상세는 `Docs/HeroRanged-20261001.md`')
        s=s.replace('CampaignCandidate-20261001-AQ/RuinRunner.exe','CampaignCandidate-20261001-AR/RuinRunner.exe')
        s=s.replace('얼굴 가림 수정이 포함됩니다.', '얼굴 가림 수정과 대검9포즈 그립 중심 궤적·거절된 주황 시안 제외·충전 불꽃 정리가 포함됩니다.')
    elif name=='ARCHITECTURE.md':
        s+='\n2026-10-01 대검 내려찍기: NativeCombatEffect의 CreateCleave가 실제 AttackAnimationSequence/Progress/ContactsResolved를 읽는다. 피해를 예약하지 않으며 실제 접촉 프레임에 원본 bright key4를 보여준다. LateUpdate에서 HeldWeaponView.BladePoint(0)로 표시 그립을 따른다. CancelMeleeAttack은 CancelCleave로 즉시 숨긴다. broad crossguard 및 충전 완료 MetalSpark는 본체 아래이고, 준비 수명이 완료 불꽃까지 정리한다. ChargedCut Y형 시안은 ArtReview로 이동하여 Resources에서 제외한다. 자세한 실제 화면/실패/검증 범위는 Docs/GreatswordEffects-20261001.md.\n'
    elif name=='ASSET_SOURCES.md':
        s+='\n## 2026-10-01 대검 궤적\n\nFrostwindz 공식 무료 Pixel Art VFX - Slashes의 원본128×128 Slash3/color5 9장을 unchanged PNG로 사용한다. 상업 게임/수정 허용, 독립 재배포/재판매/AI 학습 금지. Slash2/color4 Y형7장은 사용자 거절로 런타임에서 제외했다.64px첫시안·원본 ZIP·라이선스·SHA·실패 화면은 개발 자료에 보존하며 Docs/GreatswordEffects-20261001.md와 배포 Frostwindz-Slashes.txt에 기록한다. 다른 무료 후보 IceMaan/JasonTomLee는 이 수정에 채택하지 않았다.\n'
    elif name=='WORK_STATUS.md':
        s=f'''## 2026-10-01 어색한 대검 Y형 시안 제외·그립 궤적 연결

- 전체 목표 ACTIVE/미완료. 주황색 갈래 시안은 Resources에서 제외하고 GUID/원본을 ArtReview에 보존했다. Frostwindz 원본128px9포즈 참격이 표시 손의 그립을 중심으로 움직이며 실제 .42 접촉에 밝은키4를 보여준다. 대검 넓은 손잡이와 충전 완료 불꽃의 얼굴 가림, 교체 후 참격 한 프레임 및 충전 완료 불꽃 잔류를 수정했다. 피해/이동/입력/마나/판정 크기는 바꾸지 않았다.
- {dataset.name}의{assertions}단언/{samples}원본1280×720화면 PASS.18행동+대검3종 이동·일시정지·대시/완전충전 취소·교체·메뉴. 실제 얼굴/상체{face['sampledUpperBodyPixels']}픽셀 비교 가림0. 장비/표적 공급 격리 검토이며 정상 캠페인·음향 청취·성능 검사가 아니다.163650 교체/얼굴4166픽셀 FAIL,170335 충전 불꽃199픽셀 FAIL을 보존했다. 실제CSV시간 무가속H264/출처/한계는 Docs/GreatswordEffects-20261001.md.
- 최신 AR {build['buildId']} 성공{build['totalSizeBytes']}바이트/{build['buildTimeMs']/1000:.3f}초/오류0·기존 경고3. {smoke.name}의 실제 프로세스{process['processId']}startup/native 리소스·충돌·장비·성장·저장 PASS·exit0. Builds/CampaignCandidate-20261001-AR/RuinRunner.exe가 현재 후보다. AQ/AP 및 실패 화면·과거 Console buffer를 보존한다.
- 정상3캠페인·맵/적/보스/피격/재장전·다른 효과 마감·분량·1080p60fps/반복 런 메모리는 남는다. 기존 기록은 당시 빌드 기준으로 보존한다.

'''+s
    p.write_text(s,encoding='utf-8')
print(json.dumps(dict(dataset=str(dataset),assertions=assertions,samples=samples,facePixels=face['sampledUpperBodyPixels'],activeNativePNGs=len(manifest['images']),rejectedPreservedPNGs=len(manifest['rejectedImages']))))
