"""Native body-only authoring using the accepted Sscary joint-authoring method.

The source head/face and backpack pixels are copied, never regenerated.
Joint rasterization is adapted from AuthorSscaryMelee20261001.cs and
AuthorArmedGrip20261001.cs. No PixelLab, Aseprite, or image-generation API.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale'
OUT = ROOT / 'Tools/Art/Staging/NativeHero/20261103-body-fourdir'
SIZE = (48, 64)
INK = (0, 0, 0, 255)
SKIN = (229, 149, 159, 255)
SKIN_DARK = (193, 106, 125, 255)
CLOTH = (60, 84, 143, 255)
CLOTH_DARK = (50, 65, 127, 255)
CLEAR = (0, 0, 0, 0)
DIRS = {
    'SW': dict(source='left_down', old=4, near=(21,32), far=(24,32), sign=-1, rear=False, pack=(28,30,32,39), body=(21,26)),
    'SE': dict(source='right_down',old=2, near=(28,32), far=(25,32), sign=1, rear=False, pack=(18,30,22,39), body=(23,28)),
    'NW': dict(source='left_up',old=5, near=(21,32), far=(24,32), sign=-1, rear=True, pack=(22,30,33,39), body=(21,23)),
    'NE': dict(source='right_up',old=1, near=(28,32), far=(24,32), sign=1, rear=True, pack=(17,30,28,39), body=(25,29)),
}

CLIPS = {
    'dagger': {'label':'단검','seconds':.64},
    'greatsword': {'label':'대검','seconds':.88},
    'katana': {'label':'도','seconds':.72},
    'spear': {'label':'창','seconds':.76},
    'staff': {'label':'지팡이','seconds':.96},
    'orb': {'label':'오브','seconds':.96},
    'gun': {'label':'총','seconds':.72},
}

# Compact, planted attacks. Each pose is (near x/y, far x/y, lean, sink).
# The original source has short arms: do not extend a bone to reach a target.
ACTION_POSES = {
    'dagger': [(2,3,-1,3,0,0),(-1,2,-2,2,-1,0),(5,0,1,2,1,0),(4,2,0,3,1,0),(2,3,-1,3,0,0)],
    'greatsword': [(2,3,4,2,0,0),(0,-4,2,-3,-1,1),(4,2,5,1,1,1),(2,4,4,3,1,1),(2,3,4,2,0,0)],
    'katana': [(0,4,2,3,0,0),(-2,2,0,3,-1,1),(5,-1,3,0,1,0),(3,3,2,3,1,0),(0,4,2,3,0,0)],
    'spear': [(2,2,5,1,0,0),(-1,2,3,2,-1,1),(5,0,5,1,1,0),(4,1,5,2,1,0),(2,2,5,1,0,0)],
    'staff': [(2,1,0,3,0,0),(1,-3,2,1,0,0),(4,-1,4,1,1,0),(3,1,2,3,0,0),(2,1,0,3,0,0)],
    'orb': [(1,3,3,3,0,0),(2,-2,4,-1,0,0),(5,0,5,1,1,0),(3,2,4,2,0,0),(1,3,3,3,0,0)],
    'gun': [(3,2,5,2,0,0),(4,0,5,0,0,0),(1,-1,3,-1,-1,0),(2,1,4,1,0,0),(3,2,5,2,0,0)],
}
for name, action in ACTION_POSES.items():
    clip = CLIPS[name]
    times = (0, .34, .43, .56, 1)
    if name == 'greatsword': times = (0, .43, .52, .65, 1)
    if name == 'gun': times = (0, .30, .36, .48, 1)
    clip['keys'] = [(t, *p, t) for t,p in zip(times, action)]
    clip['planted'] = True
    clip['open'] = False  # No extra finger pixels enlarging the source hands.
def sha(data): return hashlib.sha256(data).hexdigest()
def read(path): return Image.open(path).convert('RGBA')

def curve(keys,t):
    for i in range(len(keys)-1):
        if t <= keys[i+1][0]: break
    a,b=keys[i],keys[i+1];dt=b[0]-a[0];u=(t-a[0])/dt
    # Bounded easing cannot overshoot the authored reach or keep drifting past
    # impact. Time spacing gives a short strike and a controlled recovery.
    u=max(0,min(1,u));u=u*u*(3-2*u)
    return [a[k]+(b[k]-a[k])*u for k in range(1,len(a))]

def protected(source,d):
    px=source.load();mask={}
    x0,y0,x1,y1=d['pack']
    for y in range(64):
        for x in range(48):
            c=px[x,y]
            if not c[3]:continue
            if y<32:
                mask[x,y]=c
            elif x0<=x<x1 and y0<=y<y1 and c not in (SKIN,SKIN_DARK):
                # The pack rectangle includes a few arm-outline pixels. Protect
                # source backpack colors and its actual outline, not abandoned hands.
                neighbors=[px[xx,yy] for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)) if 0<=xx<48 and 0<=yy<64]
                if c!=INK or not any(n in (SKIN,SKIN_DARK) for n in neighbors):mask[x,y]=c
    return mask

def strip_arms(source,d,mask):
    out=source.copy();src=source.load();px=out.load();lo,hi=d['body']
    for y in range(32,40):
        for x in range(48):
            if (x,y) in mask:continue
            c=src[x,y]
            skin=c in (SKIN,SKIN_DARK)
            edge=c==INK and any(src[xx,yy] in (SKIN,SKIN_DARK) for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)) if 0<=xx<48 and 32<=yy<40)
            if skin or edge:px[x,y]=CLOTH if lo<=x<=hi and y<=36 else CLEAR
    return out

def draw_arm(canvas, shoulder, hand, d, bend, behind=False, open_hand=False):
    sx,sy=shoulder;hx,hy=hand;dx=hx-sx;dy=hy-sy
    upper=2.75;lower=2.75
    distance=math.hypot(dx,dy)
    ux,uy=(dx/distance,dy/distance) if distance>.001 else (0,1)
    reach=max(.5,min(upper+lower-.15,distance))
    hx=sx+ux*reach;hy=sy+uy*reach
    along=(upper*upper-lower*lower+reach*reach)/(2*reach)
    height=math.sqrt(max(0,upper*upper-along*along))
    elbow=(sx+ux*along-uy*height*d['sign'],sy+uy*along+ux*height*d['sign'])
    cells={}
    def line(a,b,light,dark):
        steps=max(1,math.ceil(math.dist(a,b)*3))
        for i in range(steps+1):
            f=i/steps;x=round(a[0]+(b[0]-a[0])*f);y=round(a[1]+(b[1]-a[1])*f)
            cells[x,y]=light;cells[x,y+1]=dark
    line(shoulder,elbow,CLOTH,CLOTH_DARK);line(elbow,(hx,hy),SKIN,SKIN_DARK)
    x=round(hx);y=round(hy)
    cells[x-1,y]=SKIN;cells[x,y]=SKIN;cells[x-1,y+1]=SKIN_DARK;cells[x,y+1]=SKIN_DARK
    if open_hand:cells[x+d['sign'],y-1]=SKIN;cells[x,y-1]=SKIN
    px=canvas.load()
    for (x,y) in cells:
        for xx,yy in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
            if 0<=xx<48 and 0<=yy<64 and (xx,yy) not in cells and (not behind or px[xx,yy][3]==0):px[xx,yy]=INK
    for (x,y),c in cells.items():
        if 0<=x<48 and 0<=y<64 and (not behind or px[x,y][3]==0):px[x,y]=c
    return [round(hx)-.5, round(hy)+.5], {'shoulder':list(shoulder),'elbow':list(elbow),'wrist':[hx,hy], 'bone_lengths':[upper,lower]}

def pose(source,walk,d,clip,t):
    mask=protected(source,d);base=strip_arms(source,d,mask)
    near_x,near_y,far_x,far_y,shift,crouch,foot=curve(clip['keys'],t)
    tx=round(shift*d['sign']);ty=round(crouch)
    out=Image.new('RGBA',SIZE)
    seam=39 if d['rear'] else 37
    # Same source-pixel upper body and same source trouser/boot poses as the
    # accepted authoring pipeline. No generated head or resampled texture.
    out.paste(base.crop((0,0,48,seam)),(tx,ty))
    leg_index = 0
    legs=source if leg_index==0 else walk[leg_index]
    out.paste(legs.crop((0,seam,48,64)),(0,seam))
    near=(d['near'][0]+tx,d['near'][1]+ty)
    far=(d['far'][0]+tx,d['far'][1]+ty)
    # Forward offsets are foreshortened vertically in the same fixed view.
    nh=(near[0]+near_x*d['sign'],near[1]+near_y)
    fh=(far[0]+far_x*d['sign'],far[1]+far_y)
    support,support_bones=draw_arm(out,far,fh,d,1,behind=d['rear'],open_hand=clip['open'])
    grip,near_bones=draw_arm(out,near,nh,d,1,open_hand=clip['open'])
    # Restore every protected source pixel AFTER arms, matching original occlusion.
    px=out.load()
    for (x,y),c in mask.items():px[x+tx,y+ty]=c
    return out, {'translation':[tx,ty], 'hand_top_left':grip,'support_hand_top_left':support,
                 'leg_source_frame':leg_index,'phase':round(t,6), 'arms':{'near':near_bones,'support':support_bones}}

def sample_clip(source,walk,d,clip):
    # Evaluate the authored joint curves densely, then pick distinct raster poses
    # in time order. This is joint-space authoring, never image interpolation.
    candidates=[];seen=set();previous=None
    for i in range(257):
        frame,meta=pose(source,walk,d,clip,i/256)
        h=sha(frame.tobytes())
        seen.add(h)
        # Returning through an earlier pose is valid motion. Global duplicate
        # removal erased recovery poses and forced a jump back to the start.
        if h != previous:
            candidates.append((frame,meta,h));previous=h
    if len(candidates)<32:raise ValueError(f"Only {len(candidates)} distinct authored poses")
    # Prefer evenly spaced phases rather than evenly spaced unique-image indices,
    # which can compress busy passages and create long holds at key poses.
    selected=[];last=-1
    for i in range(32):
        upper=len(candidates)-(32-i)
        index=min(range(last+1,upper+1),key=lambda j:abs(candidates[j][1]['phase']-i/32))
        selected.append(candidates[index]);last=index
    # Retain the true joint-curve timing; endpoint returns to the ready pose.
    times=[x[1]['phase'] for x in selected]+[1.0]
    duration_ms=round(clip['seconds']*1000)
    durations=[max(1,round((times[i+1]-times[i])*duration_ms)) for i in range(32)]
    durations[-1]+=duration_ms-sum(durations)
    if durations[-1]<1:
        durations[-2]+=durations[-1]-1;durations[-1]=1
    return selected,durations,len(seen)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    report={'method':'Native joints from accepted 2026-10-01 authoring; exact source head/face/backpack',
            'size':SIZE,'ppu':32,'pivot_bottom_left':[24,22],'weapon_pixels_included':False,
            'sources':{},'clips':[], 'status':'temporary_on_hold',
            'classification':'Temporary draft; not adopted; user deferred use on 2026-11-03',
            'production_ready':False}
    native_sheets={};all_previews={}
    for direction,d in DIRS.items():
        path=SOURCE/'idle'/d['source']/'00.png';source=read(path)
        source_hash=sha(path.read_bytes());report['sources'][direction]={'path':path.relative_to(ROOT).as_posix(),'file_sha256':source_hash}
        walk=[read(SOURCE/'walk'/d['source']/f'{i:02d}.png') for i in range(8)]
        mask=protected(source,d)
        for name,clip in CLIPS.items():
            selected,durations,available=sample_clip(source,walk,d,clip)
            folder=OUT/name/direction;folder.mkdir(parents=True,exist_ok=True)
            records=[];preview=[];sheet=Image.new('RGBA',(48*8,64*4))
            for i,(frame,meta,h) in enumerate(selected):
                tx,ty=meta['translation'];px=frame.load()
                assert all(px[x+tx,y+ty]==c for (x,y),c in mask.items()),'Identity changed'
                assert set(frame.getchannel('A').getdata())<={0,255},'Partial alpha'
                p=folder/f'{i:02d}.png';frame.save(p)
                sheet.paste(frame,(i%8*48,i//8*64))
                records.append(dict(index=i,rgba_sha256=h,duration_ms=durations[i],**meta))
                bg=Image.new('RGBA',SIZE,'#27313d');bg.alpha_composite(frame)
                preview.append(bg.resize((48*6,64*6),Image.Resampling.NEAREST))
            sheet.save(folder/'sheet.png')
            # An explicit terminal ready pose for one-shot playback is a boundary
            # copy, not a 33rd new pose. It is not added to the cyclic sheet.
            selected[0][0].save(folder/'return.png')
            preview[0].save(folder/'preview.png',save_all=True,append_images=preview[1:],duration=durations,loop=0,disposal=0,blend=0)
            data={'motion':name,'label':clip['label'],'direction':direction,'frames':records,
                  'source':report['sources'][direction], 'original_identity_pixels_preserved':len(mask),
                  'unique_frames':len({r['rgba_sha256'] for r in records}),
                  'available_native_poses':available, 'duration_ms':sum(durations),
                  'terminal_ready_file':'return.png', 'terminal_is_boundary_copy':True}
            (folder/'clip.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
            report['clips'].append({k:v for k,v in data.items() if k!='frames'})
            native_sheets[name,direction]=sheet;all_previews[name,direction]=(selected,durations)
            print(name,direction,'unique',data['unique_frames'],'available',available,'identity',len(mask),flush=True)
        assert sha(path.read_bytes())==source_hash,'Source modified'
    # A static comparison: original and four selected action phases for every family.
    scale=6;cw=48*scale;ch=64*scale
    review=Image.new('RGB',(cw*5,ch*7),'#27313d');draw=ImageDraw.Draw(review)
    for row,(name,clip) in enumerate(CLIPS.items()):
        source=read(SOURCE/'idle'/'right_down'/'00.png')
        frames=[source]+[all_previews[name,'SE'][0][i][0] for i in (5,12,20,28)]
        for col,frame in enumerate(frames):
            enlarged=frame.resize((cw,ch),Image.Resampling.NEAREST)
            review.paste(enlarged,(col*cw,row*ch),enlarged)
            draw.text((col*cw+8,row*ch+10),name+(' SOURCE' if col==0 else f' pose {col}'),fill='white')
    review.save(OUT/'source-vs-body-actions-6x.png')
    for name in CLIPS:
        combined=Image.new('RGBA',(48*8,64*16))
        for row,direction in enumerate(DIRS):combined.paste(native_sheets[name,direction],(0,row*64*4))
        combined.save(OUT/name/'four-directions.png')
    (OUT/'manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':main()
