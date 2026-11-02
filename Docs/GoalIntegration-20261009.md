# 2026-10-09 통합 작업 기록

목표 원문: `C:/Users/Hyeon/.codex/attachments/7ec46eda-1560-4ca9-970f-d49d351b15ae/goal-objective.md`.
전체 게임 완료 판정은 아직 미완료다. 기존 게임/서버/생성기/장비를 보존하며 잔여 연결과 품질을 마감한다.

## 현재 판정과 최신 근거

현재 목표의 작업 제한은 후속 원문 `C:/Users/Hyeon/.codex/attachments/ca8265b1-5096-4eea-abdc-2ef21746fd05/goal-objective.md`를 따른다. 아래 이전 기록의 당시 잔여 항목은 최신 근거로 대체하며, 실패 자료는 보존한다.

- 근접·원거리 두 정상 완주에 후보 I의 기동 캠페인과 특수행동·마나 캠페인이 추가됐다. 기동 경로는 `windows-mobility-07-victory-resume/campaign-chain.json`처럼 실제 원정과 재실행 복구를 합친 한 캠페인이며, 복구 구간 자체를 추가 완주로 세지 않는다. `windows-skill-06/server-journey.json`은 세 보스·승리 정산·귀환·재출정·중단 정리를 통과했으나, 종료 요청 뒤 프로세스가 남아 `shutdown-hang.json`으로 기록하고 해당 격리 프로세스만 정리했다. `exit.json`의 -1을 정상 종료 합격으로 바꾸지 않는다.
- 승리 정산의 서버 확정 응답 유실 직후 실제 Windows 앱을 종료·재실행했다. `windows-mobility-07-victory-stop/crash-ticket.json`과 `windows-mobility-07-victory-resume/recovery-report.json`은 동일 미확정 operationId의 재전송, 정산 1회 반영, 정상 마을 재진입을 확인한다. 전투 중간 상태 복원은 구현 범위가 아니다.
- `windows-skill-06/performance.json`은 실제 OS 전경 1080p의 전투와 보스전을 포함한다. 최초 마을 진입과 지역 전환·지도·인벤토리의 긴 프레임도 제외하지 않았다. 진단용 PNG 저장 비용이 포함되어 있으므로 전체 구간이 항상 60fps라고 주장하지 않는다.
- 최신 후보 K는 `Builds/GoalCandidate-20261009-213303`, API/DB 포함 패키지는 `Deliverables/RuinRunner-Candidate-20261009-213303`이다. 실제 빌드 결과는 해당 `build-result.json`, 패키지의 냉간 시작·재시작·격리·파일 불변 결과는 `Backend/Validation/local-release-20261009-123834.json`을 참조한다. 패키지 검사는 Windows 게임을 실행하지 않은 API/DB 검사이며 정상 캠페인과 구분한다. 연속 모션 시청·오디오 청취는 아직 답변을 기다린다.

후속 J의 `windows-adversity-j-01/adversity.json`은 정상 패배·재도전·보상 보류/선택·자연 획득으로 찬 가방의 추가 획득 거부·미수령 장비를 두고 출발·승리·귀환·중단 정리를 통과했고 `exit.json`은 0이다. 다만 가방 검사 중 새 레벨 보상창을 자동 도구가 놓쳐 실제 카드를 한 번 클릭했으며, `native-ui-events.json`에 보조 입력과 범위를 기록했다. 완전 무인 검증으로 주장하지 않는다. K에서는 정상 보상창을 먼저 처리하도록 도구를 수정했고, `windows-adversity-k-01/adversity.json`의 모든 조건이 수동 보상 선택 없이 통과했다. 같은 실행의 `server-journey.json`은 세 보스·엔딩·승리 정산·귀환·재출정·중단 원정 정리를 확인하고, `exit.json`은 0이다. 짧아진 안내는 해당 `adversity-09.png`에서 실제 줄바꿈·가림 없이 확인했다. 네 빌드 성향의 이전 증거를 대체하거나 추가 성향으로 세지 않는다.

