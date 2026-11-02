"""Read-only game-capture layout. No source asset is edited or rescaled."""
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
base=ROOT/'Logs/Validation/2026-10-04-weapon-redesign/114338'
ids=['cinder_tanto','azure_dirk','tide_sword','ember_sword','sentinel_cleaver','glacier_greatsword',
     'copper_revolver','spark_pistol','signal_pistol','scout_rifle','coil_rifle',
     'breach_shotgun','twin_bore','siege_mortar','sun_cannon']
for page in range(5):
    canvas=Image.new('RGB',(1280,675),(34,37,41));d=ImageDraw.Draw(canvas)
    for row,key in enumerate(ids[page*3:page*3+3]):
        source=Image.open(base/('views-'+key+'.png'))
        d.text((8,row*225+5),key+' / N NE E SE S SW W NW',fill='white')
        for direction in range(8):
            x=direction%4*240;y=direction//4*240
            crop=source.crop((x+40,y+25,x+200,y+225))
            canvas.paste(crop,(direction*160,row*225+25))
    canvas.save(base/('added-views-'+str(page)+'.png'))
# User preview is four crops from the actual 1280x720 render, at unchanged pixel size.
chosen=['training_katana','moon_blade','blood_moon_katana','scrap_pistol','rust_sword','iron_greatsword','iron_mace','scout_rifle']
canvas=Image.new('RGB',(800,420),(34,37,41));d=ImageDraw.Draw(canvas)
for i,key in enumerate(chosen):
    source=Image.open(base/('views-'+key+'.png'))
    # Third direction is east; preserve 1:1 screenshot pixels.
    crop=source.crop((480+20,35,480+220,215))
    canvas.paste(crop,(i%4*200,i//4*210+24))
    d.text((i%4*200+6,i//4*210+7),key,fill='white')
canvas.save(base/'native-weapon-preview.png')
print(base)
