"""Compare current capture phases with native opaque head/upper-body pixels.

Original stationary phases, corrected in-camera moving phases, and all freshly
recorded dash/pause/slot transitions. Earlier failed evidence remains untouched.
"""
from pathlib import Path
import csv,json,sys
import numpy as np
from PIL import Image
from art_source_path import resolve_art_path
root=Path(__file__).resolve().parents[2]
folders=[Path(s).resolve() for s in sys.argv[1:4]]
out=root/'ArtDirection/Review-20261003/firearm-reload/current-screen-check';out.mkdir(exist_ok=True)
groups={}
for index,folder in enumerate(folders):
    for row in csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')):
        if index<2 and '-view-' not in row['phase']:continue
        key=row['phase']
        if key not in groups or groups[key][0]!=folder:groups[key]=(folder,[])
        groups[key][1].append(row)
assert len([k for k in groups if '-view-' in k])==80
assert len([k for k in groups if '-dash-vector-' in k])==160
cache={};total=bad=frame_count=outside=0;failures=[]
for phase,(folder,rows) in groups.items():
    for r in rows:
        path=r['bodyAsset']
        if path not in cache:cache[path]=np.asarray(Image.open(resolve_art_path(path)).convert('RGBA'))
        sprite=cache[path];screen=np.asarray(Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB'))
        sy,sx=np.nonzero(sprite[:33,:,3]==255)
        x=np.floor(float(r['originSX'])+(sx+.5)*float(r['scaleX'])).astype(int)
        y=np.floor(screen.shape[0]-float(r['originSY'])+(sy+.5)*float(r['scaleY'])).astype(int)
        valid=(x>=0)&(x<screen.shape[1])&(y>=0)&(y<screen.shape[0])
        outside+=int(np.count_nonzero(~valid))
        delta=np.max(abs(screen[y[valid],x[valid]].astype(int)-sprite[sy[valid],sx[valid],:3].astype(int)),axis=1)
        count=int(np.count_nonzero(delta>3));total+=len(delta);bad+=count;frame_count+=1
        if count or not np.all(valid):failures.append(dict(folder=str(folder),frame=r['frame'],phase=phase,wrongPixels=count,outside=int(np.count_nonzero(~valid))))
proof=dict(result='PASS' if bad==0 and outside==0 else 'FAIL',phases=len(groups),frames=frame_count,comparedPixels=total,changedPixels=bad,outsideScreenPixels=outside,failures=failures,
    scope='All 80 reload phase recordings (20 camera-corrected replacements) plus all 40 newly repaired eight-direction dash/pause/slot cases. Exact native opaque head/upper-body coordinates; no excluded overlaps or calibrated offsets. Not normal gameplay/performance.',
    sources=[dict(phase=k,folder=str(v[0]),frames=len(v[1])) for k,v in groups.items()])
(out/'screen-face-qa.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in proof.items() if k not in ('sources','failures','scope')}))
assert total>0 and bad==0 and outside==0,'Inspect actual occlusion/identity/framing failures before accepting.'
