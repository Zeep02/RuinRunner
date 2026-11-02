# 목표 백엔드·저장 작업 — 2026-10-09

## 착수 판정

- 보존: ASP.NET Core/PostgreSQL, 게스트 소유권, profile/run/economy 원자적 거래, operationId 재전송, 카탈로그 버전 고정, legacy 원본·환급 이력, DPAPI 복구 저널. 최신 과거 증거는 2026-10-04이며 정상 완주나 현재 Windows 빌드 합격으로 재사용하지 않는다.
- 부분: 현재 coordinator는 마을·원정·경제에 연결되어 있으나 미확정 정산 자동 재생, 프로필 초기화, 전체 수치의 버전 카탈로그 연결은 추가 작업이 필요했다.
- 미완료: `campaign-v1` 장신구 stats는 비어 있다. 정상 UI/게임 입력, Windows 실행, 배포·장애 복구의 현재 검증은 메인 통합 담당 영역이다.

## 구현

- 추가 마이그레이션 `005_profile_reset.sql`: 초기화 확인·초기화 전 상태 감사 기록. 기존 001~004와 출판된 카탈로그는 수정하지 않았다.
- `POST /v1/profiles/{id}/reset-challenge`, `POST .../reset`: 인증 소유자/프로필/revision에 묶인 5분 확인. 두 단계 모두 활성 원정을 거부하고, 단일 owner transaction에서 상태·ledger·감사 기록·operation receipt를 확정한다. 프로필 ID/이름/슬롯, 기존 원정, 원본 이관 snapshot을 삭제하지 않는다.
- 초기화는 기본 구성/유산과 0회 기록으로 돌아가며 revision을 증가한다. `legacyImportBlocked=true`, `resetCount`와 기존 migration/refund version을 보존해 예전 로컬 원본이 자동 재이관되지 않는다.
- 클라이언트: `BeginResetAsync(slot)`, `ResetChallenge`, `ConfirmResetAsync(challenge)`, `CancelReset()`. 미확정 명령이 있으면 초기화를 거부한다. 확인 응답 유실은 같은 operationId로 재시도한다. 선택 프로필은 동일 ID일 때만 갱신하며, 재생 영수증보다 최신 서버 revision을 다시 읽는다.
- `TickRecoveryAsync`는 경제뿐 아니라 일반 명령(정산 포함)을 재생하고 서버 투영을 갱신한다. 다른 슬롯 선택 후 이전 원정을 유지하던 경계를 고쳤고 새 슬롯의 카탈로그를 현재 버전으로 읽는다. coordinator 폐기 후 이벤트 전달을 중단한다.

## 현재 검증

- API dotnet Release 빌드: 경고 0, 오류 0.
- 전용 클러스터/API: `Tools/Backend/Start-IsolatedBackend.ps1 -Instance goal-20261009`, API `http://127.0.0.1:5196/`, PostgreSQL 55439. 일반 DB/자격정보는 읽거나 시작하지 않았다. 종료는 같은 명령의 `-Stop`이며 데이터는 보존한다.
- `python Tools/Backend/Test-ProfileReset.py --instance goal-20261009`: **23 PASS**, `Backend/Validation/profile-reset-20261009-063037.json`. 매 실행 새 게스트·새 슬롯만 생성한다. 만료 주입도 이 전용 DB의 새 확인 ID에만 했다.
- 검사: 소유권/슬롯 결합, 준비 무변경, stale/expired/active 거부, 동시 재전송, 초기화 후 재이관 거부, 원본·원정 보존, audit/ledger 1회, 다른 슬롯 보존, 재출정.
- 이는 HTTP/PostgreSQL 기능 검사다. 실제 Unity UI 초기화·재접속, 정상 캠페인, Windows 게임 화면 합격을 뜻하지 않는다.

기존 수정 파일의 원본은 `Backups/2026-10-09-goal/backend`에 보존했다.

