"""Record the completed source-preserving atlas unit from actual AW evidence, UTF-8."""
from pathlib import Path
from datetime import datetime
import json

root = Path(__file__).resolve().parents[2]
review = root / 'ArtDirection/Review-20261002/native-frame-atlas'
logs = root / 'Logs/Validation/2026-10-02-native-frame-atlas'
load = lambda path: json.loads(path.read_text(encoding='utf-8-sig'))
build = load(review / 'build-AW-summary.json')
footprint = load(review / 'build-footprint-AW.json')
migration = load(review / 'migration-result.json')
settings = load(review / 'editor-catalog-settings.json')
smoke = load(logs / 'windows-smoke-AW-163720/process.json')
gpu_process = load(logs / 'windows-gpu-AW-163826/process.json')
gpu = load(logs / 'windows-gpu-AW-163826/gpu-source-result.json')
ui = load(logs / 'windows-ui-AW-164140/process.json')
capture = load(logs / 'windows-ui-AW-164140/capture-content.json')
assert build['result'] == 'Succeeded' and migration['result'] == settings['result'] == gpu['result'] == capture['result'] == 'PASS'
assert all(row['exitCode'] == 0 and row['buildId'] == build['buildId'] for row in (smoke, gpu_process, ui))
assert gpu['sourceFrames'] == 14349 and gpu['mismatches'] == 0 and footprint['rawNativeTextures'] == footprint['retiredObjects'] == 0
elapsed = lambda proof: (datetime.fromisoformat(proof['endedAtUtc']) - datetime.fromisoformat(proof['startedAtUtc'])).total_seconds()
exe = 'Builds/CampaignCandidate-20261002-AW/RuinRunner.exe'
doc = f'''# 개별 프레임 보존과 빌드 리소스 정리 — 2026-10-02

현재 Windows 후보는 `{exe}`, `{build['buildId']}`다. 얼굴·배낭·발·손 연결, 여섯 대기 자세, 공격·복귀의 그림과 재생 시계는 그대로 유지했다. 이 단위는 리소스 포함/조회 방식의 변경이며 전체 게임 완성 판정은 아니다.

`TerrainExternal`이 모든 외부 이미지를 묶는 동시에 같은 PNG 텍스처들이 Resources로 포함되고 있었다. AV의 텍스처는997,187,932바이트였고, 더 이상 표시하지 않는 이전 공격·대기 조합도 들어갔다. 현재 프레임을 원본 PNG에서 직접 참조하는 두 ScriptableObject 카탈로그와 전용 V2 아틀라스로 연결했다. Addressables·새 패키지·유료 에셋·생성 그림·리샘플링을 도입하지 않았다.

- 현재 PNG: `Assets/RuinRunner/ArtSources/Hero/ArmedGripV3` 12,266개, `WeaponStandingV4` 2,083개.
- 이전 저작/검토용 16,502개: `Assets/RuinRunner/ArtSources/Archive/SscaryMaleCombat`. 기존 저작 도구의 논리 경로는 Editor에서 원본으로 연결된다.
- AssetDatabase 폴더 이동 전후30,851개의 PNG/메타 바이트와 GUID를 비교했다. 변경0. 폴더 GUID/메타도 보존했다. `source-inventory.json`, `source-folder-aliases.json`, `migration-result.json`에 이전·현재 위치와 SHA가 있다.
- 기존 PNG는 계속48×64/발 피벗(24,22)/32PPU/FullRect다. 아틀라스는Point/RGBA32/밉맵 없음/4픽셀 패딩/회전·tight packing 없음, built-in 포함이다. 빌드 전 두 카탈로그의 모든 원본 PNG와 실제 Sprite geometry를 확인하고 픽셀 SHA를 생성한다. 메타/아틀라스 YAML을 손으로 편집하지 않았다.
- `IndividualArt`는 기존 논리 ID와 sampler API를 유지한다. 카탈로그는Root당 한 번 로드하고 기존 body/손 시계를 건드리지 않는다. 일반 UI/지형의 Texture 조회는 기존대로 유지한다. 보존된 과거 캡처/manifest의 PNG 경로는 `Tools/Art/art_source_path.py`로 읽으며 기록 자체를 고치지 않는다.

빌드 결과는 {build['buildStartedAt']}~{build['buildEndedAt']}, {build['buildTimeMs']/1000:.3f}초, 오류{build['totalErrors']}·경고{build['totalWarnings']}다. AV의{footprint['baselineBytes']:,}바이트에서 AW의{footprint['candidateBytes']:,}바이트로 {footprint['savedBytes']:,}바이트/{footprint['reductionPercent']:.2f}% 감소했다. 실제 DetailedBuildReport에서 원본 native 텍스처 중복0, 보관 버전 객체0을 확인했다. 텍스처는{footprint['textureObjects']:,}객체/{int(footprint['textureBytes']):,}바이트다. 소스/카탈로그/원본 목록21SHA는 빌드 후 일치했다. 이전 AV와 기존 빌드는 보존했다. 해시/용량 근거는 `build-source-AW.json`, `build-footprint-AW.json`, `build-AW-summary.json`이며 전체 보고서는 `build-AW.json`이다.

현재 후보 실행 검증:

|검증|현재 결과|범위|
|---|---|---|
|Windows smoke|PID{smoke['processId']}, `{smoke['profile']}`, exit0, {elapsed(smoke):.3f}초|기동·카탈로그·충돌·장비·성장·저장 서비스. 새14,349프레임/44,080,128전체 픽셀의 packed/source SHA 일치; 13페이지. 별도 외부1,456PNG/6,938,837픽셀도 일치.|
|실제 GPU native 원본 렌더|PID{gpu_process['processId']}, exit0, 113PNG 페이지|{gpu['graphicsDevice']}. SpriteRenderer의 실제 atlas UV/alpha/RGB를native1:1로 모두 비교.14,349프레임/44,080,128픽셀 차이0.|
|표시된 Windows UI fixture|PID{ui['processId']}, `{ui['profile']}`, exit0|1280×720의30PNG 모두 비어 있지 않음. 메뉴·시작HUD·첫 지역 보스 화면을 직접 검토. staged 상태이며 정상 입력/캠페인/전체30화면 수동 평가가 아님.|
|현재 Editor 참조/설정|14,349카탈로그 참조/논리 lookup 일치, 이전 저작 소스 접근 유지|Built-in/V2. `editor-catalog-settings.json`. 중지된 Editor 품질 기본값AA2/anisotropic ForceEnable은 런타임 CameraFollow의AA0/Disabled·MSAA/HDR/동적해상도false와 구분했다. 카메라/기준640×360/PPU를 이번 단위에서 바꾸지 않았다.|

현재 로그는 `Logs/Validation/2026-10-02-native-frame-atlas/windows-smoke-AW-163720`, `windows-gpu-AW-163826`, `windows-ui-AW-164140`이다. GPU 원본 page000/096과 메뉴/시작HUD/첫보스 원본을 직접 열어 보았다. GPU 모든 셀의 수치상 원본 일치를 모션 품질 수동 평가로 바꾸어 보고하지 않는다. 기존 V4의1배속/무기 방향/복귀 평가는 `Docs/WeaponStances-20261002.md`에 별도 기록했다.

실패/제약도 보존한다. `windows-ui-AW-163850`은 프로세스exit0/CAPTURED이지만 숨긴 창의30화면이 모두 검어서 시각 증거에서 제외했다. `capture-content.json` FAIL을 남긴 뒤 표시된 창에서 다시 검증했다. 최초 Editor inspection은 보관 PNG 이름을00000으로 가정했으나 이전 버전은00형식이었다. 검사 경로를 실제 원본 이름으로 고쳐 통과했으며 생산 소스 문제로 세지 않는다. 해당 기록은 `editor-inspection-first-attempt.json`이다. CLI의 최초 inventory 60초 관측timeout 뒤 실제 inventory 완성과 이동 전 전체 목록을 확인하고 진행했다. 관측timeout을 성공/실패로 추정하지 않았다.

smoke의12.773초는 AV의20.490초보다 짧지만, 검증하는 보관 프레임 수와 검증 경로가 달라 순수 로딩 성능 향상치로 쓰지 않는다. 새 카탈로그/아틀라스는 packed pixel 검사를 위해 읽기 가능하며, CPU/GPU 메모리의 실제 반복 런 안정성과1080p 전경 성능은 아직 측정해야 한다. 현재 native 참조의 그림은 줄이지 않았다. 전용 피격·재장전, 부족한 적/보스 모션·환경/크기/맵 마감, 최신 최소세빌드 방향의 정상 캠페인·분량·성능·음향/입력 검증은 계속 미완료다.

주요 변경: `Runtime/Feedback/NativeHeroFrameCatalog.cs`, `IndividualArt.cs`, `Editor/NativeHeroAtlasPrebuild.cs`, `Runtime/Diagnostics/RuntimeSmokeTest.cs`, `NativeHeroAtlasValidation.cs`, 검증 flag만 추가한 `Core/GameBootstrap.cs`. `IPreprocessBuildWithReport`로 카탈로그/현재 atlas를 생성하고 기존 TerrainAtlasPrebuild는 남은 외부/지형을 계속 구성한다. 무료 Sscary 원본의 기존 사용·수정 조건은 `ASSET_SOURCES.md`, `Assets/StreamingAssets/ThirdPartyNotices/Sscary-Adventurer.txt`에 그대로 보존한다.
'''
target = root / 'Docs/NativeFrameAtlases-20261002.md'
assert not target.exists(), 'Preserve the existing completed atlas record.'
target.write_text(doc, encoding='utf-8')

