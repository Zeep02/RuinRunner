from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]
base=root/'Logs/Validation/2026-10-01-weapon-identity'
samples=[('Katana parry',base/'presentation-110830/training_katana-action-peak.png'),
 ('Bow spread',base/'presentation-110830/hunter_bow-action-peak.png'),
 ('Shotgun cone',base/'presentation-110830/scrap_shotgun-action-peak.png'),
 ('Rifle piercing',base/'presentation-110830/storm_rifle-action-peak.png'),
 ('Mace ground impact',base/'presentation-112019/iron_mace-action-peak.png'),
 ('Cannon explosion',base/'presentation-112019/dragon_cannon-action-peak.png')]
out=Image.new('RGB',(940,810),(30,33,42));d=ImageDraw.Draw(out)
for i,(title,path) in enumerate(samples):
 x=i%2*470;y=i//2*270;d.text((x+8,y+5),title,fill='white')
 out.paste(Image.open(path).convert('RGB').crop((510,205,970,445)),(x+5,y+25))
out.save(root/'ArtDirection/Review-20261001/weapon-fx/final-native-overview.png')
print('Six native 1x screen crops, no rescale or synthesized frames')
