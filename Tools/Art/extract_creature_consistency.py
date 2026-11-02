"""Technical cell extraction from the generated pixel-art candidate. No painted pixels.

All cells share one sampling scale and canvas. Candidate-only, never auto-adopts.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / 'ArtDirection/Generated/CreatureConsistency-20260921'
source = Image.open(SRC/'charger-v2.png').convert('RGBA')
out = SRC/'charger-v2-candidates'
out.mkdir(exist_ok=True)
frames=[]; report=[]
# Sheet cell size is 512. All sprites use the same 8:1 scale.
# Alpha threshold removes the generator's unintended transparent halo only.
# Keep artwork, including its face, unmodified for honest candidate review.
for i in range(6):
    x=(i%3)*512; y=(i//3)*512
    cell=source.crop((x,y,x+512,y+512))
    a=np.array(cell)
    a[:,:,3]=np.where(a[:,:,3]>=240,255,0)
    a[a[:,:,3]==0,:3]=0
    im=Image.fromarray(a).resize((64,64),Image.Resampling.NEAREST)
    im.save(out/f'{i:02}.png');frames.append(im)
    report.append({'frame':i,'sourceCell':[x,y,512,512],'sampling':8,'bbox':im.getbbox()})
sheet=Image.new('RGB',(6*256,280),(29,32,43)); d=ImageDraw.Draw(sheet)
gif=[]
for i,im in enumerate(frames):
    big=im.resize((256,256),Image.Resampling.NEAREST)
    sheet.paste(big,(i*256,24),big);d.text((i*256+8,5),str(i),fill='white')
    bg=Image.new('RGBA',(256,256),(29,32,43,255));bg.alpha_composite(big);gif.append(bg.convert('RGB'))
sheet.save(SRC/'charger-v2-contact.png')
gif[0].save(SRC/'charger-v2-review.gif',save_all=True,append_images=gif[1:],duration=180,loop=0,disposal=2)
(SRC/'charger-v2-extraction.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