## 후속 완료 — 카탈로그·복구·실행 묶음

- 새 `campaign-v2`는 장신구 30개와 세트 보너스 8개의 661개 기본/효과 수치를 갖는다. 수치 스키마와 범위 검사는 API와 클라이언트가 공유한다. 설명의 `{key}`는 실제 효과가 읽는 같은 불변 데이터로, 현재 문화권과 무관하게 숫자를 출력한다. 조건 분기·행동·재귀 방지는 C#에 유지한다.
- `AccessoryTuning`과 원정별 세트 snapshot을 장비·PlayerStats·ItemCombatEffects가 보유한다. 전역 카탈로그를 다시 연결해도 기존 소유 아이템과 플레이어의 수치는 바뀌지 않는다. 잘못된/누락된 수치와 설명 placeholder는 배치 연결 전에 거부한다.
- v1은 기존 bytes와 동작 기본값을 보존한다. v2의 의도한 동작 차이는 `trail_spindle` 표식 요구량을 기존 실제 2개에서 설명과 일치하는 3개로 바꾼 것이다. 새 버전 출판 후 v1 원정은 v1 수치·경제·정산을 계속 사용한다. 출판된 v2 또한 이후 같은 이름으로 덮어쓰면 안 된다.
- 경제 응답은 서버 확정 후 `confirmed-unprojected`로 저널에 먼저 저장한다. 적용 callback이 없거나 예외가 나면 복구 대기를 유지하며, 다음 시도는 저장한 영수증을 사용해 서버에 같은 거래를 다시 만들지 않는다. 정상 callback 반환 뒤에만 `confirmed`로 승인한다. 메인 GameController는 소유 인스턴스/제거의 멱등 처리, Heal 직후 중복 marker, 이후 시각 효과 순서로 연동했다.
- `Start-IsolatedBackend.ps1`의 테스트 서버 5196과 별도 카탈로그 서버 5197은 명시적 v2로 실행한다. 일반 개발/사용자 런처의 기본 카탈로그는 바꾸지 않았다. 실제 사용자 DB에서 초기화·장애 주입·테스트 정산을 수행하지 않았다.

## 후속 검증 근거

| 증거 | 결과와 한계 |
|---|---|
| `Backend/Validation/tuning-20261009-064313.json` | 실제 공유 스키마/클라이언트 불변 파서 712 PASS. 661개 수치, 38개 설명, 불변성·범위·문화권 검사. 정적/분리 실행이며 게임 체감 증거가 아니다. |
| `Backend/Validation/catalog-pinning-20261009-064609.json` | 전용 HTTP/PostgreSQL에서 v1→v2 출판 및 재시작 9 PASS. 이전 원정/경제/정산 보존, 새 원정 v2. |
| `Backend/Validation/unity-catalog-recovery-20261009-065209-439e44.json` | 메인이 실제 Unity에서 실행한 공급 상태 검사 23 PASS. 수치/설명, 기존 소유 장비/세트 고정, 실제 ManaAegis 자원 소모·공유 cooldown, 적용 예외 후 저널 재생·아이템/회복 1회. `normalGameplay=false`. |
| `Logs/Validation/2026-10-09-goal/session-153329-e49bf2/result.json` | 메인이 실제 Controller/UI/API 초기화와 Pending/Confirmed 결과를 검사한 22 PASS/CAPTURED. 정상 캠페인 완주가 아니다. |
| `Backend/Validation/local-release-20261009-070508.json` | 아래 실제 배포 묶음을 Windows PowerShell 5.1로 실행한 16 PASS. 독립 신규 DB에서 포트 충돌 거부, 시작/정지/재시작, 한글 프로필·원정·경제 보존, 정산 영수증 재전송, 패키지 전 파일 해시 보존. Unity를 실행한 검사가 아니다. |

