"""Read-only source inspection; enlarged labelled tiles are QA output, not game art."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

root = Path(__file__).resolve().parents[1]
out = root / 'Logs/Validation/DungeonGathering'
out.mkdir(parents=True, exist_ok=True)
for name in ['Set 1.0', 'Set 1.1', 'Set 1.2', 'Set 4.5', 'Water', 'Torch Yellow', 'Vase Shine Anim']:
    src = Image.open(root / 'Dungeon Gathering Free Version' / (name + '.png')).convert('RGBA')
    cols, rows = src.width // 16, src.height // 16
    sheet = Image.new('RGB', (cols * 80, rows * 88), '#171922')
    draw = ImageDraw.Draw(sheet)
    for y in range(rows):
        for x in range(cols):
            tile = src.crop((x*16, y*16, x*16+16, y*16+16)).resize((64,64), Image.Resampling.NEAREST)
            sheet.paste(tile, (x*80+8,y*88+18), tile)
            draw.text((x*80+5,y*88+2), f'{x},{y}', fill='white')
    sheet.save(out / (name + '-grid.png'))
    print(out / (name + '-grid.png'))

# Audit the runtime exports against their source cells; no pixel rewriting.
rules = {}
def rule(name, source, x, y):
    rules[name] = (source, x, y)

for name, x, y in [
    ('Floor/plain',7,6), ('Floor/checker',4,6), ('Floor/cracks',9,6),
    ('Floor/bricks',8,6), ('Floor/engraved',7,5), ('Props/rubble',10,6),
    ('Props/stones',11,6), ('Props/urn',1,5), ('Wall/pillar_top',3,0),
    ('Wall/pillar_middle',3,1)
]:
    rule(name, 'Set 1.1', x, y)
for i in range(3):
    rule(f'Wall/face_{i}', 'Set 1.1', 3+i, 2)
    rule(f'Wall/base_{i}', 'Set 1.1', 3+i, 3)
    rule(f'Stairs/{i}', 'Set 1.1', 16, i)
names = ['tl','t','tr','l','c','r','bl','b','br']
for y, sy in enumerate([0,1,3]):
    for x, sx in enumerate([4,5,7]):
        rule('Trim/' + names[y*3+x], 'Set 1.2', sx, sy)
for x in range(3):
    rule('Pool/' + names[x], 'Set 4.5', x, 0)
    rule('Pool/' + names[3+x], 'Set 4.5', x, 1)
    rule('Pool/' + names[6+x], 'Set 4.5', x, 0)
for source, folder, count in [('Torch Yellow','Torch',8), ('Water','Ripple',8), ('Coin Sheet','Coin',8), ('Vase Shine Anim','Urn',16)]:
    for frame in range(count):
        rule(f'{folder}/{frame:02}', source, frame, 0)
runtime = root / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/DungeonGathering'
report = []
for name, (source, x, y) in rules.items():
    expected = Image.open(root / 'Dungeon Gathering Free Version' / (source+'.png')).convert('RGBA').crop((x*16,y*16,x*16+16,y*16+16))
    actual = Image.open(runtime / (name+'.png')).convert('RGBA')
    identical = actual.size == (16,16) and actual.tobytes() == expected.tobytes()
    report.append({'file':name+'.png','source':source+'.png','cell':[x,y],'pixel_exact':identical})
    assert identical, f'Export differs from source: {name}'
assert len(list(runtime.rglob('*.png'))) == len(rules) == 77
(out / 'source-pixel-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('PASS: 77 individual PNGs exactly match original source RGBA cells.')
