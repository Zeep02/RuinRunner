"""Technical part extraction. Explicit source rects and fixed part registration.
No hand-painted pixels and no per-animation-frame resizing.
"""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json
root=Path(__file__).resolve().parents[2]
src=root/'ArtDirection/Generated/HeroWalk-20260921'
out=root/'Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/rig'
out.mkdir(parents=True,exist_ok=True)
a=np.array(Image.open(src/'south-rig-parts-v1.png').convert('RGBA'))
print('size',a.shape,'alpha corners',[int(a[y,x,3]) for x,y in [(0,0),(100,100),(500,500)]])
a[a[:,:,3]<128]=0;a[a[:,:,3]>=128,3]=255
im=Image.fromarray(a)
# Fixed grid = 26 source pixels per authored pixel. Source joint is positioned
# at (8,12) for torso or (8,14) for each limb in a common 16px canvas.
parts={
 'torso':dict(rect=[132,150,482,515],anchor=[305,183],dest=[8,2]),
 'upperArm':dict(rect=[706,180,860,315],anchor=[782,211],dest=[8,2]),
 'forearm':dict(rect=[1198,328,1330,500],anchor=[1260,346],dest=[8,2]),
 'thigh':dict(rect=[229,625,370,780],anchor=[302,649],dest=[8,2]),
 'shin':dict(rect=[718,728,841,910],anchor=[779,752],dest=[8,2]),
 'scarf':dict(rect=[1044,678,1410,870],anchor=[1342,731],dest=[12,4])}
contact=Image.new('RGBA',(16*12*6,16*12+20),(30,33,43,255));d=ImageDraw.Draw(contact)
for i,(name,p) in enumerate(parts.items()):
    b=np.zeros_like(a);x0,y0,x1,y1=p['rect'];b[y0:y1,x0:x1]=a[y0:y1,x0:x1]
    ax,ay=p['anchor'];dx,dy=p['dest'];left=ax-dx*26;top=ay-dy*26
    part=Image.fromarray(b).crop((left,top,left+416,top+416)).resize((16,16),Image.Resampling.NEAREST)
    part.save(out/(name+'.png'))
    contact.alpha_composite(part.resize((192,192),Image.Resampling.NEAREST),(i*192,20));d.text((i*192+5,4),name,fill='white')
contact.save(src/'south-rig-parts-contact.png')
(src/'south-rig-extraction.json').write_text(json.dumps(dict(scale=1/26,canvas=[16,16],parts=parts,policy='One fixed part scale; rig uses rotations and joint positions, never per-frame scaling. Thigh source has an unwanted boot; only authored upper thigh rectangle retained.'),indent=2))
