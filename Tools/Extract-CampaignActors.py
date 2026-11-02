"""Offline, reviewed cell extraction. Common canvas/scale; never runtime opaque-bound resizing."""
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np
import json, shutil

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r'C:\Users\Hyeon\.codex\generated_images\01a0a972-e696-7f32-9cdc-426f1f068d45')
SHEETS = {'Tidekeeper':'exec-d9ae1f17-55be-4278-8b64-128781a0b527.png',
          'SealHeart':'exec-e7e16036-0518-4e2e-be9e-0f6978c921f3.png',
          'Tidecaller':'exec-f5ca7b88-fcd3-4b45-9ff0-57d73214f882.png',
          'Sentinel':'exec-4d91ac15-2809-4ff2-b3ce-b82d9678492b.png'}
STATES = ['idle','move','attack','death']

def components(mask):
    labels = np.zeros(mask.shape, dtype=np.int32)
    found=[]; label=0; h,w=mask.shape
    for y,x in zip(*np.where(mask)):
        if labels[y,x]: continue
        label+=1; stack=[(int(x),int(y))]; labels[y,x]=label
        area=0; x0=x1=int(x);y0=y1=int(y)
        while stack:
            xx,yy=stack.pop();area+=1;x0=min(x0,xx);x1=max(x1,xx);y0=min(y0,yy);y1=max(y1,yy)
            for dy,dx in ((-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)):
                nx=xx+dx;ny=yy+dy
                if 0<=nx<w and 0<=ny<h and mask[ny,nx] and labels[ny,nx]==0:
                    labels[ny,nx]=label;stack.append((nx,ny))
        found.append((label,area,(x0,y0,x1+1,y1+1)))
    return labels,found

for actor,file in SHEETS.items():
    original=ROOT/'ArtDirection'/'Generated'/'Campaign-20260916'/actor
    original.mkdir(parents=True,exist_ok=True)
    shutil.copy2(SOURCE/file,original/'original.png')
    im=Image.open(original/'original.png').convert('RGBA'); arr=np.array(im)
    labels,parts=components(arr[:,:,3]>=128)
    n=im.width; cell=n/4
    # Reviewable source alignment: the stone monolith's motion row leans to the right.
    x_anchors = [160,477,790,1105] if actor=='SealHeart' else [160,475,790,1104]
    walk_x = [201,526,843,1153] if actor=='SealHeart' else [184,485,804,1115]
    ground_y = [280,588,904,1200] if actor=='SealHeart' else [277,578,910,1197]
    minor=actor in ('Tidecaller','Sentinel')
    if minor:
        x_anchors=[160,477,790,1105];walk_x=[160,477,790,1105]
        ground_y=[292,590,910,1199] if actor=='Tidecaller' else [276,580,882,1199]
    output_size=48 if minor else 96
    sheet=Image.new('RGBA',(384,384),(29,34,47,255)); report=[]
    for r,state in enumerate(STATES):
        for c in range(4):
            if actor=='Tidecaller' and r==0 and c==1: continue # Rejected: reversed facing in supplied generation.
            # Group whole connected pieces by their cell center; avoids neighbour-cell pixels
            # when a claw or stream protrudes beyond the nominal rectangular grid.
            ids=[]
            for label,area,b in parts:
                if area<18: continue
                cx=(b[0]+b[2])/2;cy=(b[1]+b[3])/2
                rr=min(3,int(cy/cell));cc=min(3,int(cx/cell))
                if actor in ('Tidekeeper','Tidecaller') and rr==2 and cc==3 and b[2]<982: cc=2
                if (rr,cc)==(r,c):ids.append(label)
            keep=np.isin(labels,ids)
            isolated=arr.copy();isolated[:,:,3]=np.where(keep,255,0)
            # One 384px source canvas -> one 96px output for all frames and all poses.
            anchor_x=(walk_x if r==1 else x_anchors)[c];anchor_y=ground_y[r]
            left=anchor_x-192;top=anchor_y-328
            frame=Image.fromarray(isolated).crop((left,top,left+384,top+384)).resize((output_size,output_size),Image.Resampling.NEAREST)
            frame_index=c-1 if actor=='Tidecaller' and r==0 and c>1 else c
            out=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign'/actor/state/f'{frame_index:02}.png'
            out.parent.mkdir(parents=True,exist_ok=True);frame.save(out)
            sheet.alpha_composite(frame.resize((96,96),Image.Resampling.NEAREST),(c*96,r*96))
            report.append({'state':state,'source_frame':c,'frame':frame_index,'source_anchor':[anchor_x,anchor_y],'source_canvas':384,'canvas':output_size,'foot_pixel':[output_size/2,output_size*328/384],'bbox':frame.getbbox(),'components':ids})
    sheet.save(original/'contact-sheet.png')
    (original/'extraction.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(actor,im.size,'->',len(report),'individual',output_size,'px PNGs; inspect contact sheet before adoption.')
