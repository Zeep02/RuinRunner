#!/usr/bin/env python3
# weapons70.json + 고른 PixelLab 후보 -> 무기 PNG, 임포트 메타, SignatureWeaponCatalog.cs 생성.
# 2026-11-05에 고른 그림은 install_weapon_picks_20261105.py가 이 결과 위에 덮어쓴다. 이 스크립트를 다시 돌리면 그것도 다시 돌린다.
import json,os,re,uuid
from PIL import Image
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"..",".."))
STAGE=os.path.join(ROOT,"Tools/Art/Staging/PixelLab/20261103-weapons70")
OUT=os.path.join(ROOT,"Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment/Weapon")
CS=os.path.join(ROOT,"Assets/RuinRunner/Runtime/Data/SignatureWeaponCatalog.cs")
PICK={"alley_awl":("dagger",4),"saw_fang":("dagger",13),"acrobat_knife":("dagger",15),"frost_fang":("dagger",21),"viper_tongue":("dagger",33),"shadow_suture":("dagger",38),"blood_bank":("dagger",45),"hour_minute":("dagger",50),"night_thread":("dagger",60),
"merc_slab":("greatsword",0),"lumber_blade":("greatsword",1),"warden_door":("greatsword",2),"cinder_cleaver":("greatsword",3),"glacier_tomb":("greatsword",4),"thunder_slug":("greatsword",5),"gravity_stele":("greatsword",6),"executioner_scale":("greatsword",7),"dawn_gate":("greatsword",10),"continent_scar":("greatsword",9),
"trainee_blade":("katana2",3),"wanderer_blade":("katana2",7),"ironback_blade":("katana2",15),"moon_white":("katana2",20),"red_lotus":("katana2",27),"wind_cutter":("katana2",33),"falling_blossom":("katana2",36),"mu_eum":("katana2",46),"instant_king":("katana2",52,"left"),"thousand_returns":("katana2",56,"left"),
"sentry_spear":("spear",0),"hunt_harpoon":("spear",1),"side_blade_pike":("spear",2),"thunder_needle":("spear",3),"tide_fang":("spear",4),"thorn_spear":("spear",5),"chain_horizon":("spear",12),"bannerless":("spear",7),"dragon_spine":("spear",8),"polaris":("spear",11),
"novice_branch":("staff",0),"charcoal_staff":("staff",1),"pebble_staff":("staff",2),"frost_branch":("staff",3),"storm_cloud_staff":("staff",14),"swamp_lantern":("staff",15),"refractor_staff":("staff",6),"undertaker_candle":("staff",7),"star_fold":("staff",10),"season_end":("staff",11),
"whetstone_orb":("orb",0),"lantern_orb":("orb",6),"iron_core":("orb",12),"frost_satellite":("orb",19),"ember_heart":("orb",24),"storm_marble":("orb",34),"twin_star":("orb",40),"mirror_eye":("orb",46),"small_eclipse":("orb",52),"world_pendulum":("orb",59),
"scrap_revolver":("gun",0),"gate_shotgun":("gun",1),"scout_carbine":("gun",2),"furnace_sprayer":("gun",14),"frost_bolt_rifle":("gun",4),"ricochet_pistol":("gun",15),"lightning_carbine":("gun",13),"siege_engineer":("gun",7),"last_bell":("gun",10),"meteor_waybill":("gun",9)}
POWER={"Common":28,"Rare":38,"Epic":45,"Legendary":54}
DELAY={"Dagger":1.6,"Greatsword":1.3,"Katana":1.15,"Spear":1.25,"Staff":1.25,"Orb":1.2,"Pistol":1.2,"Shotgun":1.8,"Rifle":1.05,"Cannon":2.1}
SKILL={"Common":(12,2.5),"Rare":(16,3.5),"Epic":(20,4.5),"Legendary":(24,5.5)}
OVERRIDE_DELAY={"siege_engineer":.75,"last_bell":2.4,"scout_carbine":1.5,"frost_bolt_rifle":1.5}
MAG={"Pistol":(7,1.3),"Shotgun":(4,1.6),"Rifle":(12,1.5),"Cannon":(4,2),"Staff":(99,.5),"Orb":(99,.5)}
OVERRIDE_MAG={"scrap_revolver":(6,1.4),"gate_shotgun":(2,1.1),"siege_engineer":(60,2.2),"last_bell":(1,1.5),"frost_bolt_rifle":(5,1.6)}
SPEED={"Pistol":18,"Shotgun":14,"Rifle":22,"Cannon":11,"Staff":12,"Orb":12}
COLOR={"Common":".75f,.75f,.75f","Rare":".25f,.55f,1f","Epic":".75f,.25f,1f","Legendary":"1f,.62f,.12f"}
KEEP_LARGEST={"meteor_waybill"}  # 후보에 옆 칸 무기의 잘린 조각이 같이 찍혀 있다.
def largest_part(im):
    w,h=im.size;px=im.load();seen=set();best=[]
    for sy in range(h):
        for sx in range(w):
            if (sx,sy) in seen or px[sx,sy][3]==0:continue
            part=[];stack=[(sx,sy)];seen.add((sx,sy))
            while stack:
                x,y=stack.pop();part.append((x,y))
                for n in((x+1,y),(x-1,y),(x,y+1),(x,y-1)):
                    if 0<=n[0]<w and 0<=n[1]<h and n not in seen and px[n][3]>0:seen.add(n);stack.append(n)
            if len(part)>len(best):best=part
    keep=set(best);out=Image.new("RGBA",im.size,(0,0,0,0))
    for x,y in keep:out.putpixel((x,y),px[x,y])
    return out