readme = (root / 'README.md').read_text(encoding='utf-8')
start = readme.index('현재 AV는 ')
end = readme.index('\n\n', start)
readme = readme[:start] + '자세 분리까지 검토한 AV의31무기×8방향/이동·공격·복귀/원본 픽셀 검증은 [WeaponStances-20261002](Docs/WeaponStances-20261002.md)에 기록했습니다. 현재 AW는 그림·프레임·시계를 유지하며 카탈로그/아틀라스로 연결하고 이전 시안을 편집용으로 보관해1.164GB→523MB로 줄였습니다. 실제 Windows 원본14,349프레임의 GPU 픽셀/기동·장비·성장·저장과 표시된1280×720 fixture를 확인했습니다. 자세한 범위·실패·현재 빌드는 [NativeFrameAtlases-20261002](Docs/NativeFrameAtlases-20261002.md), 교차 베기는 [CrossCutEffects-20261002](Docs/CrossCutEffects-20261002.md)를 참조합니다. 전용 피격/재장전·적/보스·맵 마감과 최신 최소세빌드 방향의 정상 캠페인·분량·1080p60fps/반복 메모리·청취 검증은 계속 남습니다.' + readme[end:]
readme = readme.replace('현재 Windows 후보는 `Builds/CampaignCandidate-20261001-AV/RuinRunner.exe`입니다.', f'현재 Windows 후보는 `{exe}`입니다.', 1)
(root / 'README.md').write_text(readme, encoding='utf-8')

