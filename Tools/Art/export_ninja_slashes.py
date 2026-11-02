"""Losslessly separate Pixel-Boy/AAA's CC0 slash sequences.

Only crop and transparent canvas padding. No painting, scaling or interpolation.
Source art is in ArtDirection; runtime uses one PNG/Sprite per complete frame.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/'ArtDirection/ExternalCandidates/20260921/ninja-adventure/original/Ninja Adventure - Asset Pack'
OUTPUT = ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/NinjaAdventure/Slash'
REVIEW = ROOT/'ArtDirection/ExternalCandidates/20260921/ninja-adventure/slash-review'
REVIEW.mkdir(exist_ok=True)
# Explicitly reviewed strip geometry, not inferred from changing alpha bounds.
CLIPS = {'Arc':(38,34,6), 'Slash01':(26,32,5), 'Slash02':(66,50,6),
         'Slash03':(57,42,4), 'Circular':(63,55,6), 'Multi':(30,30,9)}
records=[]
for clip, (w,h,count) in CLIPS.items():
    if clip=='Slash03':
        # Current cells are imported/verified by CorrectKatanaSlash20261001.cs.
        # Never restore six clipped fragments or replace the native 57x42 cells
        # with the old padded-canvas export on a subsequent maintenance run.
        continue
    source=SOURCE/f'FX/Slash/SpriteSheet{clip}.png'
    sheet=Image.open(source).convert('RGBA')
    assert sheet.size==(w*count,h), (source,sheet.size)
    folder=OUTPUT/clip
    folder.mkdir(parents=True,exist_ok=True)
    # Even square canvas gives every pose the same center before the authored
    # 90-degree orientation correction in the Unity effect's child transform.
    canvas=(max(w,h)+5)//2*2
    contact=Image.new('RGBA',(count*(canvas*2+8),canvas*2+26),(29,33,44,255))
    draw=ImageDraw.Draw(contact)
    for i in range(count):
        frame=sheet.crop((i*w,0,(i+1)*w,h))
        image=Image.new('RGBA',(canvas,canvas))
        offset=((canvas-w)//2,(canvas-h)//2)
        image.paste(frame,offset)
        out=folder/f'{i:02}.png'
        image.save(out)
        loaded=Image.open(out).convert('RGBA')
        assert loaded.crop((offset[0],offset[1],offset[0]+w,offset[1]+h)).tobytes()==frame.tobytes()
        records.append(dict(clip=clip,index=i,file=out.relative_to(ROOT).as_posix(),
                            source=source.relative_to(ROOT).as_posix(),source_rect=[i*w,0,w,h],
                            canvas=canvas,offset=list(offset),pivot=[.5,.5],ppu=canvas,
                            rgba_sha256=hashlib.sha256(image.tobytes()).hexdigest()))
        xx=i*(canvas*2+8)
        draw.text((xx+4,3),str(i),fill='white')
        contact.alpha_composite(image.resize((canvas*2,canvas*2),Image.Resampling.NEAREST),(xx+4,22))
    contact.save(REVIEW/f'{clip}-contact.png')
(REVIEW/'extraction.json').write_text(json.dumps(dict(source='https://pixel-boy.itch.io/ninja-adventure-asset-pack',
    license='CC0-1.0',operation='crop and transparent padding only',records=records),indent=2),encoding='utf-8')
notices=ROOT/'Assets/StreamingAssets/ThirdPartyNotices'
notices.mkdir(parents=True,exist_ok=True)
shutil.copyfile(SOURCE/'LICENSE.txt',notices/'NinjaAdventure-CC0.txt')
(notices/'NinjaAdventure-SOURCE.txt').write_text('Ninja Adventure Asset Pack\nPixel-Boy and AAA\nhttps://pixel-boy.itch.io/ninja-adventure-asset-pack\nCC0 1.0 Universal\nRuin Runner uses separately exported slash animation frames. Original artwork is preserved; only cells and transparent canvas padding differ.\n',encoding='utf-8')
print(json.dumps(dict(frames=len(records),clips=len(CLIPS),output=str(OUTPUT))))
