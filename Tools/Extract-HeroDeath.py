"""Offline separation, fixed scale/canvas/row ground line. Source sheet preserved."""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,cv2
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'ArtDirection/Generated/HeroMotion-20260916'
OUT=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/death'
src=Image.open(ART/'death-original.png').convert('RGBA')
cw,ch=src.width/5,src.height/8
source=np.array(src)
count,labels,stats,centers=cv2.connectedComponentsWithStats((source[:,:,3]>=128).astype('uint8'),8)
# Uniform crop canvas 240px makes the ~160px first pose 32 logical pixels tall.
# Source ground is authored at row baseline ~.98; no frame-specific normalization.
canvas=240
rows=[195,392,592,779,973,1169,1357,1549]
report=[]
sheet=Image.new('RGB',(5*144,8*160),(27,31,43));d=ImageDraw.Draw(sheet)
for row in range(8):
    for col in range(5):
        box=(round(col*cw),round(row*ch),round((col+1)*cw),round((row+1)*ch))
        ids=[i for i in range(1,count) if stats[i,4]>400 and int(centers[i,0]/cw)==col and int(centers[i,1]/ch)==row]
        assert len(ids)==1,(row,col,ids)
        isolated=source.copy();isolated[:,:,3]=np.where(np.isin(labels,ids),255,0)
        x=round((col+.5)*cw)-canvas//2;y=rows[row]-round(canvas*38/48)
        frame=Image.fromarray(isolated).crop((x,y,x+canvas,y+canvas))
        frame=frame.resize((48,48),Image.Resampling.NEAREST)
        arr=np.array(frame);arr[:,:,3]=np.where(arr[:,:,3]>=128,255,0);arr[arr[:,:,3]==0,:3]=0
        frame=Image.fromarray(arr);bbox=frame.getbbox()
        assert bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<48 and bbox[3]<48,(row,col,bbox)
        path=OUT/f'{row:02d}'/f'{col:02d}.png';path.parent.mkdir(parents=True,exist_ok=True);frame.save(path)
        big=frame.resize((144,144),Image.Resampling.NEAREST);sheet.paste(big,(col*144,row*160+16),big)
        d.text((col*144+3,row*160+2),f'{row} death {col}',fill='white')
        report.append(dict(direction=row,frame=col,source_cell=box,source_anchor=[x+120,rows[row]],source_canvas=240,bbox=bbox))
sheet.save(ART/'death-contact-sheet.png')
(ART/'death-extraction.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(src.size,'Saved',len(report),'individual 48px frames with fixed scale and pivot')
