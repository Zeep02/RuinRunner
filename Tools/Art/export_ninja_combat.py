"""Lossless frame extraction for authored finishers and the encounter seal."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib
root=Path(__file__).resolve().parents[2]
source=root/'ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/FX'
output=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/NinjaAdventure/Combat'
clips={'Thrust':('Attack/Cut/SpriteSheet.png',32,32,4),'CrossCut':('Attack/CutX/SpriteSheet.png',32,32,4),
       'Cleave':('Attack/SlashDoubleCurved/SpriteSheet.png',32,32,4),'Impact':('Magic/Circle/SpriteSheetOrange.png',32,32,4),
       'SealSpark':('Magic/Spark/SpriteSheet.png',30,35,9),'SealBolt':('Elemental/Thunder/SpriteSheet.png',20,28,8)}
records=[]
review=root/'ArtDirection/ExternalCandidates/20260921/ninja-adventure/combat-review';review.mkdir(exist_ok=True)
for clip,(path,w,h,count) in clips.items():
    sheet=Image.open(source/path).convert('RGBA');assert sheet.size==(w*count,h)
    canvas=(max(w,h)+1)//2*2;offset=((canvas-w)//2,(canvas-h)//2)
    contact=Image.new('RGBA',(count*(canvas*2+8),canvas*2+24),(30,32,40,255));draw=ImageDraw.Draw(contact)
    for i in range(count):
        crop=sheet.crop((i*w,0,(i+1)*w,h));image=Image.new('RGBA',(canvas,canvas));image.paste(crop,offset)
        target=output/clip/f'{i:02}.png';target.parent.mkdir(parents=True,exist_ok=True);image.save(target)
        assert Image.open(target).crop((offset[0],offset[1],offset[0]+w,offset[1]+h)).tobytes()==crop.tobytes()
        records.append(dict(path=target.relative_to(root).as_posix(),source=path,rect=[i*w,0,w,h],ppu=32,pivot=[.5,.5],sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
        contact.alpha_composite(image.resize((canvas*2,canvas*2),Image.Resampling.NEAREST),(i*(canvas*2+8),22));draw.text((i*(canvas*2+8),2),str(i),fill='white')
    contact.save(review/(clip+'.png'))
manifest=root/'ArtDirection/ExternalCandidates/20260921/ninja-adventure/combat-extraction.json'
manifest.write_text(json.dumps(dict(license='CC0-1.0',method='Original frame cells, transparent padding only, no repaint or rescale.',images=records),indent=2),encoding='utf-8')
print(json.dumps({'frames':len(records),'review':str(review),'manifest':str(manifest)}))
