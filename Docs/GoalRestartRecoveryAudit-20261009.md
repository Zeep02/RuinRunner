# 앱 재실행 후 구매·정산 복구 경계 감사

2026-10-09 현재 소스의 읽기 전용 감사다. 실제 Windows 장애 검사 합격을 뜻하지 않는다. 이 감사에서 Assets를 수정하거나 게임/Editor를 실행하지 않았다. 전체 전투 상태 이어하기는 목표 범위 밖이다.

## 구매 응답이 끊긴 뒤 같은 validation profile로 재실행

1. 경제 queue는 네트워크 요청 전에 저장되고, 일반 command도 전송 전에 operationId/불변 payload와 함께 저장된다. 따라서 프록시의 성공 구매 응답 유실을 확인한 후 종료하면 같은 요청을 재생할 근거가 남는다.
2. 새 Controller는 `EconomyCommitted += ApplyEconomyReceipt`를 연결한 후 Connect한다. 수동 실행은 시작 메뉴에서 슬롯 화면을 열 때 Connect한다. 자동 파일럿이 없어도 복구 경로가 동작한다.
3. `Connect → Replay → Refresh → DrainEconomy`에서 pending command는 같은 UUID로 재전송한다. 일반 journal이 이미 confirmed이면 보관된 response를 사용한다. queue가 `confirmed-unprojected`이면 네트워크 없이 그 receipt를 사용한다.
4. 새 앱에서는 Player/BackendRun이 없고, 마을에서는 phase가 Town이다. `ApplyEconomyReceipt`는 해당 경우 정상 반환한다. coordinator가 이를 ACK하여 queue를 confirmed로 바꾸므로 구매 장비·회복을 마을이나 새 원정에 주입하지 않는다.
5. 슬롯 선택은 서버의 활성 원정·경제를 다시 읽는다. EnterTown은 가방을 초기화하고 `town-practice` 시작 장비만 만든다. 미정리 원정이 있으면 출정을 차단하고 원정 입구의 **이전 원정 정리**를 제공한다. 정리는 경제 복구를 먼저 끝낸 뒤 기존 원정을 abandoned로 정산한다.

이 정상 Controller 경로에서 pending이 영구히 남는 교착은 소스상 발견되지 않았다. 경제 event 구독자가 전혀 없는 단독 coordinator는 `projection_unavailable`로 계속 대기하는 것이 의도된 방어다. 현재 검사 때문에 이 방어를 제거할 필요는 없다. 저장 파일 손상/권한 거부나 서버 불가 상태까지 성공을 보장한다는 의미도 아니다.

근거: `Runtime/Backend/BackendFlowCoordinator.cs`의 Connect/Replay/DrainEconomy, `RuinBackendClient.cs`의 Mutate/Execute/RetryOperationAsync, `BackendPrivateStore.cs`의 Pending/EconomyQueue, `Core/GameController.Backend.cs`의 구독, `GameController.Economy.cs:47`의 phase/run 경계, `GameController.Town.cs`의 EnterTown/ResolveInterruptedExpedition.

## 정산 응답이 끊긴 뒤 재실행

- pending settle command는 같은 UUID로 재전송된다. 서버가 이미 정산했으면 기존 receipt가 반환되고 일반 journal은 confirmed가 된다.
- 이후 프로필 GET이 최신 유산·원정 통계의 기준이다. 슬롯 요약에 먼저 반영하고 슬롯 선택 시 ActiveData/마을 MetaProgression에 적용한다. 이미 정산된 원정은 active-run에서 반환하지 않으므로 다시 abandoned 정산하거나 새 보상을 중복 지급하지 않는다.
- **이전 Result 화면은 앱 재실행 뒤 복원하지 않는다.** `settlementRunId`, IsGameOver, LastMetaReward는 메모리 상태다. 새 coordinator는 이전 Run을 자동 선택하지 않고, SelectSlot은 활성 원정만 읽는다. 재실행 검사에서 확인할 표면은 슬롯/마을의 서버 확정 유산·통계와 미정리 원정 없음이다. `ResultSettlementConfirmed=true`나 이전 `+보상` 팝업 복원을 기대하면 현재 구현 범위를 넘는다.
- 앱을 유지한 채 정산 응답만 끊기면 별도 경로다. 기존 Run/settlementRunId가 남아 있으므로 TickRecovery의 서버 재조회가 정산된 Run을 유지하고, RefreshSettlementProjection이 Pending 결과를 Confirmed로 바꾼다. 이 동작을 앱 재시작 후 결과 화면 복원으로 보고하면 안 된다.

현재 서버 상태 복구/중단 원정 정리를 위해 필요한 최소 수정은 발견하지 못했다. 재실행 시 **지난 정산 보상도 별도로 안내**하려면 복구된 settlement receipt 식별자를 durable하게 보존하고 슬롯에 맞는 확인 안내를 추가해야 한다. 이는 현재 수행한 수정이나 검증이 아니다.

## 실제 Windows 검사 시 관찰할 값

프록시를 유지하고 **같은 실행 파일·같은 validation profile·같은 5200 origin**으로 앱만 재실행한다. 프록시 재시작은 신원 등록을 잃어 기존 테스트 게스트를 거부하므로 이 시나리오에서 하지 않는다. 검사는 일반 사용자 profile이 아닌 새 validation guest 하나만 사용한다.

- 구매 유실 직후: proxy `dropped.buy=<operationId>`, 해당 upstream status 200, 앱 종료 뒤 command/queue pending 또는 이미 복구된 confirmed 상태를 기록한다. retry가 종료 전에 완료됐다면 이를 미확정 상태에서의 재시작 검증이라고 세지 않는다.
- 다시 슬롯/마을 진입: 같은 operationId가 confirmed이고 pending/confirmed-unprojected가 0; Town 가방은 시작 연습 장비만; 이전 활성 원정 안내; 해당 원정 서버 경제의 구매 차감/장비가 정확히 1회.
- **이전 원정 정리는 이 프록시의 첫 settle이다.** 따라서 첫 settle 유실도 정리 버튼에서 발생한다. 그 직후 앱을 다시 종료/재실행하면 abandoned 정산의 복구를 검사할 수 있다. 승리/패배 결과 화면 유실을 별도로 검증하려면 새 proxy state와 새 validation guest로 별도 실행해야 한다.
- 정산 후 재실행: settle command confirmed; 최신 유산/totalRuns 1회; active run 없음; 마을 출정 재개 가능; 지난 원정 아이템/회복이 새 원정에 섞이지 않음. UI 화면과 서버 상태를 모두 확인한다.

읽기 보조 도구는 복구용 identity/bindings/profiles/legacy 파일을 열지 않는다. `operations`와 `economy-queue`만 읽어 UUID/상태/암호화 파일 hash를 기록하며 payload/response/토큰/이름은 출력하지 않는다. 기존 journal을 수정하지 않고 새 report만 생성한다.

```powershell
python Tools/Backend/Read-ValidationJournal.py --profile NEW-FAULT-PROFILE --api-port 5200 --output Logs/Validation/2026-10-09-goal/fault-before-restart.json
```

이 도구의 상태 요약도 정상 게임플레이 검증을 대체하지 않는다.
