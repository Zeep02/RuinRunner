from pathlib import Path
from PIL import Image,ImageDraw
import csv
import numpy as np
root=Path(__file__).resolve().parents[2];folder=root/'Logs/Validation/2026-09-21-hero-walk/041856'
rows=list(csv.DictReader((folder/'recording.csv').open()))
def actor_crop(im):
    a=np.array(im.convert('RGB'));r=a[220:850,800:1120].astype(float)
    red=(r[:,:,0]>90)&(r[:,:,0]>r[:,:,1]*1.55)&(r[:,:,1]<90)&(r[:,:,2]<130)
    yy,xx=np.where(red)
    assert len(yy)>8,'No scarf in camera review region'
    neck_y=round(float(np.median(yy)))+220
    return im.crop((880,neck_y-90,1040,neck_y+102))
for family in ['Longsword','Pistol']:
    chosen=[]
    for i in range(12):
        candidates=[r for r in rows if r['phase'].startswith(family+'-south') and r['pose']=='move' and int(r['bodyFrame'])==i]
        if candidates:chosen.append(candidates[0])
    contact=Image.new('RGB',(960,432),(24,27,36));d=ImageDraw.Draw(contact)
    for k,r in enumerate(chosen):
        im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06}.png")
        # Read-only scarf tracking accounts for camera lead. Each 160x192
        # game-pixel crop is copied unchanged; no resizing/stretching.
        contact.paste(actor_crop(im),(k%6*160,k//6*216+24))
        d.text((k%6*160+4,k//6*216+5),f"pose {r['bodyFrame']} / {r['frame']}",fill='white')
    contact.save(folder/(family.lower()+'-gait-actual.png'))
transition=[0,14,15,39,40,45,60,90,99,105,112,119]
contact=Image.new('RGB',(960,432),(24,27,36));d=ImageDraw.Draw(contact)
for k,n in enumerate(transition):
    r=rows[n];im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06}.png")
    contact.paste(actor_crop(im),(k%6*160,k//6*216+24))
    d.text((k%6*160+4,k//6*216+5),f"{r['pose']} {r['bodyFrame']} / {r['frame']}",fill='white')
contact.save(folder/'transitions-actual.png')
