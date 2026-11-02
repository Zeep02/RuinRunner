"""Encode actual captured frames; retain originals and document active RT viewport."""
import argparse, csv, json
from pathlib import Path
import cv2

p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('output',type=Path)
p.add_argument('--viewport',nargs=4,type=int);p.add_argument('--fps',type=float,default=30)
a=p.parse_args(); rows=list(csv.reader((a.source/'motion.csv').open()))
a.output.parent.mkdir(parents=True,exist_ok=True)
writer=cv2.VideoWriter(str(a.output),cv2.VideoWriter_fourcc(*'mp4v'),a.fps,(1280,720))
if not writer.isOpened():raise RuntimeError('MP4 encoder unavailable')
for row in rows:
    frame=cv2.imread(str(a.source/f'motion-{int(row[0]):04d}.png'))
    if frame is None:raise RuntimeError(f'Missing captured frame {row[0]}')
    if a.viewport:
        x,y,w,h=a.viewport;frame=frame[y:y+h,x:x+w]
    writer.write(cv2.resize(frame,(1280,720),interpolation=cv2.INTER_NEAREST))
writer.release()
a.output.with_suffix('.json').write_text(json.dumps({'source':str(a.source),'frames':len(rows),'fps':a.fps,
    'seconds':len(rows)/a.fps,'active_raw_viewport':a.viewport,'output':[1280,720],
    'note':'Isolated motion fixture. Simulation 60Hz; every second frame captured. Active PixelPerfect RT extracted without synthesizing frames. Not normal campaign or performance evidence.'},indent=2),encoding='utf-8')
print(a.output,len(rows),'frames',round(len(rows)/a.fps,2),'seconds')
