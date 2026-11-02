"""Prepare a render-only native-source plinth candidate; never write Assets."""
from pathlib import Path
from PIL import Image, ImageDraw
import hashlib
import json

root = Path(__file__).resolve().parents[2]
source = root / 'Assets/RuinRunner/Runtime/World/CainosRuinsArt.cs'
text = source.read_text(encoding='utf-8-sig')
start = text.index('        static void BuildPlinth(Transform parent,Rect bounds)')
end = text.index('        static Rect ClosedBounds(', start)
replacement = '''        static void BuildPlinth(Transform parent,Rect bounds)
        {
            // Solid native stone still exactly covers the existing closed cells.
            // Small surviving pavers break up the broad unmarked top without
            // altering the platform footprint, collision or source pixel size.
            for(var y=bounds.yMin;y<bounds.yMax;y++)for(var x=bounds.xMin;x<bounds.xMax;x++)
                Draw(parent,"Ruined plinth top",Sprite(PavingHash(Mathf.RoundToInt(x),Mathf.RoundToInt(y))%5==0?"Stone/quiet":"Stone/worn"),new Vector2(x+.5f,y+.5f),12,false);
            for(var row=0;bounds.yMin+.875f+row*.5f<=bounds.yMax-.5f;row++)
            for(var column=0;bounds.xMin+.5f+column*.5f+(row&1)*.25f<=bounds.xMax-.5f;column++)
            {
                var point=new Vector2(bounds.xMin+.5f+column*.5f+(row&1)*.25f,bounds.yMin+.875f+row*.5f);
                var hash=PavingHash(Mathf.RoundToInt(point.x*4),Mathf.RoundToInt(point.y*4));
                if(hash%9==0)continue;
                Draw(parent,"Ruined plinth worn slab",Sprite("StonePaving/"+(hash%32).ToString("00")),point,13,false);
            }
            for(var x=bounds.xMin;x<bounds.xMax;x+=.25f)
            {
                var i=((Mathf.RoundToInt(x*4)%4)+4)%4;
                Draw(parent,"Ruined plinth face",Sprite("Ribbon/face-"+i),new Vector2(x+.125f,bounds.yMin+.25f),14,false);
                Draw(parent,"Ruined plinth lip",Sprite("Ribbon/cap-"+i),new Vector2(x+.125f,bounds.yMin+.625f),16,false);
                Draw(parent,"Ruined plinth rear rim",Sprite("Ribbon/cap-"+i),new Vector2(x+.125f,bounds.yMax-.125f),16,false);
            }
            // Quarter-unit source strips meet exactly; the side strips stop
            // before the front/rear rows, so corners are drawn only once.
            for(var y=bounds.yMin+.75f;y<bounds.yMax-.25f;y+=.25f)
            {
                var i=((Mathf.RoundToInt(y*4)%4)+4)%4;
                var left=Draw(parent,"Ruined plinth left rim",Sprite("Ribbon/cap-"+i),new Vector2(bounds.xMin+.125f,y+.125f),16,false);
                left.transform.localRotation=Quaternion.Euler(0,0,90);
                var right=Draw(parent,"Ruined plinth right rim",Sprite("Ribbon/cap-"+i),new Vector2(bounds.xMax-.125f,y+.125f),16,false);
                right.transform.localRotation=Quaternion.Euler(0,0,-90);
            }
        }
'''
candidate = text[:start] + replacement + text[end:]
out = root / 'Tools/Art/Staging/PlinthFinish'
out.mkdir(parents=True, exist_ok=True)
target = out / 'CainosRuinsArt.cs'
target.write_text(candidate, encoding='utf-8')
art = root / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/External/Cainos'
used = {}
grass = {(114,117,27,255),(120,123,33,255),(101,108,23,255),
         (127,130,38,255),(104,111,22,255),(127,129,36,255)}
stone_paving = out / 'StonePaving'
stone_paving.mkdir(exist_ok=True)
extracted = []
for index in range(32):
    original = art / f'Paving/{index:02}.png'
    original_bytes = original.read_bytes()
    tile = Image.open(original).convert('RGBA')
    before = list(tile.get_flattened_data())
    after = [(0,0,0,0) if pixel in grass else pixel for pixel in before]
    assert all(a == b or a in grass and b == (0,0,0,0) for a,b in zip(before,after))
    tile.putdata(after)
    staged = stone_paving / f'{index:02}.png'
    tile.save(staged)
    assert original.read_bytes() == original_bytes
    extracted.append({'source':str(original.relative_to(root)).replace('\\','/'),
                      'sourceSha256':hashlib.sha256(original_bytes).hexdigest(),
                      'staged':str(staged.relative_to(root)).replace('\\','/'),
                      'sha256':hashlib.sha256(staged.read_bytes()).hexdigest(),
                      'removedExactGrassPixels':sum(pixel in grass for pixel in before),
                      'retainedOriginalStonePixels':sum(pixel not in grass for pixel in before),
                      'width':16,'height':16,'ppu':32,'pivot':[.5,.5]})

