"""Fixed-scale technical extraction of generator-authored slime rig parts."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'ArtDirection/Generated/CreatureConsistency-20260921'
OUT=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/CreatureMotion/Grunt'
a=np.array(Image.open(SRC/'grunt-parts-v1.png').convert('RGBA'))
rgb=a[:,:,:3].astype(float)
key=(rgb[:,:,0]>rgb[:,:,1]*1.5+25)&(rgb[:,:,2]>rgb[:,:,1]*1.5+25)
a[key]=0;a[~key,3]=255
(OUT/'body').mkdir(parents=True,exist_ok=True)
anchors=[(209,360),(627,360),(1045,360),(209,784),(627,784),(1045,784)]
for i,(x,y) in enumerate(anchors):
    part=np.zeros_like(a);left=i%3*418;top=i//3*418
    part[top:top+418,left:left+418]=a[top:top+418,left:left+418]
    im=Image.fromarray(part).crop((x-352,y-616,x+352,y+88)).resize((64,64),Image.Resampling.NEAREST)
    im.save(OUT/'body'/f'{i:02}.png')
head=Image.fromarray(a).crop((33,928,385,1280)).resize((32,32),Image.Resampling.NEAREST)
head.save(OUT/'head.png')
(SRC/'grunt-parts-extraction.json').write_text(json.dumps({'scale':1/11,'bodyCanvas':[64,64],'bodyAnchors':anchors,'bodyPivot':[32,8],'headSourceRect':[33,928,352,352],'headCanvas':[32,32],'headPivot':[16,16],'headRegistration':'Sample origin aligned to original two equal 2x6 eye bars; no painted eye pixels.'},indent=2))
print('Extracted candidate lower-body poses and one constant whole upper dome/head')
