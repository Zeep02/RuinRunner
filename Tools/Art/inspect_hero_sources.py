from pathlib import Path
from PIL import Image
import numpy as np
root=Path(__file__).resolve().parents[2]
p=root/'ArtDirection/Generated/HeroWalk-20260921/south-keys-v4.png'
im=Image.open(p).convert('RGBA');a=np.array(im)
print('sheet',im.size,'alpha',np.unique(a[:,:,3])[:20].tolist())
for i in range(4):
    x0=round(i*im.width/4);x1=round((i+1)*im.width/4)
    alpha=a[:,x0:x1,3];y,x=np.where(alpha>128)
    print(i,'bounds',[int(x.min()+x0),int(y.min()),int(x.max()+x0+1),int(y.max()+1)])
hero=Image.open(root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/04/01.png').convert('RGBA')
hero.resize((576,576),Image.Resampling.NEAREST).save(p.parent/'canonical-south-current.png')
