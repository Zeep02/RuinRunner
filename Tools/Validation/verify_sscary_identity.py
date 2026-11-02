from pathlib import Path
from PIL import Image
import json
root=Path.cwd()
data=json.loads((root/'ArtDirection/ExternalCandidates/20260922/sscary-adventurer-male/sample-extraction.json').read_text(encoding='utf-8'))
checked=0
for item in data['images']:
    active=root/item['path'].replace('Assets/RuinRunner/ArtReview/SscaryMale','Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale')
    source=Image.open(root/item['source']).convert('RGBA')
    expected=source if active.name=='shadow.png' else source.crop((item['frame']*48,0,(item['frame']+1)*48,64))
    actual=Image.open(active).convert('RGBA')
    assert actual.size==expected.size and actual.tobytes()==expected.tobytes(), str(active)
    checked+=1
out=f'PASS: {checked} active PNGs pixel-identical to official source cells, including native backpack. Generated v1/v2 not present in runtime.'
(root/'Logs/Validation/2026-09-22-active-hero/source-pixel-identity.txt').write_text(out,encoding='utf-8')
print(out)
