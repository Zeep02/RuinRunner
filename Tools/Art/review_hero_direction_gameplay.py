"""Read-only actual game crops, tracked from red scarf; no resize/retouch."""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,csv,sys
root=Path(__file__).resolve().parents[2]
folder=root/'Logs/Validation/2026-09-21-hero-walk'/(sys.argv[1] if len(sys.argv)>1 else '043749')
rows=list(csv.DictReader((folder/'recording.csv').open()))
for direction,name in enumerate(['north','northeast','east','southeast','south','southwest','west','northwest']):
    contact=Image.new('RGB',(832,648),(24,27,36));d=ImageDraw.Draw(contact)
    for frame in range(12):
        choices=[r for r in rows if r['pose']=='move' and int(r['direction'])==direction and int(r['bodyFrame'])==frame]
        if not choices:
            d.text((frame%4*208+6,frame//4*216+6),f'pose {frame}: not sampled',fill='white');continue
        r=choices[0];im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06}.png").convert('RGB');a=np.array(im).astype(float)
        region=a[150:850,680:1240];mask=(region[:,:,0]>90)&(region[:,:,0]>region[:,:,1]*1.55)&(region[:,:,1]<90)&(region[:,:,2]<130)
        y,x=np.where(mask);assert len(y)>8
        cy=round(float(np.median(y)))+150;cx=round(float(np.median(x)))+680+(10 if direction in [1,2,3,4] else -10)
        contact.paste(im.crop((cx-104,cy-90,cx+104,cy+102)),(frame%4*208,frame//4*216+24))
        d.text((frame%4*208+6,frame//4*216+6),f"{name} pose {frame} / sample {r['frame']}",fill='white')
    contact.save(folder/(name+'-actual-poses.png'))
print(folder)
