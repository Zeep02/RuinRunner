"""Technical, uniform-scale registration of generated plant animation parts."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'ArtDirection/Generated/CreatureConsistency-20260921'
specs={
 'Shooter':{'anchor':[(204,360),(626,360),(1037,360),(216,744),(627,744),(1046,744)],'stemY':43,'head':[32,880]},
 'Leaper':{'anchor':[(208,354),(627,354),(1038,354),(218,755),(624,755),(1035,747)],'stemY':41,'head':[32,880]},
}
for name,spec in specs.items():
    a=np.array(Image.open(SRC/f'{name.lower()}-parts-v1.png').convert('RGBA'));rgb=a[:,:,:3].astype(float)
    key=(rgb[:,:,0]>rgb[:,:,1]*1.5+25)&(rgb[:,:,2]>rgb[:,:,1]*1.5+25)
    a[key]=0;a[~key,3]=255
    out=ROOT/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/CreatureMotion/{name}'
    (out/'body').mkdir(parents=True,exist_ok=True)
    for i,(x,y) in enumerate(spec['anchor']):
        part=np.zeros_like(a);left=i%3*418;top=i//3*418
        part[top:top+418,left:left+418]=a[top:top+418,left:left+418]
        y0=y-spec['stemY']*11
        im=Image.fromarray(part).crop((x-352,y0,x+352,y0+704)).resize((64,64),Image.Resampling.NEAREST)
        im.save(out/'body'/f'{i:02}.png')
    x,y=spec['head'];head=Image.fromarray(a).crop((x,y,x+352,y+352)).resize((32,32),Image.Resampling.NEAREST)
    if name=='Leaper':
        corrected=np.array(Image.open(SRC/'leaper-head-v2.png').convert('RGBA'))
        corrected[:,:,3]=np.where(corrected[:,:,3]>=128,255,0)
        corrected[corrected[:,:,3]==0,:3]=0
        head=Image.fromarray(corrected).crop((0,-12,1280,1268)).resize((32,32),Image.Resampling.NEAREST)
    head.save(out/'head.png')
    metadata={'scale':1/11,'bodyCanvas':[64,64],'bodyPivot':[32,8],'headCanvas':[32,32],'headPivot':[16,16],**spec}
    if name=='Leaper':metadata['headOverride']={'source':'leaper-head-v2.png','sourceRect':[0,-12,1280,1268],'alphaThreshold':128,'outputCanvas':[32,32],'purpose':'Correct the asymmetric original eyes with one invariant whole head.'}
    (SRC/f'{name.lower()}-parts-extraction.json').write_text(json.dumps(metadata,indent=2))
print('Extracted fixed-scale Shooter and Leaper candidates with authored attachment points')
