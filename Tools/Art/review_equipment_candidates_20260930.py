"""Read-only contact sheets of native sources; never modifies production artwork."""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'ArtDirection/Review-20260930'
OUT.mkdir(parents=True, exist_ok=True)

def sheet(name, entries, cols=5, cell=(220,190), scale=4):
    out = Image.new('RGB', (cols*cell[0], ((len(entries)+cols-1)//cols)*cell[1]), '#5a6065')
    draw = ImageDraw.Draw(out)
    for i,(label,path) in enumerate(entries):
        im = Image.open(path).convert('RGBA')
        bounds = im.getbbox()
        x,y = (i%cols)*cell[0], (i//cols)*cell[1]
        draw.text((x+6,y+5),label,fill='white')
        if bounds:
            im = im.crop(bounds)
            draw.text((x+6,y+21),f'{im.width} x {im.height} visible',fill='#d0d4d8')
            im = im.resize((im.width*scale,im.height*scale),Image.Resampling.NEAREST)
            out.paste(im,(x+(cell[0]-im.width)//2,y+43+(cell[1]-48-im.height)//2),im)
    out.save(OUT / name)

equip = ROOT/'Assets/RuinRunner/Resources/Art/Pixel/Separated/Equipment'
gun_ids = ['scrap_pistol','flintlock','void_revolver','twin_pistols','scrap_shotgun','scattergun','storm_rifle','starbreaker','dragon_cannon']
sheet('current-guns4x.png',[(p,equip/'Weapon'/f'{p}.png') for p in gun_ids])
for pack in ['ma9ici4n-firearms','luxiuxi-weapons']:
    paths = sorted((ROOT/'ArtDirection/ExternalCandidates/20260930'/pack/'original').rglob('*.png'))
    sheet(f'{pack}4x.png',[(p.stem,p) for p in paths],cell=(220,180))
source=ROOT/'ArtDirection/ExternalCandidates/20260922/7soul-icons/original'
paths = sorted(p for p in source.glob('*.png') if p.stem.startswith(('Ac_','I_Crystal','I_Orb','I_Gem','I_Heart','I_Ruby','I_Soul','I_Stone')))
sheet('7soul-accessory-candidates4x.png',[(p.stem,p) for p in paths],cols=6)
sheet('current-accessories4x.png',[(p.stem,p) for p in sorted((equip/'Accessory').glob('*.png'))],cols=6)
print('Read-only native preview sheets saved to',OUT)
