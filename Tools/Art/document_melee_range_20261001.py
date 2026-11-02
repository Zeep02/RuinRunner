"""Record this bounded forward-range correction only after actual proofs pass."""
from pathlib import Path
from PIL import Image
import json,csv,hashlib
root=Path(__file__).resolve().parents[2]
review=root/'ArtDirection/Review-20261001/melee-fx-range'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
probe=root/'Logs/Validation/2026-10-01-melee-fx-range/probe-195514'
rows=read(probe/'range-measurements.json')
assert len(rows)==304 and (probe/'result.txt').read_text(encoding='utf-8').startswith('PASS ')
assert all(r['visibleMatchesOuterRange'] and r['insideDamage']>0 and r['outsideDamage']==0 for r in rows)
native=read(review/'actual-native-import-qa.json');assert native['result']=='PASS' and native['activeSourceCells']==36
contracts=[]
for source in dict.fromkeys(r['source'] for r in rows):
    row=next(r for r in rows if r['source']==source)
    imported=next(i for i in native['images'] if i['path']==source)
    assert imported['ppu']==row['sourcePPU'] and imported['pivot']==row['sourcePivot']
    image=Image.open(root/source).convert('RGBA');bbox=image.getbbox();pivot=row['sourcePivot']
    # Source-file y runs down. Circular and flipped greatsword faces use +localY;
    # the other original contact cells use -localY after their fixed 90° turn.
    positive=imported['family'] in ('Mace','GreatswordCleave')
    edge=(image.height-bbox[1]-pivot[1]) if positive else (bbox[3]-(image.height-pivot[1]))
    expected={'Dagger':16,'Longsword':17,'Mace':28,'Katana':20,'Greatsword':25,'GreatswordCleave':49}[imported['family']]
    assert edge==expected,(source,edge,expected)
    contracts.append(dict(source=source,family=imported['family'],canvas=image.size,alphaBBox=bbox,
        pivot=pivot,ppu=row['sourcePPU'],contactEdgePixels=edge,contactRadiusBeforeWorldScale=edge/row['sourcePPU'],
        sourceSha256=hashlib.sha256((root/source).read_bytes()).hexdigest()))
datasets=[]
for base,folder in [('melee-fx-binding','presentation-195059'),('greatsword-fx','presentation-200035')]:
    data=root/('Logs/Validation/2026-10-01-'+base)/folder
    result=(data/'result.txt').read_text(encoding='utf-8');assert result.startswith('PASS ')
    face=read(root/'ArtDirection/Review-20261001'/('melee-fx-binding' if base=='melee-fx-binding' else 'weapon-identity-more')/folder/'screen-face-qa.json')
    assert face['result']=='PASS' and face['occludedOrChangedPixels']==0
    samples=list(csv.DictReader((data/'recording.csv').open(encoding='utf-8-sig')))
    datasets.append(dict(dataset=data.relative_to(root).as_posix(),assertions=sum(s.startswith('PASS ') for s in result.splitlines())-1,
        actualScreens=len(samples),phases=len({r['phase'] for r in samples}),headPixels=face['sampledUpperBodyPixels']))
assert datasets[0]['phases']==45 and datasets[1]['phases']==21
reg=root/'Logs/Validation/2026-10-01-weapon-identity/catalog-200456/result.txt'
regtext=reg.read_text(encoding='utf-8');assert regtext.startswith('PASS ') and sum(s.startswith('PASS ') for s in regtext.splitlines())==202
build=read(review/'build-AT-summary.json');assert build['result']=='Succeeded' and build['totalErrors']==0
smoke=root/'Logs/Validation/2026-10-01-melee-fx-range/windows-smoke-AT-201027'
process=read(smoke/'process.json');assert process['buildId']==build['buildId'] and process['exitCode']==0
assert '[Prototype Smoke Test] PASS' in (smoke/'player.log').read_text(encoding='utf-8')
console=read(review/'console-final-AT.json')['data']['result']
if isinstance(console,str):console=json.loads(console)
assert not console['groundTruth']['compilationFailed'] and console['groundTruth']['consoleErrors']==0
runtime=['Feedback/MeleeSweepGeometry.cs','Player/PlayerController.cs','Player/PlayerAnimationController.cs',
         'Feedback/MeleeSlashEffect.cs','Feedback/NativeCombatEffect.cs']
