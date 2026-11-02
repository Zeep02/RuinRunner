from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np
root=Path(__file__).resolve().parents[2];out=root/'ArtDirection/Generated/HeroWalk-20260921'
contact=Image.new('RGBA',(8*144,300),(30,33,43,255));d=ImageDraw.Draw(contact)
for i in range(8):
    im=Image.open(root/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/{i:02}/01.png').convert('RGBA')
    contact.alpha_composite(im.resize((144,144),Image.Resampling.NEAREST),(i*144,12))
    a=np.array(im);a[23:]=0
    contact.alpha_composite(Image.fromarray(a).resize((144,144),Image.Resampling.NEAREST),(i*144,156))
    d.text((i*144+5,2),['N','NE','E','SE','S','SW','W','NW'][i],fill='white')
contact.save(out/'eight-direction-master-review.png')
