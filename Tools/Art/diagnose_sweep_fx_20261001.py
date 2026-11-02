"""Read-only native screen/source overlap colors; acceptance uses unchanged coordinates."""
from pathlib import Path
import csv,json,sys
from collections import Counter
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[2]
folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/melee-fx-binding'/('diagnosis-'+folder.name)
out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
frames={int(f) for f in sys.argv[2:]}
report=[]
for r in rows:
    frame=int(r['frame'])
    if frame not in frames:continue
    a=np.asarray(Image.open(root/r['bodyAsset']).convert('RGBA'))
    image=Image.open(folder/'frames'/f'frame-{frame:06d}.png').convert('RGB')
    b=np.asarray(image)
    sy,sx=np.nonzero(a[:33,:,3]==255)
    x=np.floor(float(r['originSX'])+(sx+.5)*float(r['scaleX'])).astype(int)
    y=np.floor(720-float(r['originSY'])+(sy+.5)*float(r['scaleY'])).astype(int)
    wrong=np.max(abs(b[y,x].astype(int)-a[sy,sx,:3].astype(int)),axis=1)>3
    pixels=[dict(sourceXY=[int(xx),int(yy)],expected=a[yy,xx,:3].tolist(),actual=b[py,px].tolist())
            for xx,yy,px,py in zip(sx[wrong],sy[wrong],x[wrong],y[wrong])]
    report.append(dict(frame=frame,telemetry=r,mismatches=pixels))
    left=int(float(r['originSX']));top=int(720-float(r['originSY']))
    image.crop((left,top,left+96,top+128)).save(out/f'frame-{frame}-native-body.png')
    Image.fromarray(a).save(out/f'frame-{frame}-source.png')
(out/'mismatch-colors.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps([dict(frame=r['frame'],phase=r['telemetry']['phase'],pixels=r['mismatches']) for r in report]))
