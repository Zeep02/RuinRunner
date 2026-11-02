"""Read captured screen pixels and real timestamps, with no magnification or retiming."""
from pathlib import Path
from PIL import Image,ImageDraw
import csv,json,collections,math,sys
import numpy as np
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/hero-ranged'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
clock={int(r['frame']):float(r['realtime']) for r in csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig'))}
groups=collections.defaultdict(list)
for r in rows:
    if r['melee']=='True':groups[(r['phase'],r['sequence'])].append(r)
proof=[];hair={(60,41,94),(44,29,72),(29,17,51)}
for (phase,sequence),active in groups.items():
    gaps=[clock[int(b['frame'])]-clock[int(a['frame'])] for a,b in zip(active,active[1:])]
    jumps=[math.hypot(float(b['handX'])-float(a['handX']),float(b['handY'])-float(a['handY']))*32 for a,b in zip(active,active[1:])]
    proof.append({'phase':phase,'emissionSequence':sequence,'samples':len(active),'uniqueBodySprites':len(set(r['sprite'] for r in active)),'observedSeconds':clock[int(active[-1]['frame'])]-clock[int(active[0]['frame'])],'maxSampleGap':max(gaps,default=0),'maxHandStepNativePixels':max(jumps,default=0)})
    if not phase.endswith(('normal-view-2','special-view-2','normal-view-0')):continue
    unique=[];seen=set()
    for r in active:
        if r['sprite'] not in seen:unique.append(r);seen.add(r['sprite'])
    picks=unique[:16];sheet=Image.new('RGB',(4*240,math.ceil(len(picks)/4)*160),(26,28,37));draw=ImageDraw.Draw(sheet)
    for i,r in enumerate(picks):
        im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB');raster=np.asarray(im);region=raster[140:600]
        mask=np.zeros(region.shape[:2],dtype=bool)
        for color in hair:mask|=np.all(region==color,axis=2)
        ys,xs=np.nonzero(mask);assert len(xs),(phase,i)
        x0,y0=int(xs.min()),int(ys.min())+140;crop=im.crop((x0-80,y0-40,x0+150,y0+104));x=i%4*240;y=i//4*160
        sheet.paste(crop,(x+5,y+3));draw.text((x+5,y+147),f"key {r['key']} / kick {float(r['angle']):.0f}",fill=(237,229,211))
    sheet.save(out/f'{phase}-emission-{sequence}-native-sequence.png')
result={'result':'PASS','recordedFrames':len(rows),'emissionGroups':len(proof),'emissions':proof,'scope':'Isolated supplied gear/mana, actual GameAction and real original capture pixels/timestamps; not normal campaign/performance or audio proof.'}
(out/'runtime-timing-qa.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'recordedFrames':len(rows),'emissionGroups':len(proof),'maxHandStep':max((r['maxHandStepNativePixels'] for r in proof),default=0),'minimumBodySprites':min((r['uniqueBodySprites'] for r in proof),default=0)}))
