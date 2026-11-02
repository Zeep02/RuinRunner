"""Correct only proven empty-hand backpack occlusion metadata; PNGs are read-only."""
from pathlib import Path
import hashlib, json
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
folder=ROOT/'ArtDirection/Generated/SscaryMotion-20261009/hurt-native-v5-full'
original=folder/'manifest.json'; target=folder/'manifest-reviewed.json'
assert not target.exists(), 'Preserve completed metadata review'
manifest=json.loads(original.read_text(encoding='utf-8-sig'))
skin={(229,149,159,255),(193,106,125,255)}
pack_colors={(59,18,47,255),(78,26,63,255),(123,49,85,255),(110,39,73,255),(149,66,106,255),(81,28,58,255)}
cache={}; changed=[]
def read(path):
    if path not in cache:cache[path]=Image.open(ROOT/path).convert('RGBA')
    return cache[path]
for record in manifest['frames']:
    if record['profile']!=6:continue
    pixels=read(record['path']);hx,hy=round(record['grip'][0]+.5),round(record['grip'][1]-.5)
    visible=any(pixels.getpixel((x,y)) in skin for y in range(max(0,hy-1),min(64,hy+3)) for x in range(max(0,hx-2),min(48,hx+2)))
    record['handOccludedByBackpack']=False
    if visible:continue
    # This reviewed native rear view places the free hand behind the backpack.
    # Require the actual authored palm to equal a colored original bag pixel;
    # neither a broad "behind" flag nor a transparent/missing hand can pass.
    assert record['direction']==7, record
    sx,sy=hx-record['lean'][0],hy-record['lean'][1]
    assert 22<=sx<33 and 30<=sy<39, record
    color=pixels.getpixel((hx,hy))
    assert color in pack_colors and read(record['source']).getpixel((sx,sy))==color, record
    record['behind']=True;record['handOccludedByBackpack']=True
    record['handOcclusionProof']={'kind':'approved-source-backpack','authoredPalm':[hx,hy],'originalPixel':[sx,sy],'rgba':list(color)}
    changed.append({'slot':[record[n] for n in ('profile','direction','variant','key')],'path':record['path'],'proof':record['handOcclusionProof']})
assert len(changed)==54, len(changed)
manifest['reviewedOcclusionRecords']=len(changed)
manifest['unreviewedManifestSha256']=hashlib.sha256(original.read_bytes()).hexdigest()
target.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
report={'status':'PASS exact source-backed metadata review; no PNG modification','originalManifest':str(original.relative_to(ROOT)).replace('\\','/'),'originalManifestSha256':manifest['unreviewedManifestSha256'],'reviewedManifest':str(target.relative_to(ROOT)).replace('\\','/'),'reviewedManifestSha256':hashlib.sha256(target.read_bytes()).hexdigest(),'pngFilesWritten':0,'weaponRecordsChanged':0,'unarmedOcclusionRecords':changed}
out=ROOT/'ArtDirection/Review-20261009/hero-hurt-native/v5/occlusion-review.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'reviewedManifest':str(target),'provenOcclusions':len(changed),'pngFilesWritten':0}))
