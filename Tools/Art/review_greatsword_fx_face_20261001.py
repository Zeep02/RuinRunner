"""Read-only exact-source comparison of opaque head pixels in actual cut screens."""
from pathlib import Path
from PIL import Image
import csv,json,sys
import numpy as np
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/weapon-identity-more'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
cache={};total=0;bad=0;proof=[]
for row in rows:
    if not row['pose'].startswith(('cleave','charge','sweep','reverse-sweep')):continue
    path=row['bodyAsset']
    if path not in cache:cache[path]=np.asarray(Image.open(root/path).convert('RGBA'))
    sprite=cache[path];screen=np.asarray(Image.open(folder/'frames'/f"frame-{int(row['frame']):06d}.png").convert('RGB'))
    sy,sx=np.nonzero(sprite[:33,:,3]==255)
    x=np.floor(float(row['originSX'])+(sx+.5)*float(row['scaleX'])).astype(int)
    y=np.floor(screen.shape[0]-float(row['originSY'])+(sy+.5)*float(row['scaleY'])).astype(int)
    valid=(x>=0)&(x<screen.shape[1])&(y>=0)&(y<screen.shape[0]);x=x[valid];y=y[valid];sx=sx[valid];sy=sy[valid]
    delta=np.max(np.abs(screen[y,x].astype(int)-sprite[sy,sx,:3].astype(int)),axis=1)
    wrong=int(np.count_nonzero(delta>3));total+=len(delta);bad+=wrong
    if wrong:proof.append({'frame':row['frame'],'phase':row['phase'],'wrongPixels':wrong,'opaqueUpperPixels':len(delta),'maxChannelDifference':int(delta.max())})
report={'result':'PASS' if bad==0 else 'FAIL','sampledUpperBodyPixels':total,'occludedOrChangedPixels':bad,'framesWithMismatch':proof,'scope':'Actual opaque native head/upper-body pixels at the exact recorded camera/renderer position. No replacement rendering or edited screen; alpha edges excluded.'}
(out/'screen-face-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ('framesWithMismatch','scope')}))
assert total>0 and bad==0,'Native face hidden/altered in actual cut screen; inspect screen-face-qa.json'