def native(name):
    path = (out if name.startswith('StonePaving/') else art) / (name + '.png')
    used[str(path.relative_to(root)).replace('\\', '/')] = hashlib.sha256(path.read_bytes()).hexdigest()
    return Image.open(path).convert('RGBA')

def h(x, y):
    v = ((x * 73856093 ^ y * 19349663) * 2654435761) & 0xffffffff
    v ^= v >> 16
    v = (v * 0x7feb352d) & 0xffffffff
    v ^= v >> 15
    v = (v * 0x846ca68b) & 0xffffffff
    return v ^ (v >> 16)

def render(w, height, finish):
    canvas = Image.new('RGBA', (w * 32, height * 32), (0, 0, 0, 0))
    def draw(name, x, y, rotation=0):
        sprite = native(name)
        if rotation:
            sprite = sprite.transpose(Image.Transpose.ROTATE_90 if rotation == 90 else Image.Transpose.ROTATE_270)
        canvas.alpha_composite(sprite, (round(x * 32 - sprite.width / 2), round((height - y) * 32 - sprite.height / 2)))
    for y in range(height):
        for x in range(w):
            draw('Stone/worn' if finish and h(x, y) % 5 else 'Stone/quiet', x + .5, y + .5)
    row = 0
    while .875 + row * .5 <= height - .5:
        column = 0
        while .5 + column * .5 + (row & 1) * .25 <= w - .5:
            x, y = .5 + column * .5 + (row & 1) * .25, .875 + row * .5
            value = h(round(x * 4), round(y * 4))
            if finish and value % 9:
                draw(f'StonePaving/{value % 32:02}', x, y)
            column += 1
        row += 1
    for x in range(w * 4):
        draw(f'Ribbon/face-{x % 4}', x / 4 + .125, .25)
        draw(f'Ribbon/cap-{x % 4}', x / 4 + .125, .625)
        if finish:
            draw(f'Ribbon/cap-{x % 4}', x / 4 + .125, height - .125)
    for y in range(3, height * 4 - 1):
        if finish:
            draw(f'Ribbon/cap-{y % 4}', .125, y / 4 + .125, 90)
            draw(f'Ribbon/cap-{y % 4}', w - .125, y / 4 + .125, -90)
    return canvas

review = root / 'ArtDirection/Review-20261009/plinth-finish/v2-stone-only'
review.mkdir(parents=True, exist_ok=True)
sheet = Image.new('RGBA', (520, 250), (36, 42, 33, 255))
draw = ImageDraw.Draw(sheet)
draw.text((12, 10), 'BASELINE - native 32 pixels/unit', fill='white')
draw.text((270, 10), 'CANDIDATE - native 32 pixels/unit', fill='white')
sheet.alpha_composite(render(6, 4, False), (12, 36))
sheet.alpha_composite(render(6, 4, True), (270, 36))
draw.text((12, 180), 'Original stone pixels; 6 exact source grass colors made transparent.', fill='white')
draw.text((12, 198), 'Static 6 x 4 example; actual Unity scene/playback still required.', fill='white')
sheet.save(review / 'native-comparison.png')
render(6, 4, True).save(review / 'candidate-native.png')
receipt = {
    'status': 'PREPARED OUTSIDE ASSETS; not applied or Unity verified',
    'source': str(source.relative_to(root)).replace('\\', '/'),
    'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'candidate': str(target.relative_to(root)).replace('\\', '/'),
    'candidateSha256': hashlib.sha256(target.read_bytes()).hexdigest(),
    'scope': 'BuildPlinth drawing only. Original ClosedBounds, colliders, geometry, seed/generator/version unchanged.',
    'visual': '80% worn native stone base, sparse staggered native paving, continuous native cap strips around the top. Existing front face/lip retained.',
    'newAssets': 32,
    'review': str((review / 'native-comparison.png').relative_to(root)).replace('\\', '/'),
    'reviewIsActualGameplay': False,
    'nativeSourceSha256': used,
    'extraction':extracted,
    'transparentSourceColors':sorted(grass),
}
(out / 'prepared.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in receipt.items() if k != 'nativeSourceSha256'}, indent=2))
