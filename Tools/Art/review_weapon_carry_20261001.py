"""Read-only native screenshot contact sheets; never edit game assets."""
from pathlib import Path
import csv,json,sys
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/weapon-carry'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
weapons=list(dict.fromkeys(r['phase'].split('-view-')[0] for r in rows))
for w in weapons:
    sheet=Image.new('RGB',(8*176,206),(26,25,34));draw=ImageDraw.Draw(sheet)
    for r in rows:
        if not r['phase'].startswith(w+'-view-'):continue
        d=int(r['phase'].split('-view-')[1]);im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB')
        x=float(r['originSX'])+24*float(r['scaleX']);y=720-float(r['originSY'])+28*float(r['scaleY'])
        left=round(x-88);top=round(y-86);sheet.paste(im.crop((left,top,left+176,top+176)),(d*176,30))
        draw.text((d*176+4,5),f'{w} / {d}',fill=(230,230,240))
    sheet.save(out/(w+'-eight-directions.png'))
print(json.dumps({'poses':len(rows),'weapons':len(weapons),'path':str(out)}))
