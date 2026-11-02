"""Technical extraction only: preserve full generated fields, export individual tiles.

The fields are sampled once at the game's 16PPU ground density. Their 16x16 tile
coordinates remain adjacent in the game instead of choosing a random picture per
cell. Wall material strips are cropped from the existing approved straight wall.
No pixels are painted or AI source material replaced by procedural illustrations.
"""
from pathlib import Path
import json
from PIL import Image

source = Path('ArtDirection/Generated/ContinuousFloors-20260921')
target = Path('Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Terrain')
report = []
for theme in ['Outer', 'Flooded', 'Core']:
    field = Image.open(source / f'{theme}-expanded.png').convert('RGB')
    field = field.resize((512, 512), Image.Resampling.NEAREST)
    field = field.quantize(colors=32, method=Image.Quantize.MEDIANCUT).convert('RGBA')
    field.save(source / f'{theme}-logical.png')
    folder = target / theme / 'Field'
    folder.mkdir(parents=True, exist_ok=True)
    for y in range(32):
        for x in range(32):
            field.crop((x*16, (31-y)*16, (x+1)*16, (32-y)*16)).save(folder / f'{x:02}-{y:02}.png')
    # Choose fully opaque material interior, independently of the broken old masks.
    wall = Image.open(target / theme / 'Masonry/north.png').convert('RGBA')
    folder = target / theme / 'Ribbon'
    folder.mkdir(parents=True, exist_ok=True)
    for i, x in enumerate([0, 8, 16, 22]):
        for kind, y, height in [('cap', 2, 8), ('face', 10, 16)]:
            part = wall.crop((x, y, x+8, y+height))
            assert part.getchannel('A').getextrema() == (255, 255), (theme, kind, i)
            part.save(folder / f'{kind}-{i}.png')
    report.append(dict(theme=theme, fieldTiles=1024, tilePixels=16, ppu=16,
                       fieldWorldSize=32, wrapPeriod=32, reflection=False, wallParts=8))
(source/'extraction.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report))
