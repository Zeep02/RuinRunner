"""Read-only native PNG selection/contact sheets. No resampling or production raster changes."""
import csv,json
from pathlib import Path
from PIL import Image,ImageDraw,ImageChops

root=Path('Logs/Validation/2026-10-04-sword-motion/121059')
out=root/'independent-visual-review'
out.mkdir(exist_ok=True)
rows=list(csv.DictReader((root/'body-telemetry.csv').open(encoding='utf-8-sig')))
phases={}
for r in rows: phases.setdefault(r['phase'],[]).append(r)
crop=(64,32,480,352) # source PNG coordinates; original 416x320 pixels, pasted without resizing
records=[]
for phase,group in phases.items():
    attacks={}
    for r in group:
        if 'sweep' in r['pose'] and float(r['progress'])<1:
            attacks.setdefault(int(r['sequence']),[]).append(r)
    assert len(attacks)==4,(phase,list(attacks))
    sheet=Image.new('RGB',(416*4,344*3),(22,22,25))
    draw=ImageDraw.Draw(sheet)
    chosen=[]
    for col,(seq,frames) in enumerate(attacks.items()):
        for row,target in enumerate((.18,.5,.82)):
            r=min(frames,key=lambda r:abs(float(r['progress'])-target))
            frame=int(r['frame']);path=root/'frames'/f'frame-{frame:06}.png'
            src=Image.open(path).convert('RGB');assert src.size==(512,384)
            native=src.crop(crop);sheet.paste(native,(col*416,row*344+24))
            draw.text((col*416+6,row*344+5),f'{["FIRST","REVERSE","FINISH","COUNTER"][col]} f{frame:06} p={r["progress"]} key={r["key"]}',fill=(235,235,235))
            entry=dict(r);entry['sourceFile']=str(path);entry['crop']=crop;entry['targetProgress']=target
            chosen.append(entry)
    sheet.save(out/f'{phase}.png')
    records.append({'phase':phase,'sheet':str(out/f'{phase}.png'),'frames':chosen})

# Exact border occupancy identifies native capture clipping candidates, not visual PASS.
edge=[]
for r in rows:
    path=root/'frames'/f'frame-{int(r["frame"]):06}.png'
    src=Image.open(path).convert('RGB');bg=src.getpixel((0,0))
    box=ImageChops.difference(src,Image.new('RGB',src.size,bg)).getbbox()
    if box and (box[0]<=1 or box[1]<=1 or box[2]>=511 or box[3]>=383):
        edge.append({'frame':int(r['frame']),'bbox':box})
(out/'selection.json').write_text(json.dumps({'scope':'16 clips x 4 actions x 3 representative progress positions, original pixels only; no timing or campaign claim','sourceCrop':crop,'selectedFrames':sum(len(r['frames']) for r in records),'nativeEdgeCandidates':edge,'records':records},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'sheets':len(records),'selectedFrames':sum(len(r['frames']) for r in records),'nativeEdgeCandidates':edge,'output':str(out)}))
