#!/usr/bin/env python3
# PixelLab 무기 후보를 내려받아 Tools/Art/Staging/PixelLab/20261103-weapons70/<무기군>/ 에 보관하고 검수용 시트를 만든다.
import os,sys,urllib.request,concurrent.futures as cf
from PIL import Image,ImageDraw
ROOT=os.path.join(os.path.dirname(os.path.abspath(__file__)),"Staging","PixelLab","20261103-weapons70")
BASE="https://backblaze.pixellab.ai/file/pixellab-characters/objects/5ba0435a-6d49-45f7-aa17-9ce34a6bd358/%s/rotations/frame_%d.png"
SETS={"katana2":("546e0d48-60ed-4cf3-9df2-a74de1c0eca1",64,32),"spear2":("d49a9cf7-5b7a-479c-851f-b9381720d6f7",16,48),"dagger":("47511714-211f-4c19-a0b0-2db010b5257b",64,32),"greatsword":("ee383426-2987-4155-978b-6e1f3409840a",16,48),"katana":("92cb80c9-3e93-482e-a34e-376013a4b78b",16,48),
"spear":("dfa01212-85ae-4109-8cb6-1ea3cb168884",16,64),"staff":("8e0fb996-1d0e-4059-87cc-c143e1230e7e",16,48),"orb":("48e7a5fc-d6a6-4b0b-a1a4-02923f1a131a",64,32),"gun":("481ffe59-ff0c-426e-a0fe-54c8967ebfe6",16,48)}
def get(a):
    url,path=a
    if os.path.exists(path):return True
    try:
        data=urllib.request.urlopen(urllib.request.Request(url,headers={"User-Agent":"Mozilla/5.0"}),timeout=60).read()
        open(path,"wb").write(data);return True
    except Exception as e:print("fail",url,e);return False
for name in (sys.argv[1:] or SETS):
    oid,n,size=SETS[name];d=os.path.join(ROOT,name);os.makedirs(d,exist_ok=True)
    with cf.ThreadPoolExecutor(8) as ex:ok=list(ex.map(get,[(BASE%(oid,i),os.path.join(d,"%02d.png"%i)) for i in range(n)]))
    if not all(ok):continue
    cols=8 if n>16 else 4;z=3 if size==32 else 3;cell=size*z+6
    s=Image.new("RGB",(cols*cell,(n+cols-1)//cols*(cell+12)),(44,46,58));dr=ImageDraw.Draw(s)
    for i in range(n):
        im=Image.open(os.path.join(d,"%02d.png"%i)).convert("RGBA");up=im.resize((size*z,size*z),0)
        x,y=(i%cols)*cell+3,(i//cols)*(cell+12)+12;s.paste(up,(x,y),up);dr.text((x,y-11),str(i),fill=(255,255,255))
    s.save(os.path.join(ROOT,name+"_sheet.png"));print(name,"ok")