API Release와 Runtime 오프라인 컴파일은 오류 0. Unity/Windows 컴파일과 화면 검증은 메인 담당이며 공급 검사·과거 증거를 정상 캠페인 합격으로 합산하지 않는다. 초기 패키지 시험에서 PowerShell 5.1의 `Start-Process` 종료 코드가 비어 성공을 실패로 표시한 문제를 핸들 유지/Refresh로 수정한 뒤, 위 실제 실행으로 다시 확인했다.

## 검사용 Windows/API/DB 묶음

- 경로: `Deliverables/RuinRunner-Candidate-20261009-155518`.
- 원본 Windows 후보: `Builds/GoalCandidate-20261009-155518`, Unity 6000.6.0f1, build summary 성공/오류 0/경고 6. 아트 마감과 정상 캠페인 합격이 남은 **검사용 후보**다.
- 재현: `Tools/Backend/New-LocalRelease.ps1 -PlayerDirectory <성공한 Windows 빌드 폴더> -OutputDirectory <새 폴더> -ContentVersion campaign-v2`. 기존 후보 폴더를 덮어쓰지 않는다.
- 기본 실행은 `Start Local Game.cmd`, 종료는 게임을 닫은 뒤 `Stop Local Backend.cmd`. Unity·SDK·PostgreSQL 전역 설치 없이 번들 .NET runtime과 PostgreSQL을 사용한다. 첫 실행이 `%LOCALAPPDATA%/RuinRunner/LocalBackend`에 보호된 자격정보와 UTF-8 DB를 생성하고 migration/catalog를 적용한다. 기본 API는 127.0.0.1:5186, DB는 55432다.
- 원격 서버 사용은 `Start-Game.ps1 -ApiUrl https://서버주소/`. 로컬 서버 시작을 건너뛰고 공개 주소만 게임 환경변수로 전달한다. 외부 서비스나 유료 호스팅은 생성하지 않았다.
- 격리 게임 검사는 `Start-Game.ps1 -DataRoot <별도 데이터 폴더> -ApiPort <빈 포트> -DatabasePort <빈 포트> -ValidationProfile <새 이름>`. 포트 override는 패키지 파일을 바꾸지 않는다. 실행 중 다른 묶음의 backend/사용 중 포트를 자동 종료하지 않는다.
- `manifest.json`은 모든 파일 크기/SHA-256과 candidate 상태를 기록한다. package에는 DB·secrets·개발 SDK/cache가 없고 공개 포트/버전 설정만 있다. .NET/PostgreSQL/Npgsql 및 게임 에셋 라이선스는 `Licenses`에 포함한다.
- 영속성 검사는 `python Tools/Backend/Test-LocalRelease.py --package <묶음 경로>`로 재현한다. 저장 경로는 항상 `Backend/.local/release-check-*`의 새 디렉터리이며 다른 계정/DB를 재사용하지 않는다. 검사 서버 5198/55441은 완료 후 종료했으며 테스트 데이터는 보존했다.

백엔드 기능·카탈로그 연결·독립 실행 경로는 이 근거로 확인했다. 실제 Windows 패널에서 장애/재접속 UX, 최소 네 번의 정상 캠페인, 최종 아트·프레임 시간은 전체 게임 통합 승인에 남아 있다.

## Windows 응답 유실 검사용 프록시

