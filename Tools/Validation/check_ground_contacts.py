"""Inspect actual map renders for camera-background leaks at floor-facing contacts."""
from pathlib import Path
import argparse,json,hashlib,math
from PIL import Image

parser=argparse.ArgumentParser()
parser.add_argument('after',type=Path)
parser.add_argument('--before',type=Path,required=True)
args=parser.parse_args()
report=[]
for stage in range(1,4):
    prefix=f'stage-{stage}'
    meta=json.loads((args.after/f'{prefix}-render-bounds.json').read_text())
    w,h,ppu=meta['width'],meta['height'],meta['ppu']
    xmin=meta['center']['x']-w/(2*ppu)
    ymin=meta['center']['y']-h/(2*ppu)
    ground=set()
    for row in (args.after/f'{prefix}-geometry.txt').read_text().splitlines():
        if row.startswith('open,'):
            _,x,y=row.split(',');ground.add((int(x),int(y)))
    samples=set()
    for x,y in ground:
        for dx,dy in ((0,1),(1,0),(0,-1),(-1,0)):
            if (x+dx,y+dy) in ground:continue
            for i in range(ppu):
                tangent=-.5+(i+.5)/ppu
                px=x+.5+dx*(.5+.5/ppu)-dy*tangent
                py=y+.5+dy*(.5+.5/ppu)+dx*tangent
                samples.add((math.floor((px-xmin)*ppu),h-1-math.floor((py-ymin)*ppu)))
    entries={}
    for label,folder in (('before',args.before),('after',args.after)):
        image=Image.open(folder/f'{prefix}-overview.png').convert('RGB')
        assert image.size==(w,h)
        background=image.getpixel((0,0))
        failures=[p for p in samples if image.getpixel(p)==background]
        entries[label]={'contact_pixels':len(samples),'void_leaks':len(failures),'background':background,'first_leaks':failures[:12]}
    entries['stage']=stage
    entries['geometry_unchanged']=hashlib.sha256((args.before/f'{prefix}-geometry.txt').read_bytes()).digest()==hashlib.sha256((args.after/f'{prefix}-geometry.txt').read_bytes()).digest()
    report.append(entries)
(args.after/'ground-contact-validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
assert all(r['after']['void_leaks']==0 and r['geometry_unchanged'] for r in report)
