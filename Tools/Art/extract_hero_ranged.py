"""Separate generated shooting poses on the existing 48px hero canvas."""
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
import cv2
import json

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtDirection/Generated/HeroRanged-20260920"
OUT = ROOT / "Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/shoot"
source = np.array(Image.open(ART / "original.png").convert("RGBA"))
count, labels, stats, centers = cv2.connectedComponentsWithStats((source[:, :, 3] >= 128).astype("uint8"), 8)
parts = [i for i in range(1, count) if stats[i, 4] > 500]
assert len(parts) == 24, (len(parts), "Expected exactly 24 isolated bodies")
columns = [304, 528, 752]
grounds = [181, 363, 546, 731, 914, 1102, 1280, 1460]
report = []
sheet = Image.new("RGB", (4 * 192, 8 * 208), "#202432")
draw = ImageDraw.Draw(sheet)
for direction in range(8):
    previous = Image.open(ROOT / f"Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/{direction:02}/01.png")
    sheet.paste(previous.resize((192, 192), Image.Resampling.NEAREST), (0, direction * 208 + 16), previous.resize((192, 192), Image.Resampling.NEAREST))
    draw.text((4, direction * 208 + 2), f"{direction} existing move", fill="white")
    for frame in range(3):
        candidates = [i for i in parts if abs(centers[i][0] - columns[frame]) < 65 and abs(stats[i, 1] + stats[i, 3] - grounds[direction]) < 30]
        assert len(candidates) == 1, (direction, frame, candidates)
        component = candidates[0]
        # Common scale and authored anchors, no per-pose fitting/resizing.
        isolated = source.copy()
        isolated[:, :, 3] = np.where(labels == component, 255, 0)
        isolated[isolated[:, :, 3] == 0, :3] = 0
        left, top = columns[frame] - 120, grounds[direction] - 190
        im = Image.fromarray(isolated).crop((left, top, left + 240, top + 240)).resize((48, 48), Image.Resampling.NEAREST)
        bounds = im.getbbox()
        assert bounds and min(bounds[:2]) > 0 and max(bounds[2:]) < 48
        folder = OUT / f"{direction:02}"
        folder.mkdir(parents=True, exist_ok=True)
        im.save(folder / f"{frame:02}.png")
        enlarged = im.resize((192, 192), Image.Resampling.NEAREST)
        sheet.paste(enlarged, ((frame + 1) * 192, direction * 208 + 16), enlarged)
        draw.text(((frame + 1) * 192 + 4, direction * 208 + 2), f"{direction} shoot {frame}", fill="white")
        report.append(dict(direction=direction, frame=frame, component=component, source_bounds=[int(v) for v in stats[component, :4]], anchor=[columns[frame], grounds[direction]], source_canvas=240, output_canvas=48, bounds=bounds))
sheet.save(ART / "contact.png")
(ART / "extraction.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("Separated 24 shooting poses; common 1:5 scale, 48px canvas. Review before adoption.")
