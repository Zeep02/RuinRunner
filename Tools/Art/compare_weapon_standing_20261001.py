"""Read-only actual screenshot before/after comparison, no asset modification."""
from pathlib import Path
import csv,json
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]
base=root/'Logs/Validation/2026-10-01-weapon-carry'
folders=[base/'audit-223352',base/'audit-003505']
tables=[{r['phase']:r for r in csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig'))} for folder in folders]
out=root/'ArtDirection/Review-20261001/weapon-carry';out.mkdir(parents=True,exist_ok=True)
weapons=['hunter_bow','iron_greatsword','scrap_pistol']
sheet=Image.new('RGB',(6*176,3*206),(26,25,34));draw=ImageDraw.Draw(sheet)
for wi,w in enumerate(weapons):
    for version in range(2):
        for di,d in enumerate([3,4,5]):
            r=tables[version][f'{w}-view-{d}'];im=Image.open(folders[version]/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB')
            x=float(r['originSX'])+24*float(r['scaleX']);y=720-float(r['originSY'])+28*float(r['scaleY'])
            left=round(x-88);top=round(y-86);col=version*3+di
            sheet.paste(im.crop((left,top,left+176,top+176)),(col*176,wi*206+30))
            draw.text((col*176+4,wi*206+5),f"{'BEFORE' if version==0 else 'CURRENT'} {w} / {d}",fill=(230,230,240))
sheet.save(out/'bow-melee-stance-before-after.png')
sheet.crop((3*176,0,6*176,206)).save(out/'bow-standing-current-south.png')
(out/'before-after-scope.json').write_text(json.dumps(dict(before=str(folders[0]),current=str(folders[1]),directions=[3,4,5],weapons=weapons,scope='Exact 176x176 crops from actual native 1280x720 screenshots. Original pixels retained; no resizing or painted correction.'),indent=2),encoding='utf-8')
print(str(out/'bow-melee-stance-before-after.png'))
