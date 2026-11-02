"""Inspect native captured pixels and real motion timing. No source sprite edits."""
from pathlib import Path
from PIL import Image,ImageDraw
import csv,json,collections,math,sys
root=Path(__file__).resolve().parents[2]
folder=root/(sys.argv[1] if len(sys.argv)>1 else 'Logs/Validation/2026-10-01-hero-thrust/presentation-120257')
prefix='final-' if len(sys.argv)>1 else ''
out=root/'ArtDirection/Review-20261001/hero-thrust';out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
clock={int(r['frame']):float(r['realtime'])for r in csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig'))}
phases=collections.defaultdict(list)
for row in rows:phases[row['phase']].append(row)
proof={}
hair={(60,41,94),(44,29,72),(29,17,51)}
for phase in ['worn_dagger-standing','worn_dagger-moving','rust_sword-standing','rust_sword-moving']:
    if phase not in phases:continue
    matches=[r for r in phases[phase] if r['thrust']=='True']
    groups=collections.defaultdict(list)
    for r in matches:groups[r['sequence']].append(r)
    chosen=max(groups.values(),key=len);duration=clock[int(chosen[-1]['frame'])]-clock[int(chosen[0]['frame'])]
    gaps=[clock[int(b['frame'])]-clock[int(a['frame'])]for a,b in zip(chosen,chosen[1:])]
    jumps=[math.hypot(float(b['handX'])-float(a['handX']),float(b['handY'])-float(a['handY']))*32 for a,b in zip(chosen,chosen[1:])]
    unique=[];seen=set()
    for r in chosen:
        if r['sprite'] not in seen:unique.append(r);seen.add(r['sprite'])
    picks=unique[:12]
    sheet=Image.new('RGB',(4*240,math.ceil(len(picks)/4)*160),(26,28,37));draw=ImageDraw.Draw(sheet)
    for index,r in enumerate(picks):
        im=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB')
        candidates=[(x,y)for y in range(180,550)for x in range(370,940)if im.getpixel((x,y))in hair]
        assert candidates,phase
        minx=min(x for x,y in candidates);miny=min(y for x,y in candidates)
        crop=im.crop((minx-26,miny-16,minx+204,miny+128))
        x=(index%4)*240;y=(index//4)*160;sheet.paste(crop,(x+5,y+3))
        draw.text((x+5,y+147),f"key {r['key']} / {float(r['progress']):.2f}",fill=(237,229,211))
    sheet.save(out/(prefix+phase+'-actual-native-sequence.png'))
    proof[phase]={'contactSequence':chosen[0]['sequence'],'actualThrustSamples':len(chosen),'observedDistinctSprites':len(unique),'observedSeconds':duration,'maxSampleGap':max(gaps),'maxHandStepNativePixels':max(jumps),'policy':'Crops preserve native screen pixels. Real attack clock and original gait; no resizing or synthesized frame interpolation. This is isolated view review, not campaign performance.'}
(out/(prefix+'runtime-timing-qa.json')).write_text(json.dumps(proof,indent=2),encoding='utf-8');print(json.dumps(proof))
