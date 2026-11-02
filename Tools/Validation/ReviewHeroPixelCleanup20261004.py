"""QA contact sheets from captured pixels only; never writes game art."""
from pathlib import Path
import csv,json,subprocess,sys,html
from PIL import Image,ImageDraw
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtDirection/Review-20261004/hero-pixel-cleanup'
folder=ROOT/'Logs/Validation/2026-10-04-hero-pixel-cleanup/motion-102411'
rows=list(csv.DictReader((folder/'body-telemetry.csv').open(encoding='utf-8-sig')))
recording=list(csv.DictReader((folder/'recording.csv').open(encoding='utf-8-sig')))
# Tracking happens on each simulation frame, capture at <=15fps. The LAST row
# for a capture index is the pose on the frame actually captured at that index.
by={int(r['frame']):r for r in rows}
weapons=list(dict.fromkeys(r['id'] for r in json.loads((folder/'cases.json').read_text(encoding='utf-8-sig'))))
for weapon in weapons:
    sheet=Image.new('RGB',(8*192,3*168),'#181824');draw=ImageDraw.Draw(sheet)
    for direction in range(8):
        for row,state in enumerate(['idle','move-attack','reload']):
            phase=f'{weapon}-{state}-view-{direction}'
            group=[r for r in recording if r['phase']==phase]
            if not group:continue
            c=group[len(group)//2];r=by[int(c['frame'])]
            im=Image.open(folder/'frames'/f"frame-{int(c['frame']):06d}.png")
            sx=float(r['scaleX']);sy=float(r['scaleY'])
            cx=round(float(r['originSX'])+24*sx);cy=round(im.height-float(r['originSY'])+38*sy)
            sheet.paste(im.crop((cx-80,cy-65,cx+80,cy+80)),(direction*192,row*168+18))
            draw.text((direction*192+3,row*168+3),f'{state} d{direction}',fill='white')
    sheet.save(OUT/f'{weapon}-runtime-8-directions.png')

matches=[r for r in by.values() if r['phase']=='siege_crossbow-idle-view-2' and r['bodyAsset'].endswith('/01947.png')]
assert matches
r=matches[-1];capture=folder/'frames'/f"frame-{int(r['frame']):06d}.png"
im=Image.open(capture);source=np.array(Image.open(ROOT/r['bodyAsset']).convert('RGBA'));screen=np.array(im.convert('RGBA'))
sx=float(r['scaleX']);sy=float(r['scaleY']);ox=float(r['originSX']);oy=im.height-float(r['originSY'])
ys,xs=np.where(source[:,:,3]>0);best=(100000,None)
for dx in range(-2,3):
    for dy in range(-2,3):
        tx=np.rint(ox+xs*sx).astype(int)+dx;ty=np.rint(oy+ys*sy).astype(int)+dy
        mismatch=int(np.any(screen[ty,tx,:3]!=source[ys,xs,:3],axis=1).sum())
        if mismatch<best[0]:best=(mismatch,(dx,dy))
assert best[0]==0,('Native Game View body colors do not match the claimed frame',best)
before=Image.open('C:/Users/Hyeon/AppData/Local/Temp/codex-clipboard-af6951ce-669f-4184-af0b-8eba1b7443e4.png')
a=before.crop((8+14*2,-6+10*2,8+57*2,-6+54*2)).resize((258,264),Image.Resampling.NEAREST)
b=im.crop((round(ox+14*sx),round(oy+10*sy),round(ox+57*sx),round(oy+54*sy))).resize((258,264),Image.Resampling.NEAREST)
sheet=Image.new('RGB',(536,304),'#20202c');sheet.paste(a,(4,28));sheet.paste(b,(274,28));d=ImageDraw.Draw(sheet)
d.text((8,8),'BEFORE - user capture',fill='white');d.text((278,8),'AFTER - actual Game View',fill='white');sheet.save(OUT/'reported-dot-before-after.png')
cx=round(ox+24*sx);cy=round(oy+38*sy);im.crop((cx-75,cy-60,cx+75,cy+60)).save(OUT/'reported-pose-runtime-after.png')
proof={'nativeBody':'WeaponStandingV4/01947.png','referenceOpaqueMismatches':0,'afterOpaqueMismatches':best[0],'beforeNativeScale':2,'afterNativeScale':sx,'afterFrame':str(capture),'display':'Equal native-pixel magnification for QA comparison, no game asset scaling changes. Original screenshots retained.'}
(OUT/'reported-dot-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')

clips=[('siege_crossbow-idle-view-2','siege-idle-right'),('siege_crossbow-reload-view-2','siege-reload-right'),('scrap_pistol-reload-view-4','pistol-reload-down'),('scrap_shotgun-reload-view-5','shotgun-reload-downleft'),('dragon_cannon-move-attack-view-3','cannon-attack-downright'),('worn_dagger-move-attack-view-5','dagger-attack-downleft'),('rust_sword-move-attack-view-4','sword-attack-down'),('iron_greatsword-move-attack-view-6','greatsword-attack-left')]
buttons=[]
for phase,name in clips:
    output=name+'-1x.mp4'
    subprocess.run([sys.executable,str(ROOT/'Tools/Art/encode_run_recording.py'),str(folder),'--phase',phase,'--fps','15','--codec','h264','--output',output,'--scope','Supplied weapon pixel cleanup fixture; original screen samples at wall time, no interpolation/speedup. Not normal campaign/performance/audio.'],check=True,stdout=subprocess.DEVNULL)
    buttons.append(f'<button data-file="{output}">{html.escape(name)}</button>')
page='''<!doctype html><meta charset="utf-8"><title>Hero pixel cleanup · actual 1× motion</title>
<style>body{background:#181824;color:#eee;font:14px sans-serif;margin:8px}button{padding:8px;margin:4px}video{display:block;width:1280px;height:720px;image-rendering:pixelated}</style>
<p>Original screen samples at wall time · supplied weapons · 1× speed · no interpolation · silent</p>BUTTONS<button id="replay">Replay at 1×</button>
<p id="state">Choose a clip</p><video controls muted></video>
<script>const v=document.querySelector('video'),s=document.querySelector('#state');document.querySelectorAll('[data-file]').forEach(b=>b.onclick=()=>{v.src=b.dataset.file;v.playbackRate=1;v.play();s.textContent=b.textContent});document.querySelector('#replay').onclick=()=>{v.currentTime=0;v.playbackRate=1;v.play()};v.ontimeupdate=()=>s.dataset.time=v.currentTime.toFixed(3);</script>'''.replace('BUTTONS',''.join(buttons))
(folder/'review.html').write_text(page,encoding='utf-8')
print(json.dumps({'sheets':len(weapons),'clips':len(clips),'exactReferencePoseCapture':str(capture),'actualBodyPixelMismatches':best[0],'frames':len(recording)},indent=2))
