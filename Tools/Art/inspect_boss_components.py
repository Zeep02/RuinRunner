"""Read-only connected component report to inspect generated atlas boundaries."""
from pathlib import Path
from PIL import Image
import numpy as np
import sys

path = Path(__file__).resolve().parents[2] / 'Assets/RuinRunner/Resources/Art/Pixel/Enemies/boss_animation_atlas.png'
if len(sys.argv) > 1: path = Path(sys.argv[1])
minimum = int(sys.argv[2]) if len(sys.argv) > 2 else 2300
a = np.asarray(Image.open(path).convert('RGBA'))
c = a[:,:,:3].astype(np.int16)
mask = ~((c[:,:,0]>70)&(c[:,:,2]>70)&(c[:,:,0]-c[:,:,1]>28)&(c[:,:,2]-c[:,:,1]>28))
h,w = mask.shape
flat = mask.ravel().copy()
found = []
for i in range(len(flat)):
    if not flat[i]: continue
    stack=[i]; flat[i]=False; count=0; x0=w; x1=0; y0=h; y1=0
    while stack:
        k=stack.pop(); y,x=divmod(k,w); count+=1
        x0=min(x0,x); x1=max(x1,x); y0=min(y0,y); y1=max(y1,y)
        for nxt in ((k-1 if x else -1),(k+1 if x+1<w else -1),(k-w if y else -1),(k+w if y+1<h else -1)):
            if nxt>=0 and flat[nxt]: flat[nxt]=False; stack.append(nxt)
    if count>minimum: found.append((x0,y0,x1+1,y1+1,count))
for box in sorted(found,key=lambda b:(b[1]//140,b[0])): print(box)
