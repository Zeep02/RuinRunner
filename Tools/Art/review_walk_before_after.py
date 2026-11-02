from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]
sheet=Image.new('RGBA',(1536,8*116+24),(30,33,43,255));d=ImageDraw.Draw(sheet)
d.text((5,5),'BEFORE: 4 poses per direction',fill='white');d.text((389,5),'AFTER: 12 articulated poses per direction / same whole-head master / same 2 world units per gait cycle',fill='white')
for direction,label in enumerate(['N','NE','E','SE','S','SW','W','NW']):
    y=24+direction*116;d.text((4,y),label,fill='white')
    for i in range(16):
        relative=f'HeroMotion/move/{direction:02}/{i:02}.png' if i<4 else f'HeroWalk/move/{direction:02}/{i-4:02}.png'
        im=Image.open(root/'Assets/RuinRunner/Resources/Art/Pixel/Separated'/relative).convert('RGBA')
        sheet.alpha_composite(im.resize((96,96),Image.Resampling.NEAREST),(96*i,y+16))
sheet.save(root/'ArtDirection/Generated/HeroWalk-20260921/eight-direction-before-after.png')
