"""Independent native-PNG audit; never edits source or candidate art."""
from pathlib import Path
import argparse, hashlib, json
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--revision', default='v5')
args=parser.parse_args()
manifest_path=ROOT/f'ArtDirection/Generated/SscaryMotion-20261009/hurt-native-{args.revision}-full/manifest.json'
reviewed_path=manifest_path.with_name('manifest-reviewed.json')
if reviewed_path.exists():manifest_path=reviewed_path
manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
out=ROOT/f'ArtDirection/Review-20261009/hero-hurt-native/{args.revision}'
out.mkdir(parents=True, exist_ok=True)
skin={(229,149,159,255),(193,106,125,255)}
allowed={(0,0,0,255),(229,149,159,255),(193,106,125,255),(50,65,127,255),(60,84,143,255),(110,39,73,255)}
images, originals, errors, severed={}, {}, [], []
occluded=[]
pack_colors={(59,18,47,255),(78,26,63,255),(123,49,85,255),(110,39,73,255),(149,66,106,255),(81,28,58,255)}

def image(path):
    if path not in images:
        pixels=Image.open(ROOT/path).convert('RGBA')
        assert pixels.size==(48,64), path
        images[path]=pixels
    return images[path]

def visible_hash(pixels):
    raw=bytearray(pixels.tobytes())
    for i in range(0,len(raw),4):
        if raw[i+3]==0:raw[i:i+3]=b'\0\0\0'
    return hashlib.sha256(raw).hexdigest()

unique={}
for record in manifest['frames']:
    path,source=record['path'],record['source']
    if source not in originals:originals[source]=hashlib.sha256((ROOT/source).read_bytes()).hexdigest()
    if originals[source]!=record['sourcePngSha256']:errors.append({'kind':'changed-source','path':source})
    pixels,prior=image(path),image(source)
    if pixels.crop((0,40,48,64)).tobytes()!=prior.crop((0,40,48,64)).tobytes():errors.append({'kind':'foot-strip','path':path,'source':source})
    hx,hy=round(record['grip'][0]+.5),round(record['grip'][1]-.5)
    if not any(pixels.getpixel((x,y)) in skin for y in range(max(0,hy-1),min(64,hy+3)) for x in range(max(0,hx-2),min(48,hx+2))):
        sx,sy=hx-record['lean'][0],hy-record['lean'][1]
        source_backpack=(record['profile']==6 and record['direction']==7 and record.get('behind') and record.get('handOccludedByBackpack') and 22<=sx<33 and 30<=sy<39 and pixels.getpixel((hx,hy)) in pack_colors and pixels.getpixel((hx,hy))==prior.getpixel((sx,sy)))
        if source_backpack:occluded.append([record[n] for n in ('profile','direction','variant','key')])
        else:errors.append({'kind':'hand-socket','path':path,'slot':[record[n] for n in ('profile','direction','variant','key')]})
    if path in unique:
        if unique[path]!=record['visiblePixelSha256']:errors.append({'kind':'shared-hash','path':path})
        continue
    unique[path]=visible_hash(pixels)
    if unique[path]!=record['visiblePixelSha256']:errors.append({'kind':'actual-pixel-hash','path':path})
    colors=set(pixels.get_flattened_data())
    if any(c[3] not in (0,255) for c in colors):errors.append({'kind':'partial-alpha','path':path})
    source_colors=set(prior.get_flattened_data())|allowed
    if any(c[3] and c not in source_colors for c in colors):errors.append({'kind':'unapproved-palette','path':path})
    opaque={(x,y) for y in range(64) for x in range(48) if pixels.getpixel((x,y))[3]}
    while opaque:
        start=opaque.pop();component={start};queue=[start]
        while queue:
            x,y=queue.pop()
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    neighbor=(x+dx,y+dy)
                    if neighbor in opaque:opaque.remove(neighbor);component.add(neighbor);queue.append(neighbor)
        if all(30<=y<40 for x,y in component):
            if all(pixels.getpixel(p)==(0,0,0,255) for p in component) or any(pixels.getpixel(p) in skin for p in component):
                severed.append({'path':path,'pixels':sorted(component)})
report={'status':'PASS static native-pixel audit only' if not errors and not severed else 'FAIL static native-pixel audit','manifest':str(manifest_path.relative_to(ROOT)).replace('\\','/'),'manifestSha256':hashlib.sha256(manifest_path.read_bytes()).hexdigest(),'records':len(manifest['frames']),'uniquePng':len(unique),'sourceFilesHashed':len(originals),'errors':errors,'detachedArmComponents':severed,'sourceBackpackOcclusions':len(occluded),'sourceFilesModified':0,'realTimePlaybackVerified':False}
(out/'png-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ('errors','detachedArmComponents')}|{'errors':len(errors),'detachedArmComponents':len(severed)},indent=2))
