"""Read captured pixels/timestamps, preserving native screen scale."""
from pathlib import Path
from PIL import Image,ImageDraw
import csv,json,collections,math,sys
import numpy as np
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/hero-defense'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
clock={int(r['frame']):float(r['realtime']) for r in csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig'))}
groups=collections.defaultdict(list)
for r in rows:
    if r['melee']=='True':groups[(r['phase'],r['pose'].replace('-move',''))].append(r)
proof=[];seams=[];guard_seams=[];hair={(60,41,94),(44,29,72),(29,17,51)};global_anchor=None
for (phase,pose),active in groups.items():
    gaps=[clock[int(b['frame'])]-clock[int(a['frame'])] for a,b in zip(active,active[1:])]
    jumps=[math.hypot(float(b['handX'])-float(a['handX']),float(b['handY'])-float(a['handY']))*32 for a,b in zip(active,active[1:])]
    proof.append({'phase':phase,'pose':pose,'samples':len(active),'uniqueSprites':len(set(r['sprite'] for r in active)),'observedSeconds':clock[int(active[-1]['frame'])]-clock[int(active[0]['frame'])],'maxSampleGap':max(gaps,default=0),'maxHandStepNativePixels':max(jumps,default=0)})
    if phase not in ['guard-view-2','guard-return-view-2','parry-success-view-2','charge-full-view-2','charge-partial-view-2','iron_greatsword-mobile-defense'] and not phase.startswith('slash-to-guard-2-step-0'):continue
    unique=[];seen=set()
    for r in active:
        if r['sprite'] not in seen:unique.append(r);seen.add(r['sprite'])
    picks=unique[:16];sheet=Image.new('RGB',(4*240,math.ceil(len(picks)/4)*160),(26,28,37));draw=ImageDraw.Draw(sheet)
    # Guard damage deliberately tints the original hair. Retain the preceding
    # visible anchor during that pulse; never alter captured frame colors.
    last=global_anchor
    for i,r in enumerate(picks):
        im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB');raster=np.asarray(im);region=raster[140:600]
        mask=np.zeros(region.shape[:2],dtype=bool)
        for color in hair:mask|=np.all(region==color,axis=2)
        ys,xs=np.nonzero(mask)
        if len(xs):last=(int(xs.min()),int(ys.min())+140);global_anchor=last
        assert last is not None,(phase,pose,i)
        x0,y0=last;crop=im.crop((x0-80,y0-40,x0+150,y0+104));x=i%4*240;y=i//4*160
        sheet.paste(crop,(x+5,y+3));draw.text((x+5,y+147),f"key {r['key']} / blade {float(r['angle']):.0f}",fill=(237,229,211))
    sheet.save(out/f'{phase}-{pose}-native-sequence.png')
for a,b in zip(rows,rows[1:]):
    if a['phase']==b['phase'] and a['pose'].startswith(('sweep','reverse-sweep','thrust')) and b['pose'].startswith('guard'):
        hand=math.hypot(float(b['handX'])-float(a['handX']),float(b['handY'])-float(a['handY']))*32
        assert hand<=3.2,(a,b)
        guard_seams.append({'phase':a['phase'],'from':a['pose'],'to':b['pose'],'handDeltaNativePixels':hand})
    if a['phase']==b['phase'] and a['pose'] in ('charge','charge-move') and b['pose'].startswith('charge-cleave'):
        delta=abs((float(b['angle'])-float(a['angle'])+180)%360-180)
        hand=math.hypot(float(b['handX'])-float(a['handX']),float(b['handY'])-float(a['handY']))*32
        assert hand<=3.2 and delta<=22,(a,b)
        seams.append({'phase':a['phase'],'previousKey':a['key'],'releaseKey':b['key'],'handDeltaNativePixels':hand,'bladeAngleDelta':delta})
result={'result':'PASS','recordedFrames':len(rows),'actionGroups':len(proof),'chargeReleaseSeams':seams,'attackGuardSeams':guard_seams,'actions':proof,'scope':'Isolated supplied items/targets, original real capture pixels/timestamps; no speedup/interpolation, no normal campaign/performance claim.'}
(out/'runtime-timing-qa.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'recordedFrames':len(rows),'actions':len(proof),'releaseSeams':len(seams),'maxReleaseHandStep':max((r['handDeltaNativePixels'] for r in seams),default=0),'maxReleaseBladeStep':max((r['bladeAngleDelta'] for r in seams),default=0),'guardSeams':len(guard_seams),'maxGuardHandStep':max((r['handDeltaNativePixels'] for r in guard_seams),default=0)},ensure_ascii=False))
