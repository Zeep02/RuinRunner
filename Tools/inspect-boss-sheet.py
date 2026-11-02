"""Read-only connected-component inspection of the user-supplied boss sheet."""
from collections import deque
from pathlib import Path
import numpy as np
from PIL import Image

path = Path('ArtDirection/UserProvided/Boss-20260910/original.png')
alpha = np.asarray(Image.open(path))[:, :, 3]
remaining = alpha > 32
components = []
for y, x in zip(*np.where(remaining)):
    if not remaining[y, x]:
        continue
    queue = deque([(int(x), int(y))]); remaining[y, x] = False
    left = right = int(x); top = bottom = int(y); count = 0
    while queue:
        px, py = queue.popleft(); count += 1
        left = min(left, px); right = max(right, px); top = min(top, py); bottom = max(bottom, py)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                nx, ny = px + dx, py + dy
                if 0 <= nx < alpha.shape[1] and 0 <= ny < alpha.shape[0] and remaining[ny, nx]:
                    remaining[ny, nx] = False; queue.append((nx, ny))
    if count > 100:
        components.append((left, top, right + 1, bottom + 1, count))
print('Components >100:', len(components))
for item in sorted(components, key=lambda b: (int((b[1]+b[3]) / 2 / 156.75), b[0])):
    print(item)
