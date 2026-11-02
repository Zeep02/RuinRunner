from pathlib import Path
from PIL import Image,ImageDraw
import json,csv,argparse,numpy as np
p=argparse.ArgumentParser();p.add_argument('folder',type=Path);a=p.parse_args();rows=list(csv.DictReader((a.folder/'recording.csv').open(encoding='utf-8-sig')))
groups={}
for row in rows:groups.setdefault(row['phase'],[]).append(row)
selected=[]
for phase,samples in groups.items():
 first=np.array(Image.open(a.folder/'frames'/f"frame-{int(samples[0]['frame']):06d}.png").convert('RGB'))[205:445,510:970].astype('int16')
 best=(-1,None)
 for row in samples:
  path=a.folder/'frames'/f"frame-{int(row['frame']):06d}.png";arr=np.array(Image.open(path).convert('RGB'))[205:445,510:970].astype('int16')
  score=int(((np.max(np.abs(arr-first),axis=2)>30)&(np.min(arr,axis=2)>175)).sum())
  if score>best[0]:best=(score,path)
 im=Image.open(best[1]);im.save(a.folder/(phase+'-peak.png'));selected.append((phase,im.crop((510,205,970,445)),best[1].name))
cols=3;sheet=Image.new('RGB',(cols*470,((len(selected)+cols-1)//cols)*270),(30,33,42));d=ImageDraw.Draw(sheet)
for i,(phase,im,src) in enumerate(selected):
 x=i%cols*470;y=i//cols*270;d.text((x+5,y+5),phase,fill='white');sheet.paste(im,(x+5,y+25))
sheet.save(a.folder/'native-contact-overview.png')
(a.folder/'contact-overview-info.json').write_text(json.dumps(dict(policy='Peak distinct bright-contact sample from real native 1280x720 screen. Overview uses 460x240 crops at 1x only, no frame synthesis/rescale. Full native peak PNGs preserved.',frames=[dict(phase=p,frame=s) for p,_,s in selected]),indent=2),encoding='utf-8')
print([p for p,_,_ in selected])
