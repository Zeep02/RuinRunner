"""Draw a standalone G-marked currency icon at native resolution, without APIs."""
from pathlib import Path
import re
import uuid
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/HUD/Gold.png'
SOURCE = ROOT / 'Tools/Art/Staging/Authored/20261104-gold-ui'
WIDTHS = [6, 10, 12, 12, 14, 14, 14, 14, 14, 14, 12, 12, 10, 6]
GLYPH = ['.GGGG', 'G....', 'G....', 'G.GGG', 'G...G', 'G...G', '.GGG.']
PALETTE = {'s': '#805021', 'b': '#cf8b26', 'g': '#ffd344',
           'h': '#ffe780', 'w': '#fffbc4', 'G': '#633b20'}

SOURCE.mkdir(parents=True, exist_ok=True)
icon = Image.new('RGBA', (16, 16))
draw = ImageDraw.Draw(icon)
for y, width in enumerate(WIDTHS, start=1):
    left = (16-width)//2
    right = left+width-1
    for x in range(left, right+1):
        color = 'g'
        if y in (1, 14) or x in (left, right):
            color = 's'
        elif y == 2 or x == left+1:
            color = 'w' if y < 8 else 'h'
        elif y >= 12 or x == right-1:
            color = 'b'
        elif y == 3:
            color = 'h'
        draw.point((x, y), fill=PALETTE[color])
for y, row in enumerate(GLYPH, start=4):
    for x, pixel in enumerate(row, start=5):
        if pixel == 'G':
            draw.point((x, y), fill=PALETTE['G'])
icon.save(OUTPUT)
assert icon.getbbox() == (1, 1, 15, 15)
assert set(icon.getchannel('A').getdata()) == {0, 255}
meta = Path(str(OUTPUT) + '.meta')
if not meta.exists():
    template = ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/Feedback/Gold/00.png.meta'
    data = template.read_text(encoding='utf-8')
    data = re.sub(r'(?m)^guid: \w+', 'guid: ' + uuid.uuid4().hex, data)
    data = re.sub(r'spriteID: \w+', 'spriteID: ' + uuid.uuid4().hex, data)
    meta.write_text(data, encoding='utf-8')
# Only the review image is enlarged. The delivered icon remains native 16x16.
preview = Image.new('RGBA', (16, 16), '#24313a')
preview.alpha_composite(icon)
preview.resize((128, 128), Image.Resampling.NEAREST).save(SOURCE / 'preview.png')
(SOURCE / 'source.txt').write_text(
    '2026-11-04: Locally authored pixel rows; no external image API or artwork.\n'
    'Source: Tools/Art/author_gold_ui.py\n'
    'Standalone UI currency icon: 16x16 RGBA, 14x14 visible, dark 5x7 G, top-left light.\n'
    '32 PPU / Point / no mipmaps / uncompressed. UI wiring not changed.\n',
    encoding='utf-8')
print(f'Created {OUTPUT}: 16x16 RGBA, 14x14 visible pixels, 5x7 G.')
