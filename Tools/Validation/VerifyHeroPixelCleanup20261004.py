"""Independent source/outline regression gate, before building the current catalogs."""
from pathlib import Path
from collections import Counter
import hashlib, json, re
import cv2
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
REVIEW=ROOT/'ArtDirection/Review-20261004/hero-pixel-cleanup'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def rgba(path):
    a=np.array(Image.open(path).convert('RGBA'));a[a[:,:,3]==0]=0;return a
contract=read(REVIEW/'body-foot-contract.json')
receipt=read(REVIEW/'applied.json')
failures=[];counts=Counter();cache={};changed=0
catalog_source=(ROOT/'Assets/RuinRunner/Runtime/Feedback/NativeHeroFrameCatalog.cs').read_text(encoding='utf-8-sig')
versions=re.findall(r'"([^"]+)"',re.search(r'CurrentVersions\s*=\s*\{([^}]+)\}',catalog_source).group(1))
actual={v+'/'+p.name for v in versions for p in (ROOT/'Assets/RuinRunner/ArtSources/Hero'/v).glob('*.png')}
if actual!=set(contract):failures.append('Current catalog coverage changed; update and review per-pose foot provenance before adoption.')
for key,leg in contract.items():
    path=ROOT/'Assets/RuinRunner/ArtSources/Hero'/key
    a=rgba(path)
    if leg not in cache:cache[leg]=rgba(ROOT/leg)
    if not np.array_equal(a[40:],cache[leg][40:]):failures.append('Original footfall mismatch: '+key)
    n,labels,stats,_=cv2.connectedComponentsWithStats((a[:,:,3]>0).astype('uint8'),8)
    main=1+np.argmax(stats[1:,4])
    for k in range(1,n):
        if k==main:continue
        yy,xx=np.where(labels==k)
        if yy.min()>=30 and yy.max()<40 and np.all(a[yy,xx,:3]==0):failures.append('Detached arm outline: '+key)
    counts[key.split('/')[0]]+=1
for row in receipt['rows']:
    path=ROOT/row['path'];a=rgba(path);before=rgba(ROOT/row['backup'])
    delta=np.any(a!=before,axis=2);changed+=int(delta.sum())
    if digest(path)!=row['afterSha256']:failures.append('Image changed since adoption: '+row['path'])
    if digest(Path(str(path)+'.meta'))!=row['metaSha256']:failures.append('Importer/GUID changed: '+row['path'])
    if not np.array_equal(a[:30],before[:30]):failures.append('Head changed: '+row['path'])
    # Above the feet, only the explicitly reviewed black outline islands may go.
    yy,xx=np.where(delta[:40])
    if any(not np.array_equal(before[y,x],(0,0,0,255)) or a[y,x,3]!=0 for y,x in zip(yy,xx)):
        failures.append('Unexpected change to arms/torso: '+row['path'])
    if int(delta.sum())!=row['changedPixels']:failures.append('Pixel-change receipt mismatch: '+row['path'])
proof={'result':'FAIL' if failures else 'PASS','inspectedFrames':len(contract),'catalogs':dict(counts),'correctedImages':len(receipt['rows']),'changedPixels':changed,'failures':failures,'scope':'All currently catalogued body PNGs: exact original recorded footfall, no detached black-only arm outline islands, approved head and retained arm colors unchanged, importer metadata byte-identical. This does not replace runtime motion review.'}
(REVIEW/'source-verification.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(proof,ensure_ascii=False,indent=2))
raise SystemExit(bool(failures))
