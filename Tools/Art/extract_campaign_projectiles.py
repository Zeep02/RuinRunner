"""Extract reviewed imagegen cells; preserve original and use one 16:1 pixel scale."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, numpy as np

root=Path(__file__).resolve().parents[2]
source=root/'ArtDirection/Generated/Projectiles-20260916/original.png'
target=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Projectiles'
target.mkdir(parents=True,exist_ok=True)
image=Image.open(source).convert('RGBA')
assert image.size==(1536,1024),image.size
# Actual sheet inspected: six complete subjects, 512px cells, no crossing.
# The model added semi-transparent halos despite the prompt. Retain opaque core
# only, then sample the same 16px grid for every cell. No stretch or shape painting.
names=['Arrow','Bolt','Bullet','Cannon','EnemyOrb','Arc']
proof=[]
sheet=Image.new('RGBA',(384,256),(37,36,48,255))
for i,name in enumerate(names):
    x=(i%3)*512;y=(i//3)*512
    cell=image.crop((x,y,x+512,y+512))
    # Sampling in the center of each generated pixel block, with common scale.
    cell=cell.resize((32,32),Image.Resampling.NEAREST)
    pixels=np.array(cell)
    pixels[:,:,3]=np.where(pixels[:,:,3]>=224,255,0)
    pixels[pixels[:,:,3]==0,:3]=0
    cell=Image.fromarray(pixels)
    bounds=cell.getbbox()
    assert bounds and bounds[0]>0 and bounds[1]>0 and bounds[2]<32 and bounds[3]<32,(name,bounds)
    # Align core to the common cell center once at authoring time.
    delta=(round(16-(bounds[0]+bounds[2])/2),round(16-(bounds[1]+bounds[3])/2))
    centered=Image.new('RGBA',(32,32));centered.alpha_composite(cell,delta)
    centered.save(target/(name+'.png'))
    sheet.alpha_composite(centered.resize((128,128),Image.Resampling.NEAREST),((i%3)*128,(i//3)*128))
    proof.append(dict(name=name,source=[x,y,512,512],canvas=[32,32],scale=1/16,centerOffset=delta,visibleBounds=centered.getbbox(),alpha=[0,255]))
sheet.convert('RGB').save(source.parent/'contact.png')
(source.parent/'extraction.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps(proof))
