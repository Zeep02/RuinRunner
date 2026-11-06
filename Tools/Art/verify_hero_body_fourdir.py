"""Independent checks and portable visual review of body-only native animations."""
from pathlib import Path
import base64
import hashlib
import json
import math
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Tools/Art/Staging/NativeHero/20261103-body-fourdir'
manifest=json.loads((OUT/'manifest.json').read_text(encoding='utf-8'))
directions=['SW','SE','NW','NE']
names=['dagger','greatsword','katana','spear','staff','orb','gun']
labels=['단검','대검','도','창','지팡이','오브','총']
checks=[];clips={};sources={};payload={'motions':[],'directions':['좌하단','우하단','좌상단','우상단'],'sources':{}}
def digest(data):return hashlib.sha256(data).hexdigest()
def uri(path):return 'data:image/png;base64,'+base64.b64encode(path.read_bytes()).decode()
for direction,source in manifest['sources'].items():
    p=ROOT/source['path']
    assert digest(p.read_bytes())==source['file_sha256'], 'Source file changed'
    sources[direction]=Image.open(p).convert('RGBA')
    payload['sources'][direction]=uri(p)
global_palette=set()
for source in sources.values():global_palette.update(source.getdata())
for source in manifest['sources'].values():
    source_path=ROOT/source['path']
    walk_directory=source_path.parents[2]/'walk'/source_path.parent.name
    for frame in walk_directory.glob('*.png'):
        global_palette.update(Image.open(frame).convert('RGBA').getdata())
# These are the exact source arm palette entries from the accepted C# author.
global_palette.update([(0,0,0,0),(0,0,0,255),(60,84,143,255),(50,65,127,255),(229,149,159,255),(193,106,125,255)])
for name,label in zip(names,labels):
    motion={'id':name,'label':label,'clips':{}}
    for direction in directions:
        folder=OUT/name/direction;data=json.loads((folder/'clip.json').read_text(encoding='utf-8'))
        source=sources[direction];s=source.load();frames=[];hashes=[]
        for record in data['frames']:
            path=folder/f'{record["index"]:02d}.png';im=Image.open(path).convert('RGBA');p=im.load()
            assert im.size==(48,64),'Native canvas changed'
            assert set(im.getdata())<=global_palette,'New colors or alpha blending'
            h=digest(im.tobytes());assert h==record['rgba_sha256'];hashes.append(h)
            dx,dy=record['translation']
            # Copy equality over original head, eyes, and face pixels, independently
            # of the author's protected-region function.
            for y in range(32):
                for x in range(48):
                    if s[x,y][3]:assert p[x+dx,y+dy]==s[x,y],f'Head/face changed: {name}/{direction}/{record["index"]}'
            # Independent backpack mask: original bag material colors below head.
            bag={(110,39,73,255),(149,66,106,255),(81,28,58,255),(59,18,47,255),(78,26,63,255),(162,114,66,255)}
            for y in range(32,39):
                for x in range(48):
                    if s[x,y] in bag:assert p[x+dx,y+dy]==s[x,y],f'Backpack changed: {name}/{direction}'
            assert record['duration_ms']>0
            assert record['leg_source_frame']==0, 'Attack is using a walk cycle'
            for arm in record['arms'].values():
                assert abs(math.dist(arm['shoulder'],arm['elbow'])-2.75)<1e-8
                assert abs(math.dist(arm['elbow'],arm['wrist'])-2.75)<1e-8
                assert math.dist(arm['shoulder'],arm['wrist'])<=5.35+1e-8
            # Source feet remain planted, including all transparent cells.
            seam=39 if direction in ('NW','NE') else 37
            assert im.crop((0,seam+3,48,64)).tobytes()==source.crop((0,seam+3,48,64)).tobytes()
            frames.append(im)
        assert len(frames)==32
        assert len(set(hashes))==data['unique_frames'], 'Unique count is inaccurate'
        assert data['frames'][0]['phase']==0 and hashes[-1]==hashes[0], 'Recovery does not reach ready pose'
        assert Image.open(folder/'return.png').convert('RGBA').tobytes()==frames[0].tobytes()
        assert sum(r['duration_ms'] for r in data['frames'])==data['duration_ms']
        clips[name,direction]=(frames,data)
        checks.append({'motion':name,'direction':direction,'unique':len(set(hashes)),'identity':'exact source pixels','native_size':[48,64],
                       'max_frame_ms':max(r['duration_ms'] for r in data['frames'])})
        motion['clips'][direction]={'sheet':uri(folder/'sheet.png'),'durations':[r['duration_ms'] for r in data['frames']],
                                    'phases':[r['phase'] for r in data['frames']]}
    payload['motions'].append(motion)
