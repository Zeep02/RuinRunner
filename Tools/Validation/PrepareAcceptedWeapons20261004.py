"""Write import/landmark data for reviewed originals. No raster editing."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtDirection/Review-20261004/weapon-redesign'
grips={
 'worn_dagger':(6.5,16),'wind_dagger':(6.5,16),'shadow_fang':(8.5,25),
 'rust_sword':(6.5,26),'knight_sword':(10.5,32),'sun_oath':(9.5,33),
 'iron_greatsword':(10.5,42),'dragon_slayer':(9,30),'blood_reaver':(7,39),
 'iron_mace':(7,24),'iron_hammer':(11,25),'quake_mace':(8,28),
 'cinder_tanto':(5.5,25),'azure_dirk':(6.5,20),'tide_sword':(8,29),
 'ember_sword':(7,27),'sentinel_cleaver':(10,31),'glacier_greatsword':(8,33),
 'copper_revolver':(7,10),'spark_pistol':(7,14),'signal_pistol':(12,12),
 'scout_rifle':(15,11),'coil_rifle':(5,14),'breach_shotgun':(7,11),
 'twin_bore':(6,10),'siege_mortar':(5,13),'sun_cannon':(5,15),
}
muzzles={
 'copper_revolver':(20,6),'spark_pistol':(21,7),'signal_pistol':(26,8),
 'scout_rifle':(32,5),'coil_rifle':(27,9),'breach_shotgun':(29,11),
 'twin_bore':(21,8),'siege_mortar':(29,7),'sun_cannon':(26,10),
}
rows=json.loads((OUT/'selection-draft.json').read_text(encoding='utf-8'))
accepted=[]
for row in rows:
    if row['id'] not in grips: continue
    row['grip']=grips[row['id']]
    row['muzzle']=muzzles.get(row['id'],(grips[row['id']][0],2))
    row['forwardAngle']=0 if row['id'] in muzzles else 90
    row['license']='CC0-1.0'
    accepted.append(row)
native_katana=OUT/'katana-outline-replacement.json'
if native_katana.exists():
    for source in json.loads(native_katana.read_text(encoding='utf-8'))['selected']:
        row=dict(id=source['id'],source=str(Path(source['source']).relative_to(ROOT)).replace('\\','/'),family='Katana',
                 size=[source['nativeWidth'],source['nativeHeight']],bbox=source['nativeVisibleBounds'],sourceSha256=source['sha256'],
                 grip=source['gripTopLeftPixels'],muzzle=source['bladeTipTopLeftPixels'],forwardAngle=source['forwardAngleDegrees'],
                 license='Geru Pixswords: free personal/commercial use and modification; no standalone resale or ownership claim')
        accepted.append(row)
(OUT/'accepted-selection.json').write_text(json.dumps(accepted,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(accepted),'native original sprites queued; no generated or resampled assets')
