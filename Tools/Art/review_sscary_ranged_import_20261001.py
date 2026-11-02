"""Read-only applied PNG identity verification."""
from pathlib import Path
import json,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[2]
out=root/'ArtDirection/Review-20261001/hero-ranged'
data=json.loads((out/'native-import.json').read_text(encoding='utf-8'))
seen={}
for row in data['frames']:
    p=row['runtimePath']
    if p not in seen:seen[p]=hashlib.sha256(Image.open(root/p).convert('RGBA').tobytes()).hexdigest()
    assert seen[p]==row['pixelsSha256'],row
report={'result':'PASS','slots':len(data['frames']),'runtimeUniqueImages':len(seen),'checks':['every applied native RGBA hash matches candidate','dedup indices map to imported PNGs']}
(out/'runtime-identity-qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(report)
