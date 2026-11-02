"""Bake reviewed hand landmarks from original PNGs. Read pixels only; never edit art.
Hints identify the visible gripping hand, excluding face/backpack. Runtime loads fixed data.
"""
from pathlib import Path
from PIL import Image
import json, hashlib
root=Path(__file__).resolve().parents[2]
src=root/'Assets/RuinRunner/ArtReview/SscaryMale'
dirs=['down','right_down','right_up','up','left_up','left_down']
hints={
'idle':[
[(22,34.5),(22,35.5),(22,35),(22,34),(22,34.5),(22,35.5),(22,35),(22,34.5)],
[(23,34.5),(23,35.5),(23,35),(23,34),(23,34.5),(23,35.5),(23,35),(23,34.5)],
[(30,34),(30,35),(30,35),(30,34),(30,34),(30,35),(30,35),(30,34)],
[(28,34.7),(28,35.7),(28,34.7),(28,33.7),(28,33.8),(28,34.8),(28,34.7),(28,34.7)],
[(21,36),(21,37),(21,36),(21,35),(21,35),(21,36.5),(21,36),(21,36)],
[(27,37),(27,38),(27,37),(27,36),(27,36),(27,37.5),(27,37),(27,37)]],
'walk':[
[(20,35.5),(20.5,36.5),(20.7,35.9),(20,35.5),(20.1,34.7),(20.1,34.7),(20,34),(20,35)],
[(24.5,35.5),(24.5,36.5),(24.7,35.7),(23.5,35.5),(21.5,34.5),(20.3,35.5),(21,36.5),(23.5,35.5)],
[(28.5,34.5),(28.4,33.8),(28.5,35.5),(26.4,35.1),(24,35),(23.6,36.1),(25.2,36),(28,35)],
[(28.5,33),(28.5,33),(28.5,34),(28.5,34),(27.9,34.7),(27.9,35.7),(28.7,35.7),(28.2,34.2)],
[(19.5,34.5),(19.6,33.8),(19.5,35.5),(21.6,35.1),(24,35),(24.4,36.1),(22.8,36),(20,35)],
[(23.5,35.5),(23.5,36.5),(23.3,35.7),(24.5,35.5),(26.5,34.5),(27.7,35.5),(27,36.5),(24.5,35.5)]],
'dash':[
[(23,34),(22,37),(18.8,33.2),(18.5,31),(18.5,32),(18.5,33),(18.8,36.2),(22,37)],
[(24,34),(24.7,34.9),(22.5,32),(23.5,29.5),(23.5,30.5),(23.5,30.5),(23.5,34),(24.7,34.9)],
[(30.5,33.5),(30,35),(25.5,34),(25.5,32),(25.5,33),(25.5,33),(26,36),(30,36)],
[(28.5,34),(28.5,33.5),(30,32.5),(30,31.5),(30,32.5),(30,33.5),(30,34.5),(28.5,34.5)],
[(20.5,36.5),(19,35),(23.5,34),(23.5,32),(23.5,33),(23.5,33),(23,36),(19,36)],
[(27.5,37.5),(24.3,34.9),(26.5,32),(25.5,29.5),(25.5,30.5),(25.5,30.5),(25.5,34),(24.3,34.9)]]}
records=[]; tables={}
for action,rows in hints.items():
 tables[action]=[]
 for direction,row in zip(dirs,rows):
  table=[]
  for f,hint in enumerate(row):
   path=src/action/direction/f'{f:02}.png'; im=Image.open(path).convert('RGBA')
   points={(x,y) for y in range(25,42) for x in range(10,39) if im.getpixel((x,y)) in {(229,149,159,255),(193,106,125,255)}}
   groups=[]
   while points:
    todo=[points.pop()];group=[]
    while todo:
     x,y=todo.pop();group.append((x+.5,y+.5))
     for q in [(x+1,y),(x-1,y),(x,y+1),(x,y-1)]:
      if q in points:points.remove(q);todo.append(q)
    groups.append(group)
   group=min(groups,key=lambda g:min((x-hint[0])**2+(y-hint[1])**2 for x,y in g))
   # Keep half-native-pixel precision; derive once, inspect markers before adopting.
   point=[round(sum(p[k] for p in group)/len(group)*2)/2 for k in range(2)]
   assert sum((point[k]-hint[k])**2 for k in range(2))<9,(action,direction,f,point,hint)
   table.append(point)
   records.append(dict(action=action,direction=direction,frame=f,point=point,hint=hint,sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest()))
  tables[action].append(table)
out=root/'ArtDirection/ExternalCandidates/20260922/sscary-adventurer-male/hand-sockets.json'
out.write_text(json.dumps(dict(coordinates='top-left 48x64 canvas; frame-specific authored hand center; no runtime pixel inference',records=records),indent=2),encoding='utf-8')
code='// Baked from reviewed native hands by Tools/Art/author_sscary_sockets.py. No sprite pixels modified.\nusing UnityEngine;\nnamespace RuinRunner\n{\n    public static class SscaryHandSockets\n    {\n'
code+='        private static readonly int[] DirectionRows={3,2,1,1,0,5,5,4};\n'
for action,rows in tables.items():
 code+=f'        private static readonly Vector2[,] {action.title()}={{\n'
 for row in rows:
  code+='            {'+','.join(f'new Vector2({x:g}f,{y:g}f)' for x,y in row)+'},\n'
 code+='        };\n'
code+='''        public static Vector2 Pixel(string action,int direction,int frame)
        {
            var data=action=="dash"?Dash:action=="walk"?Walk:Idle;
            return data[DirectionRows[Mathf.Clamp(direction,0,7)],Mathf.Clamp(frame,0,7)];
        }
    }
}
'''
(root/'Assets/RuinRunner/Runtime/Player/SscaryHandSockets.cs').write_text(code,encoding='utf-8')
print(f'{len(records)} hand sockets baked; no raster writes.')

