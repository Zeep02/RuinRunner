# 개별 아이템 이미지

인벤토리/퀵슬롯/툴팁/필드 드롭/들고 있는 무기는 같은 아이템 PNG를 사용합니다.

- `Weapon/<WeaponDefinition.Id>.png`
- `Passive/<PassiveItemType>.png`
- `Accessory/<AccessoryDefinition.Id>.png`

투명 RGBA PNG를 정사각형 캔버스 중앙에 배치하고, 가장자리에는 약 10% 여백을 둡니다. 색상으로 런타임 배경을 추정하지 않습니다. Point 필터, NPOT 유지, 무압축, mipmap 없음은 에디터 임포터가 적용합니다.

그림을 바꿀 때 해당 PNG만 교체하면 됩니다. 목록 순서나 기존 아틀라스 좌표는 게임의 로딩에 영향을 주지 않습니다. 플레이 중 이미 로드된 텍스처/스프라이트 캐시는 재실행 시 갱신됩니다.

`Prototype > Art > Extract Missing Individual Item PNGs`는 기존 시안에서 빠진 개별 파일만 복원하는 마이그레이션 도구입니다. 이미 교체한 파일은 덮어쓰지 않습니다. 새로운 아이템은 목록 ID에 맞춘 PNG를 직접 추가하세요.

추출 원본/이전 코드는 프로젝트의 기존 아틀라스와 `Backups/2026-09-10-item-depth`에 보존되어 있습니다. 결과 검사는 `--presentation-validation` 및 `Tools/Art/Inspect-IndividualIcons.ps1`을 사용합니다. 검사 결과 모음 이미지는 배포용 아틀라스가 아닙니다.
