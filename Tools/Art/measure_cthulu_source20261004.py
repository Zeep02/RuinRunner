from pathlib import Path
from PIL import Image
import json,hashlib,struct
root=Path('F:/codex/LoglikeCap')
source=root/'ArtDirection/ExternalCandidates/20261004/chierit-cthulu/original/free_cthulu'
data=(source/'cthulu.aseprite').read_bytes()
size,magic,count,width,height,depth=struct.unpack_from('<I5H',data)
pos=128;durations=[];tags=[]
for f in range(count):
 length,magic,chunks,dur=struct.unpack_from('<I3H',data,pos);durations.append(dur)
 chunks=struct.unpack_from('<I',data,pos+12)[0] or chunks;cp=pos+16
 for _ in range(chunks):
  chunklen,kind=struct.unpack_from('<IH',data,cp);c=data[cp+6:cp+chunklen]
  if kind==0x2018:
   n=struct.unpack_from('<H',c)[0];tp=10
   for _ in range(n):
    start,end,direction=struct.unpack_from('<HHB',c,tp);ln=struct.unpack_from('<H',c,tp+17)[0];name=c[tp+19:tp+19+ln].decode();tp+=19+ln
    tags.append(dict(name=name,start=start,end=end,direction=direction))
  cp+=chunklen
 pos+=length
records={}
for folder in sorted((source/'animations/PNG').iterdir()):
 files=sorted(folder.glob('*.png'),key=lambda p:int(p.stem.rsplit('_',1)[1])); rows=[]
 for p in files:
  im=Image.open(p).convert('RGBA'); rows.append(dict(file=str(p.relative_to(root)).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),canvas=im.size,bbox=im.getbbox(),alphaValues=sorted(set(im.getchannel('A').getdata()))))
 records[folder.name]=rows
for t in tags:t['frameDurationMs']=durations[t['start']:t['end']+1]
manifest={'candidateOnly':True,'source':'https://chierit.itch.io/free-cthulu','author':'chierit','license':'CC-BY-4.0','sourceFrameCount':count,'sourceCanvas':[width,height],'sourceDepth':depth,'tags':tags,'files':records,'importSuggestion':{'ppu':32,'scale':1,'canvasPreserved':True,'pivotPixelsBottomLeft':[96,16],'pivotReason':'fixed canvas centre X and source foot baseline Y=96 top-down; verify feet in actual game before adoption'},'limits':['one authored facing plus mirror; no separate north/south','green/navy source requires matching hostile palette','actual game overlap and body-to-effect direction not tested']}
(root/'ArtDirection/Review-20261004/seal-native-candidates/cthulu-source-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'tags':tags,'depth':depth,'count':count}))
