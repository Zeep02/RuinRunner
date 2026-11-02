"""Read-only review crops/video page from native captures. Never creates game art."""
from pathlib import Path
import csv, json, subprocess, sys, html
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parents[2]
folder=Path(sys.argv[1]).resolve()
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
groups={}
for row in rows:
    row['_capture']=str(folder);groups.setdefault(row['phase'],[]).append(row)
output=folder
if len(sys.argv)>2:
    mobile=Path(sys.argv[2]).resolve();mobile_groups={}
    for row in csv.DictReader((mobile/'body-telemetry.csv').open(encoding='utf-8-sig')):
        row['_capture']=str(mobile);mobile_groups.setdefault(row['phase'],[]).append(row)
    groups.update(mobile_groups);output=folder/'combined-review';output.mkdir(exist_ok=True)
review=output/'review';review.mkdir(exist_ok=True)
families=['hand_crossbow','scrap_pistol','storm_rifle','scrap_shotgun','dragon_cannon']
manifest=[]
for family in families:
    sheet=Image.new('RGB',(1080,8*168),'#181824');draw=ImageDraw.Draw(sheet)
    for direction in range(8):
        phase=f'{family}-manual-view-{direction}'
        group=groups[phase]
        load=[r for r in group if r['pose'].startswith('reload-')]
        for col,key in enumerate([0,3,6,9,12,15]):
            row=min(load,key=lambda r:abs(int(r['key'])-key))
            image=Image.open(Path(row['_capture'])/'frames'/f"frame-{int(row['frame']):06d}.png").convert('RGB')
            # Follow the actual rendered canvas, including carried weapon outside it.
            sx=float(row['scaleX']);sy=float(row['scaleY'])
            cx=round(float(row['originSX'])+24*sx);cy=round(image.height-float(row['originSY'])+38*sy)
            crop=image.crop((cx-72,cy-60,cx+72,cy+84))
            sheet.paste(crop,(col*180,direction*168+20))
            draw.text((col*180+4,direction*168+3),f"d{direction} key{row['key']} f{row['frame']}",fill='white')
    sheet.save(review/f'{family}-8-directions.png')
    for entry,direction in [('manual',3),('auto',4)]:
        phase=f'{family}-{entry}-view-{direction}';name=f'{phase}-1x.mp4'
        capture=groups[phase][0]['_capture']
        subprocess.run([sys.executable,str(root/'Tools/Art/encode_run_recording.py'),capture,'--phase',phase,'--fps','60','--codec','h264','--output',str(output/name),'--scope','Supplied item native reload fixture, actual wall timestamps, no interpolation or speed-up; not normal campaign/performance.'],check=True,stdout=subprocess.DEVNULL)
        manifest.append(dict(phase=phase,file=name,source=capture))
buttons=''.join(f'<button data-file="{html.escape(r["file"])}">{html.escape(r["phase"])}</button>' for r in manifest)
page='''<!doctype html><meta charset="utf-8"><title>Native firearm reload · 1× review</title>
<style>body{background:#181824;color:#eee;font:14px sans-serif;margin:8px}nav{display:flex;flex-wrap:wrap;gap:5px}button{padding:7px}video{width:1280px;height:720px;image-rendering:pixelated}p{margin:5px}</style>
<p>Supplied weapon fixtures · actual capture timestamps · playback 1× · normal campaign/performance not assessed here</p><nav>BUTTONS</nav>
<p id="state">Choose a clip</p><button id="replay">Replay selected at 1×</button><video controls muted></video>
<script>const v=document.querySelector('video'),s=document.querySelector('#state');document.querySelectorAll('[data-file]').forEach(b=>b.onclick=()=>{v.src=b.dataset.file;v.playbackRate=1;v.play();s.textContent=b.textContent});document.querySelector('#replay').onclick=()=>{v.currentTime=0;v.playbackRate=1;v.play()};v.ontimeupdate=()=>{s.dataset.time=v.currentTime.toFixed(3)};</script>'''.replace('BUTTONS',buttons)
(output/'review.html').write_text(page,encoding='utf-8')
(review/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(dict(directory=str(review),clips=len(manifest),sheets=5,scope='QA crops only; original frames retained.')))
