"""Read-only source identity/endpoint audit and native pixel review sheets."""
from pathlib import Path
import json
import hashlib
import sys
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2]
out = root / 'ArtDirection/Review-20261003/weapon-reload'
version = sys.argv[1] if len(sys.argv)>1 else 'v1'
manifest = json.loads((root / f'ArtDirection/Generated/SscaryMotion-20261003/weapon-reload-{version}/manifest.json').read_text(encoding='utf-8'))
cache = {}
def pixels(path):
    if path not in cache:
        cache[path] = np.asarray(Image.open(root / path).convert('RGBA'))
    return cache[path]

errors = []; groups = {}; head = feet = 0; endpoints = 0; max_step = 0
slots = {}
for r in manifest['frames']:
    p, s, ready = pixels(r['path']), pixels(r['source']).copy(), pixels(r['readySource']).copy()
    if 'rigidSource' in r:
        # Verify exact documented repair, then require full unchanged footfall.
        rigid=pixels(r['rigidSource']); s[40:] = rigid[40:]; ready[40:] = rigid[40:]
    if p.shape != (64, 48, 4): errors.append(['geometry', r['path']])
    for region, label in [(np.s_[:30, :], 'head'), (np.s_[30:33, 22:28], 'face'), (np.s_[40:, :], 'feet')]:
        if not np.array_equal(p[region], s[region]): errors.append([label, r['path'], r['source']])
    head += 30*48+3*6; feet += 24*48
    if r['key'] in (0, 15):
        endpoints += 1
        if not np.array_equal(p, s if r['key'] == 0 else ready): errors.append(['endpoint', r['path']])
    if hashlib.sha256(p.tobytes()).hexdigest() != r['sha256']: errors.append(['byte-hash',r['path']])
    g = (r['clip'], r['entry'], r['direction'], r['variant'])
    groups.setdefault(g, []).append(r)
    slots[g + (r['key'],)] = r
counts = []
for g, rows in groups.items():
    ordered = sorted(rows, key=lambda r:r['key'])
    for a,b in zip(ordered,ordered[1:]):
        max_step = max(max_step, float(np.linalg.norm(np.array(a['grip']) - b['grip'])))
    if g[3] == 0:
        counts.append(dict(clip=g[0],entry=g[1],direction=g[2],actualUnique=len({r['sha256'] for r in rows})))
for direction in [3,4,5]:
    sheet = Image.new('RGBA', (16*48, 6*82), (30,29,40,255)); draw = ImageDraw.Draw(sheet)
    for clip in range(6):
        for key in range(16):
            r=slots[(clip,0,direction,0,key)]
            sheet.alpha_composite(Image.open(root/r['path']).convert('RGBA'),(key*48,clip*82+15))
        draw.text((3,clip*82),slots[(clip,0,direction,0,0)]['family'],fill='white')
    sheet.convert('RGB').save(out/f'native-reload-{version}-direction-{direction}.png')
result = dict(result='PASS' if not errors else 'FAIL',scope='Read-only original full head/face/feet RGBA and exact endpoints, not motion/gameplay quality.',
              slots=len(manifest['frames']),uniquePngs=manifest['uniqueSprites'],headAndFacePixels=head,feetPixels=feet,
              exactEndpoints=endpoints,maxPrimaryHandStepPixels=max_step,stillPoseCounts=counts,errors=errors[:50])
(out/f'source-identity-{version}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('stillPoseCounts','errors')},ensure_ascii=False))
if errors: print('Identity mismatches:',len(errors)); raise SystemExit(1)
