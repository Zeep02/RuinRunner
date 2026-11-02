"""Candidate extraction only: fixed sprite sheet scale/canvas, no repainting.
Unity performs the authoring composite. Runtime hero assets are not replaced.
"""
from pathlib import Path
from PIL import Image
import numpy as np,json
root=Path(__file__).resolve().parents[2]
src=root/'ArtDirection/Generated/HeroWalk-20260921'
out=root/'Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth'
(out/'keys').mkdir(parents=True,exist_ok=True)
im=Image.open(src/'south-keys-v4.png').convert('RGBA');a=np.array(im)
a[a[:,:,3]<128]=0;a[a[:,:,3]>=128,3]=255
# One common scale, sampled around the neck/belt axis and floor line.
anchors=[(272,540),(814,540),(1356,540),(1897,540)]
for i,(x,y) in enumerate(anchors):
    cell=np.zeros_like(a);x0=round(i*im.width/4);x1=round((i+1)*im.width/4)
    cell[:,x0:x1]=a[:,x0:x1]
    frame=Image.fromarray(cell).crop((x-480,y-760,x+480,y+200)).resize((48,48),Image.Resampling.NEAREST)
    frame.save(out/'keys'/f'{i:02}.png')
head=np.array(Image.open(root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/04/01.png').convert('RGBA'))
head[23:]=0
Image.fromarray(head).save(out/'head.png')
contact=Image.new('RGBA',(192*5,192),(30,33,43,255))
for i in range(4):contact.alpha_composite(Image.open(out/'keys'/f'{i:02}.png').resize((192,192),Image.Resampling.NEAREST),(192*i,0))
contact.alpha_composite(Image.fromarray(head).resize((192,192),Image.Resampling.NEAREST),(768,0))
contact.save(src/'south-key-parts-4x.png')
(src/'south-key-extraction.json').write_text(json.dumps(dict(candidate=True,scale=1/20,canvas=[48,48],foot=[24,10],anchors=anchors,wholeHeadSource='HeroMotion/move/04/01.png',headRows=[0,23],policy='Common scale and registration; no per-frame bounds scaling or painted pixels. No runtime replacement yet.'),indent=2))
print(out)
