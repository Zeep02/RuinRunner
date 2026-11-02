"""Read-only alpha diagnostics for source PNGs; does not rewrite artwork."""
from pathlib import Path
from PIL import Image
import numpy as np

root = Path(__file__).resolve().parents[2] / 'Assets/RuinRunner/Resources/Art/Pixel'
for path in sorted(root.rglob('*.png')):
    a = np.asarray(Image.open(path).convert('RGBA'))
    rgb = a[:, :, :3].astype(np.int16)
    opaque = a[:, :, 3] > 32
    magenta = opaque & (rgb[:, :, 0] - rgb[:, :, 1] > 55) & (rgb[:, :, 2] - rgb[:, :, 1] > 55)
    white = opaque & (rgb.min(axis=2) > 225)
    edge = np.zeros(opaque.shape, bool)
    edge[:3] = edge[-3:] = True
    edge[:, :3] = edge[:, -3:] = True
    print(f'{path.relative_to(root)}: {a.shape[1]}x{a.shape[0]} transparent={np.mean(a[:,:,3]<16):.3f}, magenta={magenta.sum()}, white={white.sum()}, white_outer={np.sum(white & edge)}, partial_alpha={np.sum((a[:,:,3]>0)&(a[:,:,3]<255))}')
    if 'Enemies' in path.parts or 'VFX' in path.parts:
        old_key = (rgb[:,:,0]>180)&(rgb[:,:,2]>180)&(rgb[:,:,1]<100)&(rgb[:,:,0]+rgb[:,:,2]>410)
        print('  magenta pixels surviving CURRENT runtime key:', np.sum(magenta & ~old_key))
    if np.any(a[:,:,3] == 0):
        print('  alpha histogram [0,1..15,16..63,64..127,128..223,224..254,255]:', np.histogram(a[:,:,3], [0,1,16,64,128,224,255,256])[0].tolist())
