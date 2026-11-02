"""Read-only native capture check: visible icon bounds, center and original opaque pixels."""
from pathlib import Path
import sys, json, math
import numpy as np
from PIL import Image

root = Path(__file__).resolve().parents[2]
folder = Path(sys.argv[1]).resolve()
windows = len(sys.argv) > 2 and sys.argv[2] == 'windows'
names = ['31-centered-quickbar-ammo.png', '32-stacked-reload-gauge.png', '33-centered-inventory-quickbar.png'] if windows else sorted(p.name for p in folder.glob('*.png') if p.name.startswith(('hud-', 'reload-', 'inventory-')))
rows = []
for name in names:
    screen = np.asarray(Image.open(folder / name).convert('RGB'))
    height, width = screen.shape[:2]
    scale = max(.5, math.floor(min(width / 1280, height / 720) * 2) * .5)
    vw, vh = width / scale, height / scale
    inventory = 'inventory' in name
    for index, weapon in enumerate(['rust_sword', 'storm_rifle', 'dragon_cannon']):
        source = Image.open(root / ('Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon/' + weapon + '.png')).convert('RGBA')
        alpha = np.asarray(source)[:, :, 3]
        yy, xx = np.nonzero(alpha >= 128)
        source = source.crop((xx.min(), yy.min(), xx.max() + 1, yy.max() + 1))
        cx, cy = (vw / 2 - 375 + index * 205, vh / 2 - 199) if inventory else (vw / 2 - 92 + index * 92, vh - 50)
        area = (52, 52) if inventory else (40, 34)
        factor = min(area[0] * scale / source.width, area[1] * scale / source.height)
        factor = math.floor(factor) if factor >= 1 else factor
        sw, sh = round(source.width * factor), round(source.height * factor)
        expected = np.asarray(source.resize((sw, sh), Image.Resampling.NEAREST))
        left, top = round(cx * scale - sw / 2), round(cy * scale - sh / 2)
        actual = screen[top:top + sh, left:left + sw]
        mask = expected[:, :, 3] == 255
        wrong = int(np.count_nonzero(np.max(np.abs(actual.astype(int) - expected[:, :, :3].astype(int)), axis=2)[mask] > 3))
        delta = [abs(left + sw / 2 - cx * scale), abs(top + sh / 2 - cy * scale)]
        rows.append(dict(capture=name, weapon=weapon, opaquePixels=int(mask.sum()), wrongPixels=wrong, centerErrorPixels=delta))
proof = dict(result='PASS' if all(r['wrongPixels'] == 0 and max(r['centerErrorPixels']) <= .5 for r in rows) else 'FAIL', icons=len(rows), rows=rows,
             scope='Real native UI pixels at the geometrical slot center. Original opaque icon pixels include the outline. No screenshots altered; no estimated centroid matching.')
(folder / 'quickslot-pixel-qa.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in proof.items() if k not in ('rows', 'scope')}))
assert rows and proof['result'] == 'PASS', 'Inspect source pixels and layout before accepting.'
