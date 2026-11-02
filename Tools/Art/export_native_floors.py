"""Extract unchanged, native-density floor cells. No recoloring or resampling."""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib, json

root=Path(__file__).resolve().parents[2]
sources=root/'ArtDirection/ExternalCandidates/20260922'
rafael=next((sources/'rafael-ancient-ruins/original').rglob('Tileset-Terrain2.png'))
foozle=next((root/'ArtDirection/ExternalCandidates/20260930/foozle-dungeon/original').rglob('DungeonTileset.png'))
records=[]
def export(pack,index,source,rect):
    image=Image.open(source).convert('RGBA').crop(rect)
    path=root/f'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/{pack}/Floor/{index:02}.png'
    path.parent.mkdir(parents=True,exist_ok=True);image.save(path)
    records.append(dict(path=path.relative_to(root).as_posix(),source=source.relative_to(root).as_posix(),rectTopLeft=list(rect),
                        pivot=[.5,.5],ppu=32,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
for y in range(2):
    for x in range(5):export('RafaelRuins',y*5+x,rafael,((23+x)*32,(12+y)*32,(24+x)*32,(13+y)*32))
for y in range(3):
    for x in range(3):export('FoozleDungeon',y*3+x,foozle,(x*32,128+y*32,x*32+32,160+y*32))
folder=sources/'native-floor-selection';folder.mkdir(exist_ok=True)
(folder/'extraction.json').write_text(json.dumps(dict(images=records,method='Unchanged native32px cells; every exported pixel compared to source. Rafael two masonry courses; Foozle three stone sizes with clean/cracked/broken variants.'),indent=2),encoding='utf-8')
for entry in records:
    source=Image.open(root/entry['source']).convert('RGBA').crop(entry['rectTopLeft'])
    assert source.tobytes()==Image.open(root/entry['path']).convert('RGBA').tobytes()
out=Image.new('RGBA',(7*112,3*122),(46,34,47,255));draw=ImageDraw.Draw(out)
for i,entry in enumerate(records):
    x=i%7*112;y=i//7*122;tile=Image.open(root/entry['path']).resize((96,96),Image.Resampling.NEAREST)
    out.alpha_composite(tile,(x+4,y+22));draw.text((x+4,y+3),str(i)+' '+Path(entry['path']).parts[-3],fill='white')
out.save(folder/'native-cells-review.png')
print('Exported and pixel-verified',len(records),'individual native floor cells.')
