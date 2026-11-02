"""Inspect native authored candidates. Assets are never modified by this reviewer."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, hashlib, collections

root=Path(__file__).resolve().parents[2]
folder=root/'ArtDirection/Generated/SscaryMotion-20261001/thrust-v3'
data=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
out=root/'ArtDirection/Review-20261001/hero-thrust';out.mkdir(parents=True,exist_ok=True)
checks=[]; group=collections.defaultdict(list);palette=set()
for path in (root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale').rglob('*.png'):
    im=Image.open(path).convert('RGBA');palette.update(c for c in im.getdata() if c[3])
for row in data['frames']:
    im=Image.open(root/row['path']).convert('RGBA');src=Image.open(root/row['source']).convert('RGBA')
    assert im.size==(48,64)
    assert hashlib.sha256(im.tobytes()).hexdigest()==row['pixelsSha256']
    assert hashlib.sha256(src.tobytes()).hexdigest()==row['sourcePixelsSha256']
    assert all(im.getpixel((x,y))==src.getpixel((x,y)) for y in range(32) for x in range(48) if src.getpixel((x,y))[3]),row
    legs=Image.open(root/row['legSource']).convert('RGBA')
    assert hashlib.sha256(legs.tobytes()).hexdigest()==row['legSourcePixelsSha256']
    assert all(im.getpixel((x,y))==legs.getpixel((x,y)) for y in range(42,64) for x in range(48)),row
    assert im.getbbox()[3]==legs.getbbox()[3],row
    assert all(c in palette for c in im.getdata() if c[3]),row
    hx,hy=row['grip'];near=[im.getpixel((x,y)) for y in range(max(0,int(hy)-1),min(64,int(hy)+2)) for x in range(max(0,int(hx)-1),min(48,int(hx)+2))]
    assert any(c in {(229,149,159,255),(193,106,125,255)} for c in near) or row['direction']=='up',row
    group[(row['direction'],row['gait'])].append((row,im))
for (direction,gait),frames in group.items():
    if gait!=-1:continue
    sheet=Image.new('RGBA',(17*48,64),(31,33,44,255))
    src=Image.open(root/frames[0][0]['source']).convert('RGBA');sheet.alpha_composite(src,(0,0))
    for index,(row,im) in enumerate(frames):sheet.alpha_composite(im,((index+1)*48,0))
    sheet.resize((sheet.width*4,sheet.height*4),Image.Resampling.NEAREST).save(out/f'{direction}-original-then-16-keys-4x.png')
    gif=[]
    for row,im in frames:
        bg=Image.new('RGBA',im.size,(31,33,44,255));bg.alpha_composite(im)
        gif.append(bg.resize((192,256),Image.Resampling.NEAREST).convert('RGB'))
    phases=data['phases'];durations=[max(20,round((phases[i+1]-phases[i])*.28*1000)) for i in range(15)]+[500]
    gif[0].save(out/f'{direction}-pose-review.gif',save_all=True,append_images=gif[1:],duration=durations,loop=0)
checks={'result':'PASS','framesChecked':len(data['frames']),'checks':['48x64 native canvas','source/pixel/leg hashes','all source head/face pixels exactly unchanged','source foot poses/contact height unchanged','only official character palette','closed-hand socket near visible flesh'],'standUniquePoses':data['standUniquePoses'],'scope':'Native candidates only. Contact sheets/GIF are inspection at4x; assets remain unresampled. GIF keyed timing preview does not prove live game animation or final clip acceptance.'}
(out/'native-qa-v3.json').write_text(json.dumps(checks,indent=2,ensure_ascii=False),encoding='utf-8')
print(json.dumps(checks,ensure_ascii=False))
