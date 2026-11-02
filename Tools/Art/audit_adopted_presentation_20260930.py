"""Read-only pixel/audio identity audit. No production image modification."""
import json, hashlib, wave, math, array
from pathlib import Path
from collections import defaultdict
from PIL import Image

root=Path(__file__).resolve().parents[2]
review=root/'ArtDirection/Review-20260930'
errors=[]; clips=defaultdict(list); audio=[]; report={'scope':'Exact native source identity and nonempty/unique frames, not a visual quality approval.'}
for name,key in [('combat-presentation-import.json','imagesAndSounds'),('reward-presentation-import.json','imagesAndSounds'),('equipment-adopted.json','images')]:
    rows=json.loads((review/name).read_text(encoding='utf-8-sig'))[key]
    for row in rows:
        source=root/row['source']; target=root/row['path']
        if hashlib.sha256(source.read_bytes()).hexdigest()!=row['sourceSha256']:errors.append(f'source hash {source}')
        if target.suffix=='.wav':
            if source.read_bytes()!=target.read_bytes():errors.append(f'audio identity {target}')
            with wave.open(str(target),'rb') as wav:
                width=wav.getsampwidth(); count=wav.getnframes(); rate=wav.getframerate()
                samples=array.array('h',wav.readframes(count)) if width==2 else None
                peak=max((abs(x) for x in samples),default=0)/32768 if samples is not None else None
                rms=math.sqrt(sum((x/32768)**2 for x in samples)/len(samples)) if samples else 0
                audio.append(dict(id=target.stem,seconds=count/rate,peak=peak,rms=rms,bits=width*8))
                if count==0 or peak==0:errors.append(f'empty audio {target}')
            continue
        original=Image.open(source).convert('RGBA'); actual=Image.open(target).convert('RGBA')
        cell=row.get('sourceCell')
        if cell:
            x,y,w,h=cell;expected=original.crop((x,y,x+w,y+h))
        else:
            expected=Image.new('RGBA',(original.width+4,original.height+4));expected.paste(original,(2,2))
        if expected.size!=actual.size or expected.tobytes()!=actual.tobytes():errors.append(f'pixel identity {target}')
        if not actual.getbbox():errors.append(f'empty frame {target}')
        if 'CombatFX' in str(target) or 'RewardChest' in str(target):
            bounds=actual.getbbox();clips[str(target.parent.relative_to(root))].append(dict(path=row['path'],hash=hashlib.sha256(actual.tobytes()).hexdigest(),cell=cell,visible=max(bounds[2]-bounds[0],bounds[3]-bounds[1])))
summary={}
for clip,frames in clips.items():
    frames.sort(key=lambda f:f['path'])
    if len(set(f['hash'] for f in frames))!=len(frames):errors.append(f'duplicate native frames {clip}')
    if [f['cell'][1]*10000+f['cell'][0] for f in frames]!=sorted(f['cell'][1]*10000+f['cell'][0] for f in frames):errors.append(f'nonchronological frames {clip}')
    summary[clip]={'frames':len(frames),'unique':len(set(f['hash'] for f in frames)),'maxVisiblePixels':max(f['visible'] for f in frames)}
report.update(clips=summary,errors=errors)
(review/'native-presentation-identity-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(review/'native-audio-qa.json').write_text(json.dumps(sorted(audio,key=lambda row:row['id']),indent=2),encoding='utf-8')
print(json.dumps({'clips':len(summary),'frames':sum(x['frames'] for x in summary.values()),'errors':errors,'rewardDiameters':{k:v['maxVisiblePixels'] for k,v in summary.items() if 'Reward' in k}}))
if errors:raise SystemExit(1)
