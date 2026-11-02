---
name: ruinrunner-pixel-assets
description: RuinRunner(LoglikeCap) 픽셀 에셋 파이프라인. PixelLab MCP로 캐릭터·적·보스·아이템·타일을 원본 크기로 생성하고, pixelfx로 전투 이펙트를 만들어 Unity CombatFX로 내보낸다. 새 스프라이트, 애니메이션, 타일셋, VFX, 타격감(히트스톱) 작업이나 PixelLab·pixelfx 언급 시 사용한다.
---

# RuinRunner 픽셀 에셋 파이프라인 (PixelLab + pixelfx)

두 도구의 역할을 나눈다.

| 무엇 | 도구 | 결과 위치 |
|---|---|---|
| 캐릭터·적·보스(8방향, 걷기/공격 애니메이션), 아이템·장신구 아이콘, 지형 타일셋, 맵 오브젝트 | **PixelLab MCP** (`pixellab` 서버) | `Tools/Art/Staging/PixelLab/<날짜>-<이름>/` → 검수 후 `Resources/Art/Pixel/Separated/...` |
| 타격·폭발·마법·상태이상·획득 연출 등 VFX, 공격 타이밍(윈드업·히트스톱) | **pixelfx** (`Tools/pixelfx`, `pixelfx` 스킬) | `Tools/Art/Staging/PixelFx/<Clip>/` → `--install` 로 `Separated/CombatFX/<Clip>/` |

## 0. 먼저 지킬 프로젝트 규칙

`ArtDirection/STYLE_GUIDE.md` 와 `AGENTS.md` 가 우선한다. 핵심만:

- **원본 크기 우선.** 쓸 캔버스·가시 실루엣 크기를 먼저 정하고 그 크기로 만든다. 큰 결과를 축소하거나 작은 결과를 확대하지 않는다. 크기가 틀리면 다시 생성한다. (PixelLab 은 정확한 출력 크기를 지정할 수 있어 이 규칙을 지킬 수 있다. pixelfx 도 원본 픽셀로 그린다.)
- 32 PPU, Point, 무압축, 밉맵 없음. 월드 구도 640×360.
- 크기 기준: 플레이어 가시 몸 높이 28~34px(48×64 캔버스, 발 24,22), 일반 적 20~36px, Warden 72~88px, 신규 보스 64~96px, 한손검 20~28px, 대검 32~42px, 상자 28~36px, 필드 아이템 16~24px, 지형 1칸 32px.
- 광원 좌상단, 배우 3~4단계 명암, 진한 남색/갈색 1px 윤곽. 스테이지 색: 외곽 이끼·따뜻한 석재, 회랑 물·어두운 청록, 심부 먹빛·황금 봉인.
- 같은 작가의 무료 에셋이 있으면 그것이 먼저다(`ASSET_SOURCES.md`). 생성물은 **시안**이며 실제 게임 화면(보이는 창)에서 확인한 뒤에만 적용 완료로 기록한다.
- 특정 게임(세피리아 포함)의 스프라이트·캐릭터·로고를 복제하지 않는다. 참고는 형태·색면·여백 기준으로만 쓴다.
- 소스·출처·라이선스 기록을 지우지 않는다.

## 1. PixelLab 으로 스프라이트 만들기

PixelLab MCP 도구가 보이지 않으면 아래 "설정" 을 먼저 확인한다.

1. **크기와 용도를 정한다.** 위 표에서 캔버스와 가시 크기를 고르고 프롬프트에 적는다.
2. **참조를 준다.** 사용자의 기준 이미지(`ArtDirection/References/UnifiedPixel/01.png` 등)나 이미 채택된 스프라이트를 참조·팔레트 입력으로 넘긴다. 팔레트를 hex 로 받는 도구에는 `node Tools/Art/palette-from-png.mjs <채택 PNG> --count 8` 결과를 쓴다.
3. **생성.** 대표 도구:
   - `create_character` (8방향 `n_directions: 8`) → `animate_character` (walk, idle, attack ...). 비동기이므로 상태를 확인하며 기다린다.
   - `create_topdown_tileset` (32px), `create_map_object`, `create_image_pixflux` / `create_image_pro` (아이템·아이콘, width/height 정확히 지정, 투명 배경).
   - `edit_image`, `inpaint_image`, `animate_image` 로 부분 수정·프레임 추가.
4. **내려받아 스테이징에 보관한다.** `Tools/Art/Staging/PixelLab/<YYYYMMDD>-<이름>/` 에 원본 파일을 그대로 두고, 같은 폴더에 `request.json` 을 쓴다: 도구 이름, 전체 파라미터(프롬프트·크기·방향·seed), 반환된 id, 날짜.
5. **검수.** 실제 크기를 확인한다(가시 픽셀 bbox). 기준과 다르면 재샘플링하지 말고 다시 생성한다. 실제 게임 배율로 다른 배우·배경과 나란히 비교한다.
6. **채택.** `Assets/RuinRunner/Resources/Art/Pixel/Separated/` 의 맞는 하위 폴더에 프레임별 PNG 로 복사하고 PPU 32·피벗(발/손 소켓)을 기존 같은 종류 에셋과 맞춘다. `ASSET_SOURCES.md` 와 해당 `Docs/*.md` 에 PixelLab 생성물임과 request.json 경로, 사용자의 PixelLab 요금제/이용약관 상태를 기록한다.

