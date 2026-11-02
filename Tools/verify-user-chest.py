"""Read-only verification: original RGBA content, common base and individual import settings."""
import csv
from pathlib import Path
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[1]
source = np.array(Image.open(root / 'ArtDirection/UserProvided/Chest-20260910/original.png').convert('RGBA'))[::-1]
manifest = root / 'ArtDirection/UserProvided/Chest-20260910/extraction.csv'
rows = list(csv.DictReader(manifest.open(encoding='utf-8')))
assert len(rows) == 5
for row in rows:
    d = {key: int(value) for key, value in row.items()}
    path = root / f"Assets/RuinRunner/Resources/Art/Pixel/Separated/UserChest/{d['frame']:02}.png"
    actual = np.array(Image.open(path).convert('RGBA'))[::-1]
    expected = np.zeros((512, 512, 4), dtype=np.uint8)
    x, y, w, h = [d[key] for key in ('sourceX', 'sourceY', 'width', 'height')]
    dx, dy = 256 + x - d['anchorX'], 24 + y - d['baseY']
    expected[dy:dy+h, dx:dx+w] = source[y:y+h, x:x+w]
    assert np.array_equal(actual, expected), f"Frame {d['frame']} changed original pixels"
    assert not actual[[0, -1], :, 3].any() and not actual[:, [0, -1], 3].any()
    cell = source[:, d['frame']*2172//5:(d['frame']+1)*2172//5]
    assert (actual[:, :, 3] > 32).sum() == (cell[:, :, 3] > 32).sum(), 'Visible silhouette was clipped'
    meta = path.with_suffix('.png.meta').read_text()
    assert 'spritePixelsToUnits: 256' in meta and 'enableMipMap: 0' in meta
    print(f"PASS chest frame {d['frame']}: exact cropped RGBA, complete visible silhouette, common base/scale")
print('PASS 5 supplied chest poses; only disconnected near-transparent sheet noise excluded')
