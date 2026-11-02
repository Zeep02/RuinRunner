from pathlib import Path
from PIL import Image
import numpy as np,json,sys
direction=int(sys.argv[1]) if len(sys.argv)>1 else 4
root=Path(__file__).resolve().parents[2];folder=root/'Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth'
head=np.array(Image.open(folder/('head.png' if direction==4 else f'directions/head-{direction:02}.png')).convert('RGBA'));mask=head[:,:,3]>0
frames=[];results=[]
for i in range(12):
    a=np.array(Image.open(folder/('rigBaked' if direction==4 else f'bakedDirections/{direction:02}')/f'{i:02}.png').convert('RGBA'))
    assert a.shape==(48,48,4)
    assert set(np.unique(a[:,:,3])).issubset({0,255})
    assert not np.any(a[[0,-1],:,3]) and not np.any(a[:,[0,-1],3])
    matches=[]
    for dy in range(-2,2):
        for dx in range(-1,2):
            shifted=np.roll(np.roll(head,dy,axis=0),dx,axis=1);m=shifted[:,:,3]>0
            if np.array_equal(a[m],shifted[m]):matches.append([dx,dy])
    assert matches,f'Canonical whole head changed: frame {i}'
    frames.append(a);results.append(dict(frame=i,wholeHeadExactTranslations=matches))
assert len(set(a.tobytes() for a in frames))==12
for i,a in enumerate(frames):
    # Different leg/arm pixels are independent of whole head translation.
    changed=int(np.count_nonzero(np.any(a[25:,:,:]!=frames[(i+1)%12][25:,:,:],axis=2)))
    assert changed>0,f'No articulated body change at {i}'
    results[i]['bodyPixelsChangedToNext']=changed
out=root/'Logs/Validation/2026-09-21-hero-walk-candidate';out.mkdir(parents=True,exist_ok=True)
(out/('frame-contract.json' if direction==4 else f'frame-contract-{direction:02}.json')).write_text(json.dumps(dict(scope='Technical candidate checks; not visual or gameplay approval.',direction=direction,frames=results),indent=2))
print('PASS: 12 fixed 48px/binary-alpha/bounded frames, invariant whole head, no duplicate body pose. Visual gait still needs actual game review.')
