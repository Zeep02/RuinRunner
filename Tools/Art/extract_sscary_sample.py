"""Preserve authored 48x64 frames for an Editor-only review, without repainting."""
from pathlib import Path
from PIL import Image
import hashlib, json

root=Path(__file__).resolve().parents[2]
pack=root/'ArtDirection/ExternalCandidates/20260922/sscary-adventurer-male'
source=pack/'original/The Male adventurer - Free'
output=root/'Assets/RuinRunner/ArtReview/SscaryMale'
records=[]
for action,prefix in [('Idle','idle_'),('Walk','walk_'),('Dash','dash_'),('Death','death_normal_')]:
    files={p.stem.lower():p for p in (source/action).glob('*.png')}
    for direction in ['down','right_down','right_up','up','left_up','left_down']:
        original=files[prefix+direction]
        image=Image.open(original).convert('RGBA')
        assert image.size==(384,64), (original,image.size)
        for frame in range(8):
            crop=image.crop((frame*48,0,(frame+1)*48,64))
            path=output/action.lower()/direction/f'{frame:02}.png'
            path.parent.mkdir(parents=True,exist_ok=True);crop.save(path)
            assert Image.open(path).convert('RGBA').tobytes()==crop.tobytes()
            records.append(dict(path=path.relative_to(root).as_posix(),source=original.relative_to(root).as_posix(),frame=frame,
                                pivot=[.5,22/64],ppu=32,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
path=output/'shadow.png';Image.open(source/'Shadow.png').save(path)
records.append(dict(path=path.relative_to(root).as_posix(),source=(source/'Shadow.png').relative_to(root).as_posix(),
                    frame=0,pivot=[.5,22/64],ppu=32,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
(pack/'sample-extraction.json').write_text(json.dumps(dict(images=records,scope='Editor-only review; no combat animation or runtime adoption'),indent=2),encoding='utf-8')
print(f'{len(records)} unchanged source frames; fixed (24,22) pivot, 32PPU; no runtime replacement')
