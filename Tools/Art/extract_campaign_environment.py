"""Separate inspected imagegen subjects with one shared 4:1 scale; no redraw."""
from pathlib import Path
import json
from PIL import Image, ImageDraw
import numpy as np

root = Path(__file__).resolve().parents[2]
folder = root / 'ArtDirection/Generated/Environment-20260920'
source = Image.open(folder / 'original.png').convert('RGBA')
assert source.size == (1225, 1284), source.size
target = root / 'Assets/RuinRunner/Resources/Art/Pixel/Separated/Campaign/Environment'
target.mkdir(parents=True, exist_ok=True)
# Actual transparent gutters were inspected; the generated sheet is not a uniform grid.
xs, ys = [0, 438, 810, 1225], [0, 424, 829, 1284]
themes, kinds = ['Outer', 'Flooded', 'Core'], ['Mosaic', 'Monument', 'Cluster']
contact = Image.new('RGB', (768, 816), '#4a4f50')
proof = []
for row, theme in enumerate(themes):
    for col, kind in enumerate(kinds):
        name = theme + kind
        rect = (xs[col], ys[row], xs[col + 1], ys[row + 1])
        cell = source.crop(rect)
        pixels = np.array(cell)
        # Remove the generator's almost transparent edge halo, retaining opaque subjects.
        pixels[:, :, 3] = np.where(pixels[:, :, 3] >= 224, 255, 0)
        pixels[pixels[:, :, 3] == 0, :3] = 0
        cell = Image.fromarray(pixels)
        bounds = cell.getbbox()
        assert bounds and bounds[0] > 0 and bounds[1] > 0 and bounds[2] < cell.width and bounds[3] < cell.height, (name, bounds)
        # Common source-sized canvas preserves the same scale for every subject.
        padded = Image.new('RGBA', (512, 512))
        is_monument = kind == 'Monument'
        anchor = (256, 464 if is_monument else 256)
        source_anchor = ((bounds[0] + bounds[2]) // 2, bounds[3] if is_monument else (bounds[1] + bounds[3]) // 2)
        offset = (anchor[0] - source_anchor[0], anchor[1] - source_anchor[1])
        padded.alpha_composite(cell, offset)
        sprite = padded.resize((128, 128), Image.Resampling.NEAREST)
        visible = sprite.getbbox()
        assert visible and min(visible[:2]) > 0 and max(visible[2:]) < 128, (name, visible)
        sprite.save(target / (name + '.png'))
        contact.paste(sprite.resize((256, 256), Image.Resampling.NEAREST), (col * 256, row * 272), sprite.resize((256, 256), Image.Resampling.NEAREST))
        ImageDraw.Draw(contact).text((col * 256 + 8, row * 272 + 255), name, fill='white')
        proof.append(dict(name=name, sourceRect=rect, sourceBounds=bounds, canvas=[128, 128], sourceScale=0.25,
                          authoringOffset=offset, visibleBounds=visible, pivotPixels=[64, 12 if is_monument else 64], alpha=[0,255]))
contact.save(folder / 'contact.png')
(folder / 'extraction.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps(proof))
