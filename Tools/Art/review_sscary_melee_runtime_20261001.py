"""Native captured pixels and real timestamps; never invent animation frames."""
from pathlib import Path
from PIL import Image,ImageDraw
import csv,json,collections,math,sys
import numpy as np
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/hero-melee'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
clock={int(r['frame']):float(r['realtime'])for r in csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig'))}
groups=collections.defaultdict(list)
for r in rows:groups[(r['phase'],r['sequence'])].append(r)
proof=[];chosen={};hair={(60,41,94),(44,29,72),(29,17,51)}
for (phase,sequence),group in groups.items():
    active=[r for r in group if r['melee']=='True']
    if not active:continue
    gaps=[clock[int(b['frame'])]-clock[int(a['frame'])]for a,b in zip(active,active[1:])]
    jumps=[math.hypot(float(b['handX'])-float(a['handX']),float(b['handY'])-float(a['handY']))*32 for a,b in zip(active,active[1:])]
    entry={'phase':phase,'sequence':sequence,'pose':active[0]['pose'],'samples':len(active),'uniqueSprites':len(set(r['sprite'] for r in active)),'observedSeconds':clock[int(active[-1]['frame'])]-clock[int(active[0]['frame'])],'maxSampleGap':max(gaps,default=0),'maxHandStepNativePixels':max(jumps,default=0),'contactTransitions':int(group[-1]['contacts'])-int(group[0]['contacts'])}
    proof.append(entry)
    name=phase+'-'+active[0]['pose'].replace('-move','')
    if name not in chosen or len(active)>len(chosen[name]):chosen[name]=active
for name,active in chosen.items():
    if 'view' in name and not any(x in name for x in ['view-0','view-2','view-5']):continue
    unique=[];seen=set()
    for r in active:
        if r['sprite'] not in seen:unique.append(r);seen.add(r['sprite'])
    picks=unique[:16];sheet=Image.new('RGB',(4*240,math.ceil(len(picks)/4)*160),(26,28,37));draw=ImageDraw.Draw(sheet)
    for i,r in enumerate(picks):
        im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB')
        raster=np.asarray(im);region=raster[160:min(560,im.height)]
        mask=np.zeros(region.shape[:2],dtype=bool)
        for color in hair:mask|=np.all(region==color,axis=2)
        ys,xs=np.nonzero(mask);assert len(xs),name
        minx=int(xs.min());miny=int(ys.min())+160
        crop=im.crop((minx-70,miny-40,minx+160,miny+104));x=i%4*240;y=i//4*160
        sheet.paste(crop,(x+5,y+3));draw.text((x+5,y+147),f"key {r['key']} / {float(r['progress']):.2f}",fill=(237,229,211))
    sheet.save(out/(name+'-actual-native-sequence.png'))
(out/'runtime-timing-qa.json').write_text(json.dumps({'scope':'Supplied equipment/frozen targets. Actual composited pixels at1x, real timestamps; no resample/interpolation/speed-up. Not normal campaign or performance proof.','attacks':proof},indent=2),encoding='utf-8')
print(json.dumps({'attacks':len(proof),'distinctPoses':sorted(set(p['pose'] for p in proof)),'maxNativeHandStep':max(p['maxHandStepNativePixels'] for p in proof),'minUnique':min(p['uniqueSprites'] for p in proof),'maxCaptureGap':max(p['maxSampleGap'] for p in proof)},ensure_ascii=False))
