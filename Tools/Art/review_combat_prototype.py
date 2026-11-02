from pathlib import Path
from PIL import Image,ImageDraw
import numpy as np,csv,json
root=Path(__file__).resolve().parents[2];folder=root/'ArtDirection/Generated/HeroCombat-20260921'
rows=list(csv.DictReader((folder/'south-slash0-poses.csv').open()))
sheet=Image.new('RGBA',(4*288,3*312),(30,33,43,255));d=ImageDraw.Draw(sheet);frames=[]
head=np.array(Image.open(root/'Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/head.png').convert('RGBA'));checks=[]
for i,r in enumerate(rows):
    im=Image.open(folder/'south-slash0-preview'/f'{i:02}.png').convert('RGBA')
    sheet.alpha_composite(im.resize((288,288),Image.Resampling.NEAREST),(i%4*288,i//4*312+24))
    d.text((i%4*288+6,i//4*312+6),f"{i:02} progress={r['progress']} "+('CONTACT' if i==6 else ''),fill='white')
    a=np.array(Image.open(root/'Assets/RuinRunner/Editor/ArtSources/HeroCombat/attack_0/04'/f'{i:02}.png').convert('RGBA'));frames.append(a)
    matches=[]
    for dy in range(-2,3):
        for dx in range(-2,3):
            h=np.roll(np.roll(head,dy,axis=0),dx,axis=1);mask=h[:,:,3]>0
            if np.array_equal(a[mask],h[mask]):matches.append([dx,dy])
    checks.append(dict(frame=i,headExact=matches,edgePixels=int(np.count_nonzero(a[[0,-1],:,3]))+int(np.count_nonzero(a[:,[0,-1],3]))))
sheet.save(folder/'south-slash0-preview-contact.png')
(folder/'south-slash0-contract.json').write_text(json.dumps(dict(distinct=len(set(a.tobytes() for a in frames)),frames=checks,scope='Prototype only; visual and actual-input checks required before adoption.'),indent=2))
print('Distinct bodies',len(set(a.tobytes() for a in frames)),'whole head matches',sum(bool(c['headExact']) for c in checks),'edge pixels',sum(c['edgePixels'] for c in checks))
