"""Separate reviewed firearm/crossbow originals; explicit authored grip and muzzle coordinates."""
from pathlib import Path
from PIL import Image
import json,hashlib
root=Path(__file__).resolve().parents[2];packs=root/'ArtDirection/ExternalCandidates/20260922'
folder=packs/'native-item-selection'
entries=json.loads((folder/'melee-bow-import.json').read_text())['images']
# Coordinates on the original image before two-pixel transparent padding.
guns=[('scrap_pistol',1,'19',(4,7),(15,5)),('flintlock',1,'17',(4,7),(16,4)),
 ('void_revolver',1,'18',(4,7),(14,5)),('scrap_shotgun',1,'33',(4,6),(16,5)),
 ('scattergun',1,'43',(5,8.5),(26,4)),('storm_rifle',1,'30',(5,9),(26,7)),
 ('starbreaker',1,'54',(5,13),(34,7)),('dragon_cannon',1,'51',(7,15),(32,10)),
 ('twin_pistols',1,'25',(4,7),(13,5))]
selections=[]
for id,pack,num,grip,muzzle in guns:
 selections.append((id,packs/f'snoopeth-guns/original/Pack {pack}/2px/{num}.png',grip,muzzle,0))
for id,num,grip,muzzle in [('hand_crossbow','31',(11,20),(21,10)),('repeater','29_no_effect',(13,20),(23,10)),('siege_crossbow','33',(12,21),(23,10))]:
 selections.append((id,packs/f'craftpix-crossbows/original/PNG/Transperent/Icon{num}.png',grip,muzzle,45))
for id,source,grip,muzzle,angle in selections:
 im=Image.open(source).convert('RGBA');canvas=Image.new('RGBA',(im.width+4,im.height+4));canvas.paste(im,(2,2))
 dest=root/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon/{id}.png';canvas.save(dest)
 grip=[v+2 for v in grip];muzzle=[v+2 for v in muzzle]
 entries.append(dict(id=id,kind='Weapon',source=source.relative_to(root).as_posix(),sourceCell=None,
  sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),sourceSize=list(im.size),spriteSize=list(canvas.size),
  path=dest.relative_to(root).as_posix(),pivot=[grip[0]/canvas.width,1-grip[1]/canvas.height],grip=grip,muzzle=muzzle,angle=angle,ppu=32))
(folder/'all-weapons-import.json').write_text(json.dumps(dict(images=entries),indent=2))
cs=root/'Assets/RuinRunner/Runtime/Player/NativeWeaponArt.cs';text=cs.read_text();start=text.index('            { "worn_dagger"');end=text.index('        };',start)
lines=['            { "'+e['id']+'", new Pose('+','.join(str(v)+'f' for v in (*e['grip'],*e['muzzle'],e['angle']))+') },' for e in entries]
cs.write_text(text[:start]+'\n'.join(lines)+'\n'+text[end:])
# Projectile source is the same native art family as the bows; no oversized generated shaft.
source=packs/'7soul-icons/original/W_Bow17.png';im=Image.open(source).convert('RGBA')
dest=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Projectile/Arrow.png';dest.parent.mkdir(parents=True,exist_ok=True);im.save(dest)
(folder/'projectile-import.json').write_text(json.dumps(dict(images=[dict(path=dest.relative_to(root).as_posix(),pivot=[.5,.5],ppu=32,source=source.relative_to(root).as_posix(),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest())]),indent=2))
print('Prepared31 native held weapons,12new firearm/crossbow originals and native arrow')
