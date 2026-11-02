# 캠페인 음악 교체 — 2026-10-01

사용자 참고곡: DM DOKURO, **sanctuary** (Calamity / Sunken Sea), https://www.youtube.com/watch?v=BgLahg0wkGs.
참고곡의 녹음·멜로디·샘플은 사용하지 않았다. 아래 원작자가 무료 배포한 별도 탐험곡을 채택했다.

| 구역 | 곡 / 작곡가 | 길이 | 선택 방향 / 출처 |
|---|---|---:|---|
| 외곽 유적 | Forest Whisper / Cleyton Kauffman | 82.286초 | 플루트·현·벨 중심의 차분한 유적 탐험. https://opengameart.org/content/forest-whisper-theme |
| 침수된 회랑 | Exploration Theme / Cleyton Kauffman | 134.400초 | 피아노·신스 아르페지오와 수중 공간감. https://opengameart.org/content/exploration-theme |
| 봉인의 심부 | Ruined City Theme / Cleyton Kauffman | 112.941초 | 어둡고 비어 있는 폐허의 분위기. https://opengameart.org/content/ruined-city-theme |

세 원작자 게시물과 각 ZIP의 readme에서 CC0를 확인했다. 저자 표기를 배포용 `Assets/StreamingAssets/ThirdPartyNotices/CampaignMusic-CleytonKauffman.txt`에 포함했다. 원본 ZIP·게시물·WAV·readme는 `ArtDirection/ExternalCandidates/20261001-music`에 보존했다. 악기와 분위기 설명은 원작자의 분류에 근거하며, 자동 신호 검사가 주관적인 청취 평가를 대신하지는 않는다.

## 편집·재생 정책

- 기존 22.05kHz 모노 합성곡을 44.1kHz 스테레오 원본 기반 WAV로 교체. 기존 파일 경로와 GUID 유지.
- 세 곡의 통합 음량을 약 -20 LUFS로 정렬. 일정한 게인만 적용하므로 원곡의 강약·템포·음정·편곡은 유지한다. 피크는 -3dBTP 이하를 목표로 제한한다.
- Forest Whisper의 측정된 루프 경계 단절만 양끝 5ms 게인으로 보정. 샘플 수는 바꾸지 않는다. 다른 곡에는 페이드나 자르기를 적용하지 않았다.
- 무거운 합성 드론 대신 보유 Ninja Adventure의 Wind / WaveFar / Wind2 환경음 원본을 각 구역에 배치. CC0, 원본 바이트 그대로.
- 2개의 음악 소스와 2개의 환경음 소스를 재사용. DSP 시간 기준 2.2초 등전력 전환, 120ms 예약 여유. 로딩 완료까지 기존 곡 유지.
- 빠르게 연속 요청하면 현재 전환을 마치고 마지막 요청으로 이동한다. 같은 구역 요청은 재생 위치를 리셋하지 않는다.
- 일시정지/인벤토리/히트스톱에서도 음악 시간과 페이드는 계속 진행. 메인 메뉴 복귀 시 첫 구역 테마로 전환.
- 기존 음악/환경음/마스터 설정을 그대로 사용. 음악과 환경음은 별도 조절. 합성 음악 fallback은 제거했다.
- Vorbis 품질 0.85, Streaming, Load In Background, 스테레오·원본 샘플레이트 유지. 임포터 API로 변경하고 다시 읽어 검증했다.

재현: `Tools/Audio/fetch_campaign_music.py`, `prepare_campaign_music.py`, `Tools/Validation/ImportCampaignMusic20261001.cs`.
원본/출력 SHA256·LUFS·피크·경계 수치: `ArtDirection/Review-20261001/campaign-music-import.json`.
환경음 원본 식별: 같은 폴더 `campaign-ambience-import.json`.
이전 소스·6개 WAV·meta: `ArtDirection/Review-20261001/before-music`.
구형 `Tools/Create-CampaignAudio.py` 출력은 비활성 아카이브로 돌려 새 음악 덮어쓰기를 방지했다.

## 검증 기록

임포터 적용 결과: `Logs/Validation/2026-10-01-campaign-music/import.json` (6클립 모두 스테레오 44.1kHz, Streaming).
실행 검증은 `CampaignAudioValidation`을 명시적으로 실행할 때만 동작한다. 메뉴에서 공개 음악 전환 API와 실제 AudioSource·DSP·출력 콜백을 검사한다. 사용자 슬롯을 열거나 정상 캠페인을 진행하는 검사가 아니다. 결과·출력 WAV는 같은 로그 폴더의 실행별 하위 폴더에 보존한다.

음악 교체는 전체 게임 완성·3회 정상 완주·60fps 검증의 증거가 아니다. 보스 전용 음악이나 끝까지 플레이하며 수행하는 최종 믹싱 검수는 이번 세 구역 탐험곡 교체에 포함되지 않는다.

### 최종 실행 증거

- Editor `editor-102428`:32개 assertion PASS. 초기 요약33은 INFO행1개까지 센 값이며 집계 코드를 수정했다.24.875초48kHz 스테레오 출력 WAV, 전체 피크0.14255·클리핑0.
- Windows AG `windows-103446`:32개 assertion PASS, 정상 자동 종료 exit0.24.811초48kHz 스테레오 실제Unity출력, 전체 피크0.16928·클리핑0. 정상 캠페인 자동 플레이를 수행한 결과는 아니다.
- 최종 빌드 `build_e56af03af323`, `Builds/CampaignCandidate-20261001-AG/RuinRunner.exe`:성공, 오류0/기존경고3. 런타임 어셈블리SHA256·컴파일 소스 해시·빌드 요약은 오디오 검증 폴더에 보존.
- 첫 숨김 창 검사는 기본 runInBackground=false로 중단됐고, 두 번째는32검사 이후 배경정지 복원 때문에 종료가 지연됐다. 두 실행과 로그를 삭제하지 않았다. 검증시에만 백그라운드 실행을 켜며, Editor에서는 복원하고 종료하는 Windows검사에서는 quit처리까지 유지하도록 수정했다. 일반 게임 설정은 유지한다.
- 마지막UI `runtime-102643`에서 기존 장비/정비/이미지 처리99검사도 통과했다. 여백 툴팁 잔상 제거를 포함한다.
- 미리듣기 MP3는 `ArtDirection/Review-20261001/music-preview`의25/35/25초 발췌본이다. 발췌 미리듣기에만 짧은 시작/끝 페이드를 넣었다. 실제 게임은 원곡 전체길이의WAV를 Unity에서 스트리밍한다.
