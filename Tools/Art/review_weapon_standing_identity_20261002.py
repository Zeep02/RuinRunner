"""Verify the clean carry changed arms only, including the original ready head silhouette and feet."""
from pathlib import Path
from PIL import Image
import json,sys,numpy as np
from art_source_path import resolve_art_path
root=Path(__file__).resolve().parents[2]
version=sys.argv[1] if len(sys.argv)>1 else 'weapon-standing-v4'
m=json.loads((root/f'ArtDirection/Generated/SscaryMotion-20261001/{version}/manifest.json').read_text(encoding='utf-8'))
cache={};head=foot=core=0;changed=0
def get(p):
    if p not in cache:cache[p]=np.asarray(Image.open(resolve_art_path(p)).convert('RGBA'))
    return cache[p]
for r in m['frames']:
    a=get(r['path']);b=get(r['readySource'])
    assert a.shape==(64,48,4) and set(np.unique(a[:,:,3])).issubset({0,255})
    head+=int(np.count_nonzero(np.any(a[:30]!=b[:30],axis=-1)))
    core+=int(np.count_nonzero(np.any(a[30:33,22:28]!=b[30:33,22:28],axis=-1)))
    foot+=int(np.count_nonzero(np.any(a[40:]!=b[40:],axis=-1)))
    changed+=int(np.count_nonzero(np.any(a[30:40]!=b[30:40],axis=-1)))
report=dict(result='PASS' if head+foot+core==0 else 'FAIL',slots=len(m['frames']),unique=m['uniqueSprites'],changedOriginalHeadPixels=head,changedOriginalFaceCorePixels=core,changedOriginalFootPixels=foot,changedArmRegionPixels=changed,scope='Full original ready upper30 rows, including transparency/silhouette, central face/torso22..27 rows30..32, and lower24 rows preserved. Arm region30..39 revised from clean carry. Screen QA still checks every opaque upper33 pixel without exclusions.')
out=root/sys.argv[2] if len(sys.argv)>2 else root/f'ArtDirection/Review-20261001/weapon-carry/{version}-identity-qa.json'
out.write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report));assert head+foot+core==0
