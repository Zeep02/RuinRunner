"""Read-only review images/metadata. Outputs never enter Unity Assets."""
from pathlib import Path
from PIL import Image,ImageDraw
import hashlib,json
root=Path(__file__).resolve().parents[2]
source=root/'ArtDirection/ExternalCandidates/20261001/d3velopp-slashes/original/Spritesheet'
out=root/'ArtDirection/Review-20261001/melee-fx-range';out.mkdir(parents=True,exist_ok=True)
records=[]
for file in sorted(source.glob('*.png')):
    sheet=Image.open(file).convert('RGBA');cells=[]
    for y in range(sheet.height//128):
        for x in range(sheet.width//128):cells.append(sheet.crop((x*128,y*128,(x+1)*128,(y+1)*128)))
    stats=[]
    for i,cell in enumerate(cells):
        stats.append({'key':i,'visible':sum(a>0 for a in cell.getchannel('A').getdata()),'bbox':cell.getbbox(),'sha256':hashlib.sha256(cell.tobytes()).hexdigest()})
    chart=Image.new('RGB',(9*128,((len(cells)+8)//9)*148),(27,26,36));draw=ImageDraw.Draw(chart)
    for i,cell in enumerate(cells):
        x=i%9*128;y=i//9*148;chart.paste(cell,(x,y+20),cell);draw.text((x+3,y+2),str(i),fill='white')
    chart.save(out/(file.stem+'-review.png'))
    records.append({'source':str(file),'canvas':sheet.size,'cells':len(cells),'unique':len({s['sha256'] for s in stats}),'peak':max(stats,key=lambda s:s['visible'])['key'],'stats':stats})
(out/'candidate-frames.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps([{k:r[k] for k in ('source','cells','unique','peak')} for r in records]))
