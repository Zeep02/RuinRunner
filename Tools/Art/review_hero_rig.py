from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,json,sys
direction=int(sys.argv[1]) if len(sys.argv)>1 else 4
prefix=['north','northeast','east','southeast','south','southwest','west','northwest'][direction]
root=Path(__file__).resolve().parents[2];src=root/('Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/'+('rigBaked' if direction==4 else f'bakedDirections/{direction:02}'))
out=root/'ArtDirection/Generated/HeroWalk-20260921'
contact=Image.new('RGBA',(6*192,2*216),(30,33,43,255));d=ImageDraw.Draw(contact)
frames=[]
for i in range(12):
    im=Image.open(src/f'{i:02}.png').convert('RGBA');frames.append(im)
    contact.alpha_composite(im.resize((192,192),Image.Resampling.NEAREST),(i%6*192,i//6*216+24))
    d.text((i%6*192+8,i//6*216+4),str(i),fill='white')
contact.save(out/(prefix+'-rig-contact.png'))
previews=[]
for im in frames:
    bg=Image.new('RGBA',(48,48),(30,33,43,255));bg.alpha_composite(im)
    previews.append(bg.resize((288,288),Image.Resampling.NEAREST).convert('RGB'))
previews[0].save(out/(prefix+'-rig-cycle-review.gif'),save_all=True,append_images=previews[1:],duration=40,loop=0)
print('12 image hashes distinct:',len(set(im.tobytes() for im in frames)))
