"""Read-only source/crop validation of the supplied boss's 64 imported poses."""
import csv
from pathlib import Path
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
inputs = root / "ArtDirection/UserProvided/Boss-20260910"
source = np.asarray(Image.open(inputs / "original.png"))
frames = root / "Assets/RuinRunner/Resources/Art/Pixel/Separated/UserBoss"
with (inputs / "extraction.csv").open() as handle:
    rows = [{k: int(v) for k, v in row.items()} for row in csv.DictReader(handle)]
signatures = set()
for d in rows:
    frame = np.asarray(Image.open(frames / f"{d['row']:02d}" / f"{d['frame']:02d}.png"))
    x, w, h = d['sourceX'], d['width'], d['height']
    y = source.shape[0] - d['sourceYBottom'] - h
    dx, dy = 128 + x - d['anchorX'], 256 - 24 - h
    output = frame[dy:dy+h, dx:dx+w]
    mask = output[:, :, 3] > 0
    assert np.array_equal(output[mask], source[y:y+h, x:x+w][mask]), d
    assert (frame[:, :, 3] > 32).sum() == d['visiblePixels'], d
    assert frame[[0, -1], :, 3].max() == 0 and frame[:, [0, -1], 3].max() == 0, d
    signatures.add(frame.tobytes())
assert len(rows) == len(signatures) == 64
print('PASS: 64 distinct poses, exact source RGBA for retained pixels, all body pixels present, transparent margins.')
