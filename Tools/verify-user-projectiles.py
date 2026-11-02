"""Verify direct slices and retained source alpha. No image modifications."""
from pathlib import Path
from PIL import Image
import numpy as np

root = Path(__file__).resolve().parents[1]
original = np.array(Image.open(root / 'ArtDirection/UserProvided/BossProjectiles-20260910/original.png'))
folder = root / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/UserBossProjectiles'
files = list(folder.glob('*/*.png'))
assert len(files) == 16, 'Only first two rows may be imported'
for row, kind in enumerate(('Radial', 'Aimed')):
    for frame in range(8):
        path = folder / kind / f'{frame:02}.png'
        actual = np.array(Image.open(path))
        expected = original[row*256:(row+1)*256, frame*256:(frame+1)*256]
        assert np.array_equal(actual, expected), (kind, frame, 'changed original pixels/alpha')
        meta = path.with_suffix('.png.meta').read_text()
        assert 'spritePixelsToUnits: 320' in meta and 'enableMipMap: 0' in meta
        print(f'PASS {kind}/{frame:02}: exact original RGBA, 320 PPU')
print('PASS 16 supplied projectile frames; third row unused')
