"""Inspect native PixelLab output and package editable Aseprite review files.

No resampling or pose interpolation is applied to any production frame.
Only contact sheets and animation previews are enlarged with nearest sampling.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageDraw
import requests
from generate_hero_fourdir import ROOT, OUT

ASEPRITE = Path('F:/tools/aseprite/build/bin/aseprite.exe')

def digest(im):
    return hashlib.sha256(im.tobytes()).hexdigest()

def process(motion, direction):
    folder = OUT / motion / direction
    record = json.loads((folder / 'request.json').read_text(encoding='utf-8'))
    receipt = '\n'.join(b.get('text','') for b in record['response'].get('content',[]))
    job = re.search(r'job_id:\s*([a-zA-Z0-9-]+)',receipt).group(1)
    # This exact public download route and index parameter are supplied in the MCP receipt.
    download = re.search(r'GET (https://\S+/download)',receipt).group(1)
    raw = folder / 'frames'
    raw.mkdir(exist_ok=True)
    count = record['parameters']['frame_count'] + 1
    def fetch(i):
        target = raw / f'{i:02d}.png'
        if not target.exists():
            response=requests.get(download, params={'index': i}, timeout=60)
            response.raise_for_status()
            assert response.headers.get('Content-Type','').startswith('image/'), response.headers.get('Content-Type')
            target.write_bytes(response.content)
        return target
    with ThreadPoolExecutor(max_workers=6) as pool:
        paths=list(pool.map(fetch,range(count)))
    frames=[Image.open(path).convert('RGBA') for path in paths]
    source=Image.open(ROOT / record['source']).convert('RGBA')
    hashes=[digest(frame) for frame in frames]
    sizes=[list(frame.size) for frame in frames]
    assert all(frame.size==source.size for frame in frames), sizes
    assert hashes[0]==digest(source), 'Input frame changed'
    details=[]
    for i,frame in enumerate(frames):
        bbox=frame.getbbox()
        details.append({'index':i,'rgba_sha256':hashes[i],'bbox':bbox,
                        'colors':len(frame.getcolors(frame.width*frame.height) or []),
                        'partial_alpha_pixels':sum(0<a<255 for a in frame.getchannel('A').getdata())})
    report={'job_id':job,'source':record['source'],'native_size':list(source.size),
            'returned_frames':len(frames),'generated_frames_requested':count-1,
            'unique_rgba_frames':len(set(hashes)),
            'consecutive_duplicates':[i for i in range(1,len(frames)) if hashes[i]==hashes[i-1]],
            'last_matches_input':hashes[-1]==hashes[0],
            'status':'Candidate; numerical checks do not establish identity or motion quality',
            'frames':details}
    (folder/'audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    # Omit a duplicated terminal key in loops only; retain authored action endpoints.
    selected=paths[:-1] if motion in ('idle','move') and hashes[-1]==hashes[0] else paths
    total_ms=round(record['duration_seconds']*1000)
    times=[round((i+1)*total_ms/len(selected))-round(i*total_ms/len(selected)) for i in range(len(selected))]
    manifest={'width':source.width,'height':source.height,
              'output':(folder/f'{motion}-{direction}.aseprite').as_posix(),
              'pivot_bottom_left':[24,22],
              'frames':[{'path':p.as_posix(),'duration_ms':ms} for p,ms in zip(selected,times)],
              'tags':[{'name':f'{motion}-{direction}','from':0,'to':len(selected)-1}]}
    manifest_path=folder/'aseprite-import.json'
    manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    subprocess.run([str(ASEPRITE),'-b','--script-param',f'manifest={manifest_path.as_posix()}',
                    '--script',str(ROOT/'Tools/Art/hero_fourdir_to_aseprite.lua')],check=True,capture_output=True,text=True)
    columns=8;scale=4;cw=source.width*scale;ch=source.height*scale+20
    contact=Image.new('RGB',(columns*cw,((len(frames)+columns-1)//columns)*ch),'#29313c')
    draw=ImageDraw.Draw(contact)
    for i,frame in enumerate(frames):
        x=i%columns*cw;y=i//columns*ch
        enlarged=frame.resize((source.width*scale,source.height*scale),Image.Resampling.NEAREST)
        contact.paste(enlarged,(x,y),enlarged)
        draw.text((x+8,y+ch-18),str(i),fill='white')
    contact.save(folder/'contact-4x.png')
    preview=[]
    for p in selected:
        frame=Image.open(p).convert('RGBA')
        bg=Image.new('RGBA',frame.size,'#29313c');bg.alpha_composite(frame)
        preview.append(bg.resize((source.width*6,source.height*6),Image.Resampling.NEAREST))
    preview[0].save(folder/'preview.png',save_all=True,append_images=preview[1:],duration=times,loop=0,disposal=0,blend=0)
    print(json.dumps({k:v for k,v in report.items() if k!='frames'},ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('motion');parser.add_argument('direction')
    args=parser.parse_args();process(args.motion,args.direction)