`windows-startup-j-manual`과 `windows-startup-k-manual`은 자동 파일럿·자동 PNG 저장 없이 새 프로필의 실제 메뉴/슬롯 버튼으로 첫 마을에 들어간 비교이며 둘 다 정상 종료했다. J의 1초 이상 프레임은 검증용 PNG만의 문제가 아니었다. K는 Unity 객체 API를 호출하지 않는 숫자 자세표 초기화만 작업 스레드로 옮겼고, 가장 긴 메뉴 프레임은 약 1396ms에서 121ms로 줄었다. 원본 데이터·PNG·GUID와 주 스레드의 Unity 스프라이트 처리는 보존한다. 전체 프레임·범위는 `startup-j-k-comparison.json`, 생성 데이터 불변은 `numeric-table-source-preserved-2134.json`을 참조한다. 순간 지연이 남으므로 모든 프레임의 60fps를 주장하지 않는다. 재부팅·OS 캐시 제거·두 번째 마을 입장·전투 검사로 세지 않는다.

K의 `windows-adversity-k-01/performance.json`은 실제 OS 전경 1080p의 세 지역 일반 전투·보스전에서 p95 약 4.17ms를 기록했다. 모든 구간을 보존한 `phaseMeasurements`에는 지도·인벤토리·지역 전환의 긴 프레임도 있으며 자동 PNG 저장을 포함한다. 이 캠페인의 짧은 귀환 구간은 메모리 주기 사이에 지나가 Town 표본이 하나뿐이다.

따라서 같은 K를 자동 파일럿 없이 다시 실행하여 실제 두 번의 출정·안전 입구에서 메뉴 복귀·같은 서버 슬롯의 마을 재입장·입구에서 명시적 중단 정리를 확인했다. `windows-memory-k-manual/native-ui-events.json`, `memory-comparison.json`, `exit.json`을 참조한다. 전경 1080p에서 마을 객체 수는 각 귀환마다 603개로 돌아왔고 첫 귀환 이후 마지막 표본까지 텍스처 native bytes 증가는 141696B, managed 증가는 716800B였다. 새 시드가 일부 추가 아트를 사용하므로 텍스처 수는 정확히 같지 않다. 마지막 패널 닫기 뒤 타이머 종료까지 새 CSV 표본은 없으며 실제 마을 화면만 확인했다. 서비스 패널 표본은 원래 phase 9로 보존하고 Town으로 재분류하지 않았다. 같은 프로세스의 짧은 두 반복에서 큰 누적은 관찰되지 않았으며 무기한 무누수로 주장하지 않는다. 종료 코드는 0이다. 앞선 네 번의 수동 반복인 `windows-skill-05-manual-town/memory-comparison.json`은 이전 후보 E의 별도 근거로 유지한다. K는 원본 숫자표의 초기화 위치와 준비 완료 캐시만 바꿨으며 정리·월드·이미지 데이터는 바꾸지 않았다.

`package-ui-k-01/result.json`은 배포 폴더의 원래 `Start-Game.ps1`로 포함된 API/PostgreSQL과 실제 Windows 게임을 함께 시작한 별도 검사다. 새 격리 DB·프로필로 실제 시작/슬롯 버튼을 클릭해 마을에 들어가고, 메뉴 복귀 후 같은 프로필로 마을에 재입장했다. 실제 나가기 버튼으로 종료 코드 0을 확인한 뒤 해당 격리 백엔드만 정상 종료했다. UI 이미지는 네이티브 도구에서 직접 확인했으며 payload를 파일로 저장하지 않았다. 이 검사에 전투·경제·청취·프레임 계측은 없다. 전체 패키지 해시 검사는 앞의 냉간 패키지 검사와 별개이며, 그 이후 배포 폴더를 수정하지 않았다.

후보 I의 종료 지연 이후 J 정상 여정, J/K 수동 첫 입장, K 정상 여정과 패키지 수동 실행은 모두 정상 종료했다. 이전 종료 지연의 원인을 확정하거나 모든 과거 오류가 없었다고 주장하지 않는다. 최신 C# 작업본은 K의 `source-manifest.json`과 모두 일치한다.

이 문서의 검증 경로는 별도 표시가 없으면 `Logs/Validation/2026-10-09-goal/` 아래다. 시청·청취 답변을 기다리는 상태를 기능 검사나 캡처 성공으로 대신하지 않는다.

