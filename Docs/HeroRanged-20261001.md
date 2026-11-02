# 배낭 주인공 원거리 자세와 발사 연결

승인된 Sscary The Adventurer Male 무료 원본을 유지했다. 활·쇠뇌·권총·소총·산탄총·대포에 별도의 팔/보조 손/시위 해제/반동/복귀 자세를 추가한다. 원본 대기·보행 위상은 조준과 발사 중에도 독립적으로 진행한다. 원본 얼굴·머리·배낭의 rigid 픽셀 형태와 팔레트, 부츠, 48×64 캔버스, 32PPU, 발 피벗(24,22)을 보존한다. 이미지 재표본화·생성 래스터·Premium 파일은 채택하지 않았다. 활의 기존 표시 크기0.9도 유지한다.

## 실제 포즈와 실행 경로

`External/SscaryMaleCombat/RangedV7`에는4964고유 RGBA PNG가 있다. 여섯 클립×여섯 시점×원본 대기8/보행8변형×16시간 키의9216샘플이 동일한 이미지를 공유한다. 조합 수나 hold/중복 파일을 추가 저작 포즈로 세지 않는다.

| 클립 | up | right_up | right_down | down | left_down | left_up |
| --- | --- | --- | --- | --- | --- | --- |
| Bow | 8 | 7 | 13 | 8 | 13 | 7 |
| Crossbow | 12 | 9 | 10 | 14 | 10 | 9 |
| Pistol | 12 | 8 | 10 | 14 | 10 | 8 |
| Rifle | 12 | 8 | 10 | 14 | 10 | 8 |
| Shotgun | 11 | 12 | 15 | 15 | 15 | 12 |
| Cannon | 12 | 9 | 13 | 13 | 13 | 11 |

위 수치는 대기0 기준 고유 본체 포즈다. 짧은 실제 발사 시간을 유지하며 7~15포즈/시점이다. 첫 키와 마지막 키의 Sprite·손·가림 정보는 정확히 같다. 팔 관절, 닫힌 손, 지원 팔과 기존 소스 디딤을 사용하며 전체 이미지를 흔들거나 확대해 움직임을 대신하지 않는다.

`PlayerController`의 ranged presentation 시계는 피해·탄약·마나·쿨다운·이동 판정과 분리된다. 발사 순간 key0의 실제 손과 원본 총구를 샘플하고, 같은 프레임의 본체·손무기가 같은 시계를 사용한다. 권총 연사도 실제 각 탄 발사에서 다시 반동을 시작한다. 대시/교체/메뉴/사망은 기존 취소 흐름을 따르며 발사 콜백이나 판정을 추가하지 않는다.

총의 개머리판·활 시위는 원본 머리 뒤, 닫힌 손은 앞에 보인다. 총구 섬광·파편·화살·탄환 코어·잔상은 발사 주인공의 그림 순서를 따라 얼굴 아래에 그린다. 화면에서 총과 이펙트가 눈을 덮던 문제가 수정됐다. 무기 이미지/총구 좌표/탄속/폭발 반경/BoxCast/피해 중복 제거는 유지한다. 기존 무기별 이펙트와 오디오는 유지하며 이번 작업을 전체 FX/SFX 재제작으로 보고하지 않는다.

## 최종 검증

- ranged-v7/native-qa:9216후보의 원본 머리·배낭/부츠/팔레트/캔버스/루프 끝점 검사 PASS. runtime-identity-qa는 실제4964PNG와 채택 RGBA가 정확히 일치함을 확인했다.
- SpriteEditor DataProvider의 EditPivot capability 확인 후 실제 연결된 Editor에서 임포트했다.108.597초 작업의 실제 완료·리소스·컴파일 성공을 확인했다.
- `Logs/Validation/2026-10-01-hero-ranged/presentation-154512`:562검사 PASS(배너 포함563행),6대표 무기×6시점×기본/특수72행동,2277실제1280×720 표본,96발사 모션 그룹. 보행/후진/일시정지/대시 취소/교체/메뉴 정리 포함. 그룹마다 최소3실제 본체 이미지, 손 키 변화 최대4.9984native pixels. 실제 발사 순간 총구와 확정 발사 원점 차이≤0.002월드 단위. 탄약과 특수 마나는 실제 최초 발사 순간 검사한다.
- `catalog-154914`:나머지10무기(composite_bow/repeater/flintlock/scattergun/great_bow/siege_crossbow/twin_pistols/celestial_bow/void_revolver/starbreaker)의 양쪽 시점 기본/특수40행동,306검사 PASS(배너 포함307행),1129원본 표본/48모션 그룹. 최소3고유 본체 이미지, 손 변화 최대4native pixels.
- 각 실제 녹화의 `screen-face-qa.json`:원본의 불투명 머리·얼굴·상부 픽셀을 기록된 실제 카메라/Renderer 좌표에서 비교했다.353336+174154=527490화면 픽셀, 가림/색 변형0. 투명 가장자리와 다른 적/피격 섬광은 범위 밖이다. 이 조건을 약화하거나 비교 좌표를 결과에 맞춰 이동하지 않았다.
- `Logs/Validation/2026-09-30-combat-presentation/physics-155458`:실제2D 충돌19검사 PASS. 반복 콜라이더 피해 중복, 대포/권총 폭발의 직격 제외와 벽 차단, 고속탄의 얇은 벽 차단을 재확인했다.
- `Logs/Validation/2026-10-01-weapon-identity/lifecycle-155651`:8검사 PASS.64파편 제한·일시정지·수명·얇은 벽·차지/대시 취소·메뉴 전체 FX/파편 정리.

