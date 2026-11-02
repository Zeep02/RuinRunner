"""Select original authored pixels; pad without resampling. Review before runtime adoption."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib
root=Path(__file__).resolve().parents[2]
packs=root/'ArtDirection/ExternalCandidates/20260922'
seven=packs/'7soul-icons/original'
fish=packs/'fartfish-weapons/original/Weapon Pack'
shika=next((packs/'shikashi-icons/original').rglob('#1 - Transparent Icons.png'))
out=root/'Assets/RuinRunner/ArtReview/NativeItems'
weapons={
'worn_dagger':'W_Dagger003','wind_dagger':'W_Dagger017','shadow_fang':'W_Dagger019',
'rust_sword':'W_Sword010','knight_sword':'W_Sword003','sun_oath':'W_Sword015',
'iron_mace':'W_Mace009','iron_hammer':'W_Mace003','quake_mace':'W_Mace004',
'training_katana':'W_Sword007','moon_blade':'W_Sword014','blood_moon_katana':'W_Sword016',
'iron_greatsword':'W_Sword004','dragon_slayer':'W_Sword018','blood_reaver':'W_Sword021',
'hunter_bow':'W_Bow02','composite_bow':'W_Bow04','great_bow':'W_Bow16','celestial_bow':'W_Bow13',
'scrap_pistol':'fish:pistol','flintlock':'fish:flintlock','void_revolver':'fish:revolver',
'twin_pistols':'W_Gun002','scrap_shotgun':'fish:shotgun','scattergun':'fish:rifle_b',
'storm_rifle':'fish:rifle_a','starbreaker':'fish:ak47','dragon_cannon':'I_Cannon05',
'hand_crossbow':'shika:4:6','repeater':'fish:crossbow','siege_crossbow':'shika:11:5'}
accessories={
'crimson_ring':'shika:5:8','storm_feather':'I_Feather01','guardian_seal':'Ac_Medal02',
'mana_prism':'I_Crystal02','lucky_dice':'shika:12:13','berserker_emblem':'Ac_Medal03',
'bulwark_brooch':'Ac_Medal04','sage_sigil':'Ac_Medal01','ruby_heart':'shika:0:1',
'sapphire_flask':'P_Blue04','guardian_plate':'A_Armour02','winged_boots':'A_Shoes05',
'assassin_mark':'shika:0:0','executioners_coin':'I_GoldCoin','four_leaf_clover':'I_Clover',
'arcane_lens':'shika:8:10','vampire_fang':'I_Fang','soul_capacitor':'I_Crystal03'}
records=[]
for kind,entries in [('Weapon',weapons),('Accessory',accessories)]:
    for id,name in entries.items():
        cell=None
        if name.startswith('fish:'): source=fish/(name.split(':')[1]+'.png')
        elif name.startswith('shika:'):
            source=shika; _,x,y=name.split(':');cell=[int(x)*32,int(y)*32,32,32]
        else: source=seven/(name+'.png')
        im=Image.open(source).convert('RGBA')
        if cell: x,y,w,h=cell;im=im.crop((x,y,x+w,y+h))
        # Transparent padding reserves one native pixel around the drawn silhouette
        # for the renderer's black outline; source image pixels stay untouched.
        canvas=Image.new('RGBA',(im.width+4,im.height+4));canvas.paste(im,(2,2))
        dest=out/kind/(id+'.png');dest.parent.mkdir(parents=True,exist_ok=True);canvas.save(dest)
        records.append(dict(id=id,kind=kind,path=dest.relative_to(root).as_posix(),
            source=source.relative_to(root).as_posix(),sourceCell=cell,sourceSize=list(im.size),
            sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),pivot=[.5,.5],ppu=32))
review=Image.new('RGB',(840,((len(records)+6)//7)*130),(82,86,95));draw=ImageDraw.Draw(review)
for i,r in enumerate(records):
    im=Image.open(root/r['path']);im=im.resize((im.width*2,im.height*2),Image.Resampling.NEAREST)
    x=i%7*120;y=i//7*130;review.paste(im,(x+12,y+6),im);draw.text((x+3,y+104),r['id'],fill='white')
(packs/'native-item-selection').mkdir(exist_ok=True)
review.save(packs/'native-item-selection/review.png')
(packs/'native-item-selection/manifest.json').write_text(json.dumps(dict(images=records),indent=2))
print('Prepared',len(records),'original-pixel candidates; runtime not changed')