### 남은 시청·청취 자료

다음 영상은 원본 캡처 시각을 보존한 실제 1배속 자료다. 프레임 복제·보간·배속으로 부드러움을 만들지 않았다. 현재 도구는 연속 영상 시청과 오디오 청취를 지원하지 않으므로 몸·손·무기·효과의 연결 및 소리 끊김·중첩에 대한 검수 답변을 기다린다.

| 대상 | 실제 시간 영상 |
| --- | --- |
| 주인공 산탄총 피격 | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-goal/hero-hurt/motion-194633/vfr-review/bastion_shotgun-d2-standing-actual-time.mp4) |
| 주인공 카타나 방어·피격 | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-goal/hero-hurt/motion-194633/vfr-review/priority-guard-training_katana-actual-time.mp4) |
| Warden 피격 | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-warden-motion/194713/vfr-review/hurt-actual-time.mp4) |
| Tidekeeper 등장 | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-tide-motion/194746/vfr-review/entrance-actual-time.mp4) |
| Seal Heart Sweep | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-sealcore-motion/194844/vfr-review/SealSweep-actual-time.mp4) |
| Seal Heart Volley | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-sealcore-motion/194844/vfr-review/SealVolley-actual-time.mp4) |
| Seal Heart 피격 | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-sealcore-motion/194844/vfr-review/hurt-actual-time.mp4) |
| Seal Heart 전용 사망 | [영상](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-sealcore-motion/194844/vfr-review/death-actual-time.mp4) |

![실제 게임 출력 녹음](F:/codex/LoglikeCap/Logs/Validation/2026-10-09-goal/audio-feedback-183347/unity-output.wav)

## 이번에 반영한 통합

- 서버 프로필 초기화의 2단계 UI/Controller 연결. 로컬 이관 원본은 삭제하지 않는다. 서버가 확인한 receipt로만 새 상태를 표시한다.
- 결과 화면은 정산 미확정/서버 확정을 구분하며, 확정 보유 유산은 갱신된 서버 프로필을 반영한다.
- session epoch + profile/run identity로 마을 설정/출정/정산/초기 장비 비동기 콜백 범위를 확인하고, 파괴된 Controller의 이벤트를 해제한다.
- 슬롯의 `유산으로 새 런`을 실제 동작인 `마을로 입장`으로 바꿨다. 처리 중 뒤로 이동해 reset 완료 UI가 남는 문제를 독립 검토 후 막았다.
- 입구/통로/전투/시설별 목표는 DungeonMap의 실제 상태 API에 연결했다. 중앙의 큰 조작키 배너는 짧은 상단 안내로 바꿨다.
- 재능 화면은 12개 빽빽한 문단 대신 계열 탭과 3개 카드로 정리 중이다. 비용/선택/조건은 그대로 표시한다.
- 자동 플레이의 기본 시작을 legacyFixture에서 정상 서버 메뉴/마을 경로로 이행 중이다. 레거시는 명시적 `--legacy-combat-fixture`에서만 사용하며 정상 캠페인으로 기록하지 않는다. 새 pilot은 GameAction 이동/공격/상호작용/획득과 실제 UI 버튼 handler를 사용한다. 버튼 handler 호출은 수동 UI 입력 검증이 아님을 JSON에 명시한다.

## 실제 확인된 범위

