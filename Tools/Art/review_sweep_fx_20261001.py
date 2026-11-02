"""Read-only source keys and exact opaque head pixels in actual sweep screens."""
from pathlib import Path
import csv,json,sys,numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/melee-fx-binding'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
times=list(csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig')))
cache={};total=bad=0;proof=[];keys=[]
for phase in dict.fromkeys(r['phase'] for r in rows):
    selected=[r for r in rows if r['phase']==phase and r['fxClip'] and float(r['progress'])>=.18]
    ts=[float(r['realtime']) for r in times if r['phase']==phase]
    keys.append(dict(phase=phase,visibleSourceKeys=sorted({int(r['fxKey']) for r in selected}),samples=len(ts),medianMs=float(np.median(np.diff(ts))*1000)))
    if phase.endswith('view-2') and selected:
        peak=1 if selected[0]['fxClip']=='Katana' else 2
        r=next(r for r in selected if int(r['fxKey'])==peak)
        image=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB')
        x=float(r['originSX'])+24*float(r['scaleX']);y=720-float(r['originSY'])+33.04*float(r['scaleY'])
        left=int(max(0,min(640,x-320)));top=int(max(0,min(240,y-240)))
        image.crop((left,top,left+640,top+480)).save(out/(phase+'-native-screen.png'))
for r in rows:
    path=r['bodyAsset']
    if path not in cache:cache[path]=np.asarray(Image.open(root/path).convert('RGBA'))
    sprite=cache[path];screen=np.asarray(Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB'))
    sy,sx=np.nonzero(sprite[:33,:,3]==255)
    x=np.floor(float(r['originSX'])+(sx+.5)*float(r['scaleX'])).astype(int)
    y=np.floor(screen.shape[0]-float(r['originSY'])+(sy+.5)*float(r['scaleY'])).astype(int)
    valid=(x>=0)&(x<1280)&(y>=0)&(y<720);x=x[valid];y=y[valid];sx=sx[valid];sy=sy[valid]
    difference=np.max(np.abs(screen[y,x].astype(int)-sprite[sy,sx,:3].astype(int)),axis=1)
    count=int(np.count_nonzero(difference>3));total+=len(difference);bad+=count
    if count:proof.append(dict(frame=r['frame'],phase=r['phase'],pose=r['pose'],wrongPixels=count,maxChannelDifference=int(difference.max())))
report=dict(result='PASS' if bad==0 else 'FAIL',sampledUpperBodyPixels=total,occludedOrChangedPixels=bad,framesWithMismatch=proof,scope='All recorded original opaque head/upper-body source pixels compared at exact renderer/camera positions, no calibrated offset or altered screen.')
(out/'screen-face-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(out/'source-key-review.json').write_text(json.dumps(keys,indent=2),encoding='utf-8')
print(json.dumps(dict(result=report['result'],samples=len(rows),facePixels=total,changedPixels=bad,minVisibleKeys=min(len(r['visibleSourceKeys']) for r in keys))))
assert total>0 and bad==0,'Inspect actual screen mismatches; no acceptance claim.'
