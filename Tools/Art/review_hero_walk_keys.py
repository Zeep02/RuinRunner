from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[2]
out=Image.new('RGBA',(768,416),(30,33,43,255));d=ImageDraw.Draw(out)
for j,folder in enumerate(['Assets/RuinRunner/Resources/Art/Pixel/Separated/HeroMotion/move/04','Assets/RuinRunner/Editor/ArtSources/HeroWalkSouth/baked']):
    for i in range(4):
        im=Image.open(root/folder/f'{i:02}.png').convert('RGBA')
        out.alpha_composite(im.resize((192,192),Image.Resampling.NEAREST),(i*192,j*208+16))
        d.text((i*192+5,j*208+3),('BEFORE' if j==0 else 'KEY CANDIDATE')+f' {i}',fill='white')
out.save(root/'ArtDirection/Generated/HeroWalk-20260921/south-key-bake-comparison.png')