- Unity 6000.6.0f1 / Demo.unity / Pipeline ready / Built-in pipeline을 라이브 CLI로 확인. 최초 통합 recompile 성공.
- 사용자 DB와 분리된 API `http://127.0.0.1:5196/`, PostgreSQL 55439. 실행: `Tools/Backend/Start-IsolatedBackend.ps1 -Instance goal-20261009`.
- `Logs/Validation/2026-10-09-goal/session-153329-e49bf2/result.json`: 실제 Controller/API 22개 검사, 새 테스트 프로필 초기화/이관 재적용 차단/정산/귀환 확인. 1280×720, 1920×1080, 1600×1000 캡처. 원본 사용자 저장 파일 불변. 위치·패널·사망은 공급하므로 정상 보행/캠페인 증거가 아니다.
- 이 캡처의 `1920x1080-expedition-start.png`와 `1920x1080-returned-town.png` 파일명은 추가 해상도 반복 후여서 실제 1600×1000이다. result.json 실제 width/height가 기준이며 다음 실행부터 이름을 정정한다.
- 직접 이미지 검토: reset/정산 UI 읽기 가능, 720p 도착 시 NPC HUD 가림과 재능 과밀 확인 → 압축 마을/새 카드 UI 수정 중. 이전 캡처를 수정 후 합격 근거로 재사용하지 않는다.
- `Logs/Validation/2026-10-09-combat/153812/result.txt`: 공급 전투 fixture PASS. 앞선 153427/153627 실패도 보존. 물 보스 4행동×2국면/일시정지/실제 준비·발동 프레임, 돌진 범위·벽 차단, 신규 4무기 손 연결, tether/bastion 8방향 실제 발사/총구/재장전. 정상 캠페인·청취·부드러움·성능은 별도.

## 후속 통합 확인

- `session-155033-59a43f/result.json`: 실제 Controller/API 24개 검사 통과, 사용자 저장 불변. 새 마을/재능 카드를 3해상도로 캡처했다. 직접 검토에서 NPC가 도착 화면 안으로 들어오고 재능 비용/조건/선택이 카드 안에 표시됨을 확인했다. 마을 작업장과 수련 공간의 소품 구분은 추가 마감 중이다.
- 골드 영수증도 매번 `EquipWeapon`을 호출해 공격/설치물을 취소하고 무기 전환 성장을 발생시키던 문제를 수정했다. 실제 장비 참조가 바뀔 때만 갱신한다. 공급 영수증 검사에서 공격 취소 sequence 불변과 회복 1회 적용을 확인했다.
- 서버가 확정한 영수증은 로컬 반영 전 `confirmed-unprojected`로 보존한다. 콜백 실패 후 HTTP 재결제 없이 같은 영수증을 재생하며, 로컬 회복은 시각효과 전에 중복 방지 표식을 남긴다.
- `Backend/Validation/unity-catalog-recovery-20261009-065209-439e44.json`: 실제 Unity + 격리 API 23개 검사 PASS. v1 소유 장비/세트 고정, v2 숫자/설명/마나방벽 동작, 콜백 실패 뒤 아이템·회복 중복 방지. 공급 상태 검사이며 정상 획득 증거가 아니다.
- `world/native-placement.txt`: 실제 장식 308개/30샘플/1292개 조건 PASS. `world/native-objectives.txt`: 상태별 안내 16개 PASS.
- `world/town-walk-065240.txt` 실패로 NPC 창 닫기 Esc가 같은 프레임에 일시정지까지 켜는 버그를 발견했다. 닫기 프레임 입력 소비 후 `world/town-walk-065428.txt`에서 세 NPC, 훈련장 접근, 도착점 복귀를 실제 GameAction 보행으로 통과했다. 공급 위치 없이 걷지만 정상 캠페인 완주는 아니다.
- 자동 파일럿은 단계 전환을 출정 실패로 보던 분기를 고쳤다. `Builds/GoalCandidate-20261009-155518`에 현재 기능의 첫 Windows 검사 후보를 새로 빌드한다. 최종 채택 후보가 아니며 `build-result.json` 실제 상태를 기준으로 삼는다.

## 작업 경계

- 메인만 Unity Play/씬/import/build를 조작한다. 서브에이전트 소유권과 세부 기록은 `GoalBackend-20261009.md`, `GoalCombat-20261009.md`, `GoalWorld-20261009.md`.
- Git 저장소 없음. 수정 전 파일은 `Backups/2026-10-09-goal/{main,backend,combat,world}`에 보존.
- 기존 설치 CLI beta6는 caller/skill 표식을 지원하지 않았다. 공식 beta13 실행 파일 `.work/unity-goal.exe`를 작업 전용으로 사용한다. 기존 전역 CLI/Editor 버전은 교체하지 않았다.
- 새 최종 Windows 후보, 정상 캠페인 4회, 보스/주인공 최종 모션, 음향 청취, 최종 성능/누적 메모리 검증은 아직 미완료다. catalog-v2는 격리 검증 후 별도로 출판했으며 진행 중 원정 catalog-v1 고정은 보존한다.

