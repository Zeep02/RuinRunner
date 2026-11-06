"""Write CombatFX/BlankFrames.txt: one line per pixelfx clip, "clip head tail fps loop".
head and tail are its leading and trailing invisible frames; fps and loop (0 or 1) say how it was exported.

pixelfx renders a clip for its whole running time, so most exports start or end on empty frames. Played once they only
shorten the effect, but a looping projectile or marker blinks out each time round. NativeCombatEffect.Frames skips them.
The PNGs stay on disk as exported.

python Tools/Art/list_blank_fx_frames.py
"""
import json
import os
import re

import numpy
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated", "CombatFX")
VISIBLE_ALPHA = 24
VISIBLE_LIGHT = 36

lines, interior = [], []
for clip in sorted(os.listdir(ROOT)):
    folder = os.path.join(ROOT, clip)
    record = os.path.join(folder, "_pixelfx.json")
    if not os.path.isdir(folder) or not re.match(r"(Fx|W[A-Z]|E[A-Z]|Warden)", clip) or not os.path.exists(record):
        continue
    with open(record, encoding="utf-8") as source:
        export = json.load(source)
    names = sorted(n for n in os.listdir(folder) if re.fullmatch(r"\d+\.png", n))
    def visible(path):
        pixels = numpy.array(Image.open(path).convert("RGBA")).astype(int)
        return bool((pixels[..., 3] > VISIBLE_ALPHA).any() or (pixels[..., :3].sum(axis=2) > VISIBLE_LIGHT).any())
    seen = [visible(os.path.join(folder, n)) for n in names]
    if not any(seen):
        raise ValueError(clip + " has no visible frame")
    head = seen.index(True)
    tail = seen[::-1].index(True)
    if not all(seen[head:len(seen) - tail]):
        interior.append(clip)
    lines.append(f"{clip} {head} {tail} {round(export['fps'])} {1 if export['loop'] else 0}")
with open(os.path.join(ROOT, "BlankFrames.txt"), "w", encoding="utf-8", newline="\n") as out:
    out.write("\n".join(lines) + "\n")
print(len(lines), "clips; blank frames inside:", interior)
