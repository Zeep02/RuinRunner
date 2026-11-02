"""Separate reviewed source icons without repainting or resampling their pixels."""
from pathlib import Path
import json, hashlib
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parents[2]
packs=root/'ArtDirection/ExternalCandidates/20260922'
old=json.loads((packs/'native-item-selection/manifest.json').read_text())['images']
entries=[x.copy() for x in old if x['kind']=='Accessory']
shika=next((packs/'shikashi-icons/original').rglob('#1 - Transparent Icons.png'))
ring=next(p for p in (packs/'kyrise-icons/original').rglob('ring_03d.png') if p.parent.name=='16x16')
for e in entries:
    if e['id']=='lucky_dice':e['sourceCell']=[13*32,13*32,32,32]
    if e['id']=='ruby_heart':e['sourceCell']=[6*32,0,32,32]
    if e['id']=='mana_prism':e['source']='ArtDirection/ExternalCandidates/20260922/7soul-icons/original/I_Crystal01.png'
    if e['id']=='crimson_ring':e['source']=ring.relative_to(root).as_posix();e['sourceCell']=None
    source=root/e['source'];im=Image.open(source).convert('RGBA')
    if e['sourceCell']:
        x,y,w,h=e['sourceCell'];im=im.crop((x,y,x+w,y+h))
    e['sourceSize']=list(im.size);e['sourceSha256']=hashlib.sha256(source.read_bytes()).hexdigest()
    canvas=Image.new('RGBA',(im.width+4,im.height+4));canvas.paste(im,(2,2))
    dest=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Accessory'/(e['id']+'.png')
    dest.parent.mkdir(parents=True,exist_ok=True);canvas.save(dest)
    e['path']=dest.relative_to(root).as_posix();e['pivot']=[.5,.5];e['ppu']=32
(packs/'native-item-selection/accessory-import.json').write_text(json.dumps(dict(images=entries),indent=2))
out=Image.new('RGB',(900,840),(82,86,95));draw=ImageDraw.Draw(out)
for i,e in enumerate(entries):
    im=Image.open(root/e['path']);im=im.resize((im.width*4,im.height*4),Image.Resampling.NEAREST)
    x=i%5*180;y=i//5*210;out.paste(im,(x,y),im);draw.text((x,y+180),e['id'],fill='white')
out.save(packs/'native-item-selection/accessories-selected4x.png')
print('Separated',len(entries),'accessories, original pixel density retained')
