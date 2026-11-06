"""Native 6px gold pickup, authored directly without image-generation services."""
from pathlib import Path
import json
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/Feedback/Gold'
SOURCE = ROOT / 'Tools/Art/Staging/Authored/20261104-gold'
# Same 32px canvas and 6px silhouette as the small experience orb.
ROWS = ['..ss..', '.sggs.', 'sghhgs', 'sghhgs', '.sggs.', '..ss..']
COLORS = [
    ('#a87820', '#f5c83c', '#fff49a'),
    ('#b58222', '#ffd344', '#fff8af'),
    ('#c18c26', '#ffdf52', '#fffbc4'),
    ('#c9942a', '#ffe363', '#fffdd0'),
    ('#c18c26', '#ffdf52', '#fffbc4'),
    ('#b58222', '#ffd344', '#fff8af'),
    ('#a87820', '#f5c83c', '#fff49a'),
    ('#a3731e', '#efc237', '#fff091'),
]

def main():
    SOURCE.mkdir(parents=True, exist_ok=True)
    frames = []
    for index, colors in enumerate(COLORS):
        palette = dict(zip('sgh', colors))
        frame = Image.new('RGBA', (32, 32))
        brush = ImageDraw.Draw(frame)
        for y, row in enumerate(ROWS):
            for x, pixel in enumerate(row):
                if pixel != '.':
                    brush.point((13+x, 13+y), fill=palette[pixel])
        frame.save(OUT / f'{index:02}.png')
        assert frame.getbbox() == (13, 13, 19, 19)
        assert set(frame.getchannel('A').getdata()) == {0, 255}
        frames.append(frame)
    # Contact sheet enlargement is a preview only; installed PNGs are never scaled.
    preview = Image.new('RGB', (32*8, 32), '#24313a')
    for i, frame in enumerate(frames):
        preview.paste(frame, (32*i, 0), frame)
    preview.resize((768, 96), Image.Resampling.NEAREST).save(SOURCE / 'preview.png')
    enlarged = [f.resize((192, 192), Image.Resampling.NEAREST) for f in frames]
    enlarged[0].save(SOURCE / 'glow.gif', save_all=True, append_images=enlarged[1:],
                     duration=100, loop=0, disposal=2)
    # Native size comparison: green experience first, new yellow gold second.
    xp = Image.open(ROOT / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/Feedback/Experience/Small/00.png')
    comparison = Image.new('RGB', (64, 32), '#24313a')
    comparison.paste(xp, (0, 0), xp)
    comparison.paste(frames[0], (32, 0), frames[0])
    comparison.resize((384, 192), Image.Resampling.NEAREST).save(SOURCE / 'comparison.png')
    (SOURCE / 'source.json').write_text(json.dumps({
        'authoring': 'Explicit local pixel rows; no image API',
        'source': 'Tools/Art/author_gold_coin.py', 'date': '2026-11-04',
        'design': 'Tiny yellow luminous lump, matching the small experience orb; no coin or rotation',
        'canvas': [32, 32], 'visible_pixels': [6, 6], 'frames': 8, 'fps': 10,
        'ppu': 32, 'runtime_visual_review': 'pending',
        'previous_asset': 'Coin.png preserved with original provenance',
    }, ensure_ascii=False, indent=2), encoding='utf-8')
    print('Verified: 8 native 32x32 RGBA frames, 6x6 visible pixels, binary alpha.')

if __name__ == '__main__':
    main()
