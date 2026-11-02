"""Lossless frame extraction. Never resize, repaint, or synthesize source poses."""
from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib
ROOT=Path(__file__).resolve().parents[2]
CAND=ROOT/'ArtDirection/ExternalCandidates/20260921'
OUT=ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/External'
entries=[]
def save(frame,relative,source,rect,pivot):
    path=OUT/relative; path.parent.mkdir(parents=True,exist_ok=True)
    frame.save(path)
    entries.append(dict(path=path.relative_to(ROOT).as_posix(),source=source.relative_to(ROOT).as_posix(),
                        rect=rect,rgbaSha256=hashlib.sha256(frame.tobytes()).hexdigest(),
                        pivot=pivot,ppu=32))
# Source rows: front, back, left, right. Fixed source foot line y=40.
for creature in ('Slime1','Slime2','Slime3','Vampires2','Vampires3'):
    for state in ('Idle','Walk','Run','Attack','Hurt','Death'):
        bundle='craftpix-vampires' if creature.startswith('Vampires') else 'craftpix-slimes'
        source=CAND/f'{bundle}/original/PNG/{creature}/{state}/{creature}_{state}_body.png'
        sheet=Image.open(source).convert('RGBA'); assert sheet.height==256 and sheet.width%64==0
        for row,direction in enumerate(('down','up','left','right')):
            for col in range(sheet.width//64):
                rect=[col*64,row*64,64,64]
                frame=sheet.crop((rect[0],rect[1],rect[0]+64,rect[1]+64))
                save(frame,f'CraftPix/{creature}/{state.lower()}/{direction}/{col:02}.png',source,rect,[.5,24/64])
# Boar source: one idle row, four walk rows, four run rows, four attack rows.
# Keep native pixels and original 128 canvas. Foot position is (64,96).
source=CAND/'dotmancer-diamond/original/Paid/Spritesheets/Mobs/boar.png'
sheet=Image.open(source).convert('RGBA'); assert sheet.size==(512,1664)
for col,direction in enumerate(('down','left','up','right')):
    rect=[col*128,0,128,128]
    save(sheet.crop((rect[0],0,rect[0]+128,128)),f'Dotmancer/Boar/idle/{direction}/00.png',source,rect,[.5,.25])
for state,start in [('walk',1),('run',5),('attack',9)]:
    for offset,direction in enumerate(('down','left','right','up')):
        for col in range(4):
            x=col*128;y=(start+offset)*128;rect=[x,y,128,128]
            save(sheet.crop((x,y,x+128,y+128)),f'Dotmancer/Boar/{state}/{direction}/{col:02}.png',source,rect,[.5,.25])
manifest=CAND/'external-creature-extraction.json'
manifest.write_text(json.dumps(dict(images=entries),indent=2),encoding='utf-8')
# QA contact sheet only: native crops at exact 3x nearest, no edits to asset pixels.
preview=Image.new('RGB',(1056,560),'#242c38');draw=ImageDraw.Draw(preview)
for row,(pack,actor,states) in enumerate([
 ('CraftPix','Slime1',['idle','walk','attack']),('CraftPix','Slime2',['idle','walk','attack']),
 ('CraftPix','Slime3',['idle','walk','attack']),('Dotmancer','Boar',['idle','run','attack'])]):
    draw.text((8,row*140+5),actor,fill='white')
    col=0
    for state in states:
        files=list((OUT/pack/actor/state/'down').glob('*.png'))
        chosen=files if len(files)<=4 else [files[i] for i in (0,len(files)//3,2*len(files)//3,len(files)-1)]
        for p in chosen:
            im=Image.open(p).convert('RGBA');cx=im.width//2;foot=40 if pack=='CraftPix' else 96
            tile=im.crop((cx-20,foot-36,cx+20,foot+4)).resize((120,120),Image.Resampling.NEAREST)
            preview.paste(tile,(col*88,row*140+20),tile);col+=1
preview.save(CAND/'creature-native-review.png')
print(json.dumps(dict(frames=len(entries),manifest=manifest.as_posix(),preview=(CAND/'creature-native-review.png').as_posix())))
