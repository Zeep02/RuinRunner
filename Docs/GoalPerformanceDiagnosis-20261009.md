# Windows 첫 정상 원정 성능 실패 — 읽기 전용 진단

원본 `Logs/Validation/2026-10-09-goal/windows-melee-01`은 변경하지 않았다. 성능 합격이 아니다. 게임/Editor 추가 실행, 전원·드라이버·디스플레이 설정 변경, 사용자 프로세스 종료를 하지 않았다.

## 판정

현재 증거는 **게임 로직/셰이더의 지속 부하보다 presentation 대기**를 우선 가리킨다. 정확한 원인은 아직 분리되지 않았다. FrameTiming 계측, Unity D3D11/graphics jobs와 창 표시 경로, 다른 GPU 클라이언트의 영향은 후속 비교가 필요하다. 드라이버 FPS 제한이나 Editor가 원인이라고 확정할 근거는 없다.

원본 전체 전투 표본 5,546프레임/277.837초는 평균 19.961fps다. 세 지역의 일반/보스 여섯 분류가 모두 p50 약 50ms, p95 약 100ms여서 60fps 목표에 실패했다. 초당 한 번의 FrameTiming CSV는 717행이며, 시작 10초 이후 전경/유효 710행 중 전투/비전투가 모두 같은 대기 형태다.

| 초당 샘플 지표 | 전투 p50 / p95(ms) | 비전투 p50 / p95(ms) |
|---|---|---|
| CPU 전체 | 49.999 / 99.313 | 50.000 / 99.380 |
| CPU main 작업 | 0.714 / 1.403 | 0.606 / 1.163 |
| CPU render 작업 | 0.286 / 0.406 | 0.268 / 0.429 |
| Present 대기 | 49.245 / 98.697 | 49.388 / 98.758 |
| GPU | 0.661 / 1.252 | 0.606 / 1.616 |

