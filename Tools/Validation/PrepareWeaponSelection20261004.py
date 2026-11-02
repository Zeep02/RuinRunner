"""Selection manifest and read-only review; original source pixels are not edited."""
from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtDirection/Review-20261004/weapon-redesign';OUT.mkdir(parents=True,exist_ok=True)
S='ArtDirection/ExternalCandidates/20261004/snoopeth-melee/original/'
D='ArtDirection/ExternalCandidates/20261004/dcss-swords/original/32x32/'
G='ArtDirection/ExternalCandidates/20260922/snoopeth-guns/original/Pack 2/2px/'
specs=[
('worn_dagger',S+'Pack 1/2px/28.png','Dagger'),
('wind_dagger',S+'Pack 2/2px/31.png','Dagger'),
('shadow_fang',S+'Pack 2/2px/33.png','Dagger'),
('rust_sword',S+'Pack 1/2px/29.png','Longsword'),
('knight_sword',S+'Pack 2/2px/16.png','Longsword'),
('sun_oath',S+'Pack 2/2px/34.png','Longsword'),
('iron_greatsword',S+'Pack 1/2px/16.png','Greatsword'),
('dragon_slayer',S+'Pack 4/2px/14.png','Greatsword'),
('blood_reaver',S+'Pack 1/2px/25.png','Greatsword'),
('training_katana',D+'katana/katana_03.png','Katana'),
('moon_blade',D+'katana/katana_02.png','Katana'),
('blood_moon_katana',D+'urand/urand_katana.png','Katana'),
('iron_mace',S+'Pack 2/2px/06.png','Mace'),
('iron_hammer',S+'Pack 6/2px/01.png','Mace'),
('quake_mace',S+'Pack 4/2px/24.png','Mace'),
('cinder_tanto',S+'Pack 1/2px/13.png','Dagger'),
('azure_dirk',S+'Pack 2/2px/01.png','Dagger'),
('tide_sword',S+'Pack 4/2px/31.png','Longsword'),
('ember_sword',S+'Pack 4/2px/32.png','Longsword'),
('sentinel_cleaver',S+'Pack 4/2px/23.png','Greatsword'),
('glacier_greatsword',S+'Pack 1/2px/24.png','Greatsword'),
('copper_revolver',G+'18.png','Pistol'),
('spark_pistol',G+'4.png','Pistol'),
('signal_pistol',G+'36.png','Pistol'),
('scout_rifle',G+'22.png','Rifle'),
('coil_rifle',G+'33.png','Rifle'),
('breach_shotgun',G+'21.png','Shotgun'),
('twin_bore',G+'32.png','Shotgun'),
('siege_mortar',G+'9.png','Cannon'),
('sun_cannon',G+'5.png','Cannon'),
]
rows=[]
for id,path,family in specs:
    p=ROOT/path;im=Image.open(p).convert('RGBA')
    rows.append(dict(id=id,source=path,family=family,size=list(im.size),bbox=list(im.getbbox()),sourceSha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(OUT/'selection-draft.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
sheet=Image.new('RGB',(1200,190*((len(rows)+7)//8)),(67,70,70));draw=ImageDraw.Draw(sheet)
for n,row in enumerate(rows):
    im=Image.open(ROOT/row['source']).convert('RGBA');c=im.crop(im.getbbox());k=min(3,134//c.width,154//c.height);z=c.resize((c.width*k,c.height*k),Image.Resampling.NEAREST)
    x=n%8*150+(150-z.width)//2;y=n//8*190+6;sheet.paste(z,(x,y),z);draw.text((n%8*150+3,n//8*190+165),row['id'],fill='white')
sheet.save(OUT/'replacement-draft.png')
print(len(rows),'selected original assets; draft only')
for start in range(0,len(rows),10):
    page=Image.new('RGB',(1200,500),(59,61,68));d=ImageDraw.Draw(page)
    for j,row in enumerate(rows[start:start+10]):
        im=Image.open(ROOT/row['source']).convert('RGBA');z=im.resize((im.width*4,im.height*4),Image.Resampling.NEAREST)
        x=j%5*240+20;y=j//5*250+20;page.paste(z,(x,y),z)
        for k in range(0,im.width,4):d.text((x+k*4,y-13),str(k),fill='#ccd0de')
        for k in range(0,im.height,4):d.text((x-17,y+k*4),str(k),fill='#ccd0de')
        d.text((j%5*240+5,j//5*250+230),row['id'],fill='white')
    page.save(OUT/f'landmarks-{start//10}.png')
