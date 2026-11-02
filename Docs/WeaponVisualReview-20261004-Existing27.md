# 기존 27무기 정지 자세 직접 검토 — 2026-10-04

검토 자료: `Logs/Validation/2026-10-04-weapon-redesign/114338/views-*.png`. 다음 27개 파일을 모두 직접 열어 북·북동·동·남동·남·남서·서·북서 8방향, **216개 정지 캡처**를 확인했다. 새 15종과 공격/이동 프레임은 이 검토에 포함하지 않았다. 자동 fixture PASS를 시각 판정으로 사용하지 않았다. 원본 에셋은 수정하지 않았다.

- 단검: worn_dagger, wind_dagger, shadow_fang
- 검: rust_sword, knight_sword, sun_oath
- 대검: iron_greatsword, dragon_slayer, blood_reaver
- 카타나: training_katana, moon_blade, blood_moon_katana
- 둔기: iron_mace, iron_hammer, quake_mace
- 총기: scrap_pistol, flintlock, void_revolver, scrap_shotgun, scattergun, storm_rifle, starbreaker, dragon_cannon, twin_pistols
- 쇠뇌: hand_crossbow, repeater, siege_crossbow

## 관찰

캡처에서 손과 떨어진 무기, 팔 아래의 독립된 도트, 좌우의 명백한 방향 뒤집힘은 발견하지 못했다. 남쪽 계열도 손 연결점은 허리 위쪽에 있고, 칼날/총신이 아래로 향한다. 굵은 검은 외곽선과 픽셀 단위가 총기와 가까워졌고, 카타나 3종은 얇은 곡선 칼날, 대검은 넓은 칼날로 구분된다. 정지 화면에 대한 관찰이며 모든 상황의 시각 완성을 뜻하지 않는다.

잔여 시각 판단:

- `blood_reaver`: 남·남서·서에서 큰 초록 가드가 손/허리와 뭉쳐 실제 쥔 지점이 덜 선명하다. 떨어진 파츠는 아니지만 장식이 그립을 가린다.
- `dragon_slayer`: 검은 타원과 별 문양이 두드러져 베는 칼날보다 장식 봉에 가까워 보일 여지가 있다. 대검의 기능적 형태를 가장 잘 보여주는 것은 `iron_greatsword`다.
- `wind_dagger`: 작은 결정형 장식의 존재감이 강해 단검 형태의 가독성이 낮다. 손 연결 결함은 아니다.
- 쇠뇌 3종은 총기보다 선과 디테일이 얇고 촘촘하다. 사용자가 말한 총기의 굵은 픽셀 스타일과는 여전히 차이가 있다. 이번 근접 무기 교체 범위와 구분해 기록한다.

공격 궤적·이동 중 손목·연속 프레임의 깜빡임 여부는 이 정지 자세 리뷰에서 검증하지 않았다.