## 첫 실제 Windows 원정과 후속 후보

- `Builds/GoalCandidate-20261009-155518/RuinRunner.exe`: 새 Windows 후보 A 빌드 성공(174.664초, 0 build errors/6 warnings). Editor UDS의 `Failed to free block`/evict 진단은 별도로 기록되어 있으며 build summary에 포함되지 않았으므로 Console 오류 0으로 보고하지 않는다.
- `Logs/Validation/2026-10-09-goal/windows-melee-01/server-journey.json`: 격리 새 서버 프로필, 실제 GameAction 이동·전투·획득으로 세 스테이지/세 보스/결말 → 서버 정산 유산58 → 마을 → 재출정 → 메뉴 중단 → 명시 정리까지 성공. 무적/장비·재화 공급/텔레포트/시간 배속 없음. 메뉴/시설 선택은 실제 public UI handler 호출이므로 `manualUiInput=false`다. 순수 OS 입력 캠페인으로 바꾸어 부르지 않는다.
- 이 원정의 구매 요청2/판매 요청2/형상2/서약 수락1, 각성0/교환0이다. 확정 거래의 별도 집계는 다음 후보에 추가했으며 자연 각성/교환 완료 조건은 아직 충족하지 못했다.
- `performance.json`: 1080p 전경 실제 프레임은 모든 전투 구간 평균 약20fps, p50 약50ms/p95 약100ms로 **성능 미달**. CPU main/GPU 약1ms에 비해 present wait가 지배한다. 기록 영상은 꺼져 있었지만 동시 Editor 작업이 있어 별도 비교 검사를 진행한다. 60fps 합격으로 해석하지 않는다.
- 서약은 현재 사용하는 사본 우선으로 받고 완료·각성 공격까지 사용하도록 pilot의 선택을 수정했다. 기동 빌드는 단검/카타나와 측면 대시, 교환 재료는 일반 선택·구매에서 조합을 우선하고 준비된 발견 시설로 실제 돌아가도록 한다. 게임의 획득·전투 조건을 완화하거나 장비를 지급하지 않는다.
- 첫 원정의 보스 로그는 Tide의 Fan/Surge와 Seal의 Sweep/Volley를 잘못된 이름으로 뭉쳤다. 실제 선택 편향의 근거로 사용하지 않는다. observer를 `CurrentBossAction`으로 수정하고 AI는 그대로 보존했다.
- `session-162246-949916/result.json`: 최신 소품/포석 마을에서 실제 Controller/API 24조건, 사용자 원본 저장 불변. 3해상도 캡처이며 공급 패널/위치/사망은 정상 플레이 증거가 아니다. 720p 도착과 1600×1000 재능 카드를 직접 검토했다.
- `world/town-walk-072332.txt`: 최신 마을 세 NPC 상호작용/훈련장/도착점 왕복을 정상 GameAction으로 통과했다.
- `Logs/Validation/2026-10-09-sealcore/161315/result.txt`: 새 SealCoreV4의60 PNG/8사망·피격·등장 포즈, 4공격×2국면/피해 밴드/사망 정리1회 공급 기능 검사 PASS. 1280×720 직접 검토 후 교차 흰 먼지의 색/알파를 낮췄으므로 해당 효과의 후속 확인이 필요하다.
- `Logs/Validation/2026-10-09-sealcore-motion/161816`: 실제1280×720/시간배속1/비동기GPU 연속 캡처 평균58.61~59.76fps. 네 클립은 p95 프레임 간격30ms 기준을 넘었고 `LOW_RATE_NO_SMOOTHNESS_PASS`를 유지한다. VFR 영상은 원본 타임스탬프를 보존하고 보간/60fps 변환을 하지 않았다. 캡처/인코딩은 모션·청취 최종 합격이 아니다.
- `Builds/GoalCandidate-20261009-162422`: 후보 B 빌드 성공(194.747초, 0 build errors/6 warnings). 최신 Seal/마을/통합/pilot을 포함하며 Hero hurt/Warden 후속 교체는 포함하지 않는다.

