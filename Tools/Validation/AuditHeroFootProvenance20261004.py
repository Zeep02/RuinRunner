"""Read-only audit of native body pixels against original gait provenance."""
from pathlib import Path
import json, hashlib
import numpy as np
from PIL import Image, ImageDraw
from collections import Counter
ROOT=Path('F:/codex/LoglikeCap');OUT=ROOT/'ArtDirection/Review-20261004/hero-pixel-cleanup'
def load(p): return json.loads((ROOT/p).read_text(encoding='utf-8-sig'))
cache={}
def pixels(p):
 if p not in cache: cache[p]=np.array(Image.open(ROOT/p).convert('RGBA'))
 return cache[p]
def visible(a):
 a=a.copy();a[a[:,:,3]==0]=0;return a
legs={}
for name in ['melee-v2','short-blade-v1','thrust-v3','defense-v2','ranged-v7']:
 for row in load('ArtDirection/Generated/SscaryMotion-20261001/'+name+'/manifest.json')['frames']:
  h=row['pixelsSha256'];p=row['legSource']
  if h in legs: assert np.array_equal(pixels(p)[40:],pixels(legs[h])[40:])
  legs[h]=p
roots={};slots={};conflicts=[]
def add(version,index,leg,row):
 key=version+'/'+str(index).zfill(5)+'.png'
 if key in roots and not np.array_equal(pixels(roots[key])[40:],pixels(leg)[40:]):conflicts.append([key,roots[key],leg])
 roots[key]=leg;slots.setdefault(key,[]).append(row)
for r in load('ArtDirection/Generated/SscaryMotion-20261001/armed-grip-v3/manifest.json')['frames']:
 p=r['source'] if r['kind']==0 else legs[r['sourceSha256']]
 add('ArmedGripV3',r['runtimeIndex'],p,r)
for r in load('ArtDirection/Generated/SscaryMotion-20261001/weapon-standing-v4/manifest.json')['frames']:
 p=roots['ArmedGripV3/'+Path(r['readySource']).name];add('WeaponStandingV4',r['runtimeIndex'],p,r)
for r in load('ArtDirection/Review-20261003/firearm-reload/source-map.json')['frames']:
 p=roots['WeaponStandingV4/'+Path(r['rigidSource']).name];add('WeaponReloadV3',int(Path(r['runtimePath']).stem),p,r)
print('mapped',len(roots),'tail conflicts',len(conflicts),flush=True)
(OUT/'body-foot-contract.json').write_text(json.dumps(roots,indent=2),encoding='utf-8')
rows=[];counts=Counter();islands=Counter();examples={}
for i,(key,p) in enumerate(roots.items()):
 path='Assets/RuinRunner/ArtSources/Hero/'+key;a=pixels(path);native=pixels(p)
 diff=np.any(visible(a)[40:]!=visible(native)[40:],axis=2)
 if diff.any():
  yy,xx=np.where(diff);changes=[]
  for y,x in zip(yy+40,xx):changes.append({'x':int(x),'y':int(y),'before':a[y,x].tolist(),'native':native[y,x].tolist()})
  r=slots[key][0];row={'path':path,'legSource':p,'changes':changes,'sample':{k:v for k,v in r.items() if k in ['kindName','clip','family','profileName','direction','variant','key','entry']}}
  rows.append(row);counts[key.split('/')[0]]+=1
  sample=row['sample'];group=(key.split('/')[0],sample.get('kindName',sample.get('profileName',sample.get('family'))),sample.get('direction'))
  examples.setdefault(group,row)
 if i%4000==0:print(i,dict(counts),flush=True)
data={'scope':'Read-only comparison; reload differences can include the valid new support hand and require a per-pose mask. Never blanket-delete them.','sources':len(roots),'conflicts':conflicts,'counts':dict(counts),'frames':rows}
(OUT/'foot-provenance-audit.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
selected=list(examples.values());print('counts',counts,'examples',len(selected),flush=True)
for page in range((len(selected)+31)//32):
 sheet=Image.new('RGBA',(8*192,4*256),(88,91,83,255));draw=ImageDraw.Draw(sheet)
 for i,r in enumerate(selected[page*32:page*32+32]):
  x=i%8*192;y=i//8*256;a=Image.open(ROOT/r['path']);sheet.alpha_composite(a.resize((192,256),Image.Resampling.NEAREST),(x,y-25))
  for q in r['changes']:
   xx=x+q['x']*4;yy=y+q['y']*4-25;draw.rectangle((xx,yy,xx+3,yy+3),outline=(255,80,40))
  s=r['sample'];draw.text((x+2,y+216),Path(r['path']).parent.name+'/'+Path(r['path']).stem,fill='white');draw.text((x+2,y+231),str(s),fill='white')
 sheet.save(OUT/f'foot-audit-{page}.png')
print('done',flush=True)
