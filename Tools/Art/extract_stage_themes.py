"""Bake generated terrain sheets to individual pixel sprites with authored tile topology.

Source sheets are preserved. Row ranges are inspected authoring coordinates, not an
assumption that the image model obeyed the requested cell sizes. The previously
approved wall footprint is retained while material pixels come from each new kit.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw

source=Path('ArtDirection/Generated/StageThemes-20260920')
target=Path('Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Terrain')
old=Path('Assets/RuinRunner/Resources/Art/Pixel/Separated/DungeonGathering')
ids=['Floor/plain','Floor/cracks','Floor/bricks','Floor/engraved',
     'Masonry/north_left','Masonry/north','Masonry/north_right','Masonry/west',
     'Masonry/east','Masonry/south_left','Masonry/south','Masonry/south_right',
     'Masonry/inner_north_left','Masonry/inner_north_right','Masonry/inner_south_left','Masonry/inner_south_right',
     'Props/debris','Props/lamp','Props/urn','Props/decal']
rows=[(0,280),(280,546),(550,795),(800,1065),(1080,1390)]
report=[]

def nearest_fill(a,mask):
    valid=np.argwhere(a[:,:,3]>=200)
    assert len(valid)
    for y,x in np.argwhere(mask & (a[:,:,3]<200)):
        yy,xx=valid[np.argmin(((valid-[y,x])**2).sum(axis=1))]
        a[y,x,:3]=a[yy,xx,:3]
    a[:,:,3]=np.where(mask,255,0)
    a[~mask,:3]=0
    return a

for theme in ['Outer','Flooded','Core']:
    sheet=Image.open(source/f'{theme}-original.png').convert('RGBA')
    contact=Image.new('RGBA',(4*160,5*180),(37,39,46,255));draw=ImageDraw.Draw(contact)
    floors=[]
    for i,id in enumerate(ids):
        row,col=divmod(i,4);y0,y1=rows[row]
        x0=round(col*sheet.width/4);x1=round((col+1)*sheet.width/4)
        box=[x0,y0,x1,y1]
        if row==0:box=[x0+8,y0+8,x1-8,y1-9] # remove authored atlas separators
        tile=sheet.crop(box).resize((32,32),Image.Resampling.NEAREST)
        a=np.array(tile)
        if row==0:
            a=nearest_fill(a,np.ones((32,32),dtype=bool))
            floors.append(a)
        elif row<4:
            mask=np.array(Image.open(old/(id+'.png')).convert('RGBA').resize((32,32),Image.Resampling.NEAREST))[:,:,3]>0
            # New slab/shell/obsidian fronts are continuous surfaces, unlike the
            # old sparse brick marks. Retain silhouette, close internal row gaps.
            for y in range(32):
                xs=np.flatnonzero(mask[y])
                if len(xs):mask[y,xs[0]:xs[-1]+1]=True
            occupied=np.flatnonzero(mask.any(axis=1))
            for y in range(occupied[0],occupied[-1]+1):
                if not mask[y].any():
                    before=occupied[occupied<y][-1];after=occupied[occupied>y][0]
                    mask[y]=mask[before]&mask[after]
            a=nearest_fill(a,mask)
        else:
            a[:,:,3]=np.where(a[:,:,3]>=200,255,0);a[a[:,:,3]==0,:3]=0
        # Palette reduction at authoring time, without interpolation or runtime scaling.
        rgb=Image.fromarray(a[:,:,:3]).quantize(colors=32,method=Image.Quantize.MEDIANCUT).convert('RGB')
        a[:,:,:3]=np.array(rgb)
        final=Image.fromarray(a)
        path=target/theme/(id+'.png');path.parent.mkdir(parents=True,exist_ok=True);final.save(path)
        if row==0:
            # A 32px floor painting spans a 2x2 world patch. Store its quadrants
            # individually so clipping at the walkable boundary remains exact.
            for qy in range(2):
                for qx in range(2):
                    quadrant=final.crop((qx*16,(1-qy)*16,qx*16+16,(2-qy)*16))
                    quadrant.save(target/theme/(id+f'-{qx}-{qy}.png'))
        contact.alpha_composite(final.resize((128,128),Image.Resampling.NEAREST),(col*160+16,row*180+30))
        draw.text((col*160+4,row*180+4),id.split('/')[-1],fill='white')
        report.append(dict(theme=theme,id=id,source_box=box,output=[32,32],ppu=32,binary_alpha=True))
    contact.save(source/f'{theme}-contact.png')
(source/'extraction.json').write_text(json.dumps(report,indent=2))
print(f'Baked {len(report)} individual 32px PNGs; originals preserved.')
