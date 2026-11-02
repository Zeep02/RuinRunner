"""Review unmodified source art at integer pixel magnification."""
from pathlib import Path
from PIL import Image, ImageDraw
import json

root=Path(__file__).resolve().parents[2]
native=root/'ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack/FX'
sources=[('Explosion',native/'Elemental/Explosion/SpriteSheet.png',32,32),
 ('SmokeCircular',native/'Smoke/SmokeCircular/SpriteSheet.png',32,32),
 ('SlashDouble',native/'Attack/SlashDoubleCurved/SpriteSheet.png',32,32),
 ('SlashCurved',native/'Attack/SlashCurved/SpriteSheet.png',32,32),
 ('Rock',native/'Projectile/SpriteSheetRock.png',16,16),
 ('Spark',native/'Particle/Spark.png',8,8),('RockGray',native/'Particle/RockGray.png',8,8),
 ('Bullet',root/'ArtDirection/ExternalCandidates/20260922/scrython-guns/original/GunsAssetPack/Individual_Gun_Sprites/Extras/Bullet.png',16,16)]
out=root/'ArtDirection/Review-20261001/weapon-fx';out.mkdir(parents=True,exist_ok=True)
report=[]
for name,path,w,h in sources:
 im=Image.open(path).convert('RGBA');report.append(dict(name=name,size=im.size))
 # Contact sheets are review artifacts only; original/runtime art remains unchanged.
 frames=[im.crop((x,y,x+w,y+h)) for y in range(0,im.height,h) for x in range(0,im.width,w)]
 sheet=Image.new('RGBA',(8*(w*3+8),((len(frames)+7)//8)*(h*3+22)),(55,58,65,255));d=ImageDraw.Draw(sheet)
 for i,f in enumerate(frames):
  x=(i%8)*(w*3+8);y=(i//8)*(h*3+22);d.text((x,y),str(i),fill='white');sheet.alpha_composite(f.resize((w*3,h*3),Image.Resampling.NEAREST),(x,y+18))
 sheet.convert('RGB').save(out/(name+'.png'))
print(json.dumps(report))
