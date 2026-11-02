"""Unmodified source tiles/frames at integer preview scales; no artwork changes."""
from pathlib import Path
from PIL import Image, ImageDraw
import json
root=Path(__file__).resolve().parents[2]/'ArtDirection/ExternalCandidates/20260921'
src=Image.open(root/'cainos-basic/original/Texture/TX Tileset Grass.png').convert('RGBA')
sheet=Image.new('RGBA',(8*76,8*88),(30,33,43,255)); draw=ImageDraw.Draw(sheet)
for y in range(8):
    for x in range(8):
        tile=src.crop((x*32,y*32,(x+1)*32,(y+1)*32))
        sheet.alpha_composite(tile.resize((64,64),Image.Resampling.NEAREST),(x*76,y*88+18))
        draw.text((x*76,y*88+3),f'{x},{y}',fill='white')
sheet.save(root/'cainos-grass-tile-review.png')
report=[]
for folder in root.iterdir():
    if not folder.is_dir(): continue
    files=[p for p in (folder/'original').rglob('*.png') if '__MACOSX' not in p.parts and not p.name.startswith('._')]
    report.append(dict(pack=folder.name,pngs=len(files),images=[dict(path=str(p.relative_to(folder)),size=list(Image.open(p).size)) for p in files]))
(root/'image-inventory.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print([(r['pack'],r['pngs']) for r in report])
