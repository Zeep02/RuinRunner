"""Write only observed, current AU results. No assumed PASS or whole-goal completion."""
from pathlib import Path
import argparse,csv,json,hashlib,shutil
root=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser()
for name in ('motion','range','catalog','cancel','smoke'):p.add_argument('--'+name,required=True)
a=p.parse_args();review=root/'ArtDirection/Review-20261001/thrust-fx'
folders={k:root/getattr(a,k) for k in ('motion','range','catalog','cancel','smoke')}
results={k:(v/'result.txt').read_text(encoding='utf-8-sig') for k,v in folders.items() if k!='smoke'}
assert all(r.startswith('PASS') for r in results.values()),'Retain failure evidence; complete each required check first.'
rows=list(csv.DictReader((folders['motion']/'body-telemetry.csv').open(encoding='utf-8-sig')))
face=json.loads((review/folders['motion'].name/'screen-face-qa.json').read_text(encoding='utf-8'))
native=json.loads((review/'native-png-qa.json').read_text(encoding='utf-8'))
measurements=json.loads((folders['range']/'range-measurements.json').read_text(encoding='utf-8'))
assert len(measurements)==48 and all(r['visibleMatchesOuterRange'] and r['visibleSideMatches'] and r['insideDamage']>0 and r['outsideDamage']==0 and r['sideInsideDamage']>0 and r['sideOutsideDamage']==0 for r in measurements)
assert face['result']=='PASS' and face['occludedOrChangedPixels']==0 and native['result']=='PASS'
counts={k:sum(line.startswith('PASS ') for line in text.splitlines()[1:]) for k,text in results.items()}
assert counts['catalog']==201
build=json.loads((review/'build-AU-summary.json').read_text(encoding='utf-8-sig'))
process=json.loads((folders['smoke']/'process.json').read_text(encoding='utf-8-sig'))
assert build['status']=='completed' and build['result']=='Succeeded' and build['totalErrors']==0 and process['exitCode']==0 and process['buildId']==build['buildId']
assert 'CampaignCandidate-20261001-AU' in process['executable']
console=json.loads((review/'console-final-AU.json').read_text(encoding='utf-8-sig'))
ground=console['data']['result']['groundTruth'];assert not ground['compilationFailed'] and not ground['compiling'] and ground['consoleErrors']==0
sources=['Assets/RuinRunner/Runtime/'+s for s in ('Feedback/MeleeThrustEffect.cs','Feedback/MeleeSweepGeometry.cs','Player/PlayerController.cs','Player/PlayerAnimationController.cs','Enemies/EnemyController.cs','Diagnostics/RuntimeSmokeTest.cs')]
sha={s:hashlib.sha256((root/s).read_bytes()).hexdigest() for s in sources}
snapshot=json.loads((review/'build-source-AU.json').read_text(encoding='utf-8-sig'))
assert all(sha[r['path']]==r['sha256'].lower() for r in snapshot)
forward=max(abs(r['shortfall']) for r in measurements)
side=max(max(abs(r['actualSideMin']-r['visibleSideMin']),abs(r['actualSideMax']-r['visibleSideMax'])) for r in measurements)
phases=len({r['phase'] for r in rows});actions=sum('one real thrust input/contact' in l for l in results['motion'].splitlines())
summary=dict(goal='ACTIVE / unfinished',motion=str(folders['motion']),range=str(folders['range']),catalog=str(folders['catalog']),cancel=str(folders['cancel']),checks=counts,actionCases=actions,phases=phases,screenSamples=len(rows),face=face,native=native,forwardCases=len(measurements),maximumForwardError=forward,maximumSideError=side,build=build,process=process,groundTruth=ground,sourceSha256=sha)
(review/'unit-summary.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False),encoding='utf-8')
doc=f'''# 찌르기 원본·검 연결·범위 AU — 2026-10-01

전체 목표는 ACTIVE/미완료다. 주황 Y형 대검 수정에 이어 실제 찌르기와 표시 겹침을 정돈한 후보 단위다. 교차 베기의 큰 십자 별, 다른 효과·피격/재장전·적/보스·맵과 정상 세 캠페인·분량/1080p60fps/반복 메모리는 계속 남는다.

롱소드의 늘린 총구 섬광과 단검의 비균일 관통탄 적중 그림을 제거했다. SHEARLINE 무료 원본32×32는 여섯 실제 보이는 포즈와 두 빈 경계 키, IceMaan 무료 원본32×32는 다섯 실제 포즈다. 정확한 원본 RGBA를 개별 PNG로 분리하고 고정 피벗(7,16)/(2,16),32PPU,Point/무압축/FullRect를 Editor API로 설정했다. 임포트 관측30초 timeout 동안에도 실제 작업은 진행되어 끝났으며 독립13셀 PNG/실제Sprite 확인으로 결과를 검증했다. 이를13개의 새 저작 포즈나 본체 모션 추가로 세지 않는다. 승인된 배낭 주인공과 기존16시간 키 본체는 그대로 사용한다.

효과는 입력 때 생성되어 실제 본체 .34 접촉에 밝은 원본 키를 보여주고 현재 표시 검에서 움직인다. 원본의 수평 축·균일1.35/1.2배를 유지하며 손에서0.40/0.25단위 지점에 붙는다. 잔상을 접촉 위치에 별도 .24초 남기지 않고 동작 종료·대시·교체·죽음·메뉴 때 함께 제거한다. 피해 콜백이나 입력/이동·자원·피해량·기존 후딜을 추가하지 않았다. 롱소드 가드는 기존 회복 구간부터 연결하며 준비 단계의 즉시 가드 취소 기능을 새로 만들지 않았다.

찌르기의 보이지 않는 긴/넓은 피해 사각형은 원본 검끝과 폭·현재 native 손의 옆 위치로 교정했다. 의도적인 물리 찌르기 사거리·폭 변화이며 정상 캠페인 균형은 추후 검증 대상이다. 기존 벽 차단·중복 타격 방지·공격당 한 접촉·피해 배율·마나/탄약은 유지한다.

단검 돌진의 남쪽 방향 화면에서 적 체력바가 주인공의 머리를 가리던1254원본 픽셀도 발견했다. 고정32000/32001 정렬을 적의 월드 깊이와 주인공 아래 제한으로 바꿨다. 체력바와 보스 HUD를 숨기거나 얼굴 비교 대상에서 제외하지 않았다.

새 실제 입력 검토 {actions}행동/{phases}녹화 구간/{len(rows)}원본1280×720 표본과 {counts['motion']}단언 PASS. 정상 콤보 찌르기48방향 사례·단검RMB16사례·이동2사례·완전한 콤보2구간과 대시/교체/일시정지/메뉴를 포함한다. 실제 머리·상부 {face['sampledUpperBodyPixels']}원본 픽셀의 가림/변색0. 추가 가드 연결·실제 죽음 정리 {counts['cancel']}단언 PASS.

48일반 찌르기에서 원본 접촉 픽셀의 실제 렌더 변환을 별도로 투영하고4개의0.04단위 hurt proxy로 전방/옆 안·밖을 실제 Physics2D로 확인했다. 최대 전방 차이 {forward:.8f}, 옆 차이 {side:.8f}단위, 바깥 피해0. 단검RMB는16방향 실제 표적 피해·손 연결을 검사했으나 이4경계 proxy와 정상 캠페인 최대 사거리 균형의 대체 증거는 아니다. 31무기/18장신구 실제201기능 회귀 PASS.

최신 AU `{build['buildId']}`, {build['totalSizeBytes']}바이트/{build['buildTimeMs']/1000:.3f}초, 오류0/경고{build['totalWarnings']}. 실제 Windows 프로세스{process['processId']}/{process['profile']}의 격리기동·native리소스·충돌/장비/성장/저장 smoke PASS/exit0. 기본 실행은 정상 메뉴이며 검증 인자가 있을 때만 smoke를 실행한다. AT/AS와 원본·GUID·실패 시안·로그는 보존했다. 정상 Windows 화면 플레이·오디오 청취·60fps 성능 또는 정상 캠페인 완주로 부풀리지 않는다.

- 동작: `{folders['motion'].relative_to(root)}`. rust_sword-combo-1x.mp4 / worn_dagger-combo-1x.mp4는 원본CSV 시간의1배속 H264,1280×720,무음/무보간/무가속이다. supplied 장비·표적 검토다.
- 경계: `{folders['range'].relative_to(root)}`. 접촉 일시정지 원본 스틸이며 동영상/정상플레이 증거가 아니다.
- 가드/죽음: `{folders['cancel'].relative_to(root)}`.
- 전수 기능: `{folders['catalog'].relative_to(root)}`.
- Windows: `{folders['smoke'].relative_to(root)}`.
- 정확한 아트/빌드/Console/source SHA: `ArtDirection/Review-20261001/thrust-fx`.
- 이전 baseline `Logs/Validation/2026-10-01-finisher-fx/baseline-203952`는 이전 총구 섬광/십자 별의 실제 화면이다.
- 실패 `presentation-211037`은 이동 표적 충돌 조건, `presentation-211522`는 체력바1254픽셀 가림. `cancel-212008`은 기존 회복 전 즉시 가드 취소를 잘못 기대한 fixture, `cancel-212515`는 EOF 밖 캡처, `cancel-212637`은 죽음 후 정지 시간의 scaled wait로 끝나지 않은 fixture다. `presentation-212722`는 미완료 fixture와 겹친 검토로 공급 플레이어를 잃어 실패했다. Play Mode를 새로 시작하고 각 fixture의 완료를 확인하며 순차 재검증했다. 실패/미완료 자료를 보존하고 고친 fresh 검사만 근거로 사용했다.

출처: https://godboyhappy.itch.io/shearline-pixel-melee-slash-vfx / https://icemaan.itch.io/pixel-vfx-essentials. 무료 상업 게임 사용·수정 허용 조건을 확인했으며 독립 에셋팩 재배포는 하지 않는다. 원본 ZIP/RAR/공식 페이지와 `Assets/StreamingAssets/ThirdPartyNotices/SHEARLINE-Thrust.txt`, `IceMaan-VFX-Essentials.txt`에 이용 조건을 보존했다. 유료 Pimen thrust는 검토만 했고 구입/채택하지 않았다.
'''
for name in ('README.md','ARCHITECTURE.md','WORK_STATUS.md','ASSET_SOURCES.md'):
    backup=review/'before'/name
    if not backup.exists():shutil.copy2(root/name,backup)
(root/'Docs/ThrustEffects-20261001.md').write_text(doc,encoding='utf-8')
readme=(root/'README.md').read_text(encoding='utf-8');assert readme.count('CampaignCandidate-20261001-AT')==1
readme=readme.replace('CampaignCandidate-20261001-AT','CampaignCandidate-20261001-AU')
anchor='## 게임 흐름';assert anchor in readme
readme=readme.replace(anchor,'찌르기의 총구 섬광 재사용을 원본 검끝 효과로 교체하고 실제 손·접촉·전방/옆 판정과 체력바의 얼굴 겹침을 교정했습니다. 새48경계/66행동/201기능·Windows AU 근거와 한계는 [ThrustEffects-20261001](Docs/ThrustEffects-20261001.md)에 기록했습니다.\n\n'+anchor,1)
(root/'README.md').write_text(readme,encoding='utf-8')
work=f'''## 2026-10-01 찌르기 원본·검끝·판정·체력바 AU

- 전체 목표 ACTIVE/미완료. 늘린 RifleFlash/비균일 PiercingHit 대신 무료 물리 원본13native셀(실제 보이는6+5포즈)을 검·본체 .34 접촉/동작 수명에 연결했다. 바깥 피해 전방·폭을 원본 끝으로 교정하고 근접 남쪽 돌진 때 체력바의 얼굴 가림을 고쳤다. 얼굴/배낭/본체/피해량/자원/입력·이동·후딜·가드 회복 규칙 유지. 원본과 실패/기존 빌드는 보존.
- 새 {actions}행동/{phases}구간/{len(rows)}화면·머리/상부{face['sampledUpperBodyPixels']}픽셀 가림0/{counts['motion']}단언, 일반 찌르기48전방·옆 경계/바깥 피해0, 가드/실제 죽음{counts['cancel']}단언, 전체 장비201기능 PASS. supplied 장비·표적/격리 검토이며 정상캠페인·음향·성능 증거가 아니다. Docs/ThrustEffects-20261001.md.
- AU {build['buildId']}, {build['totalSizeBytes']}바이트/{build['buildTimeMs']/1000:.3f}초/오류0·경고{build['totalWarnings']}; 실제 process{process['processId']}/{process['profile']} 기동/nativeリ소스/충돌·장비·성장·저장 PASS/exit0. 현재 후보 Builds/CampaignCandidate-20261001-AU/RuinRunner.exe.
- 사거리·폭 교정의 정상 캠페인 균형, 교차 베기 십자 별/다른 FX·피격/재장전·적/보스·맵·정상3캠페인·분량/1080p60fps/반복 메모리는 남는다. 아래 기록은 당시 후보 기준.

'''.replace('nativeリ소스','native 리소스')
(root/'WORK_STATUS.md').write_text(work+(root/'WORK_STATUS.md').read_text(encoding='utf-8'),encoding='utf-8')
arch=(root/'ARCHITECTURE.md').read_text(encoding='utf-8')
arch+='\n\n## 2026-10-01 native physical stab AU\n\nMeleeThrustEffect binds fixed native tip/streak cells to HeldWeaponView.BladePoint and the committed action sequence/contact counter (.34). It owns no damage callbacks. MeleeHandPosition samples the current Sscary thrust socket before LateUpdate; MeleeSweepGeometry aligns the physical box to that socket and exact contact-cell forward/side extent. Uniform native display sizes are independent of old broad hitbox art stretching. Dash/swap/death/menu end owned presentation; early longsword guard retains its existing recovery rule. Enemy world HP bars now follow world depth below the hero instead of fixed32000 foreground. Current scope, failed fixtures and AU proof: Docs/ThrustEffects-20261001.md. Full campaign/art/performance goal remains unfinished.\n'
(root/'ARCHITECTURE.md').write_text(arch,encoding='utf-8')
credit=(root/'ASSET_SOURCES.md').read_text(encoding='utf-8')
credit+='\n\n## 2026-10-01 physical thrust sources AU\n\n- SHEARLINE free sampler — godboyhappy: https://godboyhappy.itch.io/shearline-pixel-melee-slash-vfx . Original native32 thrust-stab.png, eight exact cells (six visible poses plus empty boundaries), fixed pivot(7,16)/32PPU. Commercial game use/modification allowed; no standalone asset redistribution or authorship claim. Full supplied license: StreamingAssets/ThirdPartyNotices/SHEARLINE-Thrust.txt. No downloaded engine code executed.\n- FREE PIXEL VFX ESSENTIALS — IceMaan: https://icemaan.itch.io/pixel-vfx-essentials . Original Spritesheets/Slashes/Thrust.png, five exact native32 cells, fixed pivot(2,16)/32PPU. Personal/commercial use and modification allowed, attribution optional; no asset pack/store/AI-training redistribution. Official-page license text in StreamingAssets/ThirdPartyNotices/IceMaan-VFX-Essentials.txt; original acquisition page/RAR retained outside runtime.\n- Exact RGBA crop only: no repaint/resampling/generated poses. Fixed canvas, uniform native display. Independent PNG/import checks and actual-size action/camera proof: ArtDirection/Review-20261001/thrust-fx and Docs/ThrustEffects-20261001.md. These replace gun muzzle/bullet-hit graphics used by physical stabs; existing firearm sources remain available for firearm effects.\n'
(root/'ASSET_SOURCES.md').write_text(credit,encoding='utf-8')
print(json.dumps({k:v for k,v in summary.items() if k in ('goal','checks','actionCases','phases','screenSamples','maximumForwardError','maximumSideError')},ensure_ascii=False))
