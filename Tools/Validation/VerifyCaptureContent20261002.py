"""Read-only capture content check; completion/exit alone cannot prove rendering."""
from pathlib import Path
from PIL import Image
import json, sys

folder = Path(sys.argv[1]).resolve()
rows = []
for path in sorted(folder.glob('*.png')):
    with Image.open(path) as image:
        rgb = image.convert('RGB')
        extrema = rgb.getextrema()
        rows.append(dict(file=path.name, size=list(image.size), blank=all(low == high for low, high in extrema)))
blank = [row['file'] for row in rows if row['blank']]
proof = dict(result='PASS' if rows and not blank else 'FAIL', images=len(rows), blankImages=blank, frames=rows,
             scope='Native saved PNG content only; nonblank does not prove visual quality, normal input, campaign or performance.')
(folder / 'capture-content.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps({key: proof[key] for key in ('result','images','blankImages')}))
assert rows and not blank, 'Blank captured frames are not valid visual evidence.'