전투 표본의 Present 비중 중앙값은 98.5%다. 이 표는 드문 CSV 표본이며 전체 프레임 분포의 대체값이 아니다. FrameTiming은 네 프레임 지연되고 GPU 결과가 항상 정확하다고 보장되지 않는다. 따라서 이 자료만으로 실제 표시 횟수나 GPU가 항상 놀았다고 단정하지 않는다. [Unity 6000.6 Frame timing 문서](https://docs.unity3d.com/6000.6/Documentation/Manual/frame-timing-manager.html)

## 실행 환경·코드 확인

- 실패 후보는 `GoalCandidate-20261009-155518`, D3D11, graphics jobs(`gfx-threading-mode=6`), flip model, 창 모드 1920×1080, vSync=1, targetFrameRate=120이다. Desktop에서는 vSync>0이면 targetFrameRate가 무시된다. 따라서 기록된 120은 이 실행의 유효 FPS 제한을 의미하지 않는다. [Unity vSyncCount](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/QualitySettings-vSyncCount.html)
- Win32 전체 모드 조회: **기본 DISPLAY2 1920×1080 240Hz**, 위쪽 보조 DISPLAY1 1920×1080 165Hz. 둘 다 RTX 4070 Ti에 연결, RemoteSession=false. 단일 WMI GPU 레코드의 165Hz는 보조 화면만 나타냈다. 실패 기록에는 창 좌표/실제 선택 모니터가 없으므로 실패 당시 주 모니터를 사용했다고 단정하지 않는다. 50/100ms는 240Hz의 12/24주기와 일치하지만 인과관계 증명은 아니다.
- 전원은 고성능, AC/DC CPU 최소 상태 100%, PCIe ASPM off. 현재 GPU는 NVIDIA 610.47. 현재 전력/클럭은 실패 종료 후 snapshot으로 당시 부하의 증거가 아니다.
- NVIDIA DRS Get API만 호출했다. Global/Base에는 Maximum pre-rendered frames=1이 명시돼 있다. FPS/background FPS/vsync/power 항목은 -160(조회에 해당 설정 없음), RuinRunner.exe 개별 프로필은 -166(없음)이었다. 이를 강제 FPS cap=0 또는 모든 기본값 확인으로 바꾸어 보고하지 않는다. Get/Load/Destroy 외에 Set/Save API를 호출하지 않았다. [NVIDIA DRS](https://docs.nvidia.com/nvapi/group__drsapi.html), [공식 설정 ID](https://github.com/NVIDIA/nvapi/blob/main/NvApiDriverSettings.h)
- Unity 실행 파일 3개 중 2개는 AssetImportWorker/batch worker이며 실제 Editor는 1개다. 같은 화면의 앱이 여러 개라는 사실만으로 Editor contention을 입증하지 않는다. 다른 GPU 사용자 프로그램도 종료하지 않았다.
- `PerformanceRecorder.cs`의 현재 SHA-256 `87A905BB5C8C499DC3CCEC0F1C394235ED8DF5CD712DC78BC38D4D746C6B7ED3`는 첫 후보의 source-manifest와 같다. 이미 `--performance-cpu-clock-only`와 `--performance-no-vsync`가 들어 있어 A/B용 새 빌드는 불필요하다.
- 전체 전투 성능은 매 프레임 `Time.unscaledDeltaTime`에서 구한다. FrameTiming은 매 Update capture, 초당 한 번 read/write이며 메모리/객체 수 조사는 10초마다 하고 다음 2프레임을 제외한다. 비동기 API를 매 프레임 동기 대기하는 계측 코드는 없다.
- `recordingEnabled=false`는 연속 RunEvidenceRecorder 부재만 뜻한다. 자동 원정은 journey/장비 이벤트에서 스크린샷을 여전히 찍고 `CombatPressureTrace`는 피해 발생마다 CSV flush한다. 이것들은 순간 spike 후보지만 비전투에서까지 지속되는 50ms plateau의 원인으로 확인된 것은 아니다.

## 가장 작은 다음 측정

메인 담당자가 현재 Play 검사를 마친 뒤 **같은 첫 후보·같은 seed 2026100901·같은 화면 위치/1920×1080·동일 Editor idle 상태**를 유지한다. 전역 설정과 다른 사용자 앱은 그대로 둔다. 새 validation profile/새 evidence 폴더로 첫 120초만 `Run-NormalWindows.ps1 -MeasurePerformance -CpuClockOnly -PerformanceSeconds 120`을 실행한다. 연속 녹화는 켜지 않는다. 기존 실패와 같은 입력 파일럿/이벤트 capture 조건을 유지하려면 `-CombatPressureTrace`도 동일하게 둔다.

1. 이 비교만 빨라지면 FrameTiming Capture/Get 경로와 결합된 계측 영향이 우선이다. 이후 원래 계측을 켠 짧은 재현으로 일시 환경 변화와 구분한다.
2. CPU-clock-only도 20fps면, 그 조건을 유지하고 **`-NoVsync` 하나만 추가**한 동일 길이 비교를 한다. 120fps 근처로 바뀌면 동기 표시 경로가 핵심이다. 이는 임시 런타임 옵션이며 사용자 설정 파일을 수정하지 않는다.
3. 둘 다 느릴 때만 메인이 작업을 저장한 자신의 Editor를 정상 종료한 상태와 같은 조건을 비교한다. 본 진단에서는 Editor/worker를 종료하지 않았다. 결과가 바뀌어야 Editor 간섭이라고 판단할 수 있다.

`CpuClockOnly`는 이 스크립트의 Capture/Get 호출을 끌 뿐 빌드의 `enableFrameTimingStats=1`을 바꾸지 않는다. 필요하다면 다음 별도 후보에서 그 빌드 설정만 끄거나 외부 Present/ETW 계측으로 교차 확인해야 한다. 지금 이를 DX11 계측 버그라고 보고하지 않는다. 어떤 비교 결과도 정상 캠페인 성능 합격을 대신하지 않는다.

읽기 도구와 새 증거:

- `Tools/Validation/Read-PresentationEnvironment.py`: OS display/power/NVIDIA 설정 snapshot. `Logs/Validation/2026-10-09-goal/performance-diagnosis/environment-20261009-0724.json`.
- `Tools/Validation/Analyze-PresentationTiming.py`: 기존 CSV/전체 성능 JSON만 읽어 요약. 같은 폴더의 `melee-01-analysis.json`.
- 첫 후보 runtime DLL SHA-256: `E3F64CB7D5E723A85C57E229E9C5B0535836B85B80B4FE0FD73F94474716A9FB`.

## CPU-clock-only 비교 결과

메인이 후보 B(`GoalCandidate-20261009-162422`)로 새 profile/seed 2026100902/ranged, 1920×1080 전경, Editor idle 상태에서 120초 진단을 실행했다. `windows-perf-cpuclock-01/performance.json`은 `frameTimingRequested=false`, `frameTimingEnabled=true`, vSync=1, targetFps=120이다. 첫 스테이지 일반 전투 462프레임/23.023초에서 **20.067fps, p50 50.003ms, p95 100.007ms**로 기존 실패 형태가 남았다.

따라서 사용자 스크립트의 `CaptureFrameTimings/GetLatestTimings` 호출을 제거하는 것만으로 지속적인 대기가 해소되지 않았다. CSV의 `timing_available=0`과 0 값은 계측을 생략했다는 뜻이며 CPU/GPU 비용이 0이라는 증거가 아니다. 빌드 자체의 FrameTiming 통계 활성화는 여전히 켜져 있어 engine 계측 전체를 배제한 결과도 아니다.

첫 실패와는 후보/seed/무기군이 달라 엄격한 단일 변수 비교는 아니다. 다음 비교는 이 후보 B/seed/무기군/CPU-clock-only 조건을 유지한 채 `NoVsync`만 추가하도록 메인이 진행 중이다. 원본 실패나 CPU-clock 진단을 성능 합격으로 대체하지 않는다. 분석: `performance-diagnosis/cpuclock-01-analysis.json`.

## NoVsync와 실제 창 활성화 후속 결과

- 동일 B/seed/ranged/120초 CPU-clock-only에 NoVsync만 추가한 `windows-perf-novsync-01`은 450개 전투 프레임 전부 33.3ms를 초과했다. 19.9998fps, p50 50.0013ms, p95 50.1666ms다. vSync=0/target=120에서도 약 20fps 제한이 남았으므로 게임의 vSync 선택만으로 원인을 설명할 수 없다. `novsync-01-analysis.json`에 원본 hash와 분석을 보존했다.
- 이어진 같은 B의 `windows-ranged-02` 실행에서 메인이 **16:34:42+09:00 Sky로 실제 창을 활성화**하고 화면을 확인했다. 같은 프로세스의 활성화 전 9개 sparse 표본은 CPU frame p50 50.0046ms였으나, 활성화 후 22.175–139.184초의 118개 표본은 p50 4.16545ms/p95 4.194ms였다. Present 대기는 49.4444→3.56875ms로 줄었다. Unity의 focused 값은 전후 모두 1이었다.
- `ranged-02-activation-early.json`은 실행 중 CSV snapshot의 분석이며 전체 캠페인 성능 합격이 아니다. launch wall clock과 Unity 시간의 startup 차이를 고려해 활성화 경계 ±5초를 제외했다. 실제 Sky 화면/시각 증거는 메인 담당이다. 이 결과는 창 활성화/표시 분류에 따른 제한을 강하게 가리키지만 특정 NVIDIA 설정·프로그램·드라이버 버그를 확정하지 않는다.
- 전체 경로의 후보 exe로 NVIDIA application V4 조회까지 확장해도 개별 profile은 없었다. Global/Base enumerate는 3개 설정이고, 관련 명시값은 preferred refresh=1과 pre-rendered frames=1뿐이었다. FPS/배경 FPS/BatteryBoost/QuietMode 설정은 조회되지 않았다. `environment-expanded-20261009-0737.json`은 Get/Enum/Load만 사용한 당시 snapshot이다. RTSS/RivaTuner/Afterburner/GameBar라는 프로세스는 없었고 NVIDIA Overlay는 있었지만, 존재만으로 인과관계를 판단하지 않는다. 전역 설정/사용자 프로세스는 변경하지 않았다.

## 다음 후보의 계측 수정

`Runtime/Diagnostics/PerformanceRecorder.cs`와 동일 bytes의 `Tools/Validation/PerformanceRecorder20261009.staged.cs`를 작성했다. 기존 파일은 `Backups/2026-10-09-goal/backend/before-foreground-profiler`에 보존했다.

| schema 2 출력 | 의미 |
|---|---|
| `measurements` | 기존 Unity focus 기준의 실제 unscaled 전투 표본·percentile·제외 규칙을 그대로 유지한다. |
| `foregroundMeasurements` | 같은 전투 표본을 Windows foreground window가 현재 게임 프로세스 소유인지로 분류한다. 알 수 없으면 unknown이며 Unity focus로 대신하지 않는다. |
| `phaseMeasurements` | 메뉴·마을·전투·지도·가방·패널·보상·결과·전환 등 모든 Update 프레임을 기록한다. state/stage 경계와 생성 stall, 계측 overhead도 포함한다. steady combat와 별도 해석한다. |
| `frame-timing.csv` | 기존 숫자 열을 보존하고 native foreground/availability/fullscreen/phase code를 추가했다. |
| `memory.csv` | 10초마다 Texture 수/Unity native bytes/가용 flag/표본 비용(ms)을 추가했다. 결과 이후 마을·재출정·정리까지 계속 기록한다. |

foreground 조회는 `GetForegroundWindow`와 `GetWindowThreadProcessId`로 얻은 소유 PID를 자기 PID와 비교한 bool만 기록한다. 다른 PID·창 이름·프로세스 이름은 저장하지 않는다. 조회는 창을 활성화하거나 조작하지 않는다. [Microsoft foreground API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow), [window ownership API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid)

메모리 sampling은 GC를 강제하지 않는다. texture native bytes는 Unity Profiler가 제공하는 Unity object native-memory 합계이며 VRAM 사용량의 독립 측정이 아니다. Profiler 미지원으로 모두 0이면 availability=0을 기록한다. 표본 비용도 기록하여 overhead를 확인한다. [Unity native memory API](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Profiling.Profiler.GetRuntimeMemorySizeLong.html)

`Run-NormalWindows.ps1 -Fullscreen`은 1920×1080 창 테두리/제목줄 때문에 하단이 잘리는 비교를 피하기 위한 명시적 옵션이다. 기본은 기존 windowed이며, 실제 focus 확인은 메인의 승인된 UI 도구를 사용한다. launcher는 전역 화면/드라이버 설정을 수정하지 않는다. `Analyze-PresentationTiming.py`는 구형 CSV와 새 native focus 분류를 모두 읽는다.

전체 Runtime 오프라인 Release 컴파일 오류 0/기존 obsolete API 경고 4, launcher PowerShell parse 오류 0, 실제/스테이징 bytes 동일, Unity-focus/native-foreground가 불일치하는 합성 CSV 분석 6개 확인을 마쳤다. `performance-diagnosis/recorder-schema-preflight/preflight.json`. 합성·정적 확인이며 새 recorder의 실제 Windows foreground/texture/결과 이후 기록은 다음 후보에서 검증해야 한다.

## ranged-02 종료 후 성능 근거

`windows-ranged-02`는 EXIT 0, 정상 서버 여정 PASS로 종료됐다. 기존 후보 B recorder의 전투 표본은 47,490프레임/198.411초, 전체 가중 평균 239.352fps다. 세 지역 일반/보스 6개 분류 p50은 모두 약 4.167ms, 최대 p95는 4.17101ms였다. 33.3ms 초과는 8개, 최악은 58.341ms다. 실제 창 활성화 이후 sparse CPU timing 513개는 p50 4.1671ms/p95 4.2017ms다. `ranged-02-complete-analysis.json`에 원본 hash와 통계를 보존했다.

이는 이 실행의 **전투 표본이 60fps 기준을 충족한 근거**다. 자동 focus 값이 실제 OS 전경을 구분하지 못했던 초기 구간, 메인이 16:35:42 Alt+Return으로 전체 화면을 전환한 사실, 일반 전환/메뉴/지도/가방 표본 제외를 함께 기록한다. 기존 실패와 NoVsync 실패를 삭제하거나 전체 게임 성능 합격으로 덮어쓰지 않는다. 새 schema 2 recorder는 이 후보에 들어 있지 않았다.

기존 memory.csv는 54개 표본이며 0.067초의 Unity allocated 182,648,832 bytes/오브젝트 8에서 530.176초의 984,482,195 bytes/오브젝트 14,766으로 끝난다. 첫 표본은 콘텐츠 로드 전이라 단순 차이를 누수라고 단정할 수 없다. 반대로 recorder가 첫 game over에서 멈춰 이후 마을·재출정·정리 뒤 회수 상태를 기록하지 못했으므로 안정성 합격 근거도 아니다. 추가한 recorder에서 결과 이후 texture/native bytes와 구간별 시간을 검증해야 한다.
