"""Read-only checks of generated asset imports against their unmodified source PNGs."""
from pathlib import Path
from PIL import Image
import numpy as np

root = Path(__file__).resolve().parents[1]
source = root / 'ArtDirection/Generated/ThrustOptions-20260910'
assets = root / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/ThrustOptions'
sheet = np.array(Image.open(source / 'thrust-sheet.png'))
lefts = [0, 416, 876, 0, 552, 1024]
rights = [416, 876, 1536, 552, 1024, 1536]
anchors = [188, 576, 1008, 108, 662, 1116]
rows = [307, 307, 307, 757, 757, 757]
for frame in range(6):
    actual = np.array(Image.open(assets / 'Thrust' / f'{frame:02}.png'))[::-1]
    expected = np.zeros((256, 768, 4), dtype=np.uint8)
    top, bottom = (236, 380) if frame < 3 else (680, 820)
    for y in range(top, bottom):
        for x in range(lefts[frame], rights[frame]):
            dx, dy = 160 + x - anchors[frame], 128 + rows[frame] - y
            if 0 <= dx < 768 and 0 <= dy < 256:
                expected[dy, dx] = sheet[y, x]
            else:
                assert sheet[y, x, 3] <= 32
    assert np.array_equal(actual, expected), f'RGBA changed in frame {frame}'
    assert not actual[[0, -1], :, 3].any() and not actual[:, [0, -1], 3].any()
    print(f'PASS thrust {frame}: source pixels/alpha preserved, aligned, clear edges')
bar = np.array(Image.open(source / 'volume-track.png'))
actual = np.array(Image.open(assets / 'VolumeTrack.png'))
assert np.array_equal(actual, bar[282:441, 110:2062]), 'volume track pixels changed'
print('PASS generated volume image crop preserves exact RGBA')
