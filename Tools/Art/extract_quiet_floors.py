"""Export newly authored floor fields at actor/wall density, not 2x coarse pixels.
Pure source extraction and no-dither palette export; topology and placement stay
in the existing 32-unit field. Does not synthesize or draw replacement artwork.
"""
from pathlib import Path
from PIL import Image
import json

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'ArtDirection/Generated/QuietFloors-20260921'
TARGET=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Terrain'
report=[]
for theme in ['Outer','Flooded','Core']:
    raw=Image.open(SOURCE/f'{theme}-original.png').convert('RGB')
    field=raw.resize((1024,1024),Image.Resampling.NEAREST)
    field=field.quantize(colors=32,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGBA')
    field.save(SOURCE/f'{theme}-logical.png')
    for y in range(32):
        for x in range(32):
            field.crop((x*32,(31-y)*32,(x+1)*32,(32-y)*32)).save(TARGET/theme/'Field'/f'{x:02}-{y:02}.png')
    report.append(dict(theme=theme,source=list(raw.size),field=[1024,1024],tiles=1024,
                       tilePixels=32,ppu=32,worldCoverage=32,colors=32,dither=False))
(SOURCE/'extraction.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