장비/마나를 공급하고 스포너를 멈춘 격리 화면이다. 정상 캠페인, 실제 피해 밸런스,1080p60fps,전체 오디오 청취의 증거로 확대하지 않는다. 얼굴 검사도 모든 피격/다른 적/임의 방향 전환을 포함한 전체 게임 증거가 아니다.

실제시간 무음 H264는 presentation-154512의 `pistol-burst-view-2-1x.mp4`(28표본/.467522초), `cannon-mobile-1x.mp4`(43표본/.721698초)다. 원본1280×720 표본을 다음 실제 시각까지 유지하며 보간·시간 압축·임의 확대·새 프레임 합성은 없다. GameView는 기록 직전에 Repaint/QueuePlayerLoopUpdate를 요청한다. timeScale1/captureFramerate0이며 배경 GameView의 이전100ms 갱신 실패를 게임의 발사 시간을 늘려 해결하지 않았다.

## 실패 시안과 검사 도구 수정

v1~v7후보를 보존한다. v1의 북쪽 포즈 부족, v2/v3의 지원 팔이 부츠 픽셀을 침범하는 문제, v5/v6의 잘못된 어깨 치환은 채택하지 않았다. 처음 채택한v4와v5의 PNG/.meta는 Editor MoveAsset으로 Resources 밖 `ArtReview/RetiredSscaryRangedV4`, `RetiredSscaryRangedV5`에 보존했다. v4가 수치 검사를 통과했지만 총이 머리를 덮는 실제 화면은 합격으로 처리하지 않았다. v7첫 화면에서도 화살/잔상이 얼굴을 덮어 screen-face 검사가 실패했고, 실제 caster 그림 순서 연결 후 전체 자료를 재검사했다.

150548의 공급 마나 소진,152626의 검사 종료 시 마나 재생 허용치 오류,153512의 긴 프레임에 입력 시작/종료를 동시에 처리한 문제,153752의 배경100ms GameView 갱신 실패는 원본 결과/CSV/PNG/이전 fixture와 함께 보존한다. 공급 마나와 실제 최초 발사 소비, 실제 입력 전달/공격 시퀀스, 렌더 갱신을 수정했다. 최소3실제 이미지/얼굴 무가림 등의 기준과 게임 판정·발사 시간을 낮추지 않았다. Console 기록도 지우지 않았다.

저작 도구: `Tools/Art/make_sscary_ranged_20261001.py`는 C#문자열을 생성하고 래스터를 편집하지 않는다. 실제 원본 관절 저작/임포트는 `Tools/Validation/AuthorSscaryRanged20261001.cs`, `ImportSscaryRanged20261001.cs`다. 검토 도구는 `review_sscary_ranged_*.py`와 실제 입력 fixture 두 개다. 수정 전 소스와 고지문은 `ArtDirection/Review-20261001/hero-ranged/before`에 있다.

전용 피격/재장전 본체, 적·보스 모션, 후반 맵·배치·크기와 최신 세 빌드의 정상 캠페인/25~40분 참고 분량/1080p60fps/반복 런 메모리는 남는다. `HasAuthoredCombatPoses`는 false를 유지한다. 새 Windows 후보와 실제 기동 결과는 확인 후 아래에 연결한다.

최종 AQ Windows CleanBuildCache/DetailedBuildReport 후보 `build_5ded494d07b2`:2026-10-01T07:03:35.7809555Z~07:04:29.8364765Z,648774731바이트/54.055초/오류0·기존 경고3로 성공했다. 실행 파일은 `Builds/CampaignCandidate-20261001-AQ/RuinRunner.exe`이며 Data/DLL과 함께 실행한다. `build-AQ.json`에 원본 보고서를 보존한다. AP는 이번 원거리 변경 이전 소스다.

AQ의 실제 OS 프로세스43796,격리 프로필 HeroRangedAQ-160623, `windows-smoke-AQ-160623`:새 원거리12288runtime 샘플을 포함한 startup/native 리소스/충돌/장비/성장/저장 검사 PASS·exit0. 기본 실행은 정상 메뉴이며 이 검사는 명시적 -batchmode/-nographics/--smoke-test 인자로 수행했다. 정상 캠페인·보이는 플레이·청취·성능 증거가 아니다. 최종 Console groundTruth는 컴파일 실패false/컴파일 중false/오류0/기존 경고3이다. 보존 buffer의 과거 오류14행을 지우거나 현재 재현 오류로 해석하지 않았다.
