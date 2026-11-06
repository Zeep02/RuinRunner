#!/usr/bin/env python3
"""pixelfx 클립 불러오기 설정 검사. 빛은 투명 픽셀의 RGB에 있어서 alphaIsTransparency가 켜지면 색이 번져 네모로 보인다."""
import pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parents[2] / "Assets/RuinRunner/Resources/Art/Pixel/Separated/CombatFX"
bad = [meta for clip in ROOT.iterdir() if (clip / "_pixelfx.json").exists() for meta in clip.glob("*.png.meta")
       if b"alphaIsTransparency: 1" in meta.read_bytes()]
for meta in bad[:20]:
    print(meta.relative_to(ROOT))
print(len(bad), "frames with alphaIsTransparency on")
sys.exit(1 if bad else 0)