def load(wid):
    if wid=="ember_claw":return Image.open(f"{STAGE}/../20261103-ember_claw/ember_claw.png").convert("RGBA")
    p=PICK[wid];im=Image.open(f"{STAGE}/{p[0]}/{p[1]:02}.png").convert("RGBA")
    if len(p)>2:
        w,h=im.size;box=(0,0,w//2,h) if p[2]=="left" else (w//2,0,w,h);im=im.crop(box)
    if wid in KEEP_LARGEST:im=largest_part(im)
    return im.crop(im.getbbox())
GUN_GRIP={"scrap_revolver":(7,17),"gate_shotgun":(9,11),"scout_carbine":(14,10),"furnace_sprayer":(27,10),"frost_bolt_rifle":(15,9),
    "ricochet_pistol":(9,21),"lightning_carbine":(23,21),"siege_engineer":(11.5,18),"last_bell":(17,11),"meteor_waybill":(6,21)}
def rows(im,y):
    a=im.getchannel("A");xs=[x for x in range(im.width) if a.getpixel((x,min(max(y,0),im.height-1)))>0]
    return (sum(xs)/len(xs)) if xs else im.width/2
def pose(w,im):
    f=w["family"];W,H=im.size;a=im.getchannel("A")
    if f in("Dagger","Greatsword","Katana"):
        y=H-max(4,round(H*.13));return (rows(im,y)+.5,y,rows(im,1)+.5,1,90)
    if f=="Staff":
        y=round(H*.56);return (rows(im,y)+.5,y,rows(im,1)+.5,1,90)
    if f=="Spear":return (W*.3,H*.7,W-1,1,45)
    if f=="Orb":return (W/2,H/2,W/2,H/2,0)
    right=[y for y in range(H) if a.getpixel((W-1,y))>0] or [H//3]
    # 손은 그림을 보고 정한 손잡이에 둔다. 아래쪽 끝을 자동으로 찾으면 개머리판 끝을 쥔다.
    gx,gy=GUN_GRIP[w["id"]]
    return (gx,gy,W-1,sum(right)/len(right),0)
weapons=json.load(open(os.path.join(ROOT,"Tools/Art/weapons70.json"),encoding="utf-8"))["weapons"]
template=open(os.path.join(OUT,"azure_dirk.png.meta"),encoding="utf-8").read()
defs=[];poses=[]
for w in weapons:
    wid=w["id"];im=load(wid);path=os.path.join(OUT,wid+".png");im.save(path)
    g=pose(w,im);W,H=im.size
    meta=path+".meta"
    guid=re.search(r"^guid: (\w+)",open(meta,encoding="utf-8").read(),re.M).group(1) if os.path.exists(meta) else uuid.uuid4().hex
    s=re.sub(r"^guid: .*$","guid: "+guid,template,count=1,flags=re.M).replace("azure_dirk",wid)
    s=re.sub(r"spritePivot: \{x: [^,]+, y: [^}]+\}",f"spritePivot: {{x: {g[0]/W:.6f}, y: {1-g[1]/H:.6f}}}",s)
    open(meta,"w",encoding="utf-8",newline="").write(s)
    fam=w["family"];rar=w["rarity"];delay=OVERRIDE_DELAY.get(wid,DELAY[fam]);power=round(POWER[rar]*delay,1);melee=fam in("Dagger","Greatsword","Katana","Spear")
    mag=OVERRIDE_MAG.get(wid,MAG.get(fam,(0,0)));mana,cd=SKILL[rar]
    desc=f'{w["skill"]}: {w["desc"]}'
    atk="WeaponAttackType.Melee" if melee else "WeaponAttackType.Ranged"
    speed=SPEED.get(fam,1)
    defs.append(f'            Make("{wid}","{w["name"]}",WeaponRarity.{rar},WeaponFamily.{fam},{power}f,{delay}f,new Color({COLOR[rar]}),{atk},{mag[0]},{mag[1]}f,{speed}f,{mana}f,{cd}f,"{desc}"),')
    poses.append(f'            {{"{wid}",new NativeWeaponArt.Pose({g[0]}f,{g[1]}f,{g[2]}f,{g[3]}f,{g[4]}f)}},')
open(CS,"w",encoding="utf-8",newline="\n").write("""using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>The 70 signature weapons. Generated by Tools/Art/build_weapons70.py from Tools/Art/weapons70.json; edit those.</summary>
    public static class SignatureWeaponCatalog
    {
        public readonly struct Skill
        {
            public readonly float Mana,Cooldown;public readonly string Description;
            public Skill(float mana,float cooldown,string description){Mana=mana;Cooldown=cooldown;Description=description;}
        }
        static readonly Dictionary<string,Skill> skills=new Dictionary<string,Skill>();
        static readonly Dictionary<string,WeaponDefinition> byId=new Dictionary<string,WeaponDefinition>();
        public static readonly WeaponDefinition[] All=
        {
"""+"\n".join(defs)+"""
        };
        public static readonly Dictionary<string,NativeWeaponArt.Pose> Poses=new Dictionary<string,NativeWeaponArt.Pose>
        {
"""+"\n".join(poses)+"""
        };
        static WeaponDefinition Make(string id,string name,WeaponRarity rarity,WeaponFamily family,float power,float delay,Color color,WeaponAttackType type,int magazine,float reload,float speed,float mana,float cooldown,string description)
        {
            skills[id]=new Skill(mana,cooldown,description);
            var weapon=new WeaponDefinition(id,name,rarity,power,delay,color,type,magazine,reload,speed,new Vector2(.3f,.2f),1,WeaponSpecialType.ArcWave,mana,description,ItemSetType.None,family,"",1.4f,.4f);
            byId[id]=weapon;return weapon;
        }
        public static bool Has(string id)=>id!=null&&skills.ContainsKey(id);
        public static bool TryGet(string id,out Skill skill)=>skills.TryGetValue(id,out skill);
        public static WeaponDefinition Find(string id)=>byId[id];
    }
}
""")
# 검수용 라인업
cell=70;sheet=Image.new("RGB",(10*cell,7*(cell+6)),(60,70,50))
for i,w in enumerate(weapons):
    im=Image.open(os.path.join(OUT,w["id"]+".png")).convert("RGBA");sheet.paste(im,(i%10*cell+(cell-im.width)//2,i//10*(cell+6)+(cell-im.height)//2),im)
sheet=sheet.resize((sheet.width*2,sheet.height*2),0);sheet.save(os.path.join(STAGE,"lineup.png"));print("ok",len(weapons))