(OUT/'verification.json').write_text(json.dumps({'passed':True,'clips':28,'timeline_frames_per_clip':32,'authored_frames':896,
    'fixed_bone_lengths':[2.75,2.75],'maximum_reach':5.35,'planted_feet':True,
    'originals_unchanged':True,'source_head_face_backpack_preserved':True,'weapon_pixels_generated':False,
    'limitation':'Pixel/data tests do not certify gameplay timing or subjective animation quality','checks':checks},ensure_ascii=False,indent=2),encoding='utf-8')

# Portable animated overview at six times the original pixel grid; no production
# frame is resized. Each family uses its own duration rather than a shared speed.
scale=4;cw=48*scale;ch=64*scale+22;overview=[]
for time_ms in range(0,2400,20):
    canvas=Image.new('RGB',(cw*4,ch*7),'#27313d');draw=ImageDraw.Draw(canvas)
    for row,name in enumerate(names):
        for col,direction in enumerate(directions):
            frames,data=clips[name,direction];t=time_ms%data['duration_ms'];elapsed=0;index=0
            for i,r in enumerate(data['frames']):
                elapsed+=r['duration_ms']
                if elapsed>t:index=i;break
            frame=frames[index].resize((48*scale,64*scale),Image.Resampling.NEAREST)
            canvas.paste(frame,(col*cw,row*ch+22),frame)
            draw.text((col*cw+8,row*ch+5),f'{name} {direction}',fill='white')
    overview.append(canvas)
overview[0].save(OUT/'body-motion-overview.gif',save_all=True,append_images=overview[1:],duration=20,loop=0,optimize=False)

# Compact preview uses the same authored timing as the browser, one full cycle.
compact=[]
for time_ms in range(0,clips['dagger','SE'][1]['duration_ms'],10):
    canvas=Image.new('RGB',(48*4*4,64*4+22),'#27313d');draw=ImageDraw.Draw(canvas)
    for col,direction in enumerate(directions):
        frames,data=clips['dagger',direction];elapsed=0
        for i,record in enumerate(data['frames']):
            elapsed+=record['duration_ms']
            if elapsed>time_ms:break
        frame=frames[i].resize((48*4,64*4),Image.Resampling.NEAREST)
        canvas.paste(frame,(col*48*4,22),frame)
        draw.text((col*48*4+8,5),direction,fill='white')
    compact.append(canvas)
compact[0].save(OUT/'dagger-four-directions-preview.gif',save_all=True,append_images=compact[1:],duration=10,loop=0,optimize=False)