status = (root / 'WORK_STATUS.md').read_text(encoding='utf-8')
assert 'native 프레임 카탈로그·리소스 정리 AW' not in status
status = f'''## 2026-10-02 native 프레임 카탈로그·리소스 정리 AW

- 전체 목표 ACTIVE/미완료. 개별 PNG를 유지하며 현재14,349프레임을 built-in V2 아틀라스/카탈로그로 연결했다. 이전16,502개는Editor 저작용 보관 폴더로 이동했다. 실제 AssetDatabase 이동 전후30,851PNG의 바이트/메타/폴더 GUID/에셋 GUID 변경0. 원본 논리 샘플러·모션/입력/손 시계는 유지한다.
- AW `{build['buildId']}`, `{exe}`, {build['totalSizeBytes']}바이트/{build['buildTimeMs']/1000:.3f}초/오류0·기존경고3. AV보다640,573,296바이트/55.04% 감소. 실제 build report의 raw native 텍스처 중복0/보관 객체0, 빌드 후21소스 SHA 일치. RGBA32/Point/FullRect/밉맵 없음/4padding/회전·trim 없음, 새 패키지나 이미지를 도입하지 않았다.
- 실제 AW smoke PID{smoke['processId']}/{smoke['profile']} exit0: startup/현재14,349packed sprites·44,080,128픽셀/외부1,456PNG/충돌·장비·성장·저장 PASS. 실제 GPU PID{gpu_process['processId']}의SpriteRenderer native1:1/113페이지도14,349원본/44,080,128픽셀 차이0/exit0. 로그는 Logs/Validation/2026-10-02-native-frame-atlas에 있으며 정상 캠페인/1배속 품질/성능 증거와 구분한다.
- 숨김 Windows UI163850은30검은PNG여서FAIL/시각 증거 제외. 표시된 창164140 PID{ui['processId']}/exit0은30nonblankPNG; 메뉴·시작HUD·첫보스와 nativeGPU 페이지000/096을 직접 확인했다. 기존 V4의1배속 평가는 아래 당시 기록이며 최신 transport 검증을 모션 재검증으로 세지 않는다. 보관 PNG 이름을 잘못 가정한 최초 inspection 실패도 남겼다. Docs/NativeFrameAtlases-20261002.md.
- smoke12.773초는 검증 항목/원본 수가 달라 AV20.490초 대비 순수 로딩 성능으로 보고하지 않는다. readable atlas의 CPU/GPU 메모리 및 실제1080p 전경60fps/반복 런은 미측정이다. 전용 피격·재장전/적·보스/환경·맵·FX·크기 마감, 최신 세 정상 캠페인·분량·음향/일반 입력/저장 호환 검증은 남는다. AV/기존 빌드·원본·실패·사용자 저장은 보존했다.

''' + status
(root / 'WORK_STATUS.md').write_text(status, encoding='utf-8')

