# 개별 프레임 보존과 빌드 리소스 정리 — 2026-10-02

현재 Windows 후보는 `Builds/CampaignCandidate-20261002-AW/RuinRunner.exe`, `build_bb92ce70aa2d`다. 얼굴·배낭·발·손 연결, 여섯 대기 자세, 공격·복귀의 그림과 재생 시계는 그대로 유지했다. 이 단위는 리소스 포함/조회 방식의 변경이며 전체 게임 완성 판정은 아니다.

`TerrainExternal`이 모든 외부 이미지를 묶는 동시에 같은 PNG 텍스처들이 Resources로 포함되고 있었다. AV의 텍스처는997,187,932바이트였고, 더 이상 표시하지 않는 이전 공격·대기 조합도 들어갔다. 현재 프레임을 원본 PNG에서 직접 참조하는 두 ScriptableObject 카탈로그와 전용 V2 아틀라스로 연결했다. Addressables·새 패키지·유료 에셋·생성 그림·리샘플링을 도입하지 않았다.

픽셀 해시는 전체 알파와 모든 비투명 RGB를 비교한다. 보이지 않는 알파0의 RGB는 importer의 alpha dilation 영향을 받으므로 제외한다. 별도 PNG/메타 바이트 검사는 투명 RGB까지 포함한 파일 전체가 이동 전과 같음을 확인한다.

- 현재 PNG: `Assets/RuinRunner/ArtSources/Hero/ArmedGripV3` 12,266개, `WeaponStandingV4` 2,083개.
- 이전 저작/검토용 16,502개: `Assets/RuinRunner/ArtSources/Archive/SscaryMaleCombat`. 기존 저작 도구의 논리 경로는 Editor에서 원본으로 연결된다.
- AssetDatabase 폴더 이동 전후30,851개의 PNG/메타 바이트와 GUID를 비교했다. 변경0. 폴더 GUID/메타도 보존했다. `source-inventory.json`, `source-folder-aliases.json`, `migration-result.json`에 이전·현재 위치와 SHA가 있다.
- 기존 PNG는 계속48×64/발 피벗(24,22)/32PPU/FullRect다. 아틀라스는Point/RGBA32/밉맵 없음/4픽셀 패딩/회전·tight packing 없음, built-in 포함이다. 빌드 전 두 카탈로그의 모든 원본 PNG와 실제 Sprite geometry를 확인하고 픽셀 SHA를 생성한다. 메타/아틀라스 YAML을 손으로 편집하지 않았다.
- `IndividualArt`는 기존 논리 ID와 sampler API를 유지한다. 카탈로그는Root당 한 번 로드하고 기존 body/손 시계를 건드리지 않는다. 일반 UI/지형의 Texture 조회는 기존대로 유지한다. 보존된 과거 캡처/manifest의 PNG 경로는 `Tools/Art/art_source_path.py`로 읽으며 기록 자체를 고치지 않는다.

빌드 결과는 2026-10-02T07:33:00.0367534Z~2026-10-02T07:36:13.26028Z, 193.223초, 오류0·경고3다. AV의1,163,897,055바이트에서 AW의523,323,759바이트로 640,573,296바이트/55.04% 감소했다. 실제 DetailedBuildReport에서 원본 native 텍스처 중복0, 보관 버전 객체0을 확인했다. 텍스처는3,011객체/379,555,072바이트다. 소스/카탈로그/원본 목록21SHA는 빌드 후 일치했다. 이전 AV와 기존 빌드는 보존했다. 해시/용량 근거는 `build-source-AW.json`, `build-footprint-AW.json`, `build-AW-summary.json`이며 전체 보고서는 `build-AW.json`이다.

현재 후보 실행 검증:

