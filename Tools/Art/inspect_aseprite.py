"""Read RGBA source cels and tags for asset QA. No source code or embedded scripts execute.
Format: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
Only normal opaque layers are supported. Reject unsupported compositing.
"""
from pathlib import Path
from PIL import Image,ImageDraw
import struct,zlib,json,sys

def parse(path):
    data=Path(path).read_bytes()
    size,magic,count,width,height,depth=struct.unpack_from('<I5H',data)
    assert size==len(data) and magic==0xa5e0 and depth==32
    layers=[];tags=[];frames=[];pos=128
    for frame_index in range(count):
        length,fmagic,chunks,duration=struct.unpack_from('<I3H',data,pos)
        assert fmagic==0xf1fa
        chunks=struct.unpack_from('<I',data,pos+12)[0] or chunks
        cp=pos+16;cels={}
        for _ in range(chunks):
            length_chunk,kind=struct.unpack_from('<IH',data,cp);c=data[cp+6:cp+length_chunk]
            if kind==0x2004:
                flags,typ,level,_,_,blend=struct.unpack_from('<6H',c)
                opacity=c[12];name_len=struct.unpack_from('<H',c,16)[0];name=c[18:18+name_len].decode()
                layers.append(dict(name=name,flags=flags,type=typ,level=level,blend=blend,opacity=opacity))
            elif kind==0x2005:
                layer,x,y,opacity,typ,z=struct.unpack_from('<HhhBHh',c)
                if typ in (0,2):
                    w,h=struct.unpack_from('<HH',c,16)
                    pixels=c[20:] if typ==0 else zlib.decompress(c[20:])
                    im=Image.frombytes('RGBA',(w,h),pixels)
                elif typ==1:
                    linked=struct.unpack_from('<H',c,16)[0];im=frames[linked]['cels'][layer]['image'].copy()
                else: raise ValueError('Unsupported cel type '+str(typ))
                cels[layer]=dict(image=im,x=x,y=y,opacity=opacity,z=z)
            elif kind==0x2018:
                n=struct.unpack_from('<H',c)[0];tp=10
                for _ in range(n):
                    start,end,direction=struct.unpack_from('<HHB',c,tp)
                    name_len=struct.unpack_from('<H',c,tp+17)[0]
                    name=c[tp+19:tp+19+name_len].decode()
                    tags.append(dict(name=name,start=start,end=end,direction=direction));tp+=19+name_len
            cp+=length_chunk
        frames.append(dict(cels=cels,duration=duration));pos+=length
    return (width,height),layers,tags,frames

if __name__=='__main__':
    path=Path(sys.argv[1]);size,layers,tags,frames=parse(path)
    report=dict(size=size,layers=layers,tags=tags,frames=len(frames))
    (path.parent/'beta-structure.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report))
    sample=Image.new('RGB',(960,((len(tags)+3)//4)*160),'#25313b');d=ImageDraw.Draw(sample)
    for i,tag in enumerate(tags):
        out=Image.new('RGBA',size)
        for layer,cel in frames[tag['start']]['cels'].items():
            if not layers[layer]['flags']&1:continue
            assert layers[layer]['blend']==0 and layers[layer]['opacity']==255 and cel['opacity']==255
            out.alpha_composite(cel['image'],(cel['x'],cel['y']))
        x=i%4*240;y=i//4*160
        sample.paste(out.resize((size[0]*2,size[1]*2),Image.Resampling.NEAREST),(x,y+25),out.resize((size[0]*2,size[1]*2),Image.Resampling.NEAREST))
        d.text((x,y),tag['name'],fill='white')
    sample.save(path.parent/'beta-review.png')
