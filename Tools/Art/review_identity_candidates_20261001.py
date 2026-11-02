"""Read-only native-size sheets; never edit adopted or original raster images."""
from pathlib import Path
from PIL import Image,ImageDraw
import hashlib,json
root=Path(__file__).resolve().parents[2]
out=root/'ArtDirection/Review-20261001/weapon-identity-more';out.mkdir(parents=True,exist_ok=True)
src=root/'ArtDirection/ExternalCandidates/20261001/icemaan-vfx/original/Frames'
report=[]
for folder in [src/'Slashes'/n for n in ['Wide Cleave','Slash Down','Cross Slash','Katana Draw','Dagger Flick','Thrust']]+[src/'Hit Sparks'/n for n in ['Heavy Hit','Blunt Thud','Pierce Spark']]:
    paths=sorted(folder.glob('*.png'));images=[Image.open(p).convert('RGBA') for p in paths]
    w,h=images[0].size
    rgba=[im.tobytes() for im in images]
    seen=set();valid=[]
    for i,im in enumerate(images):
        if not im.getchannel('A').getbbox() or rgba[i] in seen:continue
        seen.add(rgba[i]);valid.append(i)
    sheet=Image.new('RGBA',(12*(w+8),((len(images)+11)//12)*(h+20)),(37,39,47,255));draw=ImageDraw.Draw(sheet)
    for i,im in enumerate(images):
        x=i%12*(w+8);y=i//12*(h+20)
        sheet.alpha_composite(im,(x,y+18));draw.text((x,y),str(i),fill=(240,240,220) if i in valid else (120,120,120))
    sheet.save(out/(folder.name+'.png'))
    report.append(dict(clip=folder.name,size=[w,h],files=len(images),nonemptyUnique=len(valid),sourceIndices=valid,firstAlphaBbox=images[valid[0]].getchannel('A').getbbox(),hashes=[hashlib.sha256(x).hexdigest() for x in rgba]))
(out/'candidate-review.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps([{k:v for k,v in r.items() if k!='hashes'} for r in report]))
src=root/'ArtDirection/ExternalCandidates/20261001/frostwindz-slashes/original/64x64'
sheet=Image.new('RGBA',(10*64,15*82),(37,39,47,255));draw=ImageDraw.Draw(sheet);row=0
for n in range(1,4):
    for c in range(1,6):
        folder=src/f'Slash {n}'/f'color{c}'
        paths=sorted((p for p in folder.rglob('*.png') if 'frame' in p.stem.lower()),key=lambda p:int(p.stem.lower().split('frame')[-1]))
        if not paths:continue
        for i,p in enumerate(paths):sheet.alpha_composite(Image.open(p).convert('RGBA'),(i*64,row*82+18))
        draw.text((0,row*82),f'Slash{n} color{c}',fill='white');row+=1
sheet.save(out/'frostwindz-native.png')
