"""Read-only pixel/sequence inventory and review sheets; never alters game images."""
from pathlib import Path
import csv, hashlib, json, math
from collections import defaultdict, Counter
from PIL import Image, ImageDraw
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated'
OUT = ROOT / 'Logs/Validation/2026-09-21-art-audit'
OUT.mkdir(parents=True, exist_ok=True)
groups = defaultdict(list)
rows = []
for path in sorted(ART.rglob('*.png')):
    im = Image.open(path).convert('RGBA')
    arr = np.asarray(im)
    alpha = arr[:, :, 3]
    opaque = arr[alpha > 127, :3]
    partial = int(((alpha > 0) & (alpha < 255)).sum())
    border = int((alpha[0] > 0).sum() + (alpha[-1] > 0).sum() +
                 (alpha[:, 0] > 0).sum() + (alpha[:, -1] > 0).sum())
    item = dict(path=str(path.relative_to(ART)).replace('\\', '/'), width=im.width,
                height=im.height, colors=len(np.unique(opaque, axis=0)), partialAlpha=partial,
                borderPixels=border, bbox=im.getbbox(), hash=hashlib.sha256(im.tobytes()).hexdigest())
    rows.append(item)
    groups[str(path.parent.relative_to(ART)).replace('\\', '/')].append(item)

with (OUT/'pixels.csv').open('w', newline='', encoding='utf-8') as f:
    w=csv.DictWriter(f, fieldnames=rows[0].keys()); w.writeheader(); w.writerows(rows)
summaries=[]
for group, frames in groups.items():
    summaries.append(dict(group=group, count=len(frames), unique=len({f['hash'] for f in frames}),
        sizes=dict(Counter(f"{f['width']}x{f['height']}" for f in frames)),
        maxColors=max(f['colors'] for f in frames), partialAlphaFiles=sum(f['partialAlpha']>0 for f in frames)))
(OUT/'families.json').write_text(json.dumps(summaries, indent=2), encoding='utf-8')

# Review active visual families with every authored frame. Terrain fields are
# reviewed as their assembled source rather than 3072 disconnected squares.
families = {
    'hero': ['HeroMotion/'], 'warden': ['UserBoss/'],
    'campaign-actors': ['Campaign/Tidekeeper/', 'Campaign/SealHeart/', 'Campaign/Tidecaller/', 'Campaign/Sentinel/'],
    'creatures-chests': ['Meadow/Creatures/', 'UserChest/'],
    'weapons-items': ['HeldWeapons/', 'Items/'],
    'effects': ['Feedback/', 'VFX/', 'Campaign/Projectiles/', 'World/'],
    'interface': ['UI/', 'HUD/', 'Meadow/UI/'],
    'environment': ['Campaign/Environment/', 'Campaign/Terrain/'],
}
coverage={}
for name, prefixes in families.items():
    selected=[r for r in rows if any(r['path'].startswith(p) for p in prefixes) and '/Field/' not in r['path']]
    coverage[name]=len(selected)
    for page in range(math.ceil(len(selected)/64)):
        subset=selected[page*64:(page+1)*64]
        sheet=Image.new('RGB',(1024,8*136),(29,32,43)); draw=ImageDraw.Draw(sheet)
        for i,entry in enumerate(subset):
            im=Image.open(ART/entry['path']).convert('RGBA')
            factor=max(1, min(3, 112//max(im.size)))
            if max(im.size)>112:
                im.thumbnail((112,112),Image.Resampling.NEAREST)
            else: im=im.resize((im.width*factor,im.height*factor),Image.Resampling.NEAREST)
            x=(i%8)*128; y=(i//8)*136
            sheet.paste(im,(x+(128-im.width)//2,y+(112-im.height)//2),im)
            label=entry['path'].replace('Campaign/','').replace('HeroMotion/','').replace('Meadow/','')
            draw.text((x+2,y+112),label[-25:],fill='white')
        sheet.save(OUT/f'{name}-{page+1:02}.png')
(OUT/'coverage.json').write_text(json.dumps(dict(totalIndividualImages=len(rows),reviewSheets=coverage,
    note='Pixel metrics are exhaustive. Contact-sheet inclusion is not a claim of visual approval.'),indent=2))
# Item icons live outside Separated and are texture assets rather than Sprites.
icons=sorted((ART.parent/'Items').rglob('*.png'))
for page in range(math.ceil(len(icons)/64)):
    sheet=Image.new('RGB',(1024,8*136),(29,32,43));draw=ImageDraw.Draw(sheet)
    for i,path in enumerate(icons[page*64:(page+1)*64]):
        im=Image.open(path).convert('RGBA');im.thumbnail((100,100),Image.Resampling.NEAREST)
        x=(i%8)*128;y=(i//8)*136;sheet.paste(im,(x+(128-im.width)//2,y+(112-im.height)//2),im)
        draw.text((x+2,y+112),path.stem,fill='white')
    sheet.save(OUT/f'item-icons-{page+1:02}.png')
print(json.dumps(dict(images=len(rows),families=len(groups),review=coverage)))