## 2. pixelfx 로 이펙트 만들기

자세한 제작 방법은 `pixelfx` 스킬을 따른다. 프로젝트 쪽 흐름:

1. `cd Tools/pixelfx && node tools/list.mjs <단어>` 로 후보를 찾고 `node tools/render.mjs <id>` 접촉표를 본다. 새로 만들면 `src/fx/<category>/` 에 쓴다.
2. **색을 게임에 맞춘다.** 둘 중 하나:
   - PAL 이름: `--opts '{"palette":"ice"}'` (fire ember lava smoke dust ice water electric volt poison holy heal arcane shadow blood earth stone gold leaf wood metal bone ...)
   - 스프라이트에서 추출: `--palette-from <PNG> [--hue 180-260] [--palette-count 7]`. PixelLab 으로 만든 적·보스의 색면에서 그 적 전용 이펙트 색을 뽑는 것이 두 도구를 잇는 기본 방법이다.
3. **내보내기(스테이징).** 프로젝트 루트에서:
   ```
   node Tools/Art/pixelfx-to-unity.mjs <effect-id> <ClipName> [--fps 15] [--seed N] [--palette-from ...]
   ```
   `Tools/Art/Staging/PixelFx/<ClipName>/` 에 투명 개별 프레임 `00.png...`, `_pixelfx.json`(출처·seed·옵션·피벗·가시 지름), `contact.png` 가 생긴다. 모든 프레임은 하나의 공통 크롭이며 피벗은 이펙트 원점(`--center` 면 중심)이다.
4. **검수.** `contact.png` 를 이미지로 열어 본다. 화면 크기와 비교: 32 PPU 에서 가시 지름 D px 는 D/32 유닛이다. 너무 크면 이펙트의 `size`/`radius` 같은 옵션으로 원본에서 작게 다시 그린다(축소 금지).
5. **설치.** 같은 명령에 `--install` 을 붙인다 → `Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX/<ClipName>/`. `Assets/RuinRunner/Editor/PixelFxClipImporter.cs` 가 `_pixelfx.json` 을 읽어 Sprite·PPU·피벗·Point·무압축을 설정한다(메뉴 `Prototype/Art/Reimport pixelfx CombatFX Clips` 로 재적용).
6. **코드 연결.** 스크립트가 출력한 `{"<ClipName>",<D>}` 를 `Runtime/Feedback/NativeCombatEffect.cs` 의 `diameters` 에 추가하고 `NativeCombatEffect.Create("<ClipName>", point, angle, worldDiameter, lifetime, color, ...)` 로 재생한다. 정렬은 기존 규칙(근접 검광은 손무기 앞·몸 뒤, 지면 먼지는 뒤)을 따른다.
7. **타격감.** 이펙트에 `act` 가 있으면 `_pixelfx.json` 의 fps·프레임 수와 함께 `node -e` 로 `act` 의 `windup`, `hits[].t`, `freeze` 를 읽어 플레이어 공격 진행도·히트스톱에 맞춘다. 피해 판정 시점을 이펙트에 맞춰 옮기지 말고, 이펙트를 기존 판정 시계에 맞춘다.
8. **실제 확인.** 보이는 게임 창에서 세 스테이지 배경 위에 재생해 보고 검증 기록을 `Logs/Validation/<날짜>-<주제>/` 와 `WORK_STATUS.md` 에 남긴다.

## 3. 설정 (한 번만)

PixelLab 토큰은 pixellab.ai 계정에서 발급한다. **토큰을 파일에 쓰지 않는다.** 환경 변수로만 둔다.

- Windows: `setx PIXELLAB_API_TOKEN "<토큰>"` 후 터미널·에디터를 다시 연다.
- Claude Code: 프로젝트 `.mcp.json` 의 `pixellab` 서버가 `${PIXELLAB_API_TOKEN}` 을 읽는다. 처음 열 때 프로젝트 MCP 서버 사용을 승인한다. `/mcp` 로 연결 확인.
- Codex: 프로젝트 `.codex/config.toml` 의 `[mcp_servers.pixellab]` 가 `bearer_token_env_var = "PIXELLAB_API_TOKEN"` 을 쓴다. 프로젝트가 신뢰됨이 아니면 같은 블록을 `~/.codex/config.toml` 에 넣는다.
- pixelfx 업데이트: GitHub 에서 새 버전을 받아 `Tools/pixelfx` 를 교체하고 `VENDORED_FROM.txt` 의 커밋을 갱신한다. 직접 만든 `src/fx` 이펙트는 먼저 옮겨 둔다.