stance = (root / 'Docs/WeaponStances-20261002.md').read_text(encoding='utf-8')
stance = stance.replace('현재 Windows 후보는 `Builds/CampaignCandidate-20261001-AV/RuinRunner.exe`', '자세 분리를 검증한 AV 후보는 `Builds/CampaignCandidate-20261001-AV/RuinRunner.exe`', 1)
stance = stance.replace('개별 PNG 원본·GUID를 보존하는 리소스/빌드 최적화와 실제 정상 캠페인·전경 성능 검증은 남는다.', '이후 개별 PNG 원본·GUID를 보존하며523MB로 정리한 현재 AW와 추가 packed/GPU 원본 검증은 [NativeFrameAtlases-20261002](NativeFrameAtlases-20261002.md)에 기록했다. 실제 정상 캠페인·전경 성능 검증은 남는다.', 1)
(root / 'Docs/WeaponStances-20261002.md').write_text(stance, encoding='utf-8')

with (root / 'ARCHITECTURE.md').open('a', encoding='utf-8') as stream:
    stream.write('\n\n## Native frame catalog and built-in atlas sources — 2026-10-02\n\nNativeHeroFrameCatalog preserves existing logical frame IDs while referencing individual PNG Sprites outside Resources. NativeHeroAtlasPrebuild (IPreprocessBuildWithReport) writes two source-derived catalogs and lossless V2 atlases; TerrainAtlasPrebuild retains the other external/terrain sources. No rotated/tight meshes, resampling, new action clocks or source file deletion. Retired action/stance PNGs remain available to Editor authoring through IndividualArt aliases and retain GUID/metadata. RuntimeSmoke compares every current full packed rectangle with its original visible-pixel SHA; opt-in NativeHeroAtlasValidation independently renders all14349 actual SpriteRenderer frames at native1:1 on the Windows GPU. Detailed build contents exclude raw native Texture2D duplicates and all archived sprites. CameraFollow retains its640x360 framing/32 worldPPU and runtime AA/MSAA/HDR/dynamic-resolution guards. Scope/failures/current AW evidence: Docs/NativeFrameAtlases-20261002.md. Whole-game completion and actual performance remain pending.\n')
with (root / 'ASSET_SOURCES.md').open('a', encoding='utf-8') as stream:
    stream.write('\n\n### Native frame atlas packaging — 2026-10-02\n\nNo new art or licensing terms. The approved Sscary source and existing native arm/stance edits remain individual PNGs under Assets/RuinRunner/ArtSources/Hero; earlier action/stance versions remain under ArtSources/Archive/SscaryMaleCombat with unchanged PNG bytes, metadata and GUIDs. Current14349frames reference two built-in lossless SpriteAtlases through source-derived catalogs; retired16502frames are not shipped. This packaging does not add independently authored poses or alter the approved face/backpack. Source/move/pixel/build records: ArtDirection/Review-20261002/native-frame-atlas and Docs/NativeFrameAtlases-20261002.md. Existing Sscary notices and originals are preserved.\n')
print(json.dumps(dict(result='RECORDED', buildId=build['buildId'], executable=exe, savedBytes=footprint['savedBytes'], changedSources=False)))
