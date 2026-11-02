"""Check the actual rendered cap's full width and all exterior corner pixels.

Independent of runtime mesh topology: enumerate pixel centres outside the floor
whose distance to an open unit square is at most one quarter unit. Any background
pixel in that band is a broken wall/floor junction. Obstacles retain their ground
bed, so they also must never expose background in this band.
"""
from pathlib import Path
import argparse, json, math
from PIL import Image

p=argparse.ArgumentParser();p.add_argument('folder',type=Path);args=p.parse_args()
results=[]
for stage in range(1,4):
    name=f'stage-{stage}'
    meta=json.loads((args.folder/f'{name}-render-bounds.json').read_text())
    w,h,ppu=meta['width'],meta['height'],meta['ppu']
    xmin=meta['center']['x']-w/(2*ppu);ymin=meta['center']['y']-h/(2*ppu)
    image=Image.open(args.folder/f'{name}-overview.png').convert('RGB');background=image.getpixel((0,0))
    floor=set()
    for row in (args.folder/f'{name}-geometry.txt').read_text().splitlines():
        if row.startswith('open,'):
            _,x,y=row.split(',');floor.add((int(x),int(y)))
    pixels=set();corners=set()
    for x,y in floor:
        for dx,dy in [(0,1),(1,0),(0,-1),(-1,0)]:
            if (x+dx,y+dy) in floor:continue
            for depth in range(4):
                for tangent in range(ppu):
                    t=-.5+(tangent+.5)/ppu;n=.5+(depth+.5)/ppu
                    px=x+.5+dx*n-dy*t;py=y+.5+dy*n+dx*t
                    pixels.add((math.floor((px-xmin)*ppu),h-1-math.floor((py-ymin)*ppu)))
        for dx,dy in [(-1,-1),(-1,1),(1,-1),(1,1)]:
            if (x+dx,y+dy) in floor:continue
            for a in range(4):
                for b in range(4):
                    px=x+.5+dx*(.5+(a+.5)/ppu);py=y+.5+dy*(.5+(b+.5)/ppu)
                    corners.add((math.floor((px-xmin)*ppu),h-1-math.floor((py-ymin)*ppu)))
    failures=[v for v in pixels|corners if image.getpixel(v)==background]
    results.append(dict(stage=stage,straightBandPixels=len(pixels),cornerPixels=len(corners),
                        unionPixels=len(pixels|corners),voidLeaks=len(failures),firstLeaks=failures[:20]))
(args.folder/'wall-ribbon-validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
assert all(r['voidLeaks']==0 for r in results)