## 두 번째 실제 원정과 다음 통합

- `windows-ranged-02/server-journey.json`: 새 격리 프로필/seed2026100903, 세 보스·엔딩·서버 유산55·마을·재출정·중단 원정 정리, EXIT0. 공급 상태/무적/배속 없음. 구매2/판매2/변형2/서약 각성 및 사용2/자연 재료 교환1을 실제 확인했다. 이동·전투·획득은 GameAction 입력, 메뉴와 서비스 선택은 실제 공개 버튼 handler이며 수동 UI 클릭으로 표시하지 않는다.
- `Backend/Validation/FaultProxy/windows-ranged-02-20261009-073424-c1823f/state.json`: 첫 구매와 승리 정산의 서버200 응답을 각각1회 유실. 각각 동일operationId로2요청/200을 받아 중복 없이 반영했다. 이 증거는 같은 프로세스 안의 복구이며, 앱 종료/재실행 검증을 대신하지 않는다.
- 후보 B를 실제 Windows 전경으로 활성화한 뒤 전투는 평균237.6~240fps, p95 약4.171ms. 최악58.34ms 프레임도 보존했다. 이전20fps는 Unity의 `isFocused`만으로 전경으로 분류했으므로 실제 OS 전경 증거가 부족했다. 같은 실행의 활성화 전후 비교와 NoVsync 비교는 `GoalPerformanceDiagnosis-20261009.md`. 새 Recorder는 OS 전경·지도·메뉴·생성 전환·귀환 후 메모리를 별도 계측하며 다음 후보에서 검증한다.
- `hero-hurt-native/v5/imported.json`: 8064키/5906개별PNG를 배치 import한 뒤 픽셀·피벗·원본hash 보존 PASS. 첫 느린106개-prefix와 취소 결과를 보존했다. 기존 NativeHero 카탈로그/atlas 연결과 실제 플레이 모션 검사는 아직 남아 있다.
- `Warden-20261009/unity-import-v3.json`:72개별PNG/72GUID import 확인. CLI의30초 응답 timeout 후 Editor 작업은 정상 완료했으므로 중복실행하지 않았다. 기존Warden 원본은 보존했다.
- `plinth-finish/v2-stone-only/imported.json`: 기존Cainos포석의6잔디색만 투명화한32PNG를 원본돌RGB 그대로 가져왔다. 최초검사실패는 Sprite.texture가2048px 미리보기atlas를 가리킨데서 발생했다. 실제Texture2D asset16px 검사로 바로잡았고 실패기록을 보존했다. 충돌/시드/보행영역은 변경하지 않았다.
- 상점 카드에 실제 효과 요약과 상세 패널을 추가했다. 고유 무기의 설명을 구형family/+n 수치에서 실제WeaponActionCatalog로 연결했다. 정상 실패·재도전·보류보상·가방 가득/미수령장비 정리 및 지도/인벤토리 입력을 위한 별도 자동여정을 준비했다. 최신Unity/Windows 검증 전에는 완료로 세지 않는다.

### 후속 통합 확인

교환식의 편집 원본은 `Backend/Content/catalog.*.json`이다. `ExchangeCatalogBinding.cs`는 전달된 원정 고정 카탈로그를 검증하여 변경 불가능한 사본을 만들며, `GameController.FieldServices.cs`와 상점·교환 UI 및 정상 여정 도구가 같은 사본을 참조한다. 결과 아이템은 교환식 식별자 대신 실제 `outputId`로 찾는다. 공개 카탈로그 파일은 수정하지 않았다. 서버/클라이언트 전달 일치·버전 사본 보존·잘못된 재료 거부 결과는 `Logs/Validation/2026-10-09-goal/exchange-catalog-173227.json`과 `Tools/Validation/ExchangeCatalogTests`를 참조한다.

NativeHero 카탈로그 생성은 `ArtDirection/Review-20261009/hero-hurt-native/v5/catalog.json`에서 완료 확인했다. 최신 보스 연결 검사는 `Logs/Validation/2026-10-09-boss-art/173619/result.txt`에서 PASS이나 공급 배우·강제 행동 검사이며 정상 완주나 모션 합격이 아니다. 피격 검사의 `hero-hurt/functional-173246`와 `smoke-173459`는 낮은 실제 프레임률로 여러 자세를 검증하지 못한 FAIL을 그대로 보존한다. Editor의 UDS 메모리 오류도 로그에 있으며 프로젝트 예외와 구분해야 한다.

