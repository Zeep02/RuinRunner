"""Read the author's sprite rectangles without importing their Unity scene/code."""
from pathlib import Path
import tarfile, json, re
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2] / 'ArtDirection/ExternalCandidates/20260921/cainos-basic'
out = root/'source-review'; out.mkdir(exist_ok=True)
with tarfile.open(root/'original/Pixel Art Top Down - Basic v1.2.3.unitypackage') as archive:
    members = {m.name:m for m in archive.getmembers()}
    records=[]
    for member in members.values():
        if not member.name.endswith('/pathname'): continue
        name=archive.extractfile(member).read().decode('utf-8')
        if '/Texture/' not in name or not name.endswith('.png'): continue
        data=archive.extractfile(members[member.name.rsplit('/',1)[0]+'/asset.meta']).read().decode('utf-8')
        (out/(Path(name).stem+'.meta.txt')).write_text(data,encoding='utf-8')
        records.append({'name':name,'meta':Path(name).stem+'.meta.txt'})
    (out/'original-sprite-metadata.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
for file in ['TX Tileset Wall.png','TX Props.png','TX Plant.png']:
    src=Image.open(root/'original/Texture'/file).convert('RGBA')
    preview=Image.new('RGBA',(src.width*2,src.height*2),(30,32,40,255))
    preview.alpha_composite(src.resize(preview.size,Image.Resampling.NEAREST))
    draw=ImageDraw.Draw(preview)
    for x in range(0,src.width,32):
        draw.line((x*2,0,x*2,src.height*2),fill=(80,130,150,255));draw.text((x*2+2,1),str(x),fill='white')
    for y in range(0,src.height,32):
        draw.line((0,y*2,src.width*2,y*2),fill=(80,130,150,255));draw.text((1,y*2+2),str(y),fill='white')
    preview.save(out/(Path(file).stem+'-grid.png'))
print(json.dumps(records,indent=2))
