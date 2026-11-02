"""Export original Pawn layers/cels. No resampling, recoloring, or generated inbetweens.

Object/Effect are omitted so the existing equipment system supplies the held weapon.
Review artifacts are outside Assets; the original Aseprite and download are preserved.
"""
from pathlib import Path
import hashlib,json
from PIL import Image,ImageDraw
from inspect_aseprite import parse

ROOT=Path(__file__).resolve().parents[2]
PACK=ROOT/'ArtDirection/ExternalCandidates/20260921/tiny-swords'
SOURCE=PACK/'original/Tiny Swords (Free Pack)/Units/Units (aseprite in Blue only)/Pawn.aseprite'
TARGET=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/PixelFrog/Pawn'
CLIPS={'idle':range(0,8),'move':range(64,70),'swing':range(115,121),
       'thrust':range(121,125),'heavy':range(125,131),'hammer':range(112,115)}

SKIN=(200,168,118,255)
OUTLINE=(22,28,46,255)

def original_grip(cel):
    """Separate native glove pixels from the Object cel's tool pixels.

    The source author uses the same two-color gloves on the Hands and Object
    layers. Retain skin and its existing two-pixel outline only; never synthesize
    outline/color pixels. The rest of the tool cel is not exported.
    """
    src=cel['image'];skin={(x,y) for y in range(src.height) for x in range(src.width) if src.getpixel((x,y))==SKIN}
    assert skin
    keep=set(skin)
    for x,y in skin:
        for dy in range(-2,3):
            for dx in range(-2,3):
                px,py=x+dx,y+dy
                if 0<=px<src.width and 0<=py<src.height and src.getpixel((px,py))==OUTLINE:keep.add((px,py))
    result=Image.new('RGBA',src.size)
    for p in keep:result.putpixel(p,src.getpixel(p))
    return result

def socket(hand_image):
    remaining={(x,y) for y in range(hand_image.height) for x in range(hand_image.width) if hand_image.getpixel((x,y))==SKIN}
    groups=[]
    while remaining:
        group={remaining.pop()};queue=list(group)
        while queue:
            x,y=queue.pop()
            neighbors={(x+dx,y+dy) for dx in (-1,0,1) for dy in (-1,0,1)}&remaining
            remaining-=neighbors;group|=neighbors;queue.extend(neighbors)
        if len(group)>=3:groups.append(group)
    group=max(groups,key=lambda g:sum(p[0] for p in g)/len(g))
    return [round(sum(p[i]+.5 for p in group)/len(group),3) for i in (0,1)]

def main():
    size,layers,tags,frames=parse(SOURCE)
    assert size==(192,192)
    body_layers=[i for i,l in enumerate(layers) if l['name'] in ('Body','Head','Hands')]
    shadow_layer=next(i for i,l in enumerate(layers) if l['name']=='Shadow')
    hand_layer=next(i for i,l in enumerate(layers) if l['name']=='Hands')
    object_layer=next(i for i,l in enumerate(layers) if l['name']=='Object')
    assert len(body_layers)==3
    images=[];review=Image.new('RGB',(8*192,6*218),'#30383f');draw=ImageDraw.Draw(review)
    hands_review=Image.new('RGB',review.size,'#30383f');hd=ImageDraw.Draw(hands_review)
    for row,(clip,indices) in enumerate(CLIPS.items()):
        for index,source_frame in enumerate(indices):
            f=frames[source_frame];out=Image.new('RGBA',size);shadow=Image.new('RGBA',size)
            for layer in body_layers+[shadow_layer]:
                cel=f['cels'].get(layer)
                if cel is None:continue
                assert layers[layer]['blend']==0 and layers[layer]['opacity']==255 and cel['opacity']==255 and cel['z']==0
                (shadow if layer==shadow_layer else out).alpha_composite(cel['image'],(cel['x'],cel['y']))
            hand_canvas=Image.new('RGBA',size)
            if hand_layer in f['cels']:
                hc=f['cels'][hand_layer];hand_canvas.alpha_composite(hc['image'],(hc['x'],hc['y']))
            if object_layer in f['cels']:
                oc=f['cels'][object_layer];grip=original_grip(oc)
                out.alpha_composite(grip,(oc['x'],oc['y']));hand_canvas.alpha_composite(grip,(oc['x'],oc['y']))
            grip_point=socket(hand_canvas)
            path=TARGET/clip/f'{index:02}.png';path.parent.mkdir(parents=True,exist_ok=True);out.save(path)
            hand=f['cels'].get(hand_layer);hb=hand['image'].getbbox() if hand else None
            hand_bounds=[hb[0]+hand['x'],hb[1]+hand['y'],hb[2]+hand['x'],hb[3]+hand['y']] if hand else [96,112,96,112]
            images.append(dict(path=path.relative_to(ROOT).as_posix(),sourceFrame=source_frame,clip=clip,
                sourceDurationMs=f['duration'],pivot=[.5,1/3],ppu=64,
                rgbaSha256=hashlib.sha256(out.tobytes()).hexdigest(),bounds=out.getbbox(),handBounds=hand_bounds,hand=grip_point))
            # Review preserves 1 source pixel = 2 preview pixels, cropped only in the preview.
            sample=out.crop((48,48,144,144)).resize((192,192),Image.Resampling.NEAREST)
            review.paste(sample,(index*192,row*218+24),sample)
            draw.text((index*192+4,row*218+4),f'{clip} {index} / source {source_frame}',fill='white')
            ho=Image.new('RGBA',size)
            if hand:ho.alpha_composite(hand['image'],(hand['x'],hand['y']))
            hs=ho.crop((48,48,144,144)).resize((192,192),Image.Resampling.NEAREST)
            hands_review.paste(sample,(index*192,row*218+24),sample)
            overlay=ImageDraw.Draw(hands_review)
            overlay.rectangle((index*192+(hand_bounds[0]-48)*2,row*218+24+(hand_bounds[1]-48)*2,
                index*192+(hand_bounds[2]-48)*2,row*218+24+(hand_bounds[3]-48)*2),outline='#ff9955',width=1)
            hd.text((index*192+4,row*218+4),f'{clip} {index} {hand_bounds}',fill='white')
            if clip=='idle' and index==0:
                sp=TARGET/'shadow.png';shadow.save(sp)
                images.append(dict(path=sp.relative_to(ROOT).as_posix(),sourceFrame=source_frame,clip='shadow',
                    sourceDurationMs=f['duration'],pivot=[.5,1/3],ppu=64,rgbaSha256=hashlib.sha256(shadow.tobytes()).hexdigest()))
    review.save(PACK/'pawn-body-review.png');hands_review.save(PACK/'pawn-hands-review.png')
    (PACK/'hero-extraction.json').write_text(json.dumps(dict(source=SOURCE.relative_to(ROOT).as_posix(),
        sourceSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),layers=['Body','Head','Hands'],
        note='Native original cels. Gloves separated from Object by its original two-color palette. One right-facing view, mirrored at runtime. No authored back/dash/hurt/death clips.',images=images),indent=2))
    sockets={clip:[i['hand'] for i in images if i['clip']==clip] for clip in CLIPS}
    (PACK/'hand-sockets.json').write_text(json.dumps(sockets,indent=2))
    print(json.dumps(dict(count=len(images),clips={k:len(v) for k,v in CLIPS.items()},output=str(PACK/'pawn-body-review.png'))))

if __name__=='__main__':main()
