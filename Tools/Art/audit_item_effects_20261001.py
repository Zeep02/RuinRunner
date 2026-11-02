"""Read-only provenance QA for newly adopted native effect poses."""
import hashlib
import json
from collections import defaultdict
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
review = root / 'ArtDirection/Review-20261001'
manifest = json.loads((review / 'item-effects-native-import.json').read_text(encoding='utf-8-sig'))
clips = defaultdict(list)
errors = []
rows = []
for entry in manifest['frames']:
    target = root / entry['path']
    source = root / entry['source']
    sheet = Image.open(source).convert('RGBA')
    x, y, w, h = entry['sourceCell']
    original = sheet.crop((x, sheet.height-y-h, x+w, sheet.height-y))
    frame = Image.open(target).convert('RGBA')
    digest = hashlib.sha256(frame.tobytes()).hexdigest()
    clip = target.parent.name
    if frame.size != original.size or frame.tobytes() != original.tobytes():
        errors.append(f'Changed source pixels: {target}')
    if hashlib.sha256(source.read_bytes()).hexdigest() != entry['sourceSha256']:
        errors.append(f'Changed recorded source file: {source}')
    if frame.getbbox() is None:
        errors.append(f'Empty native frame: {target}')
    clips[clip].append(digest)
    rows.append(dict(path=entry['path'], canvas=list(frame.size), rgba_sha256=digest, exact_source=True))
for clip, hashes in clips.items():
    if len(set(hashes)) != len(hashes):
        errors.append(f'Duplicate poses: {clip}')
report = dict(frames=len(rows), clips={k: len(v) for k, v in clips.items()}, errors=errors, rows=rows,
              scope='Native cell identity/uniqueness only; animation timing, physics, actual screen and campaign are separate evidence.')
(review / 'native-item-effects-identity-qa.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({k: report[k] for k in ('frames', 'clips', 'errors')}))
raise SystemExit(bool(errors))