|검증|현재 결과|범위|
|---|---|---|
|Windows smoke|PID25736, `NativeAtlasAW-smoke-163720`, exit0, 12.773초|기동·카탈로그·충돌·장비·성장·저장 서비스. 새14,349프레임/44,080,128전체 픽셀의 packed/source SHA 일치; 13페이지. 별도 외부1,456PNG/6,938,837픽셀도 일치.|
|실제 GPU native 원본 렌더|PID10524, exit0, 113PNG 페이지|Direct3D11: NVIDIA GeForce RTX 4070 Ti. SpriteRenderer의 실제 atlas UV/alpha/RGB를native1:1로 모두 비교.14,349프레임/44,080,128픽셀 차이0.|
|표시된 Windows UI fixture|PID26060, `NativeAtlasAW-ui-164140`, exit0|1280×720의30PNG 모두 비어 있지 않음. 메뉴·시작HUD·첫 지역 보스 화면을 직접 검토. staged 상태이며 정상 입력/캠페인/전체30화면 수동 평가가 아님.|
|현재 Editor 참조/설정|14,349카탈로그 참조/논리 lookup 일치, 이전 저작 소스 접근 유지|Built-in/V2. `editor-catalog-settings.json`. 중지된 Editor 품질 기본값AA2/anisotropic ForceEnable은 런타임 CameraFollow의AA0/Disabled·MSAA/HDR/동적해상도false와 구분했다. 카메라/기준640×360/PPU를 이번 단위에서 바꾸지 않았다.|

현재 로그는 `Logs/Validation/2026-10-02-native-frame-atlas/windows-smoke-AW-163720`, `windows-gpu-AW-163826`, `windows-ui-AW-164140`이다. GPU 원본 page000/096과 메뉴/시작HUD/첫보스 원본을 직접 열어 보았다. GPU 모든 셀의 수치상 원본 일치를 모션 품질 수동 평가로 바꾸어 보고하지 않는다. 기존 V4의1배속/무기 방향/복귀 평가는 `Docs/WeaponStances-20261002.md`에 별도 기록했다.

실패/제약도 보존한다. `windows-ui-AW-163850`은 프로세스exit0/CAPTURED이지만 숨긴 창의30화면이 모두 검어서 시각 증거에서 제외했다. `capture-content.json` FAIL을 남긴 뒤 표시된 창에서 다시 검증했다. 최초 Editor inspection은 보관 PNG 이름을00000으로 가정했으나 이전 버전은00형식이었다. 검사 경로를 실제 원본 이름으로 고쳐 통과했으며 생산 소스 문제로 세지 않는다. 해당 기록은 `editor-inspection-first-attempt.json`이다. CLI의 최초 inventory 60초 관측timeout 뒤 실제 inventory 완성과 이동 전 전체 목록을 확인하고 진행했다. 관측timeout을 성공/실패로 추정하지 않았다.

smoke의12.773초는 AV의20.490초보다 짧지만, 검증하는 보관 프레임 수와 검증 경로가 달라 순수 로딩 성능 향상치로 쓰지 않는다. 새 카탈로그/아틀라스는 packed pixel 검사를 위해 읽기 가능하며, CPU/GPU 메모리의 실제 반복 런 안정성과1080p 전경 성능은 아직 측정해야 한다. 현재 native 참조의 그림은 줄이지 않았다. 전용 피격·재장전, 부족한 적/보스 모션·환경/크기/맵 마감, 최신 최소세빌드 방향의 정상 캠페인·분량·성능·음향/입력 검증은 계속 미완료다.

주요 변경: `Runtime/Feedback/NativeHeroFrameCatalog.cs`, `IndividualArt.cs`, `Editor/NativeHeroAtlasPrebuild.cs`, `Runtime/Diagnostics/RuntimeSmokeTest.cs`, `NativeHeroAtlasValidation.cs`, 검증 flag만 추가한 `Core/GameBootstrap.cs`. `IPreprocessBuildWithReport`로 카탈로그/현재 atlas를 생성하고 기존 TerrainAtlasPrebuild는 남은 외부/지형을 계속 구성한다. 무료 Sscary 원본의 기존 사용·수정 조건은 `ASSET_SOURCES.md`, `Assets/StreamingAssets/ThirdPartyNotices/Sscary-Adventurer.txt`에 그대로 보존한다.
