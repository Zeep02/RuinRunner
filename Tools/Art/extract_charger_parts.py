"""Extract generator-authored rig parts on fixed canvases; no painted corrections."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'ArtDirection/Generated/CreatureConsistency-20260921'
OUT=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/CreatureMotion/Charger'
a=np.array(Image.open(SRC/'charger-parts-v2.png').convert('RGBA'))
# Remove only the flat key color; preserve the generated subject pixels.
rgb=a[:,:,:3].astype(float)
key=(rgb[:,:,0]>rgb[:,:,1]*1.5+25)&(rgb[:,:,2]>rgb[:,:,1]*1.5+25)
a[key]=0
a[~key,3]=255
im=Image.fromarray(a)
anchors=[(240,368),(744,368),(1240,368),(240,688),(744,688),(1240,688)]
(OUT/'body').mkdir(parents=True,exist_ok=True)
for i,(x,y) in enumerate(anchors):
    isolated=np.zeros_like(a)
    row=(0,400) if i<3 else (400,740)
    col=(i%3)*512
    isolated[row[0]:row[1],col:col+512]=a[row[0]:row[1],col:col+512]
    cell=Image.fromarray(isolated).crop((x-256,y-448,x+256,y+64)).resize((64,64),Image.Resampling.NEAREST)
    cell.save(OUT/'body'/f'{i:02}.png')
head=im.crop((98,756,354,1012)).resize((32,32),Image.Resampling.NEAREST)
head.save(OUT/'head.png')
(SRC/'parts-extraction.json').write_text(json.dumps({'scale':0.125,'bodyCanvas':[64,64],'bodyAnchors':anchors,'bodyPivot':[32,8],'headSourceRect':[98,756,256,256],'headCanvas':[32,32],'headPivot':[16,16],'method':'Fixed-scale cell/part extraction; immutable full-head asset, not separately fixed eyes. Head sampling origin registered to original eye bars.'},indent=2))
print('Extracted six body poses and one shared whole head; runtime rig review pending')
