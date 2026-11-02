"""Native-size contact sheets of the free, unmodified author frames."""
from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]/'ArtDirection/ExternalCandidates/20260921/slash-fx'
out=root/'review';out.mkdir(exist_ok=True)
for f in (root/'original/Demo').rglob('*.png'):
    if ' FX ' not in f.name or not f.stem.endswith('_1'):continue
    im=Image.open(f).convert('RGBA');cell=im.height;n=im.width//cell
    result=Image.new('RGBA',(cell*6,(cell+18)*((n+5)//6)),(29,32,42,255));d=ImageDraw.Draw(result)
    for i in range(n):
        result.alpha_composite(im.crop((i*cell,0,(i+1)*cell,cell)),((i%6)*cell,(i//6)*(cell+18)+18))
        d.text(((i%6)*cell+3,(i//6)*(cell+18)+3),str(i),fill='white')
    result.save(out/(f.stem+'.png'))
print(out)
