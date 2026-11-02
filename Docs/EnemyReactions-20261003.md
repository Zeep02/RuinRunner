# 적 피격·사망 표시 정리 (2026-10-03)

공격을 예고하던 적을 때리면 노란 예고색이 피격색으로 덮인 채 남았고, 연속 피격에서는 먼저 시작한 코루틴이 나중 피격 표시를 조기에 지웠다. `EnemyController`가 현재 행동의 기본 색과 0.055초 피격 표시를 따로 관리하도록 바꿨다. 새 피격은 자신의 시간을 확보하며, 끝나면 현재 행동색으로 돌아간다. 공격·이동·피해량과 기존 원본 애니메이션은 유지했다.

세 보스의 사망 시계가 일시정지 중에도 진행하던 문제도 수정했다. 실제 사망 포즈·체력바 감소·최종 사망 콜백을 함께 멈추고 재개한다. 일반 적 사망에도 같은 정지 조건을 적용했다. 원본 픽셀 사망 위에 공통 사각 파편을 덮던 효과는 제거했다. 새 본체 포즈를 추가한 단위는 아니다.

## 재현과 검증

연결된 Unity 6000.6.0f1에서 컴파일과 실제 `TakeDamage`, 공격 코루틴, 사망, 메뉴 일시정지 경로를 검사했다. 적·장비·피해를 공급한 검증이며 정상 캠페인이나 수동 입력·성능 근거는 아니다.

- `Logs/Validation/2026-10-03-enemy-reactions/baseline-162000`: 10검사 중 5실패. 예고색 복구, 겹친 피격 표시, 세 보스의 일시정지 실패를 재현했다.
- `verified-162504`: fixture가 EnemyController 자체를 꺼 두어 새 LateUpdate 시계를 실행하지 못한 실패다. 자료를 보존했고 컴포넌트를 켠 실제 동작으로 수정했다.
- `verified-162601`: 동일한 10검사 모두 PASS. 세 보스 재개 후 콜백이 각각 한 번만 실행됨을 확인했다.
- `motion-162627`: 캡처 사이 PNG 인코딩 지연이 다음 사망 첫 프레임에 유입돼 Grunt 2프레임을 놓친 기록이다. 폐기하거나 정상 기록으로 세지 않았다.
- `motion-162738`: 프레임 저장 후 시계를 안정시킨 뒤 실제 피해를 가했다. 9종의 피격·사망 18구간, 991실제 화면과 기존 사망 Sprite 전부를 기록했다. 중복 치명타의 이중 정산 방지와 메뉴 정리도 통과했다.
- 같은 폴더 `death-{Type}-1x.mp4` 9개는 실제 타임스탬프를 보존한 1280×720 H264/60fps 컨테이너다. 보간·시간 압축 없이 브라우저 1배속 재생과 원본 화면을 확인했다. 녹화 부하를 포함하므로 성능 검사로 쓰지 않는다.

## 확인된 본체 모션의 부족점

`motion-162738/native-frame-audit.json`은 실제 관찰한 Sprite 경로와 원본 RGBA 고유 수를 구별한다.

| 적 | 관찰된 사망 파일 | 실제 고유 이미지 | 현재 상태 |
| --- | ---: | ---: | --- |
| Grunt / Shooter / Leaper | 각각 10 | 각각 10 | 기존 외부 원본 유지 |
| Charger | 1 | 1 | 전용 사망 없음, 마지막 자세의 페이드만 남음 |
| Boss | 8 | 8 | 기존 원본 유지 |
| Tidekeeper / SealHeart | 각각 4 | 각각 4 | 후반 보스 동작 밀도 부족 |
| Tidecaller / Sentinel | 각각 11 | 각각 6 | 중복·완전 투명 프레임 포함, 11개 새 포즈로 세지 않음 |

원본 PNG·피벗·라이선스·재생 시간을 보존했다. 이번 변경을 적 9종 아트 완성으로 판정하지 않는다. 돌진 멧돼지와 후반 보스의 실제 새 자세가 필요하다.

주인공 전용 피격의 두 번째 생성 시안도 거절했다. 단일 자세로 제한했지만 원본 얼굴·눈과 픽셀 블록이 달라지고 가장자리가 흐려졌다. `ArtDirection/Review-20261003/hero-hurt/rejected-hurt-single-v2.png`, `hurt-v2-prompt.txt`, `hurt-v2-assessment.json`에 보존했고 프로젝트 런타임으로 가져오지 않았다. 접촉 이펙트로 전용 본체의 미완료를 대신 판정하지 않는다.

## Windows 후보 BB

- 실행: `Builds/CampaignCandidate-20261003-BB/RuinRunner.exe`, 옆 Data/DLL과 함께 실행한다. 이전 BA와 사용자 저장을 보존했다.
- `build_413ee2465f4f`, 2026-10-03 16:35:12 KST 완료. CleanBuildCache/DetailedBuildReport, 650,907,955 bytes / 154.169초 / 오류 0·기존 경고 3.
- 15,444개 빌드 소스 SHA 변경 0. raw native 중복·보관 시안·퇴역 장전 V2 포함 0. `ArtDirection/Review-20261003/enemy-reactions/build-BB*.json`, `build-integrity-BB.json`.
- 실제 Windows smoke: `Logs/Validation/2026-10-03-enemy-reactions/windows-smoke-BB-1280x720-163737`, PID39444, exit0/PASS. 기동·자원·장비·성장·충돌·격리 저장 검사다.
- Windows UI/피격/사망: `windows-ui-BB-1280x720-163804`, PID36976, exit0/PASS. 49PNG/빈 화면 0, 기존 설명 배치 96개 통과. 새 35번 예고색·36번 보스 사망 3화면을 직접 확인했다. 실제 일시정지/재개와 한 번만 정산, 사각 파편 없음도 통과했다.
- 이전 BA의 플레이어 피격·보호막 Windows 검사도 같은 BB에서 통과했다. AZ의 대시 잔상·게이지·퀵슬롯, AY의 장전을 보존한다. 이 단위에서 UI 레이아웃은 바꾸지 않았으며 1080p/비표준 UI 증거는 이전 BA/AZ 기록이다.

중요 파일은 `Assets/RuinRunner/Runtime/Enemies/EnemyController.cs`, 검증용 `Runtime/Diagnostics/UiValidation.cs`, `Tools/Validation/EnemyReactionProbe20261003.cs`, `EnemyMotionReview20261003.cs`와 BB 빌드/실행 스크립트다. 변경 전 파일은 검토 폴더의 `before/`에 보존했다. 새 외부 에셋 채택은 없다.

전체 목표는 ACTIVE/미완료다. 남은 주인공 피격 본체·적/보스 새 동작·환경과 지형 마감, 최신 세 빌드 방향의 정상 캠페인·플레이 분량·1080p 성능/반복 메모리·실제 청취가 필요하다.
