from pathlib import Path
from PIL import Image,ImageDraw
import json
root=Path(__file__).resolve().parents[2]
pack=root/'ArtDirection/ExternalCandidates/20261001/ansimuz-shooting/original'
paths=[p for p in pack.rglob('*.png') if '__MACOSX' not in p.parts and not p.name.startswith('._')]
print(json.dumps([dict(path=str(p.relative_to(pack)),size=Image.open(p).size) for p in paths]))
# Full sheets, nearest sampling; no source changes.
rows=[]
for p in paths:
 im=Image.open(p).convert('RGBA')
 if im.width>700 or im.height>250:continue
 scale=min(3,max(1,700//im.width));im=im.resize((im.width*scale,im.height*scale),Image.Resampling.NEAREST)
 rows.append((str(p.relative_to(pack)),im))
sheet=Image.new('RGBA',(750,sum(im.height+28 for _,im in rows)),(48,51,60,255));d=ImageDraw.Draw(sheet);y=0
for title,im in rows:d.text((8,y),title,fill='white');sheet.alpha_composite(im,(8,y+20));y+=im.height+28
out=root/'ArtDirection/Review-20261001/weapon-fx/Warped.png';sheet.convert('RGB').save(out)
