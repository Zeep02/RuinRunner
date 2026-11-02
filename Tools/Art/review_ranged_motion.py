"""Contact sheets of recorded game frames for visual review, not game assets."""
import csv
from pathlib import Path
from PIL import Image, ImageDraw

root = Path('Logs/Validation/2026-09-20-ranged-motion')
alignment = root / 'alignment-233000'
shots = list(alignment.glob('*.png'))
sheet = Image.new('RGB', (4*280, 4*250), '#303743')
draw = ImageDraw.Draw(sheet)
for i, path in enumerate(shots):
    shot = Image.open(path).convert('RGB')
    crop = shot.crop((835, 410, 1115, 640))
    x,y = i%4*280, i//4*250
    sheet.paste(crop,(x,y+20)); draw.text((x+5,y+3),path.stem,fill='white')
sheet.save(alignment/'catalog-review.png')

recording = root/'233035'
rows=list(csv.DictReader((recording/'recording.csv').open()))
sheet=Image.new('RGB',(4*260,8*220),'#303743');draw=ImageDraw.Draw(sheet)
for direction in range(8):
    sequence=[r for r in rows if r['phase']==f'direction-{direction}']
    choices=[sequence[0]]
    for frame in ['1','2']:
        candidates=[r for r in sequence if r['bodyFrame']==frame and r['pose']=='shoot']
        choices.append(candidates[0] if candidates else sequence[-1])
    choices.append(sequence[-1])
    for col,r in enumerate(choices):
        img=Image.open(recording/'frames'/f"frame-{int(r['frame']):06d}.png")
        crop=img.crop((840,415,1100,615));x,y=col*260,direction*220
        sheet.paste(crop,(x,y+20))
        draw.text((x+5,y+3),f"dir{direction} frame{r['frame']} {r['pose']}/{r['bodyFrame']}",fill='white')
sheet.save(recording/'shot-socket-review.png')
print(alignment/'catalog-review.png');print(recording/'shot-socket-review.png')