- `Tools/Backend/ValidationFaultProxy.py`와 `Start-ValidationFaultProxy.ps1`은 **127.0.0.1:5200 → 전용 5197**로 고정한다. 일반 API 5186이나 정상 원정 검사 중인 5196을 연결·재시작하지 않는다.
- 프록시 시작 시 프로필이 없는 게스트 한 명만 등록할 수 있다. 기존 진행도가 있는 계정/등록되지 않은 token/두 번째 게스트는 거부한다. 등록한 같은 게스트의 인증 갱신과 게임 앱 재실행은 프록시가 계속 실행 중이면 허용한다. 신원·토큰은 메모리에만 유지하므로 프록시를 새로 시작할 때는 새 validation profile이 필요하다.
- 기본값은 fault 꺼짐이다. 명시적 `-EnableFaults`로 켜면 첫 **성공한 buy와 settle** 응답만 upstream 200/영수증 확정 후 연결을 끊는다. 4xx 거부는 fault를 소비하지 않는다. 이후 동일 요청과 새 요청은 그대로 통과한다. 서버 데이터나 응답 본문은 변조하지 않는다.
- 증거 `Backend/Validation/FaultProxy/<이름>-<시간>-<ID>/state.json`에는 operationId·buy/settle 구분·HTTP 상태·요청 수·유실 수와 비밀 없는 실행 메타데이터만 쓴다. 인증 헤더·guestSecret·token·요청/응답 본문·DB 자격정보를 기록하지 않는다. CLI 시작 로그도 같은 제한을 따른다.

실제 Windows 검사의 실행 순서:

```powershell
# 이전 검사 프록시가 있을 때만 그 프로세스를 종료한다. API/DB는 계속 실행된다.
& Tools/Backend/Start-ValidationFaultProxy.ps1 -Stop
& Tools/Backend/Start-ValidationFaultProxy.ps1 -NewState -EnableFaults -Name windows-purchase-settle
# 새 validation profile 이름을 사용한다. 실제 Windows 실행은 메인 담당자가 한다.
& Deliverables/RuinRunner-Candidate-20261009-155518/Start-Game.ps1 -ApiUrl http://127.0.0.1:5200/ -ValidationProfile fault-windows-NEW-NAME
& Tools/Backend/Start-ValidationFaultProxy.ps1 -Status
& Tools/Backend/Start-ValidationFaultProxy.ps1 -Stop
```

`-NewState`는 매번 새 증거 폴더를 만들어 이전 상태를 보존한다. 이미 프록시가 실행 중이면 새 시작을 거부한다. 서비스는 숨김으로 시작하며 `-Stop`은 저장한 PID/실행 파일/스크립트 소유권이 일치할 때만 종료한다. 게임의 구매/결과 Pending→Confirmed와 인벤토리/보유량 복구는 이 도구 준비만으로 합격 처리하지 않는다.

`python Tools/Backend/Test-ValidationFaultProxy.py` 실제 HTTP 자체검사 **16 PASS**: `Backend/Validation/fault-proxy-selftest-20261009-071514.json`. 명시적 enable, 기존/다른 계정 차단, 새 게스트 재인증, 실패 구매 무유실, 구매/정산 후 첫 응답 유실, 재송신 정확히 1회, 기록의 비밀 부재를 확인했다. 구매는 요청 4회/200 4회/유실 1회, 정산은 요청 3회/200 3회/유실 1회였다. 자체검사 완료 후 프록시는 종료했다. Unity/Windows 게임을 실행한 검사는 아니다.

## 현재 v2 API 회귀 재실행

`Test-Campaign.py`는 필수 `--instance`와 명시적 `--expected-catalog`를 받는다. 인스턴스 marker와 DLL의 격리 경로를 확인하고 5186/5196을 거부한다. `--restart` 또한 그 격리 인스턴스만 대상으로 하며, 이번 실행에는 사용하지 않았다.

```powershell
python Tools/Backend/Test-Campaign.py --instance goal-catalog-20261009 --expected-catalog campaign-v2
```

`Backend/Validation/campaign-20261009-071537.json`: **57 PASS**. 새 두 계정·다른 슬롯 격리, legacy 원본 hash/환급/재송신, 조건 해금·revision, 같은 구매의 동시 재시도, 서로 다른 구매의 같은 revision 경합, 교환·판매·회복·재고·가방 한도, 동시 정산과 중복 거부를 실제 5197 API/PostgreSQL에서 확인했다. 보상/완주 통계는 HTTP 공급 값이며 `normalGameplay=false`다.

