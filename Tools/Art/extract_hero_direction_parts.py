"""Directional candidates: fixed scale/pivot extraction, original heads preserved."""
from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json
root=Path(__file__).resolve().parents[2];src=root/'ArtDirection/Generated/HeroWalk-20260921'
out=root/'Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/directions';out.mkdir(parents=True,exist_ok=True)
a=np.array(Image.open(src/'directional-torsos-v2.png').convert('RGBA'))
a[a[:,:,3]<128]=0;a[a[:,:,3]>=128,3]=255
print('source',a.shape)
anchors=[(316,197),(861,197),(1337,197),(1848,197)]
for i,(ax,ay) in enumerate(anchors):
    x0=round(i*a.shape[1]/4);x1=round((i+1)*a.shape[1]/4)
    y,x=np.where(a[:,x0:x1,3]>0);print(i,'bounds',(int(x.min()+x0),int(y.min()),int(x.max()+x0),int(y.max())))
    b=np.zeros_like(a);b[:,x0:x1]=a[:,x0:x1]
    Image.fromarray(b).crop((ax-8*28,ay-2*28,ax+8*28,ay+14*28)).resize((16,16),Image.Resampling.NEAREST).save(out/f'torso-{i:02}.png')
for i in range(8):
    im=Image.open(root/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/{i:02}/01.png').convert('RGBA');h=np.array(im);h[23:]=0
    Image.fromarray(h).save(out/f'head-{i:02}.png')
contact=Image.new('RGBA',(4*192,210),(30,33,43,255));d=ImageDraw.Draw(contact)
for i in range(4):
    contact.alpha_composite(Image.open(out/f'torso-{i:02}.png').resize((192,192),Image.Resampling.NEAREST),(i*192,18));d.text((i*192+4,2),str(i),fill='white')
contact.save(src/'directional-torso-parts-contact.png')
(src/'directional-parts-extraction.json').write_text(json.dumps(dict(scale=1/28,canvas=[16,16],jointPixel=[8,2],sourceAnchors=anchors,heads='HeroMotion/move/<direction>/01.png; exact whole upper23rows retained'),indent=2))