hashes={p:hashlib.sha256((root/'Assets/RuinRunner/Runtime'/p).read_bytes()).hexdigest() for p in runtime}
summary=dict(result='PASS',scope='Normal hand-bound Sweep/Cleave forward edge and all three charged greatswords. Supplied tiny proxies and recorded motion, not normal campaign/audio/performance.',
    cases=len(rows),maximumForwardDifference=max(abs(r['shortfall']) for r in rows),contracts=contracts,datasets=datasets,
    regression=dict(dataset=reg.parent.relative_to(root).as_posix(),assertions=201),build=build,smoke=process,
    runtimeSourceSha256=hashes,wholeGoal='ACTIVE / incomplete')
(review/'unit-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
total_pixels=sum(d['headPixels'] for d in datasets);total_screens=sum(d['actualScreens'] for d in datasets)
detail='\n'.join(f"- `{d['dataset']}`: 실제 화면 {d['actualScreens']}개, {d['phases']}동작 구간, {d['assertions']}단언 PASS." for d in datasets)
doc=f'''# 근접 궤적의 전방 끝과 피해 범위 — 2026-10-01 AT

이 수정은 손에 붙은 일반 횡베기·대검 내려베기의 전방 사거리에 한정한다. 전체 목표는 ACTIVE/미완료다. 이미지·칼 크기·공격 속도·피해량·MP/탄약·이동·취소 시점을 바꾸지 않았다. 보이지 않는 곳까지 맞던 근접 사거리는 의도적으로 짧아졌다. 최신 정상 캠페인의 전투 난이도는 후속 검증 대상이다.

## 문제와 최종 동작

기존 `MeleeComboCatalog.Step`의 큰 사각형을 그대로 피해에 사용하면서, 궤적만 손으로 옮겼다. 실제 원본 픽셀의 전방 끝은 기존 피해 상자의 `reach + width / 2`보다 짧았다. 42회 실제 입력/물리 접촉 측정 `probe-192659`에서 모두 불일치했다. 궤적이 없는 곳의 0.04단위 표적에도 피해를 줬다.

앞으로 중심을 옮긴 시험 `probe-193851`은 수치상 42회 일치했지만 칼끝과 곡선이 떨어져 보였다. 화면 검토에서 거절하고 원래의 손 연결을 복원했다. 시험 소스는 `rejected-forward-offset`, 원본은 `before`에 보존했다. 단순 통과 숫자를 시각적 합격으로 취급하지 않았다.

최종 `MeleeSweepGeometry`는 같은 원본 접촉 칸의 실제 알파 픽셀 끝·피벗·PPU와 기존 균일 표시 크기를 사용한다. 실제 현재 포즈의 손 좌표는 `PlayerAnimationController.MeleeHandPosition`으로 읽는다. 피해가 발생하는 Update와 본체·손무기를 그리는 LateUpdate에서 같은 동작/보행 시계를 사용하며, 이전 프레임 손 위치를 피해 계산에 사용하지 않는다. 기존 판정의 가까운 경계와 폭 방향 범위를 유지하고 전방 길이만 표시된 끝에 맞춘다. 프레임마다 이미지 경계를 다시 잘라 피벗을 움직이거나 작은 원본을 추가 확대하지 않는다. CrossCut·찌르기·철퇴 충격파와 다른 특수 효과에는 이 계산을 적용하지 않았다.

원본 접촉 끝 계약은 단검16/36, 롱소드17/42, 철퇴28/68, 카타나20/57, 대검 첫 베기25/70픽셀/PPU다. 대검 내려베기는128px 원본의49px 끝과88px 표시 정규화를 사용한다. 연결 Editor에서36개 실제 Sprite의 규격·피벗·PPU/Point·무압축·무mipmap을 검증하고, 독립 PNG 알파 분석으로 이6개 원본 끝을 확인했다. 다른 PPU를32로 통일하지 않았다. 신규 이미지나 유료 에셋을 채택하지 않았다.

## 검증과 범위

- `Logs/Validation/2026-10-01-melee-fx-range/probe-195514`: 15종의 적용되는 모든 일반 콤보 단계와3종 대검 완전 차지를8방향에서 측정, 304사례 PASS. 각 사례는 실제 GameAction/물리 접촉을 거쳤고, 예상 경계 안·밖의 작은 표적에서 안쪽만 피해를 받았다. 실제 원본 픽셀을 표시 Transform으로 투영해 전방 끝을 비교했다. 최대 차이 {summary['maximumForwardDifference']:.6f}게임 단위. 임의 카메라 보정이나 수정 캡처를 사용하지 않았다.
- 최초42회 수정 검증 `probe-194453`, 8방향168회 `probe-194654`도 별도 보존했다. 최신304회가 이 범위의 최종 근거다. 멈춘 접촉 화면은 모션·성능·정상 적 AI 증거가 아니다.
{detail}
- 새 실제 속도 녹화 합계 {total_screens}개 원본 화면, 얼굴·상체 {total_pixels}픽셀 가림/변형0. 걷는 콤보·부분/완전 차지·접촉·잔상·일시정지·대시/교체 취소·늦은 피해 없음·메뉴 정리를 포함했다. 공급 장비/표적 검사이며 정상 획득 캠페인을 대체하지 않는다.
- `Logs/Validation/2026-10-01-weapon-identity/catalog-200456`:31무기/18장신구 실제 입력/피해·MP·탄약·가드/패링·대시·아이콘·통계201단언 PASS.

실제1배속 대검 차지: `Logs/Validation/2026-10-01-greatsword-fx/presentation-200035/greatsword-charge-AT-1x.mp4`, 원본1280×720/119실제 샘플/2.010572초. 녹화 timestamp대로 다음 실제 화면까지 유지했으며 보간·시간 압축·카메라 확대가 없다. 음소거 검토 영상으로 소리 검증은 하지 않았다.

## Windows 후보와 남은 점검

`Builds/CampaignCandidate-20261001-AT/RuinRunner.exe`, `{build['buildId']}`, {build['totalSizeBytes']}바이트, {build['buildTimeMs']/1000:.3f}초, 오류0·경고{build['totalWarnings']}개. 실제 프로세스{process['processId']}/격리 `{process['profile']}`에서 기동·native 리소스·충돌·장비·성장·저장 smoke PASS/exit0. 숨겨진 batchmode/nographics 검사로 정상 플레이·화면·소리·60fps 증거를 대신하지 않는다. Console의 현재 ground truth 오류0/경고3·컴파일 실패false를 기록했으며 이전 오류/실패 버퍼를 지우지 않았다. AS와 모든 이전 후보를 보존했다.

검토한 [D3velopp 무료 팩](https://d3velopp.itch.io/slash-effects-pixel-2d-pack)은 후보 폴더에만 보존했다. 효과가 원소 마법에 치우치고 둥근 윤곽/명암 밀도가 현재 물리 무기와 어울리지 않아 채택하지 않았다. source ZIP/공식 이용 조건/원본 실제 칸의 조사 기록을 남겼다.

이번 검사는 전방 끝과 실제 피해를 확인한 것이다. 곡선 구석의 정밀한 면적 판정, CrossCut/찌르기/충격파·특수/장신구, 다른 legacy 사각형 효과, 피격/재장전 포즈·적/보스·맵, 최신 정상3캠페인·분량·1080p60fps/반복 런 메모리 검증은 남는다. 실제 획득과 세 방향의 빌드, 세 보스/엔딩을 완료한 것으로 확대하지 않는다.
'''
(root/'Docs/MeleeForwardRange-20261001.md').write_text(doc,encoding='utf-8')
p=root/'README.md';s=p.read_text(encoding='utf-8')
assert s.count('Builds/CampaignCandidate-20261001-AS/RuinRunner.exe')==1
s=s.replace('Builds/CampaignCandidate-20261001-AS/RuinRunner.exe','Builds/CampaignCandidate-20261001-AT/RuinRunner.exe')
s=s.replace('수정이 포함됩니다. 기본 실행은','수정과 일반 베기·대검의 표시 끝에 맞춘 전방 피해 범위 교정이 포함됩니다. 기본 실행은',1)
s=s.replace('## 게임 흐름','근접 사거리 수정의 실제 입력·원본 픽셀·Windows 근거와 남은 범위는 [MeleeForwardRange-20261001](Docs/MeleeForwardRange-20261001.md)에 기록했습니다.\n\n## 게임 흐름',1);p.write_text(s,encoding='utf-8')
p=root/'WORK_STATUS.md';s=p.read_text(encoding='utf-8')
entry=f'''## 2026-10-01 근접 표시 끝·전방 피해 범위 교정 AT

- 전체 목표 ACTIVE/미완료. 손에 붙은 곡선의 실제 픽셀 끝을 기준으로 일반 Sweep/Cleave 판정의 전방 길이를 교정했다. 눈에 보이지 않는 원거리 근접 피해가 줄었다. 이미지/속도/피해량/MP/탄약/이동·취소 시점은 유지했다. 중심 이동42회가 수치상 통과해도 떠 보이는 화면이라 거절하고 손 연결을 복원했다. 신규/유료 이미지 채택 없음.
- 15종 적용 일반 콤보+3대검 완전차지×8방향304실제 입력/물리 경계 사례 PASS, 최대 차이{summary['maximumForwardDifference']:.6f}단위. 새45+21동작/{total_screens}실제 화면·얼굴/상체{total_pixels}픽셀 가림0.31무기/18장신구201기능 PASS.36개 실제 native Sprite와6접촉 원본 끝/PPU/피벗 계약 확인. 공급 장비/작은 표적/무음 모션 검토이며 정상 캠페인/소리/성능 증거가 아니다.
- 최신 AT {build['buildId']}, {build['totalSizeBytes']}바이트/{build['buildTimeMs']/1000:.3f}초/오류0·경고3. 실제 process{process['processId']}/격리{process['profile']} 기동·리소스/충돌/장비/성장/저장 PASS/exit0. AS와 실패·거절된 시안을 보존했다. 상세/1배속영상: Docs/MeleeForwardRange-20261001.md.
- 전방 범위의 교정은 의도적인 사거리 변화다. 정상 캠페인 전투 균형·곡선 구석의 면적 판정, 다른 효과·피격/재장전·적/보스·맵·정상3캠페인·분량/1080p60fps/반복 런 메모리는 남는다. 아래 기록은 당시 후보 기준이다.

'''
p.write_text(entry+s,encoding='utf-8')
p=root/'ARCHITECTURE.md';s=p.read_text(encoding='utf-8')
s+='\n\n## 2026-10-01 근접 전방 피해 범위\n\n`MeleeSweepGeometry.TryEnvelope`은 손에 붙은 Sweep/Cleave의 원본 접촉 픽셀 끝을 기존 표시 크기로 계산하고 기존 판정의 가까운 경계/폭은 유지해 전방 길이를 제한한다. `PlayerAnimationController.MeleeHandPosition`은 Update 접촉과 LateUpdate 표시가 같은 native 포즈/보행 시계를 읽도록 한다. 이 과정은 피해·입력·애니메이션 시간을 재예약하지 않는다. 다른 모션은 기존 판정을 사용한다. 계약/304물리 경계/새 모션/AT 빌드 근거와 범위는 Docs/MeleeForwardRange-20261001.md다.\n'
p.write_text(s,encoding='utf-8')
p=root/'Docs/MeleeEffects-20261001.md';s=p.read_text(encoding='utf-8');s=s.replace('\n\n','\n\n추가 교정 AT: 전방 끝과 피해 범위 측정을 마쳤으며 [별도 기록](MeleeForwardRange-20261001.md)에 최종 변경/근거/남은 범위를 정리했다. 아래 AS 검사는 당시 구현의 기록이다.\n\n',1);p.write_text(s,encoding='utf-8')
(review/'candidate-decision.json').write_text(json.dumps(dict(result='NOT_ADOPTED',source='https://d3velopp.itch.io/slash-effects-pixel-2d-pack',
    archive='ArtDirection/ExternalCandidates/20261001/d3velopp-slashes/SlashEffect_2d_pack.zip',
    reason='Native source review: elemental-magic motifs and rounded dense shading do not fit current physical melee weapons. No runtime adoption.'),indent=2),encoding='utf-8')
print(json.dumps(dict(result='PASS',cases=len(rows),actualScreens=total_screens,headPixels=total_pixels,changedPixels=0,buildId=build['buildId'],processId=process['processId'],wholeGoal='ACTIVE / incomplete')))
