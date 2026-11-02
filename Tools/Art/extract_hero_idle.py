"""Technical extraction/registration of generated idle poses; no drawn pixels.

One uniform source scale, one 48px canvas/foot anchor. Source placements are
recorded for review; no per-frame resize or runtime silhouette estimation.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw
import cv2

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'ArtDirection/Generated/HeroIdle-20260921'
OUT=SOURCE/'candidate-frames'
source=Image.open(SOURCE/'original-v2.png').convert('RGBA')
a=np.array(source)
count,labels,stats,centers=cv2.connectedComponentsWithStats((a[:,:,3]>=128).astype('uint8'),8)
parts=[]
for i,(x,y,w,h,area) in enumerate(stats):
    if i and area>2000: parts.append((i,int(x),int(y),int(w),int(h)))
assert len(parts)==48, len(parts)
parts.sort(key=lambda p:p[2]+p[4]*.5)
rows=[sorted(parts[r*6:r*6+6],key=lambda p:p[1]) for r in range(8)]
report=[]
south=Image.open(SOURCE/'south-original.png').convert('RGBA')
sa=np.array(south)
sc,sl,ss,_=cv2.connectedComponentsWithStats((sa[:,:,3]>=128).astype('uint8'),8)
sp=[(i,*[int(v) for v in s[:4]]) for i,s in enumerate(ss) if i and s[4]>10000]
sp.sort(key=lambda p:p[2]+p[4]*.5)
south_parts=sorted(sp[:3],key=lambda p:p[1])+sorted(sp[3:],key=lambda p:p[1])
assert len(south_parts)==6
for d,parts in enumerate(rows):
    for f,(index,x,y,w,h) in enumerate(parts):
        # Register an explicit authoring-time boot baseline, without altering
        # any pose dimensions; sheet spacing itself is not perfectly uniform.
        mask=labels==index
        source_center=x+w//2
        # Torso axis excludes trailing scarf at the sides. Use lower boots.
        yy,xx=np.where(mask & (np.indices(mask.shape)[0]>=y+h-24))
        source_center=round((int(xx.min())+int(xx.max()))*.5)
        anchor=(source_center,y+h)
        isolated=a.copy();isolated[:,:,3]=np.where(mask,255,0);isolated[~mask,:3]=0
        canvas=Image.fromarray(isolated).crop((anchor[0]-128,anchor[1]-203,anchor[0]+128,anchor[1]+53))
        im=canvas.resize((48,48),Image.Resampling.NEAREST)
        source_file='original-v2.png'; source_canvas=256; source_box=[x,y,w,h]
        if d==4:
            si,sx,sy,sw,sh=south_parts[f]
            sm=sl==si; isolated=sa.copy();isolated[:,:,3]=np.where(sm,255,0);isolated[~sm,:3]=0
            # One common source scale for all six south poses: 448px body to 32px.
            ax=round(sx+sw*.5)+20; ay=sy+sh
            canvas=Image.fromarray(isolated).crop((ax-336,ay-532,ax+336,ay+140))
            im=canvas.resize((48,48),Image.Resampling.NEAREST)
            anchor=(ax,ay)
            source_file='south-original.png';source_canvas=672;source_box=[sx,sy,sw,sh]
        path=OUT/f'{d:02}'/f'{f:02}.png';path.parent.mkdir(parents=True,exist_ok=True);im.save(path)
        report.append(dict(direction=d,frame=f,source=source_file,sourceComponent=source_box,anchor=anchor,sourceCanvas=source_canvas,scale=48/source_canvas,bbox=im.getbbox()))
sheet=Image.new('RGB',(7*160,8*160),(29,32,43));draw=ImageDraw.Draw(sheet)
for d in range(8):
    frames=[]
    old=Image.open(ROOT/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/{d:02}/01.png')
    old=old.resize((144,144),Image.Resampling.NEAREST);sheet.paste(old,(0,d*160+16),old)
    draw.text((2,d*160),f'{d} current',fill='white')
    for f in range(6):
        im=Image.open(OUT/f'{d:02}'/f'{f:02}.png');big=im.resize((144,144),Image.Resampling.NEAREST)
        sheet.paste(big,((f+1)*160,d*160+16),big);draw.text(((f+1)*160+2,d*160),f'idle {f}',fill='white')
        bg=Image.new('RGBA',(144,144),(29,32,43,255));bg.alpha_composite(big);frames.append(bg.convert('RGB'))
    frames[0].save(SOURCE/f'candidate-idle-{d:02}.gif',save_all=True,append_images=frames[1:],duration=[380,260,240,280,300,340],loop=0,disposal=2)
sheet.save(SOURCE/'candidate-contact.png')
(SOURCE/'extraction-v2.json').write_text(json.dumps(report,indent=2))
print('48 candidate poses extracted for review, not yet adopted')
