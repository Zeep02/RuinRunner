"""Read-only verification of the temporary user-supplied hero extraction."""
import csv
from pathlib import Path
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
inputs = root / "ArtDirection/UserProvided/Hero-20260910"
frames = root / "Assets/RuinRunner/Resources/Art/Pixel/Separated/UserHero/Player/move"
source = np.asarray(Image.open(inputs / "original.png"))
with (inputs / "extraction.csv").open() as handle:
    rows = list(csv.DictReader(handle))
signatures = set()
visible = []
for row in rows:
    d = {key: int(value) for key, value in row.items()}
    frame = np.asarray(Image.open(frames / f"{d['direction']:02d}" / f"{d['frame']:02d}.png"))
    x, w, h = d["sourceX"], d["width"], d["height"]
    y = source.shape[0] - d["sourceYBottom"] - h
    dx, dy = 128 + x - d["anchorX"], 256 - 24 - h
    assert np.array_equal(source[y:y+h, x:x+w], frame[dy:dy+h, dx:dx+w]), d
    assert frame[[0, -1], :, 3].max() == 0 and frame[:, [0, -1], 3].max() == 0, d
    signatures.add(frame.tobytes())
    visible.append(int((frame[:, :, 3] > 32).sum()))
assert len(rows) == len(signatures) == 32
assert all(10000 < count < 45000 for count in visible)
print(f"PASS: {len(rows)} distinct frames, source RGBA copied exactly, transparent margins; visible pixels {min(visible)}..{max(visible)}.")
