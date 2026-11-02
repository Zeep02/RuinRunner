"""Read-only visual contact from real game frames; preserve aspect and scale."""
from pathlib import Path
import csv
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]
folder=root/'Logs/Validation/2026-09-21-creature-gameplay/035315'
rows=list(csv.DictReader((folder/'recording.csv').open()))
names=['Charger','Grunt','Shooter','Leaper']
out=Image.new('RGB',(960,1296),(24,26,33));d=ImageDraw.Draw(out)
for j,name in enumerate(names):
    for phase in range(3):
        candidates=[r for r in rows if r['phase']==name and r['acting']=='True' and int(r['attackPhase'])==phase]
        assert candidates,(name,phase)
        # Maximum separation from player avoids body/weapon obscuring the face.
        r=max(candidates,key=lambda r:(float(r['enemyX'])-float(r['playerX']))**2+(float(r['enemyY'])-float(r['playerY']))**2)
        im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06}.png")
        x=round(960+float(r['enemyX'])*96);y=round(540-float(r['enemyY'])*96)
        crop=im.crop((x-160,y-200,x+160,y+100))
        out.paste(crop,(phase*320,j*324+24))
        d.text((phase*320+6,j*324+6),f"{name} phase {phase} / frame {r['frame']}",fill='white')
out.save(folder/'actual-ai-phases.png')
print(folder/'actual-ai-phases.png')
