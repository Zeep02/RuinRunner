"""Read-only pixel coordinate contact sheet; never writes production images."""
from pathlib import Path
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
base=ROOT/'ArtDirection/ExternalCandidates/20261004/tetrabyte-swords/native-cells'
out=ROOT/'ArtDirection/Review-20261004/weapon-redesign/native-katana-coordinates.png'
sheet=Image.new('RGB',(1152,430),(62,65,68));d=ImageDraw.Draw(sheet)
for i,key in enumerate(['training_katana','moon_blade','blood_moon_katana']):
    im=Image.open(base/(key+'.png')).convert('RGBA');z=im.resize((320,320),Image.Resampling.NEAREST)
    x=i*384+35;y=34;sheet.paste(z,(x,y),z)
    for k in range(0,32,2):
        d.text((x+10*k,y-18),str(k),fill='white')
        d.text((x-22,y+10*k),str(k),fill='white')
    d.text((i*384+15,376),key+' / native 32x32',fill='white')
    sheet.paste(im,(i*384+180,398),im)
sheet.save(out)
print(out)
