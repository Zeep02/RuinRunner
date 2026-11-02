"""Read-only source contact sheets; never writes project sprites."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'ArtDirection/ExternalCandidates/20261004/snoopeth-melee/original'
OUT=ROOT/'ArtDirection/Review-20261004/weapon-redesign'
OUT.mkdir(parents=True,exist_ok=True)
rows=[]
for pack in sorted(SOURCE.glob('Pack *')):
    files=sorted([p for p in (pack/'2px').glob('*.png') if max(Image.open(p).size)<100],key=lambda p:(not p.stem.isdigit(),int(p.stem) if p.stem.isdigit() else 0,p.name))
    sheet=Image.new('RGB',(1280,160*((len(files)+9)//10)),(48,49,60));draw=ImageDraw.Draw(sheet)
    for n,p in enumerate(files):
        im=Image.open(p).convert('RGBA');bbox=im.getbbox()
        if not bbox:continue
        crop=im.crop(bbox);factor=min(3,110//crop.width,126//crop.height)
        zoom=crop.resize((crop.width*factor,crop.height*factor),Image.Resampling.NEAREST)
        x=(n%10)*128+(128-zoom.width)//2;y=(n//10)*160+7
        sheet.paste(zoom,(x,y),zoom);draw.text(((n%10)*128+5,(n//10)*160+140),p.stem,fill='white')
        rows.append(dict(pack=pack.name,file=p.name,size=im.size,bbox=bbox,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    sheet.save(OUT/(pack.name.replace(' ','-')+'.png'))
(OUT/'source-inventory.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('Reviewed source sheets:',len(rows),'PNGs')
for category,folder in [('timberwolf',ROOT/'ArtDirection/ExternalCandidates/20261004/timberwolf-weapons/original/oubliette_weapons - free'),('guns',ROOT/'ArtDirection/ExternalCandidates/20260922/snoopeth-guns/original/Pack 2/2px')]:
    files=sorted([p for p in folder.glob('*.png') if max(Image.open(p).size)<100])
    if category=='timberwolf':files=[p for p in files if any(s in p.name for s in ('katana','mace','hammer'))]
    sheet=Image.new('RGB',(1280,150*((len(files)+7)//8)),(65,66,71));draw=ImageDraw.Draw(sheet)
    for n,p in enumerate(files):
        im=Image.open(p).convert('RGBA');crop=im.crop(im.getbbox());factor=max(1,min(3,145//crop.width,116//crop.height));zoom=crop.resize((crop.width*factor,crop.height*factor),Image.Resampling.NEAREST)
        x=(n%8)*160+(160-zoom.width)//2;y=(n//8)*150+4;sheet.paste(zoom,(x,y),zoom)
        label=p.stem.replace('spr_wep_','');draw.text(((n%8)*160+3,(n//8)*150+126),label,fill='white')
    sheet.save(OUT/(category+'.png'))
