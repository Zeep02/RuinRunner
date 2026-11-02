"""Read-only asset audit; writes comparison previews only under Logs/Validation."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

root = Path(__file__).resolve().parents[1]
out = root / 'Logs/Validation/FixedDemoArt'
out.mkdir(parents=True, exist_ok=True)
hero = root / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/Demo/Player/move'
icons = root / 'Assets/RuinRunner/Resources/Art/Pixel/Items/Demo'
canvas = Image.new('RGB', (1056, 820), '#313944')
draw = ImageDraw.Draw(canvas)
audit = []
for direction in range(8):
    for frame in range(4):
        path = hero / f'{direction:02}/{frame:02}.png'
        im = Image.open(path).convert('RGBA')
        alpha = im.getchannel('A')
        bounds = alpha.getbbox()
        colors = {c for c in im.getdata() if c[3]}
        assert bounds and bounds[3] == 36, (path, bounds)
        assert len(colors) <= 14
        assert set(alpha.getdata()) == {0, 255}
        audit.append({'path': str(path.relative_to(root)), 'bounds': bounds, 'colors': len(colors)})
        x, y = direction * 130 + 8, frame * 150 + 18
        canvas.paste(im.resize((120, 120), Image.Resampling.NEAREST), (x, y), im.resize((120, 120), Image.Resampling.NEAREST))
        draw.text((x+4, y+120), f'{direction}:{frame}', fill='white')
paths = sorted(icons.rglob('*.png'))
assert len(paths) == 12
for i, path in enumerate(paths):
    im = Image.open(path).convert('RGBA')
    alpha = im.getchannel('A')
    b = alpha.getbbox()
    assert set(alpha.getdata()) == {0, 255}
    assert b[0] > 0 and b[1] > 0 and b[2] < 32 and b[3] < 32
    assert abs((b[0] + b[2])/2 - 16) <= 1 and abs((b[1]+b[3])/2 - 16) <= 1, (path, b)
    colors = {c for c in im.getdata() if c[3]}
    assert len(colors) <= 10
    audit.append({'path': str(path.relative_to(root)), 'bounds': b, 'colors': len(colors)})
    x, y = (i % 12)*87 + 8, 665
    zoom = im.resize((80, 80), Image.Resampling.NEAREST)
    canvas.paste(zoom, (x,y), zoom)
    draw.text((x,y+85), path.stem[:12], fill='white')
canvas.save(out / 'demo-art-contact.png')
(out / 'audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
print(f'PASS: {len(audit)} separate images; alpha, palette, padding and center/foot alignment.')
