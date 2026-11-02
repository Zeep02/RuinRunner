# 제공 캐릭터 임시 테스트

- 원본: `ArtDirection/UserProvided/Hero-20260910/original.png` (887×1774 RGBA).
- 사용자가 제공한 8행 × 4열을 그대로 사용. 새로운 이미지 생성, 색상 변경, 좌우 복제, 프레임 재생성 없음.
- 위에서부터 북 / 북동 / 동 / 남동 / 남 / 남서 / 서 / 북서로 연결.
- 원본의 불균일한 행 간격을 측정해 분리. 프레임마다 별도 256×256 PNG, 같은 176 PPU 및 피벗 (128, 76).
- 색상·알파는 추출 영역의 원본 픽셀을 그대로 복사. 비율 변경이나 프레임별 확대/축소 없음. 발 기준 정렬만 적용.
- 런타임 리소스: `Assets/RuinRunner/Resources/Art/Pixel/Separated/UserHero/Player/move/{방향}/{프레임}.png`.
- 기존 `Demo/Player` 이미지와 기존 Windows 배포본은 보존.

## 실행

임시 빌드: `Builds/UserHeroDemoTestV2/RuinRunnerPrototype.exe`.

창모드 화면은 1280×720이며 테두리 크기 조절을 막음. 전체화면에서 돌아와도 같은 크기를 사용.
Dungeon Gathering 타일을 연결한 데모 던전에서 테스트하며, 몬스터·보스 이미지는 이번 캐릭터 시트 교체 대상이 아님.

## 재현

1. Unity 메뉴 `Prototype/Art/Bake User Hero`로 32개 프레임 추출.
2. `python Tools/verify-user-hero.py`: 32개 프레임 고유성, 원본 RGBA 일치, 투명 여백 검사.
3. 별도 표시 창에서 `--motion-validation`: 8방향 각각 4프레임 및 이동·공격·대시 회귀 검사.
4. 별도 표시 창에서 `--smoke-test`: 캐릭터 리소스, 맵 충돌, 인벤토리 등 초기화 검사.

자동 검증은 실제 사용자 저장 슬롯을 시작하지 않는 메모리 임시 런을 사용한다.
