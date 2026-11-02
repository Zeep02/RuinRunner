"""Separate unchanged Cainos pixels; preserve original crop and import provenance."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, re, hashlib

root=Path(__file__).resolve().parents[2]
source=root/'ArtDirection/ExternalCandidates/20260921/cainos-basic'
output=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Cainos'
records=[]
def save(name, texture, rect, pivot=(.5,.5)):
    image=Image.open(source/'original/Texture'/texture).convert('RGBA')
    x,y,w,h=rect; crop=image.crop((x,y,x+w,y+h))
    file=output/(name+'.png');file.parent.mkdir(parents=True,exist_ok=True);crop.save(file)
    assert Image.open(file).convert('RGBA').tobytes()==crop.tobytes()
    records.append(dict(path=str(file.relative_to(root)).replace('\\','/'),source=texture,rectTopLeft=rect,
                        pivot=list(pivot),ppu=32,sha256=hashlib.sha256(file.read_bytes()).hexdigest()))

for y in range(4):
    for x in range(8):save(f'Grass/{x:02}-{y:02}','TX Tileset Grass.png',(x*32,y*32,32,32))
for y in range(8):
    for x in range(4):save(f'Paving/{y*4+x:02}','TX Tileset Grass.png',(x*16,128+y*16,16,16))
save('Grass/solid','TX Tileset Grass.png',(0,0,1,1))
# One continuous 32px source strip split into the renderer's quarter-cell pieces.
for i in range(4):
    save(f'Ribbon/cap-{i}','TX Tileset Wall.png',(40+i*8,32,8,8))
    save(f'Ribbon/face-{i}','TX Tileset Wall.png',(168+i*8,54,8,16))
save('Stone/quiet','TX Tileset Stone Ground.png',(32,32,32,32))
save('Stone/worn','TX Tileset Stone Ground.png',(192,32,32,32))

selected={
 'TX Props.png':{'TX Props Statue':'statue','TX Props Stone Lantern':'lantern','TX Props Pillar':'pillar',
                 'TX Props Pillar Broken':'broken-pillar','TX Props Pot A':'pot-a','TX Props Pot B':'pot-b',
                 'TX Props Altar':'altar','TX Props - Stone 05':'stone-a','TX Props - Stone 06':'stone-b',
                 'TX Props - Brick 03':'fallen-bricks'},
 'TX Plant.png':None,
}
for texture,names in selected.items():
    text=(source/'source-review'/(Path(texture).stem+'.meta.txt')).read_text(encoding='utf-8')
    blocks=re.split(r'\n    - serializedVersion:',text.split('  spriteSheet:',1)[1])[1:]
    tree_parts={}
    for block in blocks:
        match=re.search(r'\n      name: (.+)',block)
        if not match:continue
        name=match.group(1).strip()
        if names is not None and name not in names:continue
        values={k:int(float(v)) for k,v in re.findall(r'\n        (x|y|width|height): ([\d.]+)',block)}
        if len(values)!=4:continue
        image=Image.open(source/'original/Texture'/texture)
        rect=(values['x'],image.height-values['y']-values['height'],values['width'],values['height'])
        if 'TX Tree' in name:
            tree_parts.setdefault(name.rsplit(' ',1)[0],[]).append(rect)
        leaf=names[name] if names is not None else re.sub(r'[^A-Za-z0-9]+','-',name.replace('TX Plant ','')).strip('-').lower()
        # Contact points use integer source pixels. No resizing or per-frame bbox normalization.
        w,h=rect[2:];save(('Props/' if names is not None else 'Plants/')+leaf,texture,rect,(w//2/w,0))
    for name,parts in tree_parts.items():
        left=min(p[0] for p in parts);top=min(p[1] for p in parts)
        right=max(p[0]+p[2] for p in parts);bottom=max(p[1]+p[3] for p in parts)
        save('Plants/'+name.replace('TX Tree ','').lower(),texture,(left,top,right-left,bottom-top),((right-left)//2/(right-left),0))
manifest=source/'environment-extraction.json'
manifest.write_text(json.dumps(dict(method='Unmodified source crops, native32PPU. Original source metadata read-only; no scene/package import.',images=records),indent=2),encoding='utf-8')
review=source/'source-review/environment-contact.png'
props=[r for r in records if '/Props/' in r['path'] or '/Plants/' in r['path']]
sheet=Image.new('RGBA',(6*180,((len(props)+5)//6)*195),(30,32,40,255));draw=ImageDraw.Draw(sheet)
for i,r in enumerate(props):
    sprite=Image.open(root/r['path']);x=i%6*180;y=i//6*195
    scale=1 if sprite.height>84 or sprite.width>84 else 2
    sprite=sprite.resize((sprite.width*scale,sprite.height*scale),Image.Resampling.NEAREST)
    sheet.alpha_composite(sprite,(x+4,y+20));draw.text((x+4,y+2),Path(r['path']).stem,fill='white')
sheet.save(review)
print(json.dumps({'count':len(records),'manifest':str(manifest),'contact':str(review)}))
