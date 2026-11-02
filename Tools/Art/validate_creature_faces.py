"""Read-only checks and comparison sheets for the adopted creature face correction."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated'
OUT=ROOT/'Logs/Validation/2026-09-21-creature-consistency/face-proof'
OUT.mkdir(parents=True,exist_ok=True)
offsets={
 'Charger':[(11,12),(11,12),(12,12),(11,11),(12,10),(13,11)],
 'Grunt':[(0,8),(0,8),(-1,8),(0,9),(1,8),(1,7)],
 'Shooter':[(0,6),(0,6),(-1,6),(1,6),(0,6),(0,7)],
 'Leaper':[(0,9),(0,9),(-1,9),(1,9),(0,8),(0,11)],
}
eye_windows={'Charger':(13,13,20,17),'Grunt':(10,11,23,17),'Shooter':(11,12,22,16),'Leaper':(11,14,20,18)}
sheet=Image.new('RGB',(1152,4*304),(29,32,43));d=ImageDraw.Draw(sheet);report=[]
for row,(name,anchors) in enumerate(offsets.items()):
    root=ART/'CreatureMotion'/name
    head=np.array(Image.open(root/'head.png').convert('RGBA'));mask=head[:,:,3]>0;reference=None
    x0,y0,x1,y1=eye_windows[name]
    region=head[y0:y1,x0:x1];m=(region[:,:,0]<100)&(region[:,:,1]<120)&(region[:,:,2]<110)&(region[:,:,3]>0)
    cols=np.where(m.any(0))[0];groups=[]
    for c in cols:
        if not groups or c>groups[-1][-1]+1:groups.append([int(c)])
        else:groups[-1].append(int(c))
    assert len(groups)==2,(name,'eye components',groups)
    eyes=[m[:,g] for g in groups]
    assert eyes[0].shape==eyes[1].shape and np.array_equal(eyes[0],eyes[1]),(name,'unequal eye shapes')
    assert eyes[0].all(),(name,'non-rectangular eye bars')
    hashes=[]
    for i,(x,y) in enumerate(anchors):
        im=Image.open(root/'frames'/f'{i:02}.png').convert('RGBA');a=np.array(im)
        assert im.size==(64,64)
        assert not ((a[:,:,3]>0)&(a[:,:,3]<255)).any(),(name,i,'partial alpha')
        assert not (a[0,:,3].any() or a[-1,:,3].any() or a[:,0,3].any() or a[:,-1,3].any()),(name,i,'cutoff')
        padded=np.pad(a,((16,16),(16,16),(0,0)))
        crop=padded[56-y:88-y,32+x:64+x];pixels=crop[mask]
        if reference is None:reference=pixels
        assert np.array_equal(reference,pixels),(name,i,'head pixels drift')
        before=Image.open(ART/f'Meadow/Creatures/{name}/{i:02}.png').convert('RGBA').resize((128,128),Image.Resampling.NEAREST)
        after=im.resize((128,128),Image.Resampling.NEAREST)
        d.text((i*192+6,row*304+2),f'{name} {i} before',fill='white');sheet.paste(before,(i*192+24,row*304+20),before)
        d.text((i*192+6,row*304+148),'after (same world size basis)',fill='white');sheet.paste(after,(i*192+24,row*304+172),after)
        hashes.append(im.tobytes())
    assert len(set(hashes))==6,(name,'duplicate complete frames')
    report.append({'type':name,'poses':6,'eyeShape':[eyes[0].shape[1],eyes[0].shape[0]],'headInvariant':True,'canvas':[64,64],'pivot':[32,8],'ppu':32,'uniqueFullPoses':6,'note':'Uniqueness is not a motion-quality verdict. Head and body still require runtime review.'})
sheet.save(OUT/'before-after-all-creatures.png')
(OUT/'face-consistency.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