`session-173749-f0aaee/result.json`은 실제 Controller/API와 세 해상도 UI를 캡처했고 원래 사용자 저장 및 Editor 상태 복원을 확인했다. 공급 위치·패널·사망을 사용했으므로 정상 캠페인 증거로 세지 않는다. 직접 확인한 상점 요약·무기 상세·재능·인벤토리·지도·결과 화면에서는 잘림과 버튼 겹침이 보이지 않았다. 신규 Windows 후보는 `Builds/GoalCandidate-20261009-174003/build-result.json`에서 상태를 확인하며, 이 기록만으로 최종 게임 합격을 주장하지 않는다.

### 후속 실제 모션·복구 검증

NativeHero atlas의 첫 사용 지연을 캐릭터 준비 때 해소했다. `hero-hurt/smoke-181621`은 실제 시간의 피격 연속 캡처를 완료했고, `functional-181707`의 모든 피격 사례는 통과했지만 짧은 방어 캡처는 실패했다. 방어의 실제 회복 구간까지 담은 `priority-182125`의 관련 검사는 통과했다. 원본 프레임 시각을 보존한 VFR 영상과 native 자세 검토 이미지를 각각 해당 폴더에서 참조한다. Warden `2026-10-09-warden-motion/182408`과 Tide `2026-10-09-tide-motion/182529`도 실제 시간 연속 캡처를 완료했다. 낮은 프레임 간격 품질 표시는 그대로 보존하며, 캡처 완료를 최종 모션 합격으로 바꾸지 않는다.

`windows-recovery-buy-c3-stop`과 `windows-recovery-buy-c3-resume`은 실제 Windows 구매의 확정 응답 유실 후 앱을 종료·재실행하고 같은 요청을 복구했다. `recovery-report.json`과 proxy 증거를 참조한다. 승리 정산의 종료·재실행은 아직 별도 검증이 필요하다. `windows-skill-03-settle-stop`과 `windows-skill-04-victory-stop`은 정상 패배이며 완주로 세지 않는다. 완주 횟수는 여전히 앞서 기록한 두 캠페인이다.

`2026-10-01-campaign-music/editor-183244/result.txt`은 새 Play 세션에서 음악 전환·재설정 검사를 통과했다. `audio-feedback-183347/unity-output.wav`는 실제 출력 녹음이며, 앞선 fixture가 음악 재생을 멈춘 녹음과 구분한다. 현재 도구는 오디오 입력을 지원하지 않아 청취 판단은 사용자 검수 답변을 기다린다.

후보 D는 `Builds/GoalCandidate-20261009-182823`이며 해당 API/DB 포함 패키지는 `Deliverables/RuinRunner-Candidate-20261009-182823`이다. 냉간 시작·재시작·격리·패키지 불변 검사는 `Backend/Validation/local-release-20261009-093305.json`을 참조한다. 후보 E에서는 정상 입력 도구가 현재 표적 외의 화면에 보이는 공격 예고에도 회피하고, 빌드 성향과 정상 시작 유산 예산을 함께 고려하게 했다. 게임의 적·피해·보상·자원 규칙은 이 변경에 포함되지 않는다. E는 빌드와 후속 플레이 결과 확인 전까지 최종 후보가 아니다.

후보 E의 `windows-skill-05-victory-stop`은 새 프로필의 정상 성장으로 세 보스를 쓰러뜨리고 서버 승리 정산까지 도달했다. 후반 두 보스의 패턴·국면과 서약 각성 후 실제 사용은 같은 폴더의 전투 기록을 참조한다. 그러나 정산 재전송이 종료보다 먼저 확정되어 복구 티켓 발급은 실패했다. 이 사례를 미확정 정산의 앱 재실행 복구 합격으로 세지 않는다.