html='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>주인공 몸동작 검수</title>
<style>body{margin:24px;background:#151c25;color:#e6edf7;font:16px sans-serif}h1{font-size:22px}button,select,input{font:inherit;margin:6px;padding:8px}canvas{image-rendering:pixelated;background:#27313d;max-width:none;height:auto}p{color:#b8c6d8}.controls{display:flex;align-items:center;flex-wrap:wrap}#status{font-variant-numeric:tabular-nums}</style>
<h1>[임시 · 사용 보류 · 미채택] 주인공 몸동작</h1><p>왼쪽은 원본, 오른쪽 네 칸은 좌하단 · 우하단 · 좌상단 · 우상단입니다. 팔 길이 고정 · 발 고정 · 공격 가속. 클립당 32프레임이며 복귀 중 같은 자세가 재등장할 수 있습니다. 게임 적용 전 검수본.</p>
<div class="controls"><select id="motion"></select><button id="play">일시정지</button><label>속도 <select id="speed"><option value="1">1×</option><option value="0.5">0.5×</option><option value="0.25">0.25×</option></select></label><label>확대 <select id="scale"><option>1</option><option>2</option><option selected>3</option><option>4</option><option>6</option><option>8</option></select>배</label><input id="scrub" type="range" min="0" max="31" value="0"><span id="status"></span></div><canvas id="view"></canvas>
<p>몸체 PNG에는 무기·오브·투사체·효과가 없습니다. 프레임별 손 좌표와 재생 시간은 함께 저장되어 있습니다.</p>
<script>const data=PAYLOAD;const dirs=['SW','SE','NW','NE'];const motion=document.querySelector('#motion'),canvas=document.querySelector('#view'),ctx=canvas.getContext('2d');
let ready=false,playing=true,time=0,last=0,steps=0;let images={};
for(const m of data.motions){let o=document.createElement('option');o.value=m.id;o.textContent=m.label;motion.append(o)}
async function img(url){return new Promise((resolve,reject)=>{let i=new Image;i.onload=()=>resolve(i);i.onerror=reject;i.src=url})}
async function load(){for(const d of dirs)images['src'+d]=await img(data.sources[d]);for(const m of data.motions)for(const d of dirs)images[m.id+d]=await img(m.clips[d].sheet);ready=true}
function frameAt(ds,t){let total=ds.reduce((a,b)=>a+b,0);t%=total;for(let i=0;i<ds.length;i++){if(t<ds[i])return i;t-=ds[i]}return 31}
function render(now){let dt=last?Math.min(now-last,100):0;last=now;if(ready){let m=data.motions.find(x=>x.id===motion.value);if(playing)time+=dt*Number(document.querySelector('#speed').value);let z=Number(document.querySelector('#scale').value);canvas.width=48*z*5;canvas.height=64*z+30;ctx.imageSmoothingEnabled=false;ctx.fillStyle='#27313d';ctx.fillRect(0,0,canvas.width,canvas.height);ctx.fillStyle='white';ctx.font='14px sans-serif';ctx.fillText('원본 (우하단)',8,20);ctx.drawImage(images.srcSE,0,30,48*z,64*z);let indices=[];for(let j=0;j<4;j++){let d=dirs[j];let i=playing?frameAt(m.clips[d].durations,time):Number(document.querySelector('#scrub').value);indices.push(i);ctx.fillText(data.directions[j]+' '+String(i+1).padStart(2,'0')+'/32',(j+1)*48*z+8,20);ctx.drawImage(images[m.id+d],i%8*48,Math.floor(i/8)*64,48,64,(j+1)*48*z,30,48*z,64*z)}steps++;document.querySelector('#status').textContent='재생 프레임 '+indices.join(' / ')+' · '+steps;canvas.dataset.frames=indices.join(',');canvas.dataset.renderCount=steps;if(playing)document.querySelector('#scrub').value=indices[1]}requestAnimationFrame(render)}
document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'일시정지':'재생'};document.querySelector('#scrub').oninput=()=>{playing=false;document.querySelector('#play').textContent='재생'};motion.onchange=()=>{time=0};load();requestAnimationFrame(render);</script></html>'''
(OUT/'review.html').write_text(html.replace('PAYLOAD',json.dumps(payload,ensure_ascii=False)),encoding='utf-8')
print('PASS: 28 clips, 896 timeline frames; fixed bone lengths, planted feet, recovery, identity, canvas/alpha/palette checked.')
