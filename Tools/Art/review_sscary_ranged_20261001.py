"""Read-only pose/identity review. Review magnification never enters game assets."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib, collections, sys, math
root=Path(__file__).resolve().parents[2]
folder=root/'ArtDirection/Generated/SscaryMotion-20261001'/sys.argv[1] if len(sys.argv)>1 else root/'ArtDirection/Generated/SscaryMotion-20261001/melee-v1'
out=root/'ArtDirection/Review-20261001/hero-ranged'/folder.name;out.mkdir(parents=True,exist_ok=True)
data=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
palette=set(); groups=collections.defaultdict(list); cache={}; hidden=[]
def read(path):
    if path not in cache:cache[path]=Image.open(root/path).convert('RGBA')
    return cache[path]
for p in (root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale').rglob('*.png'):
    palette.update(c for c in Image.open(p).convert('RGBA').getdata() if c[3])
packs={'up':(19,32,28,39),'right_up':(17,30,28,39),'right_down':(18,30,22,39),'down':(0,0,0,0),'left_down':(28,30,32,39),'left_up':(22,30,33,39)}
skin={(229,149,159,255),(193,106,125,255)}
def pack_pixel(src,x,y):
    c=src.getpixel((x,y))
    edge=c==(0,0,0,255) and any(src.getpixel((u,v)) in skin for u,v in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)] if 0<=u<48 and 33<=v<40)
    return c[3] and c not in skin and not edge
for row in data['frames']:
    im=read(row['path']); src=read(row['source']); legs=read(row['legSource']); dx,dy=row['lean']
    assert im.size==(48,64) and hashlib.sha256(im.tobytes()).hexdigest()==row['pixelsSha256'],row
    assert hashlib.sha256(src.tobytes()).hexdigest()==row['sourcePixelsSha256'],row
    assert all(im.getpixel((x+dx,y+dy))==src.getpixel((x,y)) for y in range(33) for x in range(48) if src.getpixel((x,y))[3]),row
    x0,y0,x1,y1=packs[row['direction']]
    assert all(im.getpixel((x+dx,y+dy))==src.getpixel((x,y)) for y in range(y0,y1) for x in range(x0,x1) if pack_pixel(src,x,y)),row
    assert all(im.getpixel((x,y))==legs.getpixel((x,y)) for y in range(42,64) for x in range(48)),row
    assert all(c in palette for c in im.getdata() if c[3]),row
    hx,hy=row['grip'];near=[im.getpixel((x,y)) for y in range(max(0,int(hy)-1),min(64,int(hy)+2)) for x in range(max(0,int(hx)-1),min(48,int(hx)+2))]
    if not any(c in {(229,149,159,255),(193,106,125,255)} for c in near):hidden.append({k:row[k] for k in ('clip','direction','variant','frame','grip')})
    groups[(row['clip'],row['direction'],row['variant'])].append(row)
for key, rows in groups.items():
    assert rows[0]['pixelsSha256']==rows[-1]['pixelsSha256'] and rows[0]['grip']==rows[-1]['grip'],key
for (clip,direction,variant),rows in groups.items():
    if variant!=0:continue
    sheet=Image.new('RGBA',(8*48,2*64),(31,33,44,255))
    for i,row in enumerate(rows):sheet.alpha_composite(read(row['path']),((i%8)*48,(i//8)*64))
    sheet.resize((sheet.width*4,sheet.height*4),Image.Resampling.NEAREST).save(out/f'{clip}-{direction}-16-keys-4x.png')
qa={'result':'PASS','samples':len(data['frames']),'uniqueImages':len(set(r['pixelsSha256'] for r in data['frames'])),'standUniquePoses':data['standUniquePoses'],'checks':['exact source/RGBA hashes','48x64 native','rigid source head and backpack identity','source feet at42+','official palette only','same ready/recovered endpoints'],'socketsWithNoVisibleSkin':hidden,'scope':'Offline candidates; visual acceptance and live held-weapon occlusion/timing still required.'}
(out/'native-qa.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in qa.items() if k!='socketsWithNoVisibleSkin'},ensure_ascii=False));print('hidden socket count',len(hidden))
