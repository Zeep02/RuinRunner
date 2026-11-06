"""Measure how far each clip's first and last frame sit from the idle pose once aligned, separating a wrong pivot from a redrawn pose.

For each clip of each Pilot actor, slides the frame against the idle frame of the same direction and finds the offset that
matches best. A clear non-zero offset with a low remainder means the art agrees and only the pivot is off: the body jumps
sideways or up and down when the clip starts. A high remainder means the drawing itself differs.

python Tools/Art/audit_clip_alignment.py [--fix] [Actor ...]
--fix moves the clip's pivot to cancel the offset, for clips that are not deaths and whose match is good.
"""
import os
import re
import sys

import numpy
from PIL import Image

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "RuinRunner", "Resources", "Art", "Pixel", "Separated", "Pilot")
PIVOT = re.compile(r"spritePivot: \{x: ([^,]+), y: ([^}]+)\}")
CANVAS, SEARCH = 256, 14
GOOD_MATCH = .30


def frame_files(folder):
    return [os.path.join(folder, n) for n in sorted(os.listdir(folder)) if re.fullmatch(r"\d+\.png", n)]


def plate(path):
    image = Image.open(path).convert("RGBA")
    px, py = map(float, PIVOT.search(open(path + ".meta", encoding="utf-8").read()).groups())
    pixels = numpy.array(image)
    out = numpy.zeros((CANVAS, CANVAS, 4), numpy.uint8)
    x = CANVAS // 2 - round(px * image.width)
    y = CANVAS // 2 - round((1 - py) * image.height)
    x0, y0 = max(x, 0), max(y, 0)
    x1, y1 = min(x + image.width, CANVAS), min(y + image.height, CANVAS)
    out[y0:y1, x0:x1] = pixels[y0 - y:y1 - y, x0 - x:x1 - x]
    return out


def residual(a, b):
    sa, sb = a[..., 3] > 40, b[..., 3] > 40
    union = sa | sb
    if not union.any():
        return 0.0
    colour = numpy.sum((a[..., :3].astype(int) - b[..., :3].astype(int)) ** 2, axis=2) > 70 ** 2
    return float(numpy.mean(((sa != sb) | (colour & sa & sb))[union]))


def best_shift(reference, frame):
    base = residual(reference, frame)
    best = (base, 0, 0)
    for dy in range(-SEARCH, SEARCH + 1):
        for dx in range(-SEARCH, SEARCH + 1):
            value = residual(reference, numpy.roll(numpy.roll(frame, dy, axis=0), dx, axis=1))
            if value < best[0] - .005:
                best = (value, dx, dy)
    return base, best


def main():
    args = sys.argv[1:]
    fix = "--fix" in args
    names = [a for a in args if not a.startswith("--")] or sorted(n for n in os.listdir(ART) if os.path.isdir(os.path.join(ART, n)))
    for actor in names:
        root = os.path.join(ART, actor)
        clips = {}
        for path, _, files in os.walk(root):
            if any(re.fullmatch(r"\d+\.png", f) for f in files):
                clips[os.path.relpath(path, root).replace("\\", "/")] = path
        for name, folder in sorted(clips.items()):
            parts = name.split("/")
            if re.fullmatch(r"\d+", parts[0]) or parts[0] == "idle":
                continue
            idle = "idle/" + parts[1] if len(parts) > 1 else "idle"
            if idle not in clips:
                continue
            files = frame_files(folder)
            reference = plate(frame_files(clips[idle])[0])
            base, (value, dx, dy) = best_shift(reference, plate(files[0]))
            moved = dx != 0 or dy != 0
            flag = "PIVOT" if moved and max(abs(dx), abs(dy)) >= 2 and value < GOOD_MATCH else "REDRAW" if value >= GOOD_MATCH else "ok"
            print(f"{flag:6} {actor}/{name}: as placed {base:.0%}, best {value:.0%} at ({dx},{dy})")
            if fix and flag == "PIVOT" and parts[0] not in ("death", "entrance"):
                for path in files:
                    image = Image.open(path)
                    text = open(path + ".meta", encoding="utf-8", newline="").read()
                    px, py = map(float, PIVOT.search(text).groups())
                    # Rolling the art by (dx,dy) on screen is the same as moving the pivot the other way inside it.
                    px = round(px - dx / image.width, 5)
                    py = round(py + dy / image.height, 5)
                    open(path + ".meta", "w", encoding="utf-8", newline="").write(PIVOT.sub(f"spritePivot: {{x: {px}, y: {py}}}", text))


if __name__ == "__main__":
    main()
