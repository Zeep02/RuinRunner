from PIL import Image,ImageDraw
from pathlib import Path
import json
root=Path('F:/codex/LoglikeCap')
out=root/'ArtDirection/Review-20261004/seal-native-candidates';out.mkdir(parents=True,exist_ok=True)
hero=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/SscaryMale/idle/down/00.png'
ct=root/'ArtDirection/ExternalCandidates/20261004/chierit-cthulu/original/free_cthulu/animations/PNG'
golem=root/'ArtDirection/ExternalCandidates/20261004/monopixel-golems/original/Golems_Free_Version/Golem_1/Blue/No_Swoosh_VFX/Golem_1_idle.png'
current=root/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/10k/SealGuardian/idle/down/00.png'
imgs=[('Hero',Image.open(hero).convert('RGBA')),('Cthulu',Image.open(ct/'idle/idle_1.png').convert('RGBA')),('Mono FREE',Image.open(golem).convert('RGBA').crop((0,0,90,64))),('Current rejected guardian',Image.open(current).convert('RGBA'))]
canvas=Image.new('RGBA',(980,490),(48,43,58,255));draw=ImageDraw.Draw(canvas)
records=[]
for i,(name,img) in enumerate(imgs):
 bbox=img.getbbox();records.append({'name':name,'canvas':img.size,'bbox':bbox,'visibleWH':(bbox[2]-bbox[0],bbox[3]-bbox[1])})
 cut=img.crop(bbox)
 x=20+i*240
 draw.text((x,15),name,fill='white');draw.text((x,32),'native 1x',fill='white')
 canvas.alpha_composite(cut,(x+75-cut.width//2,125-cut.height))
 draw.line((x,126,x+210,126),fill=(120,105,120,255))
 draw.text((x,148),f'{cut.width} x {cut.height} visible pixels',fill='white')
 draw.text((x,198),'same 2x nearest screen scale',fill='white')
 cut=cut.resize((cut.width*2,cut.height*2),Image.Resampling.NEAREST)
 canvas.alpha_composite(cut,(x+75-cut.width//2,395-cut.height))
 draw.line((x,396,x+210,396),fill=(120,105,120,255))
draw.text((20,438),'QA composition only; original source pixels unchanged. No Runtime adoption.',fill='white')
canvas.convert('RGB').save(out/'hero-native-comparison.png')
manifest={'comparison':records,'animations':{}}
for action in sorted(ct.iterdir()):
 files=sorted(action.glob('*.png'),key=lambda p:int(p.stem.rsplit('_',1)[1]))
 if not files:continue
 row=Image.new('RGBA',(len(files)*150,180),(48,43,58,255));d=ImageDraw.Draw(row)
 boxes=[]
 for i,p in enumerate(files):
  im=Image.open(p).convert('RGBA');bbox=im.getbbox();boxes.append(bbox)
  row.alpha_composite(im,(i*150,25));d.text((i*150+2,2),p.name,fill='white')
 row.convert('RGB').save(out/('cthulu-'+action.name+'-native.png'))
 manifest['animations'][action.name]={'count':len(files),'canvas':Image.open(files[0]).size,'bboxes':boxes}
(out/'measurements.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest['comparison']))
print({k:v['count'] for k,v in manifest['animations'].items()})
