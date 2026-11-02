from pathlib import Path
from PIL import Image
import numpy as np, json
root=Path(__file__).resolve().parents[2]
pack=root/'ArtDirection/ExternalCandidates/20260922/srzolda-summons'
src=np.array(Image.open(pack/'blue_summon_Sheet.png').convert('RGBA'))
# The distributed sheet is an EXACT 8x nearest-neighbour enlargement.
# Recover its native pixels losslessly; no approximation or painted edits.
native=src[::8,::8]
assert np.array_equal(src,np.repeat(np.repeat(native,8,0),8,1))
out=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Srzolda/Summon'
out.mkdir(parents=True,exist_ok=True)
records=[]
for n in range(36):
    path=out/f'{n:02}.png'
    Image.fromarray(native[:,n*64:(n+1)*64]).save(path)
    records.append(dict(path=path.relative_to(root).as_posix(),pivot=[.5,.5],ppu=32))
(pack/'extraction.json').write_text(json.dumps(dict(images=records,scope='36 native 64x64 frames; exact reversible 8x enlargement removed; no repaint'),indent=2))
print('36 frames; exact native pixel recovery verified')
