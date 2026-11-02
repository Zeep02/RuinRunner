"""Contact sheet of untouched Game View crops, for motion review only."""
import argparse,csv,json
from pathlib import Path
from PIL import Image,ImageDraw

p=argparse.ArgumentParser();p.add_argument('capture',type=Path);a=p.parse_args()
rows=list(csv.DictReader((a.capture/'recording.csv').open(encoding='utf-8-sig')))
sheet=Image.new('RGB',(6*200,8*190),'#242832');draw=ImageDraw.Draw(sheet);proof=[]
for direction in range(8):
    for pose in range(6):
        hits=[r for r in rows if r['phase']==f'standing-{direction}' and r['pose']=='idle' and int(r['direction'])==direction and int(r['bodyFrame'])==pose]
        assert hits,(direction,pose)
        r=hits[len(hits)//2];im=Image.open(a.capture/'frames'/f"frame-{int(r['frame']):06d}.png")
        crop=im.crop((864,450,1056,610))
        x=pose*200;y=direction*190;sheet.paste(crop,(x+4,y+26))
        draw.text((x+4,y+7),f"Dir {direction} / pose {pose} / f{r['frame']}",fill='white')
        proof.append(r)
sheet.save(a.capture/'actual-standing-contact.png')
(a.capture/'contact-selection.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(a.capture/'actual-standing-contact.png')