선행 실패 `campaign-20261009-071524.json`(42개 확인 후 stage-0 heal out_of_stock)은 새 동시 구매 검사가 기존 회복 검사의 1회 재고를 먼저 소비한 fixture 충돌이다. 동시 구매를 stage-1 상인으로 분리한 뒤 새 계정으로 전체 검사를 재실행했으며, 실패 기록과 실제 게임의 1회 재고 규칙은 보존했다.

## 앱 재실행 전 읽기 전용 감사

`Docs/GoalRestartRecoveryAudit-20261009.md`에 실제 Controller/queue/command 복구 경계를 기록했다. 새 앱의 슬롯 화면 Connect에서 이전 구매를 재생·확인하고 Town/다른 원정에는 투영하지 않는 구조이므로 소스상 pending 교착은 발견하지 못했다. 미정리 활성 원정은 마을에서 명시적으로 abandoned 정산한다. 정산 응답 유실 후 앱 재실행은 서버 프로필의 유산·통계와 active run 없음으로 확인되며, 이전 Result 화면/`+보상` 팝업은 복원하지 않는다. 아직 실제 Windows 유실/강제 종료/재실행 합격은 아니다.

`Tools/Backend/Read-ValidationJournal.py`는 workspace의 지정 validation profile에서 `operations`/`economy-queue` 두 종류만 읽고 UUID/상태/암호화 hash만 새 보고서에 기록한다. identity·token·bindings·사용자 일반 save를 열지 않는다. 실제 CPU-clock 진단 profile의 58개 encrypted record를 읽어 모두 읽는 동안 불변임을 확인했고, 5개 경로 탈출 입력을 거부했다. `Logs/Validation/2026-10-09-goal/performance-diagnosis/cpuclock-journal-readonly.json`은 confirmed 31 commands/27 economy records다. 이는 도구 점검이지 구매 응답 유실 복구의 실행 증거가 아니다.

## 실제 Windows 정상 원정 및 같은 앱 내 응답 유실 복구

`Logs/Validation/2026-10-09-goal/windows-ranged-02`는 후보 B `GoalCandidate-20261009-162422`/seed 2026100903/ranged/새 validation guest/격리 proxy 5200→5197로 실행했다. `exit.json`은 EXIT 0이고 `server-journey.json`은 세 수호자·엔딩→서버 정산→마을→재출정→중단 원정 정리 PASS다. 자동 GameAction 이동·전투·상호작용과 실제 UI 버튼의 public handler를 사용했으며 `manualUiInput=false`, `cheatsOrSuppliedState=false`다. 수동 플레이로 보고하지 않는다.

- 구매 요청/확정 2/2, 판매 2/2, 정상 획득 무기 변형 2, 서약 2/각성 2/실제 공격 사용 2, 자연 획득 재료 교환 1/확정 1.
- `Backend/Validation/FaultProxy/windows-ranged-02-20261009-073424-c1823f/state.json`: 첫 buy `95970951-6bb2-4f51-8c40-404332316ecd`와 승리 settle `6bb8c86c-aaf9-41c1-aedf-75bb557ebe38` 모두 upstream 200 후 첫 응답을 한 번 끊었다. 각각 **총 요청 2회(최초+재시도 1회)**/200 두 번/response drop 한 번이다. 게임의 서버·로컬 투영 확인과 최종 여정 성공이 함께 있다.
- player.log의 Curl error 52 두 번은 위 의도된 응답 유실에 대응한다. 이후 PASS를 확인했다. 이 실행에서는 앱 프로세스를 종료하지 않았으므로 **앱 재실행 복구 합격으로 세지 않는다.**
- 정상 규칙을 따른 ranged 캠페인 완주 1회로 기록한다. 다른 빌드 유형의 정상 완주·최종 아트·최종 배포 전체를 이 실행 하나로 완료 처리하지 않는다.
