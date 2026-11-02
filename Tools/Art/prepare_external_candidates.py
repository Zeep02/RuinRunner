"""Lossless sprite-sheet separation for review. Does not change runtime assets.

Original pixels, cell canvases and ordering are retained. These are candidates,
not approved replacements. Every output records its exact source rectangle.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib
import json

ROOT = Path(__file__).resolve().parents[2]
PACKS = ROOT / 'ArtDirection/ExternalCandidates/20260921'
OUT = PACKS / 'prepared'
OUT.mkdir(exist_ok=True)
records = []


def split(source, name, size, ppu):
    source = PACKS / source
    sheet = Image.open(source).convert('RGBA')
    w, h = size
    assert sheet.width % w == 0 and sheet.height % h == 0, source
    folder = OUT / name
    folder.mkdir(parents=True, exist_ok=True)
    frames = []
    for y in range(sheet.height // h):
        for x in range(sheet.width // w):
            bounds = (x*w, y*h, (x+1)*w, (y+1)*h)
            frame = sheet.crop(bounds)
            label = f'{y:02}-{x:02}'
            dest = folder / f'{label}.png'
            frame.save(dest)
            # Equality is checked after writing, including transparent RGB.
            assert Image.open(dest).convert('RGBA').tobytes() == frame.tobytes()
            pixels = frame.tobytes()
            records.append(dict(file=dest.relative_to(PACKS).as_posix(),
                                source=source.relative_to(PACKS).as_posix(),
                                rect_top_left=list(bounds), canvas=[w, h],
                                candidate_ppu=ppu, sha256=hashlib.sha256(pixels).hexdigest(),
                                alpha_bounds=frame.getchannel('A').getbbox()))
            frames.append((label, frame))
    # Review output only: full cells at integer scale, never normalize body size.
    scale = 2 if w <= 100 else 1
    columns = min(8, len(frames))
    rows = (len(frames) + columns - 1)//columns
    cw, ch = w*scale + 8, h*scale + 24
    contact = Image.new('RGBA', (columns*cw, rows*ch), (29, 33, 44, 255))
    draw = ImageDraw.Draw(contact)
    for i, (label, frame) in enumerate(frames):
        xx, yy = i % columns*cw, i//columns*ch
        draw.text((xx+4, yy+3), label, fill='white')
        contact.alpha_composite(frame.resize((w*scale, h*scale), Image.Resampling.NEAREST), (xx+4, yy+20))
    contact.save(folder / 'contact.png')


split('cainos-basic/original/Texture/TX Tileset Grass.png', 'Cainos/GrassTiles', (32, 32), 32)
zerie = 'zerie-tiny-rpg/original/Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc/Characters(100x100 split)'
for actor in ('Orc', 'Soldier'):
    for source in sorted((PACKS / zerie / actor / actor).glob('*.png')):
        # The combined overview repeats the named actions and includes padding.
        # It must not inflate the number of candidate animation frames.
        if source.stem == actor:
            continue
        split(source.relative_to(PACKS), f'Zerie/{actor}/{source.stem}', (100, 100), 32)
tiny = 'tiny-swords/original/Tiny Swords (Free Pack)'
for source in sorted((PACKS / tiny / 'Particle FX').glob('*.png')):
    height = Image.open(source).height
    split(source.relative_to(PACKS), f'TinySwords/FX/{source.stem}', (height, height), 64)

manifest = dict(status='review-only; no runtime adoption', transforms='lossless crop only; no resizing or recoloring',
                excluded='Combined Orc/Soldier overview sheets duplicate named actions. Any prior review exports remain unreferenced.',
                records=records)
(OUT / 'separation-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(dict(individual_pngs=len(records), runtime_changes=False, output=str(OUT))))