`windows-skill-05-manual-town`은 같은 승리 프로필의 실제 UI 재입장, 네 번의 출정·메뉴 복귀·중단 원정 정리와 다섯 번째 출정을 확인했다. 자동 파일럿은 사용하지 않았다. `manual-events.json`, `memory-comparison.json`, `performance.json`에 범위와 한계를 기록했다. 마을 객체 수는 귀환 때 복원됐고 첫 귀환 뒤 텍스처 메모리는 안정된 범위에 있었으나 자원 캐시·관리 힙 증가는 그대로 기록한다. 전환 지연과 긴 단발 프레임을 포함한 자료이며 전투 성능 자료는 아니다.

빈 슬롯의 입장 버튼 문구를 줄여 줄바꿈을 수정했다. `session-192736-95e156`의 세 해상도 원본 PNG에서 직접 확인했고 사용자 저장과 Editor 상태는 복원했다. 이 UI 변경까지 포함한 후보 F의 빌드 상태는 `Builds/GoalCandidate-20261009-192759/build-result.json`을 참조한다. 후속 캠페인·정산 복구·패키지 검증 전에는 최종 후보로 채택하지 않는다.

`windows-mobility-04-victory-stop`은 정상 패배·정산·귀환·재출정·중단 정리까지 확인한 실패 원정이며 완주에 포함하지 않는다. 파일럿의 근접 공격과 대시 입력만 조정했고, 기동 준비의 정상 예산을 방향 전환 대시 재능에 사용하도록 했다. 게임의 전투·자원·성장 조건은 변경하지 않았다. 후보 G의 자동 슬롯 진입 실패는 `windows-mobility-05-victory-stop`에 보존한다. 같은 실행 파일·프로필의 실제 슬롯 버튼으로는 정상 마을에 들어갔으며 `windows-mobility-05-manual-entry/native-entry.json`에 범위를 기록했다. 파일럿은 연결 작업이 끝난 뒤 같은 슬롯 버튼을 제한 횟수만 재시도하게 보완했다.

이전 LOW_RATE 구간만 새로 캡처했다. 주인공 `hero-hurt/motion-194633`, Warden `2026-10-09-warden-motion/194713`, Tide `2026-10-09-tide-motion/194746`, Seal `2026-10-09-sealcore-motion/194844`의 원본 시간·캡처 간격과 VFR 재생 시각이 일치했다. 준비 대기는 게임 시간과 실제 시간을 모두 확인하며 시간 배속을 바꾸지 않는다. 앞선 짧은 방어 클립의 시간 불일치와 다른 fixture의 성급한 동시 호출 거절은 실패 자료로 남긴다. 시청·청취 판정은 캡처·인코딩 합격과 별개다.

`GoalEditor-20261009-1935.log`에는 Editor 종료 시 JobTempAlloc과 메모리 경고가 있다. 실제 Windows 게임의 런타임 오류로 바꾸어 설명하거나 오류 없음으로 숨기지 않는다. 검증 fixture는 상태 복원을 확인했으며 Player의 메모리·성능 증거와 별도로 해석한다.

후보 H는 `Builds/GoalCandidate-20261009-200559`이며 API/DB 포함 패키지의 냉간 시작·재시작·격리·정산 중복 방지·패키지 불변 검사는 `Backend/Validation/local-release-20261009-112052.json`에서 통과했다. 이 배포 검사는 정상 게임 완주나 전경 성능을 대신하지 않는다. `windows-mobility-06-victory-stop`은 서버 프로필 생성 뒤 마을에 들어가지 못하고 종료되어 실패 기록으로 유지한다. 제한된 버튼 재시도만으로 해결되지 않았으므로 프로필 선택 실패와 세션 변경에 따른 응답 취소를 구분하는 진단 로그를 추가했다.

후속 수동 창의 활성화는 `foreground window did not report a process id` 오류가 새 바인딩 재시도에서도 반복됐다. 입력 없는 실제 메뉴 캡처는 가능했으나 이 후속 실행에서 수동 슬롯 진입이나 전경 검증에 성공했다고 주장하지 않는다. 사용자에게 창을 직접 전경으로 가져오도록 요청했고, 앞서 받은 UI 조작 재개 허용은 그대로 유효하다.
