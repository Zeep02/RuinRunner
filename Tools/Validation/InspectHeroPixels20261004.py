"""Read-only pixel provenance / connected-component review; no asset writes."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw
from collections import Counter

ROOT = Path('F:/codex/LoglikeCap')
OUT = ROOT / 'ArtDirection/Review-20261004/hero-pixel-cleanup'
OUT.mkdir(parents=True, exist_ok=True)
ref = np.array(Image.open('C:/Users/Hyeon/AppData/Local/Temp/codex-clipboard-af6951ce-669f-4184-af0b-8eba1b7443e4.png').convert('RGBA'))
print('reference palette', Counter(map(tuple, ref.reshape(-1, 4))).most_common(18))
base = ROOT / 'Assets/RuinRunner/ArtSources/Hero'
paths = sorted(base.glob('*/*.png'))
arr = np.stack([np.array(Image.open(p).convert('RGBA')) for p in paths])
# Locate the approved native head, then compare the whole body at that same
# pixel grid. Weapons/background may occupy transparent source pixels.
head = arr[0].copy(); head[30:] = 0
ys, xs = np.where(head[:,:,3] > 0)
best = (-1, None)
for ox in range(-15,30):
    for oy in range(-25,15):
        tx=xs*2+ox;ty=ys*2+oy
        valid=(tx>=0)&(tx<ref.shape[1])&(ty>=0)&(ty<ref.shape[0])
        if not valid.all(): continue
        score=np.all(ref[ty,tx,:3]==head[ys,xs,:3],axis=1).sum()
        if score>best[0]: best=(int(score),(ox,oy))
print('alignment first head',best)
# Different view/head phases are covered by scoring each source in a small
# neighborhood of the common head alignment.
scores=[]
for ox in range(best[1][0]-2,best[1][0]+3):
 for oy in range(best[1][1]-2,best[1][1]+3):
  target=np.zeros((64,48,4),dtype=np.uint8)
  for y in range(64):
   for x in range(48):
    tx=x*2+ox;ty=y*2+oy
    if 0<=tx<ref.shape[1] and 0<=ty<ref.shape[0]: target[y,x]=ref[ty,tx]
  opaque=arr[:,:,:,3]>0
  same=np.all(arr[:,:,:,:3]==target[None,:,:,:3],axis=3)&opaque
  mismatch=((~same)&opaque).sum(axis=(1,2))
  for i in np.argsort(mismatch)[:8]:
   scores.append({'path':str(paths[i].relative_to(ROOT)).replace('\\','/'),'mismatchedOpaque':int(mismatch[i]),'matchedOpaque':int(same[i].sum()),'offset':[ox,oy]})
scores=sorted(scores,key=lambda r:r['mismatchedOpaque'])
unique=[]
for s in scores:
 if s['path'] not in [r['path'] for r in unique]: unique.append(s)
unique=unique[:16]
(OUT/'reference-match.json').write_text(json.dumps(unique,indent=2),encoding='utf-8')
sheet=Image.new('RGBA',(8*240,2*300),(85,89,81,255));draw=ImageDraw.Draw(sheet)
for i,row in enumerate(unique):
 x=i%8*240;y=i//8*300
 sheet.alpha_composite(Image.open(ROOT/row['path']).resize((240,320),Image.Resampling.NEAREST),(x,y-25))
 draw.text((x+3,y+268),Path(row['path']).parent.name,fill='white')
 draw.text((x+3,y+282),Path(row['path']).name+' diff='+str(row['mismatchedOpaque']),fill='white')
sheet.save(OUT/'reference-match.png')
print('best matches',unique[:5])
