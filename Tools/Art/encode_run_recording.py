"""Encode timestamped screen samples at real-time speed (no simulation speedup)."""
import csv,argparse,json,subprocess
from pathlib import Path
import cv2
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--fps',type=float,default=10);p.add_argument('--phase',default=None)
p.add_argument('--codec',choices=['mp4v','h264'],default='mp4v')
p.add_argument('--output',default='normal-input-1x.mp4');p.add_argument('--scope',default='Normal input run; consult run assessment for completion status.');a=p.parse_args()
rows=list(csv.DictReader((a.directory/'recording.csv').open(encoding='utf-8-sig')))
if a.phase is not None:rows=[row for row in rows if row.get('phase')==a.phase]
rows=[r for r in rows if (a.directory/'frames'/f"frame-{int(r['frame']):06d}.png").exists()]
assert rows,'no recorded frames'
start=float(rows[0]['realtime']);end=float(rows[-1]['realtime']);duration=end-start
sample=cv2.imread(str(a.directory/'frames'/f"frame-{int(rows[0]['frame']):06d}.png"));h,w=sample.shape[:2]
out=a.directory/a.output
if a.codec=='h264':
    import imageio_ffmpeg
    encode_log=(a.directory/(Path(a.output).stem+'-encoder.log')).open('w',encoding='utf-8')
    writer=subprocess.Popen([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','warning','-f','rawvideo','-pix_fmt','bgr24',
        '-s:v',f'{w}x{h}','-framerate',str(a.fps),'-i','-','-an','-c:v','libx264','-preset','veryfast','-crf','18',
        '-pix_fmt','yuv420p','-movflags','+faststart',str(out)],stdin=subprocess.PIPE,stderr=encode_log)
else:
    writer=cv2.VideoWriter(str(out),cv2.VideoWriter_fourcc(*'mp4v'),a.fps,(w,h));assert writer.isOpened()
index=0;previous=None;frame=None
for n in range(round(duration*a.fps)+1):
    time=start+n/a.fps
    while index+1<len(rows) and float(rows[index+1]['realtime'])<=time:index+=1
    if previous!=index:
        frame=cv2.imread(str(a.directory/'frames'/f"frame-{int(rows[index]['frame']):06d}.png"));previous=index
    if a.codec=='h264':writer.stdin.write(frame.tobytes())
    else:writer.write(frame)
if a.codec=='h264':
    writer.stdin.close();result=writer.wait();encode_log.close();assert result==0,'H264 encoding failed; see encoder log'
else:writer.release()
proof=dict(output=str(out),codec=a.codec,scope=a.scope,phase=a.phase,source_samples=len(rows),source_start=start,source_end=end,seconds=duration,output_fps=a.fps,resolution=[w,h],policy='Each sample held until the next actual timestamp. No simulated frames, interpolation, cropped camera, or time compression. Recording overhead is not a performance test; encoded preview preserves PNG originals separately.')
proof_path=a.directory/('video-info.json' if a.phase is None else Path(a.output).stem+'-info.json')
proof_path.write_text(json.dumps(proof,indent=2),encoding='utf-8');print(json.dumps(proof))
