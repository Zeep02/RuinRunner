"""Native-size crops from real screens, source-key/timestamp summary, no image edits."""
from pathlib import Path
from PIL import Image
import csv,json,sys,numpy as np
root=Path(__file__).resolve().parents[2];folder=root/sys.argv[1]
out=root/'ArtDirection/Review-20261001/weapon-identity-more'/folder.name;out.mkdir(parents=True,exist_ok=True)
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
times=list(csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig')))
report=[]
for phase in dict.fromkeys(r['phase'] for r in rows):
    picked=[r for r in rows if r['phase']==phase and r['fxClip'] and float(r['progress'])>=.18]
    if not picked:continue
    unique={int(r['fxKey']) for r in picked};ts=[float(r['realtime']) for r in times if r['phase']==phase]
    report.append(dict(phase=phase,visibleSourceKeys=sorted(unique),recordedSamples=sum(r['phase']==phase for r in rows),medianSampleMs=float(np.median(np.diff(ts))*1000),foregroundSamples=sum(r.get('editorForeground')=='True' for r in rows if r['phase']==phase)))
    if phase in ('normal-view-2','charge-full-view-2','normal-view-7','charge-full-view-7'):
        peak=4
        r=next((r for r in picked if int(r['fxKey'])==peak),picked[len(picked)//2]);screen=Image.open(folder/'frames'/f"frame-{int(r['frame']):06d}.png").convert('RGB')
        x=float(r['originSX'])+24*float(r['scaleX']);y=screen.height-float(r['originSY'])+33.04*float(r['scaleY'])
        left=int(max(0,min(screen.width-640,x-320)));top=int(max(0,min(screen.height-480,y-240)))
        screen.crop((left,top,left+640,top+480)).save(out/(phase+'-native-screen.png'))
(out/'actual-source-key-review.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
assert all(len(r['visibleSourceKeys'])>=5 for r in report),'too few actual source images in rendered action'
