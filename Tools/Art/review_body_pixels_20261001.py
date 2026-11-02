"""Compare all original opaque head/upper-body pixels with actual native output. Read-only."""
from pathlib import Path
import csv,json,sys,numpy as np
from PIL import Image
from art_source_path import resolve_art_path
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/sys.argv[2] if len(sys.argv)>2 else root/'ArtDirection/Review-20261001/weapon-carry'/folder.name
out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
cache={};total=bad=0;proof=[]
for r in rows:
    path=r['bodyAsset']
    if path not in cache:cache[path]=np.asarray(Image.open(resolve_art_path(path)).convert('RGBA'))
    sprite=cache[path];screen=np.asarray(Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB'))
    sy,sx=np.nonzero(sprite[:33,:,3]==255)
    x=np.floor(float(r['originSX'])+(sx+.5)*float(r['scaleX'])).astype(int)
    y=np.floor(screen.shape[0]-float(r['originSY'])+(sy+.5)*float(r['scaleY'])).astype(int)
    valid=(x>=0)&(x<screen.shape[1])&(y>=0)&(y<screen.shape[0]);x=x[valid];y=y[valid];sx=sx[valid];sy=sy[valid]
    difference=np.max(np.abs(screen[y,x].astype(int)-sprite[sy,sx,:3].astype(int)),axis=1)
    count=int(np.count_nonzero(difference>3));total+=len(difference);bad+=count
    if count:proof.append(dict(frame=r['frame'],phase=r['phase'],pose=r['pose'],wrongPixels=count,maxChannelDifference=int(difference.max())))
report=dict(result='PASS' if bad==0 else 'FAIL',sampledUpperBodyPixels=total,occludedOrChangedPixels=bad,framesWithMismatch=proof,scope='Every recorded original opaque head/upper-body pixel compared at exact renderer/camera positions. No excluded overlaps, calibrated offsets or altered screenshots.')
(out/'screen-face-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(dict(result=report['result'],frames=len(rows),facePixels=total,changedPixels=bad)))
assert total>0 and bad==0,'Inspect actual screen mismatch; do not claim acceptance.'
