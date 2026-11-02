"""Offline PNG separation/resampling requested by the goal. No runtime slicing.

Uniform scale per source sheet; per-row source ground anchors keep actual poses,
including bent knees and extended legs. Original user files remain untouched.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
import json

ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'ArtDirection/Generated/HeroMotion-20260916'
OUT=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion'
report=[]

def components(mask):
    labels=np.zeros(mask.shape,dtype=np.int32); parts=[]; label=0; h,w=mask.shape
    for y,x in zip(*np.where(mask)):
        if labels[y,x]: continue
        label+=1; stack=[(int(x),int(y))]; labels[y,x]=label
        area=0; x0=x1=int(x); y0=y1=int(y)
        while stack:
            xx,yy=stack.pop(); area+=1; x0=min(x0,xx);x1=max(x1,xx);y0=min(y0,yy);y1=max(y1,yy)
            for dy,dx in ((-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)):
                nx=xx+dx;ny=yy+dy
                if 0<=nx<w and 0<=ny<h and mask[ny,nx] and labels[ny,nx]==0:
                    labels[ny,nx]=label;stack.append((nx,ny))
        if area>400: parts.append((label,area,(x0,y0,x1+1,y1+1)))
    return labels,parts

def store_frame(im, action, direction, frame, info):
    a=np.array(im); a[:,:,3]=np.where(a[:,:,3]>=128,255,0)
    a[a[:,:,3]==0,:3]=0
    im=Image.fromarray(a)
    path=OUT/action/f'{direction:02d}'/f'{frame:02d}.png'
    path.parent.mkdir(parents=True,exist_ok=True); im.save(path)
    report.append(dict(action=action,direction=direction,frame=frame,bbox=im.getbbox(),**info))
    return im

# Existing authored move frames share their original 256px canvas. The same
# 40/256 resample and (4,2) offset maps their ground line y232 to output y38.
for direction in range(8):
    for frame in range(4):
        src=Image.open(ROOT/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/UserHero/Player/move/{direction:02d}/{frame:02d}.png').convert('RGBA')
        im=Image.new('RGBA',(48,48)); im.alpha_composite(src.resize((40,40),Image.Resampling.NEAREST),(4,2))
        store_frame(im,'move',direction,frame,dict(source='user move',scale=40/256,offset=[4,2]))

for source,columns,canvas,ground in [('attack',6,240,[179,356,537,716,896,1075,1254,1433]),
                                     ('defense',4,288,[216,438,660,875,1097,1319,1535,1750])]:
    src=Image.open(ART/f'{source}-original.png').convert('RGBA')
    cw=src.width/columns; ch=src.height/8
    arr=np.array(src); labels,parts=components(arr[:,:,3]>=128)
    print(source,src.size,'cell',cw,ch,'common source canvas',canvas)
    for row in range(8):
        # Defenses have 220px rows; floor lines share the same 0.98 row position.
        gy=ground[row] if ground else round((row+.975)*ch)
        for col in range(columns):
            # Whole connected bodies can cross grid lines. Assign each by its
            # center, then isolate before cropping so a neighbouring head is
            # neither clipped nor leaked into this frame's lower gutter.
            cellbox=(round(col*cw),round(row*ch),round((col+1)*cw),round((row+1)*ch))
            ids=[label for label,area,b in parts if int((b[0]+b[2])*.5/cw)==col and int((b[1]+b[3])*.5/ch)==row]
            if len(ids)!=1: raise ValueError((source,row,col,ids))
            isolated=arr.copy();isolated[:,:,3]=np.where(np.isin(labels,ids),255,0)
            cx=round((col+.5)*cw)
            left=cx-canvas//2; top=gy-round(canvas*38/48)
            im=Image.fromarray(isolated).crop((left,top,left+canvas,top+canvas))
            im=im.resize((48,48),Image.Resampling.NEAREST)
            action='attack' if source=='attack' else ['dash','dash','guard','hurt'][col]
            frame=col if source=='attack' else 1 if col==1 else 0
            store_frame(im,action,row,frame,dict(source=source,source_cell=cellbox,source_anchor=[cx,gy],source_canvas=canvas))

sheet=Image.new('RGB',(12*144,8*160),(27,31,43)); draw=ImageDraw.Draw(sheet)
for row in range(8):
    entries=[('move',0),('move',1)]+[('attack',i) for i in range(6)]+[('dash',0),('dash',1),('guard',0),('hurt',0)]
    for col,(action,frame) in enumerate(entries):
        im=Image.open(OUT/action/f'{row:02d}'/f'{frame:02d}.png')
        large=im.resize((144,144),Image.Resampling.NEAREST)
        sheet.paste(large,(col*144,row*160+16),large)
        draw.text((col*144+3,row*160+2),f'{row} {action} {frame}',fill='white')
sheet.save(ART/'contact-sheet.png')
(ART/'extraction.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('Saved',len(report),'individual 48px sprites; inspect contact sheet before adoption.')
