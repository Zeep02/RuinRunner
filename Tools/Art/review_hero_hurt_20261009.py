"""Read-only contact sheets from native candidate PNGs; no runtime art editing."""
from pathlib import Path
import argparse
import hashlib
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--revision', default='v2')
parser.add_argument('--full', action='store_true')
parser.add_argument('--variant', type=int, default=0)
parser.add_argument('--views', default='up,right,down')
parser.add_argument('--skip-audit', action='store_true')
args = parser.parse_args()
assert args.revision in ('v2', 'v3', 'v4', 'v5'), args.revision
assert 0 <= args.variant < (16 if args.full else 1)
CANDIDATE = ROOT / f'ArtDirection/Generated/SscaryMotion-20261009/hurt-native-{args.revision}-{"full" if args.full else "preview"}'
OUT = ROOT / ('ArtDirection/Review-20261009/hero-hurt-native' + ('' if args.revision == 'v2' else f'/{args.revision}'))
OUT.mkdir(parents=True, exist_ok=True)
manifest = json.loads((CANDIDATE / 'manifest.json').read_text(encoding='utf-8-sig'))
records = {(r['profile'], r['direction'], r['key']): r for r in manifest['frames'] if r['variant'] == args.variant}
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 22)
small = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 18)
profiles = ['Dagger', 'Sword/Katana', 'Heavy melee', 'Bow', 'Pistol', 'Two-hand gun']
if manifest.get('profiles', 6) == 7: profiles.append('Unarmed')
proof = {'status': 'Candidate contact sheets only; no motion, import, or acceptance claim', 'sheets': []}
scale, left, cell_w, row_h, top = 4, 175, 192, 282, 94

views = {'up':0, 'right-up':1, 'right':2, 'right-down':3, 'down':4, 'left-down':5, 'left':6, 'left-up':7}
for view in args.views.split(','):
    direction=views[view]
    sheet = Image.new('RGB', (left + 10 * cell_w + 14, top + len(profiles) * row_h + 56), (23, 25, 30))
    draw = ImageDraw.Draw(sheet)
    draw.text((15, 10), f'Native hurt {args.revision} / {view} / native variant {args.variant} / source at left / 4x nearest', font=font, fill=(235, 237, 244))
    draw.text((15, 40), 'Face and backpack retained; source foot strip exact. Cyan tick: hand socket; it is a review overlay.', font=small, fill=(180, 191, 206))
    for col, label in enumerate(['SOURCE'] + [f'key {n}' for n in range(9)]):
        draw.text((left + col * cell_w + 48, top - 26), label, font=small, fill=(211, 218, 231))
    used = []
    for profile, name in enumerate(profiles):
        row_y = top + profile * row_h
        draw.text((12, row_y + 92), name, font=small, fill=(234, 235, 242))
        base = records[(profile, direction, 0)]
        source = ROOT / base['source']
        assert hashlib.sha256(source.read_bytes()).hexdigest() == base['sourcePngSha256'], source
        for col in range(10):
            record = base if col == 0 else records[(profile, direction, col - 1)]
            path = source if col == 0 else ROOT / record['path']
            pixels = Image.open(path).convert('RGBA')
            assert pixels.size == (48, 64), path
            preview = pixels.resize((192, 256), Image.Resampling.NEAREST)
            x = left + col * cell_w
            draw.rectangle((x, row_y, x + 191, row_y + 255), fill=(46, 49, 58) if col % 2 else (38, 41, 49))
            sheet.paste(preview, (x, row_y), preview)
            # Keep the pose readable: a short tick is adjacent to the exact socket,
            # rather than covering the hand with a filled marker.
            hx, hy = record['grip']
            sx, sy = round(x + hx * scale), round(row_y + hy * scale)
            draw.line((sx + 4, sy, sx + 11, sy), fill=(91, 214, 234), width=1)
            draw.line((sx, sy + 4, sx, sy + 11), fill=(91, 214, 234), width=1)
            if col:
                draw.text((x + 34, row_y + 259), f'h {hx:4.1f},{hy:4.1f}', font=small, fill=(165, 184, 204))
            used.append(str(path.relative_to(ROOT)).replace('\\', '/'))
    draw.text((15, sheet.height - 39), '9 keys include identical entry/exit; do not count repeated endpoints as distinct poses. Static review only.', font=small, fill=(180, 191, 206))
    suffix='' if args.variant==0 else f'-variant{args.variant:02d}'
    destination = OUT / f'contact-{view}{suffix}.png'
    sheet.save(destination)
    proof['sheets'].append({'path': str(destination.relative_to(ROOT)).replace('\\', '/'), 'direction': direction, 'variant':args.variant, 'profiles': len(profiles), 'keys': 9, 'sources': used})
(OUT / f'contact-sheets-variant{args.variant:02d}.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2), encoding='utf-8')
if args.skip_audit:
    print(json.dumps({'sheets':[s['path'] for s in proof['sheets']], 'sourceFilesModified':0}))
    raise SystemExit(0)

# A separate PNG-level check catches loose old hand outlines that identity and
# foot provenance alone cannot detect. This does not alter candidate pixels.
checked, failures = set(), []
for record in manifest['frames']:
    if record['path'] in checked:
        continue
    checked.add(record['path'])
    pixels = Image.open(ROOT / record['path']).convert('RGBA')
    source = Image.open(ROOT / record['source']).convert('RGBA')
    assert pixels.crop((0, 40, 48, 64)).tobytes() == source.crop((0, 40, 48, 64)).tobytes(), record
    opaque = {(x, y) for y in range(64) for x in range(48) if pixels.getpixel((x, y))[3]}
    while opaque:
        start = opaque.pop()
        component, queue = {start}, [start]
        while queue:
            x, y = queue.pop()
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    neighbor = (x + dx, y + dy)
                    if neighbor in opaque:
                        opaque.remove(neighbor)
                        component.add(neighbor)
                        queue.append(neighbor)
        if all(30 <= y < 40 and pixels.getpixel((x, y)) == (0, 0, 0, 255) for x, y in component):
            failures.append({'path': record['path'], 'profile': record['profile'], 'direction': record['direction'], 'key': record['key'], 'pixels': sorted(component)})
audit = {'candidateOnly': True, 'uniqueSpritesChecked': len(checked), 'blackOnlyDisconnectedArmComponents': len(failures), 'sourceFootStripExact': True, 'examples': failures}
(OUT / 'outline-audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
print(json.dumps({'sheets': [s['path'] for s in proof['sheets']], 'sourceFilesModified': 0, 'detachedArmComponents': len(failures)}))
